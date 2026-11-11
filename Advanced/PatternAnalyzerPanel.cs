using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using RFTool.AdvancedSystem;

namespace RFTool.Advanced;

internal sealed class SavedPattern
{
    public string Name { get; set; } = "";
    public string Group { get; set; } = "Default";
    public string Pattern { get; set; } = "";
}

/// <summary>Penyimpanan pola (patterns.json di folder aplikasi).</summary>
internal sealed class PatternStore
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { WriteIndented = true };
    private readonly string path;

    public List<SavedPattern> Items { get; private set; } = new List<SavedPattern>();

    public PatternStore(string path)
    {
        this.path = path;
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(path))
            {
                Items = JsonSerializer.Deserialize<List<SavedPattern>>(File.ReadAllText(path), Options)
                        ?? new List<SavedPattern>();
            }
        }
        catch (Exception ex)
        {
            DebugLog.Warn("Gagal memuat patterns.json: " + ex.Message);
            Items = new List<SavedPattern>();
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(Items, Options));
        }
        catch (Exception ex)
        {
            DebugLog.Warn("Gagal menyimpan patterns.json: " + ex.Message);
        }
    }

    public void AddOrReplace(SavedPattern pattern)
    {
        Items.RemoveAll(p =>
            string.Equals(p.Name, pattern.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.Group, pattern.Group, StringComparison.OrdinalIgnoreCase));

        Items.Add(pattern);
        Items = Items.OrderBy(p => p.Group, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                     .ToList();
        Save();
    }

    public void Remove(SavedPattern pattern)
    {
        Items.Remove(pattern);
        Save();
    }
}

/// <summary>Halaman "Pattern Analyzer": cari pola byte, uji keunikan, simpan per fitur.</summary>
internal sealed class PatternAnalyzerPanel : UserControl
{
    private const int MaxHits = 2000;

    private readonly TargetProvider provider;
    private readonly AddressManager manager;
    private readonly PatternStore store;

    private readonly TextBox patternBox;
    private readonly ComboBox scopeCombo;
    private readonly Button scanBtn;
    private readonly Button cancelBtn;
    private readonly Label verdict;
    private readonly ProgressBar progress;
    private readonly ListView results;
    private readonly TextBox nameBox;
    private readonly TextBox groupBox;
    private readonly ListView savedList;
    private readonly Label status;

    private List<ulong> hits = new List<ulong>();
    private CancellationTokenSource? cts;
    private bool busy;

    public event Action<string>? BrowseRequested;
    public event Action<string>? DisassembleRequested;

    public PatternAnalyzerPanel(TargetProvider provider, AddressManager manager)
    {
        this.provider = provider;
        this.manager = manager;
        store = new PatternStore(Path.Combine(Application.StartupPath, "patterns.json"));

        Dock = DockStyle.Fill;
        BackColor = AdvTheme.Back;
        ForeColor = AdvTheme.Fore;

        // ---------- atas ----------
        var top = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = AdvTheme.Back };

        top.Controls.Add(AdvTheme.Lbl("Pattern").At(0, 8));
        patternBox = AdvTheme.Txt(480, "48 8B ?? ?? 89").At(60, 4);
        top.Controls.Add(patternBox);

        top.Controls.Add(AdvTheme.Lbl("Scope").At(0, 44));
        scopeCombo = AdvTheme.Cmb(190, "Semua memori", "Hanya executable");
        scopeCombo.Location = new Point(60, 40);
        scopeCombo.DropDown += (s, e) => RefreshScopes();
        top.Controls.Add(scopeCombo);

        scanBtn = AdvTheme.Btn("Scan", 80).At(260, 39);
        cancelBtn = AdvTheme.Btn("Cancel", 80).At(346, 39);
        cancelBtn.Enabled = false;
        top.Controls.Add(scanBtn);
        top.Controls.Add(cancelBtn);

        verdict = new Label
        {
            Location = new Point(436, 44),
            Size = new Size(300, 22),
            ForeColor = AdvTheme.Dim,
            Text = "Belum dipindai"
        };
        top.Controls.Add(verdict);

        progress = new ProgressBar { Location = new Point(0, 74), Size = new Size(540, 12), Minimum = 0, Maximum = 100 };
        top.Controls.Add(progress);

        // ---------- hasil ----------
        results = new ListView { Dock = DockStyle.Fill, MultiSelect = true };
        AdvTheme.StyleList(results);
        results.Columns.Add("Address", 140);
        results.Columns.Add("Module + offset", 230);
        results.Columns.Add("Protect", 140);

