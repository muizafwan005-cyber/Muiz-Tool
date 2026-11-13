using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using RFTool.AdvancedSystem;

namespace RFTool.Advanced;

/// <summary>Kontrol tampilan hex: alamat | 16 byte hex | ASCII. Byte yang berubah ditandai merah.</summary>
internal sealed class HexView : Control
{
    private const int Cols = 16;

    private readonly TargetProvider provider;
    private readonly Font mono = new Font("Consolas", 10F);
    private int charW = 8;
    private int rowH = 18;
    private int addrDigits = 8;

    private byte[] data = Array.Empty<byte>();
    private bool[] valid = Array.Empty<bool>();
    private byte[] heat = Array.Empty<byte>();
    private const byte HeatTicks = 8;

    public ulong BaseAddress { get; private set; }
    public ulong Selected { get; private set; }

    public event EventHandler? SelectionChanged;

    public HexView(TargetProvider provider)
    {
        this.provider = provider;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        TabStop = true;
        BackColor = AdvTheme.Back;
        ForeColor = AdvTheme.Fore;
        Font = mono;

        Size sample = TextRenderer.MeasureText(
            "0000000000",
            mono,
            new Size(1000, 100),
            TextFormatFlags.NoPadding);

        charW = Math.Max(6, sample.Width / 10);
        rowH = Math.Max(14, sample.Height + 3);
    }

    public int VisibleRows => Math.Max(1, ClientSize.Height / rowH);

    private int AddressX => 6;
    private int BytesX => AddressX + (addrDigits + 2) * charW;
    private int AsciiX => BytesX + Cols * 3 * charW + charW;

    public void GoTo(ulong address)
    {
        // Alamat yang dituju berada di kiri atas, seperti Cheat Engine (tanpa pembulatan ke 16).
        BaseAddress = address;
        Selected = address;
        heat = Array.Empty<byte>();
        Reload();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Reload()
    {
        int rows = VisibleRows;
        int n = rows * Cols;

        byte[] nd = new byte[n];
        bool[] ok = new bool[n];
        TargetProcess? t = provider.Get();

        if (t != null)
        {
            addrDigits = t.Is32Bit ? 8 : 16;

            if (t.TryRead(BaseAddress, nd, n, out int got) && got == n)
            {
                Array.Fill(ok, true);
            }
            else
            {
                // Ada halaman yang tidak terbaca: baca per baris.
                byte[] row = new byte[Cols];

                for (int r = 0; r < rows; r++)
                {
                    int g;

                    if (t.TryRead(BaseAddress + (ulong)(r * Cols), row, Cols, out g) && g == Cols)
                    {
                        Array.Copy(row, 0, nd, r * Cols, Cols);

                        for (int c = 0; c < Cols; c++)
                            ok[r * Cols + c] = true;
                    }
                }
            }
        }

        // Byte yang berubah ditandai merah lalu memudar dalam beberapa siklus refresh.
        if (heat.Length != n)
            heat = new byte[n];

        bool sameView = data.Length == n && valid.Length == n;

        for (int i = 0; i < n; i++)
        {
            if (heat[i] > 0)
                heat[i]--;

            if (sameView && ok[i] && valid[i] && data[i] != nd[i])
                heat[i] = HeatTicks;
        }

        data = nd;
        valid = ok;
        Invalidate();
    }

    public byte[]? ReadAtSelected(int length)
    {
        TargetProcess? t = provider.Get();
        return t?.ReadBytes(Selected, length);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Reload();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);

        if (IsHandleCreated)
            Reload();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);

        int rows = VisibleRows;
        string fmt = addrDigits == 8 ? "X8" : "X16";
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

