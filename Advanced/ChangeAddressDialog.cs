using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using RFTool.AdvancedSystem;

namespace RFTool.Advanced;

/// <summary>Konversi antara ekspresi pointer "[[base]+10]+4" dan (base, daftar offset).</summary>
internal static class PointerExpression
{
    private static bool IsWrapped(string s)
    {
        s = s.Trim();

        if (s.Length < 2 || s[0] != '[' || s[s.Length - 1] != ']')
            return false;

        int depth = 0;

        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '[')
                depth++;
            else if (s[i] == ']')
            {
                depth--;

                if (depth == 0)
                    return i == s.Length - 1;
            }
        }

        return false;
    }

    /// <summary>
    /// offsets berurutan dari offset TERAKHIR (atas) ke offset PERTAMA (bawah, dekat base),
    /// sama dengan urutan tampilan di Cheat Engine. Mengembalikan false jika bukan pointer.
    /// </summary>
    public static bool TryParse(string expression, out string baseExpression, out List<long> offsets)
    {
        offsets = new List<long>();
        string current = (expression ?? "").Trim();

        while (true)
        {
            string head;
            long offset = 0;

            Match m = Regex.Match(current, @"^(.*\])\s*([+-])\s*(?:0x)?([0-9A-Fa-f]+)$");

            if (m.Success && IsWrapped(m.Groups[1].Value))
            {
                head = m.Groups[1].Value.Trim();

                if (!long.TryParse(m.Groups[3].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offset))
                    break;

                if (m.Groups[2].Value == "-")
                    offset = -offset;
            }
            else if (IsWrapped(current))
            {
                head = current;
            }
            else
            {
                break;
            }

            current = head.Substring(1, head.Length - 2).Trim();
            offsets.Add(offset);
        }

        baseExpression = current;
        return offsets.Count > 0;
    }

    public static string Build(string baseExpression, IList<long> offsets)
    {
        string s = baseExpression.Trim();

        for (int i = offsets.Count - 1; i >= 0; i--)
        {
            long o = offsets[i];

            string suffix = o == 0
                ? ""
                : (o > 0 ? "+" + o.ToString("X") : "-" + (-o).ToString("X"));

            s = "[" + s + "]" + suffix;
        }

        return s;
    }
}

/// <summary>Dialog "Change address" bergaya Cheat Engine, termasuk Pointer dan daftar offset.</summary>
internal sealed class ChangeAddressDialog : Form
{
    private sealed class OffsetRow
    {
        public Button Minus = null!;
        public TextBox Box = null!;
        public Button Plus = null!;
        public Label Preview = null!;
    }

    private const int PointerTop = 262;
    private const int DialogWidth = 580;

    private readonly TargetProvider provider;
    private readonly AddressEntry entry;

    private readonly TextBox addressBox;
    private readonly Label liveLabel;
    private readonly TextBox descBox;
    private readonly ComboBox typeCombo;
    private readonly CheckBox hexCheck;
    private readonly CheckBox signedCheck;
    private readonly TextBox valueBox;
    private readonly TextBox groupBox;
    private readonly CheckBox pointerCheck;
    private readonly Panel pointerPanel;
    private readonly TextBox baseBox;
    private readonly Label baseLabel;
    private readonly Button addOffsetBtn;
    private readonly Button removeOffsetBtn;
    private readonly Label errorLabel;
    private readonly Button okBtn;
    private readonly Button cancelBtn;
    private readonly System.Windows.Forms.Timer timer;

    private readonly List<OffsetRow> rows = new List<OffsetRow>();