        // ---------- bawah: pola tersimpan ----------
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 240, BackColor = AdvTheme.Back };

        savedList = new ListView { Dock = DockStyle.Fill };
        AdvTheme.StyleList(savedList);
        savedList.Columns.Add("Name", 150);
        savedList.Columns.Add("Group", 110);
        savedList.Columns.Add("Pattern", 360);

        status = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            ForeColor = AdvTheme.Dim,
            Text = "Simpan pola per fitur. File: patterns.json di folder aplikasi."
        };

        var row = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = AdvTheme.Back };
        row.Controls.Add(AdvTheme.Lbl("Name").At(0, 8));
        nameBox = AdvTheme.Txt(150).At(46, 4);
        row.Controls.Add(nameBox);
        row.Controls.Add(AdvTheme.Lbl("Group").At(206, 8));
        groupBox = AdvTheme.Txt(120, "Default").At(252, 4);
        row.Controls.Add(groupBox);

        Button saveBtn = AdvTheme.Btn("Save", 70).At(0, 36);
        Button loadBtn = AdvTheme.Btn("Load", 70).At(76, 36);
        Button testBtn = AdvTheme.Btn("Load + Scan", 100).At(152, 36);
        Button deleteBtn = AdvTheme.Btn("Delete", 70).At(258, 36);
        row.Controls.Add(saveBtn);
        row.Controls.Add(loadBtn);
        row.Controls.Add(testBtn);
        row.Controls.Add(deleteBtn);

        bottom.Controls.Add(savedList);
        bottom.Controls.Add(status);
        bottom.Controls.Add(row);

        Controls.Add(results);
        Controls.Add(bottom);
        Controls.Add(top);

        // ---------- event ----------
        scanBtn.Click += (s, e) => RunScan();
        cancelBtn.Click += (s, e) => cts?.Cancel();
        saveBtn.Click += (s, e) => SavePattern();
        loadBtn.Click += (s, e) => LoadSelected(false);
        testBtn.Click += (s, e) => LoadSelected(true);
        deleteBtn.Click += (s, e) => DeleteSelected();
        savedList.DoubleClick += (s, e) => LoadSelected(false);
        results.DoubleClick += (s, e) => BrowseSelected();

        ContextMenuStrip menu = AdvTheme.NewMenu();
        menu.Items.Add(AdvTheme.Item("Browse this memory region", BrowseSelected, Keys.Control | Keys.B, true));
        menu.Items.Add(AdvTheme.Item("Disassemble this memory region", DisassembleSelected, Keys.Control | Keys.D));
        menu.Items.Add(AdvTheme.Item("Add to Address Manager (4 Bytes)", AddSelected));
        menu.Items.Add(AdvTheme.Item("Copy address", CopySelected, Keys.Control | Keys.C));
        results.ContextMenuStrip = menu;

        RefreshSaved();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            cts?.Cancel();

        base.Dispose(disposing);
    }

    private void Say(string text)
    {
        status.Text = text;
    }

    // ---------------------------------------------------------
    // SCAN
    // ---------------------------------------------------------

    private void RefreshScopes()
    {
        string current = scopeCombo.Text;
        TargetProcess? t = provider.Get();

        while (scopeCombo.Items.Count > 2)
            scopeCombo.Items.RemoveAt(scopeCombo.Items.Count - 1);

        if (t != null)
        {
            foreach (ModuleInfo m in t.GetModules())
                scopeCombo.Items.Add("Module: " + m.Name);
        }

        int index = scopeCombo.Items.IndexOf(current);
        scopeCombo.SelectedIndex = index >= 0 ? index : 0;
    }

    private Func<MemRegion, bool>? BuildFilter(TargetProcess t, string scope)
    {
        if (scope == "Hanya executable")
            return r => r.IsExecutable;

        if (scope.StartsWith("Module: ", StringComparison.Ordinal))
        {
            string name = scope.Substring(8);

            foreach (ModuleInfo m in t.GetModules())
            {
                if (string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    ulong lo = m.Base;
                    ulong hi = m.End;
                    return r => r.Base < hi && r.End > lo;
                }
            }
        }

        return null;
    }

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

        if (!AobPattern.TryParse(patternBox.Text, out AobPattern? pattern, out string error) || pattern == null)
        {
            Say("Pattern tidak valid: " + error);
            return;
        }

        Func<MemRegion, bool>? filter = BuildFilter(t, scopeCombo.Text);
        var found = new List<ulong>();

        busy = true;
        scanBtn.Enabled = false;
        cancelBtn.Enabled = true;
        results.Items.Clear();
        progress.Value = 0;
        verdict.ForeColor = AdvTheme.Dim;
        verdict.Text = "Memindai...";

        cts = new CancellationTokenSource();
        CancellationToken token = cts.Token;

        try
        {
            await Task.Run(() =>
            {
                PatternSearcher.Search(
                    t, pattern, 0, ulong.MaxValue, filter,
                    address =>
                    {
                        found.Add(address);
                        return found.Count < MaxHits;
                    },
                    percent => ReportProgress(percent),
                    token);
            });

            hits = found;
            ShowResults(t);
        }
        catch (OperationCanceledException)
        {
            verdict.Text = "Dibatalkan";
        }
        catch (Exception ex)
        {
            verdict.Text = "Gagal";
            Say("Scan gagal: " + ex.Message);
            DebugLog.Error("Pattern scan gagal: " + ex.Message);
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

    private void ShowResults(TargetProcess t)
    {
        var items = new List<ListViewItem>();

        foreach (ulong address in hits)
        {
            string protect = t.QueryRegion(address, out MemRegion r) ? r.ProtectText : "-";

            items.Add(new ListViewItem(new[]
            {
                t.FormatAddress(address),
                t.Describe(address),
                protect
            }));
        }

        results.Items.AddRange(items.ToArray());

        if (hits.Count == 1)
        {
            verdict.ForeColor = AdvTheme.Accent;
            verdict.Text = "UNIK (1 hasil)";
        }
        else if (hits.Count == 0)
        {
            verdict.ForeColor = Color.FromArgb(255, 120, 120);
            verdict.Text = "TIDAK DITEMUKAN";
        }
        else
        {
            verdict.ForeColor = Color.FromArgb(255, 200, 90);
            verdict.Text = (hits.Count >= MaxHits ? MaxHits + "+" : hits.Count.ToString()) + " hasil (tidak unik)";
        }

        DebugLog.Info("Pattern scan '" + patternBox.Text.Trim() + "': " + hits.Count + " hasil.");
    }

    // ---------------------------------------------------------
    // HASIL
    // ---------------------------------------------------------

    private List<ulong> SelectedAddresses()
    {
        var list = new List<ulong>();

        foreach (int i in results.SelectedIndices)
        {
            if (i >= 0 && i < hits.Count)
                list.Add(hits[i]);
        }

        return list;
    }

    private void BrowseSelected()
    {
        List<ulong> sel = SelectedAddresses();

        if (sel.Count == 0)
        {
            Say("Pilih satu hasil dulu.");
            return;
        }

        BrowseRequested?.Invoke(sel[0].ToString("X"));
    }

    private void DisassembleSelected()
    {
        List<ulong> sel = SelectedAddresses();

        if (sel.Count == 0)
        {
            Say("Pilih satu hasil dulu.");
            return;
        }

        DisassembleRequested?.Invoke(sel[0].ToString("X"));
    }

    private void AddSelected()
    {
        TargetProcess? t = provider.Get();
        List<ulong> sel = SelectedAddresses();

        if (t == null || sel.Count == 0)
        {
            Say("Pilih hasil dan pastikan sudah attach.");
            return;
        }

        foreach (ulong address in sel.Take(200))
        {
            var entry = new AddressEntry
            {
                Description = "Pattern " + (nameBox.Text.Length > 0 ? nameBox.Text : "hasil"),
                Address = t.Describe(address),
                ValueType = "4 Bytes",
                Signed = true,
                Value = "0",
                Group = groupBox.Text.Length > 0 ? groupBox.Text : "Default"
            };

            if (EntryIO.TryRead(provider, entry, out string text, out _))
                entry.Value = text;

            manager.Add(entry);
        }

        Say(Math.Min(sel.Count, 200) + " alamat ditambahkan ke Address Manager.");
    }

    private void CopySelected()
    {
        TargetProcess? t = provider.Get();
        List<ulong> sel = SelectedAddresses();

        if (sel.Count == 0)
            return;

        try
        {
            Clipboard.SetText(string.Join(
                Environment.NewLine,
                sel.Select(a => t != null ? t.FormatAddress(a) : a.ToString("X"))));
            Say(sel.Count + " alamat disalin.");
        }
        catch (Exception ex)
        {
            Say("Gagal menyalin: " + ex.Message);
        }
    }

    // ---------------------------------------------------------
    // POLA TERSIMPAN
    // ---------------------------------------------------------

    private void RefreshSaved()
    {
        savedList.Items.Clear();

        foreach (SavedPattern p in store.Items)
            savedList.Items.Add(new ListViewItem(new[] { p.Name, p.Group, p.Pattern }));
    }

    private SavedPattern? SelectedSaved()
    {
        if (savedList.SelectedIndices.Count == 0)
            return null;

        int index = savedList.SelectedIndices[0];
        return index >= 0 && index < store.Items.Count ? store.Items[index] : null;
    }

    private void SavePattern()
    {
        string name = nameBox.Text.Trim();

        if (name.Length == 0)
        {
            Say("Isi Name dulu.");
            return;
        }

        if (!AobPattern.TryParse(patternBox.Text, out _, out string error))
        {
            Say("Pattern tidak valid: " + error);
            return;
        }

        store.AddOrReplace(new SavedPattern
        {
            Name = name,
            Group = groupBox.Text.Trim().Length > 0 ? groupBox.Text.Trim() : "Default",
            Pattern = patternBox.Text.Trim()
        });

        RefreshSaved();
        Say("Pola '" + name + "' disimpan.");
    }

    private void LoadSelected(bool scanAfter)
    {
        SavedPattern? p = SelectedSaved();

        if (p == null)
        {
            Say("Pilih pola tersimpan dulu.");
            return;
        }

        patternBox.Text = p.Pattern;
        nameBox.Text = p.Name;
        groupBox.Text = p.Group;

        if (scanAfter)
            RunScan();
    }

    private void DeleteSelected()
    {
        SavedPattern? p = SelectedSaved();

        if (p == null)
            return;

        store.Remove(p);
        RefreshSaved();
        Say("Pola '" + p.Name + "' dihapus.");
    }
}
