using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using RFTool.AdvancedSystem;

namespace RFTool.Advanced;

/// <summary>Halaman "Memory Scanner": First/Next Scan, hasil, kirim ke Address Manager.</summary>
internal sealed class MemoryScannerPanel : UserControl
{
    private readonly TargetProvider provider;
    private readonly AddressManager manager;
    private readonly MemoryScanner scanner = new MemoryScanner();

    private readonly Label targetLabel;
    private readonly Label foundLabel;
    private readonly Label statusLabel;
    private readonly ListView results;
    private readonly Button firstBtn;
    private readonly Button nextBtn;
    private readonly Button undoBtn;
    private readonly Button cancelBtn;
    private readonly Button addBtn;
    private readonly TextBox valueBox;
    private readonly TextBox startBox;
    private readonly TextBox stopBox;
    private readonly TextBox alignBox;
    private readonly CheckBox hexCheck;
    private readonly CheckBox unicodeCheck;
    private readonly CheckBox writableCheck;
    private readonly CheckBox execCheck;
    private readonly ComboBox scanTypeCombo;
    private readonly ComboBox valueTypeCombo;
    private readonly ProgressBar progress;
    private readonly System.Windows.Forms.Timer refreshTimer;

    private ScanMode[] modes = new[] { ScanMode.Exact, ScanMode.Unknown };
    private CancellationTokenSource? cts;
    private bool busy;

    public MemoryScannerPanel(TargetProvider provider, AddressManager manager)
    {
        this.provider = provider;
        this.manager = manager;

        Dock = DockStyle.Fill;
        BackColor = AdvTheme.Back;
        ForeColor = AdvTheme.Fore;

        // ---------- kiri: hasil ----------
        var left = new Panel { Dock = DockStyle.Fill, BackColor = AdvTheme.Back };

        results = new ListView { Dock = DockStyle.Fill, VirtualMode = true, MultiSelect = true };
        AdvTheme.StyleList(results);
        results.Columns.Add("Address", 150);
        results.Columns.Add("Value", 110);
        results.Columns.Add("Previous", 110);
        results.Columns.Add("Type", 70);
        results.RetrieveVirtualItem += Results_RetrieveVirtualItem;
        results.DoubleClick += (s, e) => AddSelected();

        foundLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            Text = "Found: 0",
            ForeColor = AdvTheme.Fore,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        targetLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            Text = "Target: belum attach",
            ForeColor = AdvTheme.Dim,
            TextAlign = ContentAlignment.MiddleLeft
        };

        left.Controls.Add(results);
        left.Controls.Add(foundLabel);
        left.Controls.Add(targetLabel);

        // ---------- kanan: opsi ----------
        var right = new Panel { Dock = DockStyle.Right, Width = 335, BackColor = AdvTheme.Back };
        int y = 4;

        firstBtn = AdvTheme.Btn("First Scan", 104).At(8, y);
        nextBtn = AdvTheme.Btn("Next Scan", 104).At(118, y);
        undoBtn = AdvTheme.Btn("Undo Scan", 104).At(228, y);
        right.Controls.Add(firstBtn);
        right.Controls.Add(nextBtn);
        right.Controls.Add(undoBtn);

        y += 40;
        right.Controls.Add(AdvTheme.Lbl("Value").At(8, y + 4));
        valueBox = AdvTheme.Txt(190).At(80, y);
        hexCheck = AdvTheme.Chk("Hex").At(278, y + 2);
        right.Controls.Add(valueBox);
        right.Controls.Add(hexCheck);

        y += 34;
        right.Controls.Add(AdvTheme.Lbl("Scan Type").At(8, y + 4));
        scanTypeCombo = AdvTheme.Cmb(240, "Exact Value", "Unknown Initial Value").At(80, y);
        right.Controls.Add(scanTypeCombo);

        y += 34;
        right.Controls.Add(AdvTheme.Lbl("Value Type").At(8, y + 4));
        valueTypeCombo = AdvTheme.Cmb(150, ValueCodec.TypeNames);
        valueTypeCombo.SelectedIndex = (int)ScanValueType.Int32;
        valueTypeCombo.Location = new Point(80, y);
        unicodeCheck = AdvTheme.Chk("Unicode").At(238, y + 2);
        unicodeCheck.Visible = false;
        right.Controls.Add(valueTypeCombo);
        right.Controls.Add(unicodeCheck);

        y += 42;
        right.Controls.Add(AdvTheme.Lbl("Memory Scan Options", true).At(8, y));