    private ChangeAddressDialog(string title, AddressEntry entry, TargetProvider provider)
    {
        this.entry = entry;
        this.provider = provider;

        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = AdvTheme.Back;
        ForeColor = AdvTheme.Fore;
        ClientSize = new Size(DialogWidth, 400);

        // ---------- Address + nilai live ----------
        Controls.Add(AdvTheme.Lbl("Address:").At(8, 8));
        addressBox = AdvTheme.Txt(200).At(8, 28);
        liveLabel = AdvTheme.Lbl("=??").At(214, 31);
        Controls.Add(addressBox);
        Controls.Add(liveLabel);

        // ---------- Description ----------
        Controls.Add(AdvTheme.Lbl("Description").At(8, 60));
        descBox = AdvTheme.Txt(DialogWidth - 20, entry.Description).At(8, 80);
        Controls.Add(descBox);

        // ---------- Type ----------
        Controls.Add(AdvTheme.Lbl("Type").At(8, 112));
        typeCombo = AdvTheme.Cmb(190, ValueCodec.TypeNames);
        typeCombo.Location = new Point(8, 132);
        typeCombo.SelectedIndex = (int)ValueCodec.TypeFromName(entry.ValueType);
        Controls.Add(typeCombo);

        hexCheck = AdvTheme.Chk("Hexadecimal", entry.Hex).At(8, 164);
        signedCheck = AdvTheme.Chk("Signed", entry.Signed).At(118, 164);
        Controls.Add(hexCheck);
        Controls.Add(signedCheck);

        // ---------- Value + Group ----------
        Controls.Add(AdvTheme.Lbl("Value (nilai target untuk Write / Freeze)").At(8, 192));
        valueBox = AdvTheme.Txt(300, entry.Value).At(8, 212);
        Controls.Add(valueBox);

        Controls.Add(AdvTheme.Lbl("Group").At(318, 192));
        groupBox = AdvTheme.Txt(DialogWidth - 318 - 12, entry.Group).At(318, 212);
        Controls.Add(groupBox);

        // ---------- Pointer ----------
        pointerCheck = AdvTheme.Chk("Pointer").At(8, 240);
        Controls.Add(pointerCheck);

        pointerPanel = new Panel
        {
            Location = new Point(0, PointerTop),
            Size = new Size(DialogWidth, 140),
            BackColor = AdvTheme.Back,
            Visible = false
        };

        baseBox = AdvTheme.Txt(306);
        baseLabel = AdvTheme.Lbl("-> ??", true);
        addOffsetBtn = AdvTheme.Btn("Add Offset", 100);
        removeOffsetBtn = AdvTheme.Btn("Remove Offset", 112);

        pointerPanel.Controls.Add(baseBox);
        pointerPanel.Controls.Add(baseLabel);
        pointerPanel.Controls.Add(addOffsetBtn);
        pointerPanel.Controls.Add(removeOffsetBtn);
        Controls.Add(pointerPanel);

        // ---------- bawah ----------
        errorLabel = new Label
        {
            Size = new Size(DialogWidth - 16, 20),
            ForeColor = Color.FromArgb(255, 120, 120)
        };
        Controls.Add(errorLabel);

        okBtn = AdvTheme.Btn("OK", 90);
        cancelBtn = AdvTheme.Btn("Cancel", 90);
        cancelBtn.DialogResult = DialogResult.Cancel;
        Controls.Add(okBtn);
        Controls.Add(cancelBtn);

        AcceptButton = okBtn;
        CancelButton = cancelBtn;

        // ---------- isi awal ----------
        if (PointerExpression.TryParse(entry.Address, out string baseExpression, out List<long> offsets))
        {
            pointerCheck.Checked = true;
            baseBox.Text = baseExpression;

            foreach (long offset in offsets)
                AddOffsetRow(FormatOffset(offset), rows.Count);
        }
        else
        {
            addressBox.Text = entry.Address;
        }

        UpdateTypeState();
        LayoutAll();

        // ---------- event ----------
        pointerCheck.CheckedChanged += (s, e) => OnPointerToggled();
        typeCombo.SelectedIndexChanged += (s, e) => { UpdateTypeState(); UpdateLive(); };
        hexCheck.CheckedChanged += (s, e) => UpdateLive();
        signedCheck.CheckedChanged += (s, e) => UpdateLive();
        valueBox.TextChanged += (s, e) => UpdateLive();
        baseBox.TextChanged += (s, e) => UpdateLive();
        addOffsetBtn.Click += (s, e) => { AddOffsetRow("0", 0); LayoutAll(); UpdateLive(); };
        removeOffsetBtn.Click += (s, e) => RemoveTopOffset();
        okBtn.Click += (s, e) => Accept();

        timer = new System.Windows.Forms.Timer { Interval = 400 };
        timer.Tick += (s, e) => UpdateLive();
        timer.Start();

        UpdateLive();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Stop();
        timer.Dispose();
        base.OnFormClosed(e);
    }

    /// <summary>Menampilkan dialog. draft diisi hasil edit jika user menekan OK.</summary>
    public static bool Edit(IWin32Window owner, string title, AddressEntry draft, TargetProvider provider)
    {
        using var dialog = new ChangeAddressDialog(title, draft, provider);
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }

    // ---------------------------------------------------------
    // OFFSET
    // ---------------------------------------------------------

    private static string FormatOffset(long offset)
    {
        return offset < 0 ? "-" + (-offset).ToString("X") : offset.ToString("X");
    }

