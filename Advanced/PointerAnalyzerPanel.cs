using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using RFTool.AdvancedSystem;

namespace RFTool.Advanced;

/// <summary>Halaman "Pointer Analyzer": mencari jalur pointer static menuju sebuah alamat.</summary>
internal sealed class PointerAnalyzerPanel : UserControl
{
    private readonly TargetProvider provider;
    private readonly AddressManager manager;

    private readonly TextBox addressBox;
    private readonly TextBox depthBox;
    private readonly TextBox offsetBox;
    private readonly TextBox resultsBox;
    private readonly Button scanBtn;
    private readonly Button cancelBtn;
    private readonly ComboBox typeCombo;
    private readonly ListView list;
    private readonly ProgressBar progress;
    private readonly Label status;

    private List<PointerPath> paths = new List<PointerPath>();
    private ulong lastTarget;
    private CancellationTokenSource? cts;
    private bool busy;

    /// <summary>Diminta membuka Memory Viewer di alamat target (hex).</summary>
    public event Action<string>? BrowseRequested;

    public PointerAnalyzerPanel(TargetProvider provider, AddressManager manager)
    {
        this.provider = provider;
        this.manager = manager;

        Dock = DockStyle.Fill;
        BackColor = AdvTheme.Back;
        ForeColor = AdvTheme.Fore;

        // ---------- atas: parameter ----------
        var top = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = AdvTheme.Back };

        top.Controls.Add(AdvTheme.Lbl("Target").At(0, 8));
        addressBox = AdvTheme.Txt(260).At(64, 4);
        scanBtn = AdvTheme.Btn("Scan", 80).At(334, 3);
        cancelBtn = AdvTheme.Btn("Cancel", 80).At(420, 3);
        cancelBtn.Enabled = false;

        top.Controls.Add(addressBox);
        top.Controls.Add(scanBtn);
        top.Controls.Add(cancelBtn);

        top.Controls.Add(AdvTheme.Lbl("Depth").At(0, 44));
        depthBox = AdvTheme.Txt(36, "3").At(48, 40);
        top.Controls.Add(depthBox);

        top.Controls.Add(AdvTheme.Lbl("Max offset (hex)").At(96, 44));
        offsetBox = AdvTheme.Txt(56, "400").At(206, 40);
        top.Controls.Add(offsetBox);

        top.Controls.Add(AdvTheme.Lbl("Max results").At(276, 44));
        resultsBox = AdvTheme.Txt(52, "500").At(354, 40);
        top.Controls.Add(resultsBox);

        progress = new ProgressBar { Location = new Point(0, 72), Size = new Size(500, 12), Minimum = 0, Maximum = 100 };
        top.Controls.Add(progress);

        // ---------- hasil ----------
        list = new ListView { Dock = DockStyle.Fill, MultiSelect = true };
        AdvTheme.StyleList(list);
        list.Columns.Add("Base (static)", 210);
        list.Columns.Add("Offsets", 180);
        list.Columns.Add("Depth", 50);
        list.Columns.Add("Check", 70);
        list.Columns.Add("Expression", 320);
        list.DoubleClick += (s, e) => AddSelected();