        y += 26;
        right.Controls.Add(AdvTheme.Lbl("Start").At(8, y + 4));
        startBox = AdvTheme.Txt(240, "0").At(80, y);
        right.Controls.Add(startBox);

        y += 30;
        right.Controls.Add(AdvTheme.Lbl("Stop").At(8, y + 4));
        stopBox = AdvTheme.Txt(240, "7FFFFFFFFFFF").At(80, y);
        right.Controls.Add(stopBox);

        y += 32;
        writableCheck = AdvTheme.Chk("Writable", true).At(80, y);
        execCheck = AdvTheme.Chk("Executable").At(180, y);
        right.Controls.Add(writableCheck);
        right.Controls.Add(execCheck);

        y += 30;
        right.Controls.Add(AdvTheme.Lbl("Alignment").At(8, y + 4));
        alignBox = AdvTheme.Txt(50, "4").At(80, y);
        right.Controls.Add(alignBox);
        right.Controls.Add(AdvTheme.Lbl("(Fast Scan; 1 = semua alamat)", true).At(138, y + 4));

        y += 38;
        progress = new ProgressBar { Location = new Point(8, y), Size = new Size(312, 14), Minimum = 0, Maximum = 100 };
        right.Controls.Add(progress);

        y += 22;
        cancelBtn = AdvTheme.Btn("Cancel", 100).At(8, y);
        cancelBtn.Enabled = false;
        right.Controls.Add(cancelBtn);

        y += 40;
        addBtn = AdvTheme.Btn("Add Selected to Address Manager", 312).At(8, y);
        right.Controls.Add(addBtn);

        y += 38;
        statusLabel = new Label
        {
            Location = new Point(8, y),
            Size = new Size(312, 90),
            ForeColor = AdvTheme.Dim,
            Text = "Pilih process di bagian atas, klik ATTACH, lalu isi Value dan klik First Scan."
        };
        right.Controls.Add(statusLabel);

        Controls.Add(left);
        Controls.Add(right);

        // ---------- event ----------
        firstBtn.Click += (s, e) => OnFirstOrNew();
        nextBtn.Click += (s, e) => RunScan(false);
        undoBtn.Click += (s, e) => OnUndo();
        cancelBtn.Click += (s, e) => cts?.Cancel();
        addBtn.Click += (s, e) => AddSelected();
        valueTypeCombo.SelectedIndexChanged += (s, e) => OnValueTypeChanged();
        unicodeCheck.CheckedChanged += (s, e) => OnValueTypeChanged();

        refreshTimer = new System.Windows.Forms.Timer { Interval = 800 };
        refreshTimer.Tick += (s, e) => OnTimer();
        refreshTimer.Start();