    private static bool TryParseOffset(string text, out long value)
    {
        value = 0;
        text = (text ?? "").Trim();
        bool negative = false;

        if (text.StartsWith("-", StringComparison.Ordinal))
        {
            negative = true;
            text = text.Substring(1).Trim();
        }
        else if (text.StartsWith("+", StringComparison.Ordinal))
        {
            text = text.Substring(1).Trim();
        }

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            text = text.Substring(2);

        if (!long.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
            return false;

        if (negative)
            value = -value;

        return true;
    }

    private void AddOffsetRow(string text, int index)
    {
        var row = new OffsetRow
        {
            Minus = AdvTheme.Btn("<", 24),
            Box = AdvTheme.Txt(250, text),
            Plus = AdvTheme.Btn(">", 24),
            Preview = new Label
            {
                AutoSize = false,
                Size = new Size(DialogWidth - 330, 22),
                ForeColor = AdvTheme.Dim,
                TextAlign = ContentAlignment.MiddleLeft
            }
        };

        row.Minus.Click += (s, e) => StepOffset(row.Box, -1);
        row.Plus.Click += (s, e) => StepOffset(row.Box, 1);
        row.Box.TextChanged += (s, e) => UpdateLive();

        pointerPanel.Controls.Add(row.Minus);
        pointerPanel.Controls.Add(row.Box);
        pointerPanel.Controls.Add(row.Plus);
        pointerPanel.Controls.Add(row.Preview);

        rows.Insert(index, row);
    }

    private void RemoveTopOffset()
    {
        if (rows.Count <= 1)
            return;

        OffsetRow row = rows[0];
        rows.RemoveAt(0);

        pointerPanel.Controls.Remove(row.Minus);
        pointerPanel.Controls.Remove(row.Box);
        pointerPanel.Controls.Remove(row.Plus);
        pointerPanel.Controls.Remove(row.Preview);

        row.Minus.Dispose();
        row.Box.Dispose();
        row.Plus.Dispose();
        row.Preview.Dispose();

        LayoutAll();
        UpdateLive();
    }

    private void StepOffset(TextBox box, int direction)
    {
        if (!TryParseOffset(box.Text, out long value))
            value = 0;

        ScanValueType type = SelectedType();
        long step = Math.Max(1, ValueCodec.Size(type));

        box.Text = FormatOffset(value + direction * step);
    }

    // ---------------------------------------------------------
    // TATA LETAK
    // ---------------------------------------------------------

    private ScanValueType SelectedType()
    {
        int index = typeCombo.SelectedIndex;
        return index >= 0 && index < ValueCodec.TypeNames.Length ? (ScanValueType)index : ScanValueType.Int32;
    }

    private void UpdateTypeState()
    {
        bool integer = ValueCodec.IsInteger(SelectedType());
        hexCheck.Enabled = integer;
        signedCheck.Enabled = integer;
    }

    private void LayoutAll()
    {
        bool pointer = pointerCheck.Checked;
        pointerPanel.Visible = pointer;
        addressBox.ReadOnly = pointer;

        int y = PointerTop;

        if (pointer)
        {
            int py = 0;

            foreach (OffsetRow row in rows)
            {
                row.Minus.Location = new Point(8, py);
                row.Box.Location = new Point(36, py + 1);
                row.Plus.Location = new Point(290, py);
                row.Preview.Location = new Point(322, py + 2);
                py += 30;
            }

            baseBox.Location = new Point(8, py + 2);
            baseLabel.Location = new Point(322, py + 6);
            py += 34;

            addOffsetBtn.Location = new Point(8, py);
            removeOffsetBtn.Location = new Point(114, py);
            py += 34;

            pointerPanel.Size = new Size(DialogWidth, py);
            y = PointerTop + py;
        }

        errorLabel.Location = new Point(8, y);
        y += 24;

        okBtn.Location = new Point(8, y);
        cancelBtn.Location = new Point(104, y);

        ClientSize = new Size(DialogWidth, y + 40);
    }

    private void OnPointerToggled()
    {
        if (pointerCheck.Checked)
        {
            if (rows.Count == 0)
            {
                baseBox.Text = addressBox.Text.Trim();
                AddOffsetRow("0", 0);
            }
        }
        else if (addressBox.Text == "??")
        {
            addressBox.Text = "";
        }

        LayoutAll();
        UpdateLive();
    }

    // ---------------------------------------------------------
    // NILAI LIVE
    // ---------------------------------------------------------

    private bool EvaluateChain(TargetProcess t, out ulong final)
    {
        final = 0;

        if (!AddressExpression.TryEvaluate(t, baseBox.Text, out ulong address, out _))
        {
            baseLabel.Text = "-> ??";

            foreach (OffsetRow row in rows)
                row.Preview.Text = "??";

            return false;
        }

        bool good = true;

        for (int i = rows.Count - 1; i >= 0; i--)
        {
            OffsetRow row = rows[i];

            if (!good)
            {
                row.Preview.Text = "??";
                continue;
            }

            if (!t.TryReadPointer(address, out ulong pointer))
            {
                good = false;
                row.Preview.Text = "?? (gagal baca " + address.ToString("X") + ")";

                if (i == rows.Count - 1)
                    baseLabel.Text = "-> ??";

                continue;
            }

            if (!TryParseOffset(row.Box.Text, out long offset))
            {
                good = false;
                row.Preview.Text = "offset tidak valid";
                continue;
            }

            if (i == rows.Count - 1)
                baseLabel.Text = "-> " + pointer.ToString("X");

            ulong next = unchecked(pointer + (ulong)offset);

            row.Preview.Text =
                pointer.ToString("X") + (offset >= 0 ? "+" : "-") +
                Math.Abs(offset).ToString("X") + " = " + next.ToString("X");

            address = next;
        }

        final = address;
        return good;
    }

    private int LiveSize(ScanValueType type)
    {
        if (type != ScanValueType.String && type != ScanValueType.ByteArray)
            return ValueCodec.Size(type);

        if (ValueCodec.TryParse(valueBox.Text, type, hexCheck.Checked, false, out byte[] bytes, out _))
            return Math.Max(1, bytes.Length);

        return 16;
    }

    private void UpdateLive()
    {
        TargetProcess? t = provider.Get();

        if (t == null)
        {
            liveLabel.Text = "=?? (belum attach)";
            return;
        }

        ScanValueType type = SelectedType();
        ulong address;
        bool ok;

        if (pointerCheck.Checked)
        {
            ok = EvaluateChain(t, out address);
            addressBox.Text = ok ? t.FormatAddress(address) : "??";
        }
        else
        {
            ok = AddressExpression.TryEvaluate(t, addressBox.Text, out address, out _);
        }

        if (!ok)
        {
            liveLabel.Text = "=??";
            return;
        }

        byte[]? data = t.ReadBytes(address, LiveSize(type));

        liveLabel.Text = data == null
            ? "=??"
            : "=" + ValueCodec.Format(data, type, false, signedCheck.Checked, hexCheck.Checked);
    }

    // ---------------------------------------------------------
    // OK
    // ---------------------------------------------------------

    private void Fail(string message)
    {
        errorLabel.Text = message;
    }

    private void Accept()
    {
        errorLabel.Text = "";

        if (string.IsNullOrWhiteSpace(descBox.Text))
        {
            Fail("Description wajib diisi.");
            return;
        }

        ScanValueType type = SelectedType();
        string address;

        if (pointerCheck.Checked)
        {
            if (string.IsNullOrWhiteSpace(baseBox.Text))
            {
                Fail("Base address wajib diisi.");
                return;
            }

            var offsets = new List<long>();

            foreach (OffsetRow row in rows)
            {
                if (!TryParseOffset(row.Box.Text, out long offset))
                {
                    Fail("Offset '" + row.Box.Text + "' tidak valid (heksadesimal, boleh diawali -).");
                    return;
                }

                offsets.Add(offset);
            }

            address = PointerExpression.Build(baseBox.Text, offsets);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(addressBox.Text))
            {
                Fail("Address wajib diisi.");
                return;
            }

            address = addressBox.Text.Trim();
        }

        if (valueBox.Text.Trim().Length > 0 &&
            !ValueCodec.TryParse(valueBox.Text, type, hexCheck.Checked, false, out _, out string valueError))
        {
            Fail(valueError);
            return;
        }

        TargetProcess? t = provider.Get();

        if (t != null && !AddressExpression.TryEvaluate(t, address, out _, out string addressError))
        {
            Fail("Address: " + addressError);
            return;
        }

        entry.Description = descBox.Text.Trim();
        entry.Address = address;
        entry.ValueType = ValueCodec.Name(type);
        entry.Value = valueBox.Text.Trim();
        entry.Group = string.IsNullOrWhiteSpace(groupBox.Text) ? "Default" : groupBox.Text.Trim();
        entry.Hex = hexCheck.Checked;
        entry.Signed = signedCheck.Checked;

        DialogResult = DialogResult.OK;
        Close();
    }
}