        for (int r = 0; r < rows; r++)
        {
            int y = r * rowH;
            ulong rowAddress = BaseAddress + (ulong)(r * Cols);

            TextRenderer.DrawText(
                g, rowAddress.ToString(fmt, CultureInfo.InvariantCulture), mono,
                new Point(AddressX, y + 1), AdvTheme.Dim, flags);

            for (int c = 0; c < Cols; c++)
            {
                int idx = r * Cols + c;
                bool ok = idx < valid.Length && valid[idx];
                ulong address = rowAddress + (ulong)c;
                bool selected = address == Selected;

                int hx = BytesX + c * 3 * charW;
                int ax = AsciiX + c * charW;

                if (selected)
                {
                    using (var br = new SolidBrush(AdvTheme.SelectBack))
                    {
                        g.FillRectangle(br, hx - 2, y, 2 * charW + 4, rowH);
                        g.FillRectangle(br, ax - 1, y, charW + 2, rowH);
                    }
                }

                Color color = !ok
                    ? AdvTheme.Dim
                    : (idx < heat.Length && heat[idx] > 0 ? Color.FromArgb(255, 90, 90) : Color.FromArgb(120, 220, 140));

                string hex = ok ? data[idx].ToString("X2", CultureInfo.InvariantCulture) : "??";
                char ch = ok && data[idx] >= 32 && data[idx] < 127 ? (char)data[idx] : '.';

                TextRenderer.DrawText(g, hex, mono, new Point(hx, y + 1), color, flags);
                TextRenderer.DrawText(g, ch.ToString(), mono, new Point(ax, y + 1), AdvTheme.Fore, flags);
            }
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        int row = e.Y / rowH;
        int col;

        if (e.X >= BytesX && e.X < BytesX + Cols * 3 * charW)
            col = (e.X - BytesX) / (3 * charW);
        else if (e.X >= AsciiX && e.X < AsciiX + Cols * charW)
            col = (e.X - AsciiX) / charW;
        else
            return;

        if (row < 0 || row >= VisibleRows)
            return;

        Selected = BaseAddress + (ulong)(row * Cols + Math.Min(col, Cols - 1));
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Shift(-(e.Delta / 120) * 3 * Cols);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        switch (keyData & Keys.KeyCode)
        {
            case Keys.Up:
            case Keys.Down:
            case Keys.Left:
            case Keys.Right:
            case Keys.PageUp:
            case Keys.PageDown:
                return true;
        }

        return base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        int page = VisibleRows * Cols;

        switch (e.KeyCode)
        {
            case Keys.Left: MoveSelection(-1); break;
            case Keys.Right: MoveSelection(1); break;
            case Keys.Up: MoveSelection(-Cols); break;
            case Keys.Down: MoveSelection(Cols); break;
            case Keys.PageUp: Shift(-page); break;
            case Keys.PageDown: Shift(page); break;
            default: return;
        }

        e.Handled = true;
    }

    public void Shift(long delta)
    {
        if (delta < 0)
        {
            ulong d = (ulong)(-delta);
            BaseAddress = BaseAddress >= d ? BaseAddress - d : 0;
            Selected = Selected >= d ? Selected - d : 0;
        }
        else
        {
            BaseAddress += (ulong)delta;
            Selected += (ulong)delta;
        }

        heat = Array.Empty<byte>();
        Reload();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void MoveSelection(long delta)
    {
        if (delta < 0)
        {
            ulong d = (ulong)(-delta);
            Selected = Selected >= d ? Selected - d : 0;
        }
        else
        {
            Selected += (ulong)delta;
        }

        int rows = VisibleRows;

        ulong window = (ulong)(rows * Cols);

        if (Selected < BaseAddress)
        {
            // Geser mundur per baris penuh sampai alamat terpilih terlihat.
            ulong gap = BaseAddress - Selected;
            ulong back = (gap + Cols - 1) / Cols * Cols;
            BaseAddress = BaseAddress >= back ? BaseAddress - back : 0;
            heat = Array.Empty<byte>();
            Reload();
        }
        else if (Selected >= BaseAddress + window)
        {
            ulong over = Selected - (BaseAddress + window) + 1;
            BaseAddress += (over + Cols - 1) / Cols * Cols;
            heat = Array.Empty<byte>();
            Reload();
        }
        else
        {
            Invalidate();
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            mono.Dispose();

        base.Dispose(disposing);
    }
}

/// <summary>Halaman "Memory Viewer".</summary>
internal sealed class MemoryViewerPanel : UserControl
{
    private readonly TargetProvider provider;
    private readonly AddressManager manager;
    private readonly HexView hex;
    private readonly TextBox addressBox;
    private readonly TextBox patternBox;
    private readonly TextBox writeBox;
    private readonly CheckBox autoCheck;
    private readonly Label regionLabel;
    private readonly Label valueLabel;
    private readonly Label statusLabel;
    private readonly Button findBtn;
    private readonly System.Windows.Forms.Timer timer;

    /// <summary>Diminta membuka Code Analyzer di alamat ini (hex).</summary>
    public event Action<string>? DisassembleRequested;

    /// <summary>Diminta membuka Pointer Analyzer dengan target alamat ini (hex).</summary>
    public event Action<string>? PointerScanRequested;

    private CancellationTokenSource? searchCts;
    private bool searching;

    public MemoryViewerPanel(TargetProvider provider, AddressManager manager)
    {
        this.provider = provider;
        this.manager = manager;

        Dock = DockStyle.Fill;
        BackColor = AdvTheme.Back;
        ForeColor = AdvTheme.Fore;

        // ---------- atas ----------
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = AdvTheme.Back,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };

        addressBox = AdvTheme.Txt(200, "RF_Online.bin+C28B40");
        addressBox.Margin = new Padding(0, 2, 6, 0);
        Button goBtn = AdvTheme.Btn("Go", 44);
        Button prevBtn = AdvTheme.Btn("< Page", 56);
        Button nextBtn = AdvTheme.Btn("Page >", 56);
        Button refreshBtn = AdvTheme.Btn("Refresh", 64);
        Button addListBtn = AdvTheme.Btn("Add to List", 84);
        autoCheck = AdvTheme.Chk("Auto", true);
        autoCheck.Margin = new Padding(6, 5, 0, 0);

        top.Controls.Add(addressBox);
        top.Controls.Add(goBtn);
        top.Controls.Add(prevBtn);
        top.Controls.Add(nextBtn);
        top.Controls.Add(refreshBtn);
        top.Controls.Add(addListBtn);
        top.Controls.Add(autoCheck);

        regionLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            ForeColor = AdvTheme.Dim,
            Text = "Region: -"
        };

        // ---------- bawah ----------
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 128, BackColor = AdvTheme.Back };

