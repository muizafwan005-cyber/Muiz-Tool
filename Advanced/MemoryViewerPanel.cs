using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

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
    private bool[] changed = Array.Empty<bool>();

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
        BaseAddress = address & ~0xFUL;
        Selected = address;
        changed = Array.Empty<bool>();
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

        if (changed.Length != n)
            changed = new bool[n];

        if (data.Length == n)
        {
            for (int i = 0; i < n; i++)
            {
                if (ok[i] && valid.Length == n && valid[i] && data[i] != nd[i])
                    changed[i] = true;
            }
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
                    : (idx < changed.Length && changed[idx] ? Color.FromArgb(255, 90, 90) : Color.FromArgb(120, 220, 140));

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

        changed = Array.Empty<bool>();
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

        if (Selected < BaseAddress)
        {
            BaseAddress = Selected & ~0xFUL;
            changed = Array.Empty<bool>();
            Reload();
        }
        else if (Selected >= BaseAddress + (ulong)(rows * Cols))
        {
            ulong rowStart = Selected & ~0xFUL;
            ulong back = (ulong)((rows - 1) * Cols);
            BaseAddress = rowStart >= back ? rowStart - back : 0;
            changed = Array.Empty<bool>();
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

    private CancellationTokenSource? searchCts;
    private bool searching;

    public MemoryViewerPanel(TargetProvider provider)
    {
        this.provider = provider;

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

        addressBox = AdvTheme.Txt(230, "RF_Online.bin+C28B40");
        addressBox.Margin = new Padding(0, 2, 6, 0);
        Button goBtn = AdvTheme.Btn("Go", 50);
        Button prevBtn = AdvTheme.Btn("< Page", 64);
        Button nextBtn = AdvTheme.Btn("Page >", 64);
        Button refreshBtn = AdvTheme.Btn("Refresh", 70);
        autoCheck = AdvTheme.Chk("Auto");
        autoCheck.Margin = new Padding(6, 5, 0, 0);

        top.Controls.Add(addressBox);
        top.Controls.Add(goBtn);
        top.Controls.Add(prevBtn);
        top.Controls.Add(nextBtn);
        top.Controls.Add(refreshBtn);
        top.Controls.Add(autoCheck);

        regionLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            ForeColor = AdvTheme.Dim,
            Text = "Region: -"
        };

        // ---------- bawah ----------
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 112, BackColor = AdvTheme.Back };

        valueLabel = new Label
        {
            Location = new Point(0, 4),
            Size = new Size(900, 20),
            ForeColor = AdvTheme.Dim,
            Text = "Pilih satu byte untuk melihat interpretasi nilainya."
        };

        bottom.Controls.Add(valueLabel);

        bottom.Controls.Add(AdvTheme.Lbl("Cari AOB").At(0, 34));
        patternBox = AdvTheme.Txt(300, "48 8B ?? ?? 89").At(70, 30);
        findBtn = AdvTheme.Btn("Find Next", 84).At(378, 29);
        bottom.Controls.Add(patternBox);
        bottom.Controls.Add(findBtn);

        bottom.Controls.Add(AdvTheme.Lbl("Tulis byte").At(0, 64));
        writeBox = AdvTheme.Txt(300, "90 90").At(70, 60);
        Button writeBtn = AdvTheme.Btn("Write", 84).At(378, 59);
        bottom.Controls.Add(writeBox);
        bottom.Controls.Add(writeBtn);

        statusLabel = new Label
        {
            Location = new Point(0, 90),
            Size = new Size(900, 20),
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
        findBtn.Click += (s, e) => OnFind();
        writeBtn.Click += (s, e) => WriteBytes();
        hex.SelectionChanged += (s, e) => UpdateInfo();

        timer = new System.Windows.Forms.Timer { Interval = 500 };
        timer.Tick += (s, e) =>
        {
            if (autoCheck.Checked && Parent != null && Visible && !searching)
            {
                hex.Reload();
                UpdateInfo();
            }
        };
        timer.Start();
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
            valueLabel.Text = "byte: ??  (alamat tidak bisa dibaca)";
            return;
        }

        var sb = new StringBuilder();
        sb.Append("byte: ").Append(b[0]);
        sb.Append("   word: ").Append(BitConverter.ToInt16(b, 0));
        sb.Append("   integer: ").Append(BitConverter.ToInt32(b, 0));
        sb.Append("   int64: ").Append(BitConverter.ToInt64(b, 0));
        sb.Append("   float: ").Append(BitConverter.ToSingle(b, 0).ToString("0.###", CultureInfo.InvariantCulture));
        sb.Append("   double: ").Append(BitConverter.ToDouble(b, 0).ToString("0.###", CultureInfo.InvariantCulture));
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
}