        RebuildScanTypes();
        UpdateButtons();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refreshTimer.Stop();
            refreshTimer.Dispose();
            cts?.Cancel();
        }

        base.Dispose(disposing);
    }

    // ---------------------------------------------------------
    // STATE UI
    // ---------------------------------------------------------

    private void Status(string text)
    {
        if (IsDisposed)
            return;

        statusLabel.Text = text;
    }

    private ScanValueType SelectedType()
    {
        return scanner.HasScan ? scanner.Type : (ScanValueType)valueTypeCombo.SelectedIndex;
    }

    private bool SelectedUnicode()
    {
        return scanner.HasScan ? scanner.Unicode : unicodeCheck.Checked;
    }

    private ScanMode SelectedMode()
    {
        int i = scanTypeCombo.SelectedIndex;
        return i >= 0 && i < modes.Length ? modes[i] : ScanMode.Exact;
    }

    private void RebuildScanTypes()
    {
        bool isString = SelectedType() == ScanValueType.String;
        scanTypeCombo.Items.Clear();

        if (!scanner.HasScan)
        {
            if (isString)
            {
                modes = new[] { ScanMode.Exact };
                scanTypeCombo.Items.Add("Exact Value");
            }
            else
            {
                modes = new[] { ScanMode.Exact, ScanMode.Unknown };
                scanTypeCombo.Items.Add("Exact Value");
                scanTypeCombo.Items.Add("Unknown Initial Value");
            }
        }
        else if (isString)
        {
            modes = new[] { ScanMode.Exact };
            scanTypeCombo.Items.Add("Exact Value");
        }
        else
        {
            modes = new[] { ScanMode.Exact, ScanMode.Increased, ScanMode.Decreased, ScanMode.Unchanged, ScanMode.Changed };
            scanTypeCombo.Items.Add("Exact Value");
            scanTypeCombo.Items.Add("Increased Value");
            scanTypeCombo.Items.Add("Decreased Value");
            scanTypeCombo.Items.Add("Unchanged Value");
            scanTypeCombo.Items.Add("Changed Value");
        }

        scanTypeCombo.SelectedIndex = 0;
    }

    private void OnValueTypeChanged()
    {
        if (scanner.HasScan)
            return;

        var type = (ScanValueType)valueTypeCombo.SelectedIndex;
        unicodeCheck.Visible = type == ScanValueType.String;
        hexCheck.Enabled = type != ScanValueType.Float && type != ScanValueType.Double && type != ScanValueType.String;
        alignBox.Text = ValueCodec.DefaultAlignment(type, unicodeCheck.Checked).ToString();
        RebuildScanTypes();
    }

    private void UpdateButtons()
    {
        bool has = scanner.HasScan;
        firstBtn.Text = has ? "New Scan" : "First Scan";
        firstBtn.Enabled = !busy;
        nextBtn.Enabled = has && !busy;
        undoBtn.Enabled = scanner.CanUndo && !busy;
        cancelBtn.Enabled = busy;
        addBtn.Enabled = !busy && scanner.ResultCount > 0;
        valueTypeCombo.Enabled = !has && !busy;
        unicodeCheck.Enabled = !has && !busy;
        startBox.Enabled = !has && !busy;
        stopBox.Enabled = !has && !busy;
        alignBox.Enabled = !has && !busy;
        writableCheck.Enabled = !has && !busy;
        execCheck.Enabled = !has && !busy;
        scanTypeCombo.Enabled = !busy;
        valueBox.Enabled = !busy;
    }

    private void UpdateFound()
    {
        if (scanner.IsSnapshot)
        {
            foundLabel.Text = "Snapshot: " + (scanner.SnapshotBytes / (1024 * 1024)) + " MB (lanjut Next Scan)";
            results.VirtualListSize = 0;
        }
        else
        {
            foundLabel.Text = "Found: " + scanner.ResultCount.ToString("N0");
            results.VirtualListSize = scanner.ResultCount;
        }

        results.Invalidate();
    }

    private void OnTimer()
    {
        if (IsDisposed || Parent == null || !Visible)
            return;

        TargetProcess? t = provider.Get();
        targetLabel.Text = t == null
            ? "Target: belum attach"
            : "Target: " + t.Name + " (PID " + t.Id + ", " + (t.Is32Bit ? "32-bit" : "64-bit") + ")";

        if (!busy && scanner.ResultCount > 0)
            results.Invalidate();
    }

    // ---------------------------------------------------------
    // SCAN
    // ---------------------------------------------------------

    private void OnFirstOrNew()
    {
        if (busy)
            return;

        if (scanner.HasScan)
        {
            scanner.Reset();
            valueTypeCombo.Enabled = true;
            RebuildScanTypes();
            OnValueTypeChanged();
            UpdateFound();
            UpdateButtons();
            progress.Value = 0;
            Status("Scan direset. Isi Value lalu klik First Scan.");
            return;
        }

        RunScan(true);
    }

    private void OnUndo()
    {
        if (busy || !scanner.CanUndo)
            return;

        scanner.Undo();
        RebuildScanTypes();
        UpdateFound();
        UpdateButtons();
        Status("Undo scan berhasil.");
    }

    private void ReportProgress(int percent)
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        BeginInvoke(new Action(() =>
        {
            if (!IsDisposed)
                progress.Value = Math.Clamp(percent, 0, 100);
        }));
    }

    private bool BuildSpec(TargetProcess t, ScanValueType type, bool unicode, byte[]? value, out ScanSpec? spec)
    {
        spec = null;

        if (!AddressExpression.TryEvaluate(t, startBox.Text, out ulong start, out string err1))
        {
            Status("Start tidak valid: " + err1);
            return false;
        }

        if (!AddressExpression.TryEvaluate(t, stopBox.Text, out ulong stop, out string err2))
        {
            Status("Stop tidak valid: " + err2);
            return false;
        }

        if (stop > t.MaxAddress)
            stop = t.MaxAddress;

        if (stop <= start)
        {
            Status("Stop harus lebih besar dari Start.");
            return false;
        }

        int align = ValueCodec.DefaultAlignment(type, unicode);

        if (int.TryParse(alignBox.Text.Trim(), out int a) && a >= 1 && a <= 4096)
            align = a;

        spec = new ScanSpec
        {
            Type = type,
            Value = value,
            Unicode = unicode,
            Start = start,
            Stop = stop,
            WritableOnly = writableCheck.Checked,
            IncludeExecutable = execCheck.Checked,
            Alignment = align
        };

        return true;
    }

    private async void RunScan(bool first)
    {
        if (busy)
            return;

        TargetProcess? t = provider.Get();

        if (t == null)
        {
            Status("Belum attach ke process. Pilih process di bagian atas lalu klik ATTACH.");
            return;
        }

        ScanMode mode = SelectedMode();
        ScanValueType type = SelectedType();
        bool unicode = SelectedUnicode();
        byte[]? value = null;

        if (mode == ScanMode.Exact)
        {
            if (!ValueCodec.TryParse(valueBox.Text, type, hexCheck.Checked, unicode, out byte[] parsed, out string error))
            {
                Status(error);
                return;
            }

            value = parsed;
        }

        ScanSpec? spec = null;

        if (first && !BuildSpec(t, type, unicode, value, out spec))
            return;

        busy = true;
        UpdateButtons();
        progress.Value = 0;
        Status(first ? "Memindai memori..." : "Mempersempit hasil...");

        cts = new CancellationTokenSource();
        CancellationToken token = cts.Token;
        var sw = Stopwatch.StartNew();

        try
        {
            await Task.Run(() =>
            {
                if (first)
                    scanner.FirstScan(t, spec!, mode, ReportProgress, token);
                else
                    scanner.NextScan(t, mode, value, ReportProgress, token);
            });

            Status("Selesai dalam " + sw.Elapsed.TotalSeconds.ToString("0.0") + " detik.");
            DebugLog.Info("Scan " + (first ? "pertama" : "lanjutan") + " selesai: " + scanner.ResultCount + " hasil.");
        }
        catch (OperationCanceledException)
        {
            Status("Scan dibatalkan.");
        }
        catch (ScanLimitException ex)
        {
            Status(ex.Message);
        }
        catch (Exception ex)
        {
            Status("Scan gagal: " + ex.Message);
            DebugLog.Error("Scan gagal: " + ex.Message);
        }
        finally
        {
            busy = false;
            cts?.Dispose();
            cts = null;

            if (!IsDisposed)
            {
                progress.Value = 0;
                RebuildScanTypes();
                UpdateFound();
                UpdateButtons();
            }
        }
    }

    // ---------------------------------------------------------
    // HASIL
    // ---------------------------------------------------------

    private string ReadCurrentText(TargetProcess? t, ulong address)
    {
        if (t == null)
            return "??";

        int size = scanner.ValueSize;
        byte[]? data = t.ReadBytes(address, size);

        return data == null ? "??" : ValueCodec.Format(data, scanner.Type, scanner.Unicode);
    }

    private void Results_RetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
    {
        if (!scanner.HasScan || scanner.IsSnapshot || e.ItemIndex < 0 || e.ItemIndex >= scanner.ResultCount)
        {
            e.Item = new ListViewItem(new[] { "", "", "", "" });
            return;
        }

        TargetProcess? t = provider.Get();
        ulong address = scanner.AddressAt(e.ItemIndex);
        string addressText = t != null ? t.FormatAddress(address) : address.ToString("X8");
        string previous = ValueCodec.Format(scanner.PreviousAt(e.ItemIndex), scanner.Type, scanner.Unicode);

        e.Item = new ListViewItem(new[]
        {
            addressText,
            ReadCurrentText(t, address),
            previous,
            ValueCodec.Name(scanner.Type)
        });
    }

    private void AddSelected()
    {
        if (busy || scanner.IsSnapshot || scanner.ResultCount == 0)
            return;

        TargetProcess? t = provider.Get();

        if (t == null)
        {
            Status("Belum attach ke process.");
            return;
        }

        int added = 0;

        foreach (int index in results.SelectedIndices)
        {
            if (index < 0 || index >= scanner.ResultCount)
                continue;

            ulong address = scanner.AddressAt(index);

            manager.Add(new AddressEntry
            {
                Description = "No description",
                Address = t.Describe(address),
                ValueType = ValueCodec.Name(scanner.Type),
                Value = ReadCurrentText(t, address),
                Group = "Default"
            });

            added++;

            if (added >= 500)
                break;
        }

        Status(added == 0
            ? "Pilih satu atau lebih baris hasil dulu."
            : added + " alamat ditambahkan ke Address Manager.");
    }
}