        valueLabel = new Label
        {
            Location = new Point(0, 2),
            Size = new Size(620, 34),
            ForeColor = AdvTheme.Dim,
            Text = "Pilih satu byte untuk melihat interpretasi nilainya."
        };

        bottom.Controls.Add(valueLabel);

        bottom.Controls.Add(AdvTheme.Lbl("Cari AOB").At(0, 48));
        patternBox = AdvTheme.Txt(300, "48 8B ?? ?? 89").At(70, 44);
        findBtn = AdvTheme.Btn("Find Next", 84).At(378, 43);
        bottom.Controls.Add(patternBox);
        bottom.Controls.Add(findBtn);

        bottom.Controls.Add(AdvTheme.Lbl("Tulis byte").At(0, 78));
        writeBox = AdvTheme.Txt(300, "90 90").At(70, 74);
        Button writeBtn = AdvTheme.Btn("Write", 84).At(378, 73);
        bottom.Controls.Add(writeBox);
        bottom.Controls.Add(writeBtn);

        statusLabel = new Label
        {
            Location = new Point(0, 106),
            Size = new Size(640, 20),
            ForeColor = AdvTheme.Dim
        };

        bottom.Controls.Add(statusLabel);

        hex = new HexView(provider) { Dock = DockStyle.Fill };

        Controls.Add(hex);
        Controls.Add(bottom);
        Controls.Add(regionLabel);
        Controls.Add(top);

