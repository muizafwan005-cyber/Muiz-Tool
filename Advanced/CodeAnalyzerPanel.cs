using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using RFTool.AdvancedSystem;
using Ic = Iced.Intel;

namespace RFTool.Advanced;

/// <summary>Halaman "Code Analyzer": disassembler x86/x64 (Iced) dengan navigasi branch.</summary>
internal sealed class CodeAnalyzerPanel : UserControl
{
    private sealed class Row
    {
        public ulong Address;
        public int Length;
        public string Bytes = "";
        public string Text = "";
        public string Comment = "";
        public bool HasTarget;
        public ulong Target;
        public Ic.Instruction Instruction;
    }

    private const int ReadSize = 1280;
    private const int MaxRows = 400;

    private readonly TargetProvider provider;
    private readonly AddressManager manager;

    private readonly TextBox addressBox;
    private readonly ListView list;
    private readonly TextBox info;
    private readonly Label status;

    private readonly List<Row> rows = new List<Row>();
    private readonly Stack<ulong> history = new Stack<ulong>();
    private ulong currentStart;
    private bool hasStart;

    /// <summary>Diminta membuka Memory Viewer di alamat ini (hex).</summary>
    public event Action<string>? BrowseRequested;

    public CodeAnalyzerPanel(TargetProvider provider, AddressManager manager)
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

        addressBox = AdvTheme.Txt(230, "RF_Online.bin+1000");
        addressBox.Margin = new Padding(0, 2, 6, 0);

        Button goBtn = AdvTheme.Btn("Go", 44);
        Button backBtn = AdvTheme.Btn("< Back", 60);
        Button nextBtn = AdvTheme.Btn("Next >", 60);
        Button followBtn = AdvTheme.Btn("Follow", 64);
        Button browseBtn = AdvTheme.Btn("Browse", 66);

        top.Controls.Add(addressBox);
        top.Controls.Add(goBtn);
        top.Controls.Add(backBtn);
        top.Controls.Add(nextBtn);
        top.Controls.Add(followBtn);
        top.Controls.Add(browseBtn);

        // ---------- daftar instruksi ----------
        list = new ListView { Dock = DockStyle.Fill, MultiSelect = false };
        AdvTheme.StyleList(list);
        list.Font = AdvTheme.Mono;
        list.Columns.Add("Address", 190);
        list.Columns.Add("Bytes", 170);
        list.Columns.Add("Instruction", 260);
        list.Columns.Add("Comment", 240);