        // ---------- bawah: aksi ----------
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 74, BackColor = AdvTheme.Back };

        Button verifyBtn = AdvTheme.Btn("Verify", 70).At(0, 2);
        Button addBtn = AdvTheme.Btn("Add to List", 90).At(76, 2);
        typeCombo = AdvTheme.Cmb(90, ValueCodec.TypeNames);
        typeCombo.SelectedIndex = (int)ScanValueType.Int32;
        typeCombo.Location = new Point(172, 3);
        Button browseBtn = AdvTheme.Btn("Browse Target", 104).At(268, 2);
        Button copyBtn = AdvTheme.Btn("Copy Expr", 84).At(378, 2);

        bottom.Controls.Add(verifyBtn);
        bottom.Controls.Add(addBtn);
        bottom.Controls.Add(typeCombo);
        bottom.Controls.Add(browseBtn);
        bottom.Controls.Add(copyBtn);

        status = new Label
        {
            Location = new Point(0, 36),
            Size = new Size(640, 36),
            ForeColor = AdvTheme.Dim,
            Text = "Isi Target dengan alamat yang ingin dicari pointer-nya (hex, module+offset, atau pointer)."
        };

        bottom.Controls.Add(status);

        Controls.Add(list);
        Controls.Add(bottom);
        Controls.Add(top);

        // ---------- event ----------
        scanBtn.Click += (s, e) => RunScan();
        cancelBtn.Click += (s, e) => cts?.Cancel();
        verifyBtn.Click += (s, e) => VerifyAll();
        addBtn.Click += (s, e) => AddSelected();
        browseBtn.Click += (s, e) => BrowseTarget();
        copyBtn.Click += (s, e) => CopyExpression();

        ContextMenuStrip menu = AdvTheme.NewMenu();
        menu.Items.Add(AdvTheme.Item("Add to Address Manager", AddSelected, Keys.None, true));
        menu.Items.Add(AdvTheme.Item("Verify", VerifyAll));
        menu.Items.Add(AdvTheme.Item("Copy expression", CopyExpression, Keys.Control | Keys.C));
        menu.Items.Add(AdvTheme.Item("Browse target in Memory Viewer", BrowseTarget, Keys.Control | Keys.B));
        list.ContextMenuStrip = menu;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            cts?.Cancel();

        base.Dispose(disposing);
    }

    /// <summary>Dipanggil dari luar (menu "Pointer scan for this address").</summary>
    public void SetTarget(string addressText)
    {
        addressBox.Text = addressText;
        Say("Target diisi: " + addressText + ". Klik Scan untuk mencari pointer.");
    }

    private void Say(string text)
    {
        status.Text = text;
    }

    // ---------------------------------------------------------
    // SCAN
    // ---------------------------------------------------------

    private async void RunScan()
    {
        if (busy)
            return;

        TargetProcess? t = provider.Get();

        if (t == null)
        {
            Say("Belum attach ke process.");
            return;
        }

        if (!AddressExpression.TryEvaluate(t, addressBox.Text, out ulong target, out string error))
        {
            Say("Target tidak valid: " + error);
            return;
        }

        if (!int.TryParse(depthBox.Text.Trim(), out int depth) || depth < 1 || depth > 6)
        {
            Say("Depth harus 1 sampai 6.");
            return;
        }

        if (!int.TryParse(offsetBox.Text.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int maxOffset) ||
            maxOffset < 0 || maxOffset > 0xFFFF)
        {
            Say("Max offset harus hex antara 0 dan FFFF.");
            return;
        }

        if (!int.TryParse(resultsBox.Text.Trim(), out int maxResults) || maxResults < 1 || maxResults > 20000)
        {
            Say("Max results harus 1 sampai 20000.");
            return;
        }

        var options = new PointerScanOptions
        {
            MaxDepth = depth,
            MaxOffset = maxOffset,
            MaxResults = maxResults
        };

        busy = true;
        lastTarget = target;
        scanBtn.Enabled = false;
        cancelBtn.Enabled = true;
        list.Items.Clear();
        progress.Value = 0;
        Say("Memulai pointer scan ke 0x" + t.FormatAddress(target) + " ...");

        cts = new CancellationTokenSource();
        CancellationToken token = cts.Token;
        var started = DateTime.UtcNow;

        try
        {
            PointerScanResult result = await Task.Run(() =>
                PointerScanner.Scan(t, target, options, ReportProgress, token));

            paths = result.Paths;

            var items = new List<ListViewItem>();

            foreach (PointerPath p in paths)
            {
                items.Add(new ListViewItem(new[]
                {
                    p.BaseText,
                    p.OffsetsText,
                    p.Depth.ToString(),
                    "",
                    p.Expression
                }));
            }

            list.Items.AddRange(items.ToArray());

            double seconds = (DateTime.UtcNow - started).TotalSeconds;
            Say(paths.Count + " jalur ditemukan dalam " + seconds.ToString("0.0") + " dtk. " + result.Note);
            DebugLog.Info("Pointer scan 0x" + t.FormatAddress(target) + ": " + paths.Count + " jalur.");
        }
        catch (OperationCanceledException)
        {
            Say("Pointer scan dibatalkan.");
        }
        catch (Exception ex)
        {
            Say("Pointer scan gagal: " + ex.Message);
            DebugLog.Error("Pointer scan gagal: " + ex.Message);
        }
        finally
        {
            busy = false;
            cts?.Dispose();
            cts = null;

            if (!IsDisposed)
            {
                scanBtn.Enabled = true;
                cancelBtn.Enabled = false;
                progress.Value = 0;
            }
        }
    }

    private void ReportProgress(string text, int percent)
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        BeginInvoke(new Action(() =>
        {
            if (IsDisposed)
                return;

            progress.Value = Math.Clamp(percent, 0, 100);
            status.Text = text;
        }));
    }

    // ---------------------------------------------------------
    // AKSI
    // ---------------------------------------------------------

    private List<PointerPath> SelectedPaths()
    {
        var selected = new List<PointerPath>();

        foreach (int i in list.SelectedIndices)
        {
            if (i >= 0 && i < paths.Count)
                selected.Add(paths[i]);
        }

        return selected;
    }

    private void VerifyAll()
    {
        TargetProcess? t = provider.Get();

        if (t == null)
        {
            Say("Belum attach ke process.");
            return;
        }

        int ok = 0;

        for (int i = 0; i < paths.Count && i < list.Items.Count; i++)
        {
            string text;

            if (!AddressExpression.TryEvaluate(t, paths[i].Expression, out ulong value, out _))
                text = "GAGAL";
            else if (value == lastTarget)
            {
                text = "OK";
                ok++;
            }
            else
                text = "BEDA";

            list.Items[i].SubItems[3].Text = text;
        }

        Say("Verify: " + ok + " dari " + paths.Count + " jalur masih menunjuk ke 0x" + t.FormatAddress(lastTarget) + ".");
    }

    private void AddSelected()
    {
        List<PointerPath> selected = SelectedPaths();

        if (selected.Count == 0)
        {
            Say("Pilih satu atau lebih jalur dulu.");
            return;
        }

        string typeName = typeCombo.Text;
        int added = 0;

        foreach (PointerPath p in selected.Take(200))
        {
            var entry = new AddressEntry
            {
                Description = "Pointer " + p.BaseText,
                Address = p.Expression,
                ValueType = typeName,
                Value = "0",
                Group = "Pointer"
            };

            if (EntryIO.TryRead(provider, entry, out string text, out _))
                entry.Value = text;

            manager.Add(entry);
            added++;
        }

        Say(added + " jalur ditambahkan ke Address Manager.");
    }

    private void BrowseTarget()
    {
        if (lastTarget == 0)
        {
            Say("Belum ada target. Jalankan Scan dulu.");
            return;
        }

        BrowseRequested?.Invoke(lastTarget.ToString("X"));
    }

    private void CopyExpression()
    {
        List<PointerPath> selected = SelectedPaths();

        if (selected.Count == 0)
            return;

        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, selected.Select(p => p.Expression)));
            Say(selected.Count + " ekspresi disalin.");
        }
        catch (Exception ex)
        {
            Say("Gagal menyalin: " + ex.Message);
        }
    }
}