        // ---------- event ----------
        goBtn.Click += (s, e) => GoToText();
        addressBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                GoToText();
            }
        };

        prevBtn.Click += (s, e) => hex.Shift(-(long)(hex.VisibleRows * 16));
        nextBtn.Click += (s, e) => hex.Shift((long)(hex.VisibleRows * 16));
        refreshBtn.Click += (s, e) => { hex.Reload(); UpdateInfo(); };
        addListBtn.Click += (s, e) => AddSelectedToManager(ScanValueType.Int32);
        findBtn.Click += (s, e) => OnFind();
        writeBtn.Click += (s, e) => WriteBytes();
        hex.SelectionChanged += (s, e) => UpdateInfo();

        timer = new System.Windows.Forms.Timer { Interval = 250 };
        timer.Tick += (s, e) =>
        {
            if (autoCheck.Checked && Parent != null && Visible && !searching)
            {
                hex.Reload();
                UpdateInfo();
            }
        };
        timer.Start();

        BuildHexMenu();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Stop();
            timer.Dispose();
            searchCts?.Cancel();
        }

        base.Dispose(disposing);
    }

    private void Say(string text)
    {
        statusLabel.Text = text;
    }

    private void GoToText()
    {
        TargetProcess? t = provider.Get();

        if (t == null)
        {
            Say("Belum attach ke process.");
            return;
        }

        if (!AddressExpression.TryEvaluate(t, addressBox.Text, out ulong address, out string error))
        {
            Say("Alamat tidak valid: " + error);
            return;
        }

        hex.GoTo(address);
        hex.Focus();
        Say("");
    }

    private void UpdateInfo()
    {
        TargetProcess? t = provider.Get();

        if (t == null)
        {
            regionLabel.Text = "Region: belum attach";
            return;
        }

        ulong sel = hex.Selected;

        if (t.QueryRegion(sel, out MemRegion region))
        {
            ModuleInfo? m = t.FindModule(sel);

            regionLabel.Text =
                "Protect: " + region.ProtectText +
                "   Base=" + t.FormatAddress(region.Base) +
                "   Size=" + region.Size.ToString("X") +
                "   Type=" + region.TypeText +
                "   Module=" + (m != null ? m.Name : "-") +
                "   Selected=" + t.Describe(sel);
        }
        else
        {
            regionLabel.Text = "Region: tidak dapat di-query";
        }

        byte[]? b = t.ReadBytes(sel, 8);

        if (b == null)
        {
            valueLabel.Text = t.FormatAddress(sel) + "  :  byte: ??  (alamat tidak bisa dibaca)";
            return;
        }

        var sb = new StringBuilder();
        sb.Append(t.FormatAddress(sel)).Append("  :  byte: ").Append(b[0]);
        sb.Append("   word: ").Append(BitConverter.ToInt16(b, 0));
        sb.Append("   integer: ").Append(BitConverter.ToInt32(b, 0));
        sb.Append("   int64: ").Append(BitConverter.ToInt64(b, 0));
        sb.Append("   float: ").Append(BitConverter.ToSingle(b, 0).ToString("G6", CultureInfo.InvariantCulture));
        sb.Append("   double: ").Append(BitConverter.ToDouble(b, 0).ToString("G10", CultureInfo.InvariantCulture));
        valueLabel.Text = sb.ToString();
    }

    // ---------------------------------------------------------
    // CARI POLA
    // ---------------------------------------------------------

    private async void OnFind()
    {
        if (searching)
        {
            searchCts?.Cancel();
            return;
        }

        TargetProcess? t = provider.Get();

        if (t == null)
        {
            Say("Belum attach ke process.");
            return;
        }

        if (!AobPattern.TryParse(patternBox.Text, out AobPattern? pattern, out string error) || pattern == null)
        {
            Say("Pattern tidak valid: " + error);
            return;
        }

        ulong start = hex.Selected + 1;
        ulong found = 0;
        bool hit = false;

        searching = true;
        findBtn.Text = "Stop";
        Say("Mencari dari 0x" + t.FormatAddress(start) + " ...");

        searchCts = new CancellationTokenSource();
        CancellationToken token = searchCts.Token;

        try
        {
            await Task.Run(() =>
            {
                PatternSearcher.Search(
                    t, pattern, start, ulong.MaxValue, null,
                    address =>
                    {
                        found = address;
                        hit = true;
                        return false;
                    },
                    null, token);
            });

            if (hit)
            {
                hex.GoTo(found);
                addressBox.Text = t.Describe(found);
                Say("Ditemukan di " + t.Describe(found));
            }
            else
            {
                Say("Pattern tidak ditemukan setelah alamat terpilih.");
            }
        }
        catch (OperationCanceledException)
        {
            Say("Pencarian dihentikan.");
        }
        catch (Exception ex)
        {
            Say("Pencarian gagal: " + ex.Message);
        }
        finally
        {
            searching = false;
            searchCts?.Dispose();
            searchCts = null;

            if (!IsDisposed)
                findBtn.Text = "Find Next";
        }
    }

    // ---------------------------------------------------------
    // TULIS BYTE
    // ---------------------------------------------------------

    private void WriteBytes()
    {
        TargetProcess? t = provider.Get();

        if (t == null)
        {
            Say("Belum attach ke process.");
            return;
        }

        var bytes = new List<byte>();

        foreach (string token in writeBox.Text.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!byte.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
            {
                Say("Byte '" + token + "' bukan hex yang valid.");
                return;
            }

            bytes.Add(b);
        }

        if (bytes.Count == 0)
        {
            Say("Isi byte hex dulu, contoh: 90 90.");
            return;
        }

        ulong address = hex.Selected;

        if (t.Write(address, bytes.ToArray()))
        {
            Say(bytes.Count + " byte ditulis ke " + t.Describe(address));
            DebugLog.Info("Memory Viewer menulis " + bytes.Count + " byte ke 0x" + t.FormatAddress(address));
            hex.Reload();
            UpdateInfo();
        }
        else
        {
            Say("Write gagal: " + TargetProcess.ErrorText(t.LastError));
        }
    }

    // ---------------------------------------------------------
    // DIPANGGIL DARI LUAR (Browse dari Scanner / Address Manager)
    // ---------------------------------------------------------

    public void GoToAddress(ulong address)
    {
        TargetProcess? t = provider.Get();

        if (t != null)
            addressBox.Text = t.Describe(address);

        hex.GoTo(address);
        hex.Focus();
        Say("Browse: 0x" + (t != null ? t.FormatAddress(address) : address.ToString("X")));
    }

    // ---------------------------------------------------------
    // MENU KLIK KANAN + ADD KE ADDRESS MANAGER
    // ---------------------------------------------------------

    private void AddSelectedToManager(ScanValueType type)
    {
        TargetProcess? t = provider.Get();

        if (t == null)
        {
            Say("Belum attach ke process.");
            return;
        }

        ulong address = hex.Selected;
        string value;

        if (type == ScanValueType.String)
        {
            // Baca sampai 64 byte atau sampai karakter NUL pertama.
            byte[] buffer = new byte[64];
            t.TryRead(address, buffer, buffer.Length, out int got);
            value = got > 0 ? ValueCodec.Format(new ReadOnlySpan<byte>(buffer, 0, got), type) : "";
        }
        else if (type == ScanValueType.ByteArray)
        {
            string? lengthText = AdvTheme.Prompt(this, "Array of byte", "Jumlah byte (1-256):", "8");

            if (lengthText == null)
                return;

            if (!int.TryParse(lengthText.Trim(), out int length) || length < 1 || length > 256)
            {
                Say("Jumlah byte harus 1 sampai 256.");
                return;
            }

            byte[]? data = t.ReadBytes(address, length);
            value = data != null ? ValueCodec.Format(data, type) : "00";
        }
        else
        {
            byte[]? data = t.ReadBytes(address, ValueCodec.Size(type));
            value = data != null ? ValueCodec.Format(data, type) : "0";
        }

        manager.Add(new AddressEntry
        {
            Description = "No description",
            Address = t.Describe(address),
            ValueType = ValueCodec.Name(type),
            Signed = ValueCodec.DefaultSigned(type),
            Value = value,
            Group = "Default"
        });

        Say("Ditambahkan ke Address Manager: " + t.Describe(address) + " (" + ValueCodec.Name(type) + ")");
        DebugLog.Info("Memory Viewer menambah alamat 0x" + t.FormatAddress(address) + " ke Address Manager.");
    }

    private void BuildHexMenu()
    {
        ContextMenuStrip menu = AdvTheme.NewMenu();

        menu.Items.Add(AdvTheme.Item(
            "Add this address to the address list",
            () => AddSelectedToManager(ScanValueType.Int32),
            Keys.Control | Keys.A,
            true));

        var addAs = new ToolStripMenuItem("Add this address as")
        {
            ForeColor = AdvTheme.Fore,
            BackColor = AdvTheme.Side
        };

        ScanValueType[] types =
        {
            ScanValueType.Byte, ScanValueType.Int16, ScanValueType.Int32,
            ScanValueType.Int64, ScanValueType.Float, ScanValueType.Double,
            ScanValueType.String, ScanValueType.ByteArray
        };

        foreach (ScanValueType type in types)
        {
            ScanValueType captured = type;
            addAs.DropDownItems.Add(AdvTheme.Item(ValueCodec.Name(type), () => AddSelectedToManager(captured)));
        }

        menu.Items.Add(addAs);
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(AdvTheme.Item("Go to address...", () => { addressBox.Focus(); addressBox.SelectAll(); }, Keys.Control | Keys.G));
        menu.Items.Add(AdvTheme.Item("Copy address", CopyAddress, Keys.Control | Keys.C));
        menu.Items.Add(AdvTheme.Item("Change bytes here (isi kotak Tulis byte)", () => { writeBox.Focus(); writeBox.SelectAll(); }));
        menu.Items.Add(AdvTheme.Item("Search byte pattern...", () => { patternBox.Focus(); patternBox.SelectAll(); }, Keys.Control | Keys.F));
        menu.Items.Add(AdvTheme.Item("Refresh", () => { hex.Reload(); UpdateInfo(); }, Keys.F5));
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(AdvTheme.Item(
            "Disassemble this memory region",
            () => DisassembleRequested?.Invoke(hex.Selected.ToString("X")),
            Keys.Control | Keys.D));

        menu.Items.Add(AdvTheme.Item(
            "Pointer scan for this address",
            () => PointerScanRequested?.Invoke(hex.Selected.ToString("X")),
            Keys.Control | Keys.P));

        hex.ContextMenuStrip = menu;
    }

    private void CopyAddress()
    {
        TargetProcess? t = provider.Get();
        string text = t != null ? t.FormatAddress(hex.Selected) : hex.Selected.ToString("X");

        try
        {
            Clipboard.SetText(text);
            Say("Alamat disalin: " + text);
        }
        catch (Exception ex)
        {
            Say("Gagal menyalin: " + ex.Message);
        }
    }
}