        // ---------- bawah: detail ----------
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 170, BackColor = AdvTheme.Back };

        status = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 22,
            ForeColor = AdvTheme.Dim,
            Text = "Isi alamat lalu klik Go. Klik dua kali pada call/jmp untuk mengikuti."
        };

        info = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = AdvTheme.Mono,
            BackColor = AdvTheme.Input,
            ForeColor = AdvTheme.Fore,
            BorderStyle = BorderStyle.None
        };

        bottom.Controls.Add(info);
        bottom.Controls.Add(status);

        Controls.Add(list);
        Controls.Add(bottom);
        Controls.Add(top);

        // ---------- event ----------
        goBtn.Click += (s, e) => GoFromBox();
        addressBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                GoFromBox();
            }
        };

        backBtn.Click += (s, e) => GoBack();
        nextBtn.Click += (s, e) => NextPage();
        followBtn.Click += (s, e) => FollowSelected();
        browseBtn.Click += (s, e) => BrowseSelected();
        list.DoubleClick += (s, e) => FollowSelected();
        list.SelectedIndexChanged += (s, e) => ShowDetail();

        ContextMenuStrip menu = AdvTheme.NewMenu();
        menu.Items.Add(AdvTheme.Item("Follow branch / call", FollowSelected, Keys.Enter, true));
        menu.Items.Add(AdvTheme.Item("Browse this memory region", BrowseSelected, Keys.Control | Keys.B));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(AdvTheme.Item("Copy address", () => CopyColumn(0), Keys.Control | Keys.C));
        menu.Items.Add(AdvTheme.Item("Copy bytes", () => CopyColumn(1)));
        menu.Items.Add(AdvTheme.Item("Copy instruction", () => CopyColumn(2)));
        list.ContextMenuStrip = menu;
    }

    private void Say(string text)
    {
        status.Text = text;
    }

    // ---------------------------------------------------------
    // DIPANGGIL DARI LUAR
    // ---------------------------------------------------------

    public void GoToAddress(ulong address)
    {
        TargetProcess? t = provider.Get();

        if (t != null)
            addressBox.Text = t.Describe(address);

        Disassemble(address, true);
    }

    // ---------------------------------------------------------
    // NAVIGASI
    // ---------------------------------------------------------

    private void GoFromBox()
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

        Disassemble(address, true);
    }

    private void GoBack()
    {
        if (history.Count == 0)
        {
            Say("Tidak ada riwayat.");
            return;
        }

        ulong previous = history.Pop();
        Disassemble(previous, false);
    }

    private void NextPage()
    {
        if (rows.Count == 0)
            return;

        Row last = rows[rows.Count - 1];
        Disassemble(last.Address + (ulong)last.Length, true);
    }

    private Row? SelectedRow()
    {
        if (list.SelectedIndices.Count == 0)
            return null;

        int index = list.SelectedIndices[0];
        return index >= 0 && index < rows.Count ? rows[index] : null;
    }

    private void FollowSelected()
    {
        Row? row = SelectedRow();

        if (row == null)
            return;

        if (!row.HasTarget)
        {
            Say("Instruksi ini bukan branch/call langsung.");
            return;
        }

        TargetProcess? t = provider.Get();

        if (t != null)
            addressBox.Text = t.Describe(row.Target);

        Disassemble(row.Target, true);
    }

    private void BrowseSelected()
    {
        Row? row = SelectedRow();
        ulong address = row != null ? row.Address : (hasStart ? currentStart : 0);

        if (address == 0)
        {
            Say("Belum ada alamat.");
            return;
        }

        BrowseRequested?.Invoke(address.ToString("X"));
    }

    private void CopyColumn(int column)
    {
        int index = list.SelectedIndices.Count > 0 ? list.SelectedIndices[0] : -1;

        if (index < 0 || index >= list.Items.Count)
            return;

        try
        {
            Clipboard.SetText(list.Items[index].SubItems[column].Text);
        }
        catch (Exception ex)
        {
            Say("Gagal menyalin: " + ex.Message);
        }
    }

    // ---------------------------------------------------------
    // DISASSEMBLE
    // ---------------------------------------------------------

    private void Disassemble(ulong address, bool pushHistory)
    {
        TargetProcess? t = provider.Get();

        if (t == null)
        {
            Say("Belum attach ke process.");
            return;
        }

        byte[] buffer = new byte[ReadSize];
        t.TryRead(address, buffer, buffer.Length, out int got);

        if (got <= 0)
        {
            Say("Gagal membaca memori di 0x" + t.FormatAddress(address) + ": " + TargetProcess.ErrorText(t.LastError));
            return;
        }

        if (pushHistory && hasStart)
            history.Push(currentStart);

        currentStart = address;
        hasStart = true;

        var reader = new Ic.ByteArrayCodeReader(buffer, 0, got);
        Ic.Decoder decoder = Ic.Decoder.Create(t.Is32Bit ? 32 : 64, reader);
        decoder.IP = address;

        var formatter = new Ic.IntelFormatter();
        formatter.Options.UppercaseHex = true;
        var output = new Ic.StringOutput();

        rows.Clear();
        var items = new List<ListViewItem>();

        while (reader.CanReadByte && rows.Count < MaxRows)
        {
            decoder.Decode(out Ic.Instruction instr);

            int offset = (int)(instr.IP - address);
            int length = instr.Length;

            if (length <= 0 || offset + length > got)
                break;

            string bytes = BitConverter.ToString(buffer, offset, length).Replace('-', ' ');
            string text;

            if (instr.IsInvalid)
            {
                text = "db " + bytes.Replace(" ", ",");
            }
            else
            {
                formatter.Format(instr, output);
                text = output.ToStringAndReset();
            }

            var row = new Row
            {
                Address = instr.IP,
                Length = length,
                Bytes = bytes,
                Text = text,
                Instruction = instr
            };

            if (!instr.IsInvalid)
            {
                Ic.FlowControl flow = instr.FlowControl;

                bool direct =
                    (flow == Ic.FlowControl.UnconditionalBranch ||
                     flow == Ic.FlowControl.ConditionalBranch ||
                     flow == Ic.FlowControl.Call) &&
                    (instr.Op0Kind == Ic.OpKind.NearBranch16 ||
                     instr.Op0Kind == Ic.OpKind.NearBranch32 ||
                     instr.Op0Kind == Ic.OpKind.NearBranch64);

                if (direct)
                {
                    row.HasTarget = true;
                    row.Target = instr.NearBranchTarget;
                    row.Comment = "-> " + t.Describe(row.Target);
                }
                else if (instr.IsIPRelativeMemoryOperand)
                {
                    row.Comment = "[" + t.Describe(instr.IPRelativeMemoryAddress) + "]";
                }
            }

            rows.Add(row);

            items.Add(new ListViewItem(new[]
            {
                t.Describe(row.Address),
                row.Bytes,
                row.Text,
                row.Comment
            }));
        }

        list.BeginUpdate();
        list.Items.Clear();
        list.Items.AddRange(items.ToArray());
        list.EndUpdate();

        if (list.Items.Count > 0)
        {
            list.Items[0].Selected = true;
            list.Items[0].Focused = true;
        }

        Say(rows.Count + " instruksi dari 0x" + t.FormatAddress(address) + (t.Is32Bit ? " (32-bit)" : " (64-bit)"));
        ShowDetail();
    }

    // ---------------------------------------------------------
    // DETAIL INSTRUKSI
    // ---------------------------------------------------------

    private void ShowDetail()
    {
        Row? row = SelectedRow();
        TargetProcess? t = provider.Get();

        if (row == null || t == null)
        {
            info.Text = "";
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Alamat      : " + t.Describe(row.Address) + "  (0x" + t.FormatAddress(row.Address) + ")");
        sb.AppendLine("Bytes       : " + row.Bytes + "  (" + row.Length + " byte)");
        sb.AppendLine("Instruksi   : " + row.Text);

        if (row.Instruction.IsInvalid)
        {
            sb.AppendLine("Instruksi tidak valid / data.");
            info.Text = sb.ToString();
            return;
        }

        Ic.Instruction instr = row.Instruction;

        sb.AppendLine("Kode        : " + instr.Code);
        sb.AppendLine("Alur kontrol: " + instr.FlowControl);

        try
        {
            var formatter = new Ic.IntelFormatter();
            formatter.Options.UppercaseHex = true;
            var output = new Ic.StringOutput();

            sb.AppendLine("Operand     :");

            for (int i = 0; i < instr.OpCount; i++)
            {
                formatter.FormatOperand(instr, output, i);
                sb.AppendLine("  op" + i + " [" + instr.GetOpKind(i) + "] " + output.ToStringAndReset());
            }

            var factory = new Ic.InstructionInfoFactory();
            Ic.InstructionInfo detail = factory.GetInfo(instr);

            sb.AppendLine("Register    :");

            foreach (Ic.UsedRegister r in detail.GetUsedRegisters())
                sb.AppendLine("  " + r);

            sb.AppendLine("Memori      :");

            foreach (Ic.UsedMemory m in detail.GetUsedMemory())
                sb.AppendLine("  " + m);
        }
        catch (Exception ex)
        {
            sb.AppendLine("(detail operand tidak tersedia: " + ex.Message + ")");
        }

        if (row.HasTarget)
            sb.AppendLine("Target      : " + t.Describe(row.Target) + "  (klik dua kali untuk mengikuti)");

        info.Text = sb.ToString();
    }
}
