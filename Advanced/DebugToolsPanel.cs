using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace RFTool.Advanced;

/// <summary>Halaman "Debug Tools": info process, module, region, validasi/baca alamat, log.</summary>
internal sealed class DebugToolsPanel : UserControl
{
    private readonly TargetProvider provider;
    private readonly List<Panel> pages = new List<Panel>();
    private readonly List<Button> tabButtons = new List<Button>();

    private readonly TextBox processBox;
    private readonly ListView moduleList;
    private readonly ListView regionList;
    private readonly CheckBox committedOnly;
    private readonly TextBox addressBox;
    private readonly TextBox lengthBox;
    private readonly TextBox checkOutput;
    private readonly TextBox logBox;

    public DebugToolsPanel(TargetProvider provider)
    {
        this.provider = provider;

        Dock = DockStyle.Fill;
        BackColor = AdvTheme.Back;
        ForeColor = AdvTheme.Fore;

        string[] names = { "Process", "Modules", "Regions", "Validate / Read", "Log" };

        var strip = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = AdvTheme.Back,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };

        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            Button b = AdvTheme.Btn(names[i], 120);
            b.Click += (s, e) => ShowPage(index);
            tabButtons.Add(b);
            strip.Controls.Add(b);
        }

        var host = new Panel { Dock = DockStyle.Fill, BackColor = AdvTheme.Back };

        // ---------- 1. Process ----------
        Panel p1 = NewPage(host);
        processBox = NewOutput();
        Button refresh1 = AdvTheme.Btn("Refresh", 90);
        refresh1.Dock = DockStyle.Top;
        refresh1.Width = 90;
        p1.Controls.Add(processBox);
        p1.Controls.Add(MakeToolbar(refresh1));
        refresh1.Click += (s, e) => RefreshProcess();

        // ---------- 2. Modules ----------
        Panel p2 = NewPage(host);
        moduleList = new ListView { Dock = DockStyle.Fill };
        AdvTheme.StyleList(moduleList);
        moduleList.Columns.Add("Module", 160);
        moduleList.Columns.Add("Base", 130);
        moduleList.Columns.Add("Size", 90);
        moduleList.Columns.Add("Path", 420);
        Button refresh2 = AdvTheme.Btn("Refresh", 90);
        p2.Controls.Add(moduleList);
        p2.Controls.Add(MakeToolbar(refresh2));
        refresh2.Click += (s, e) => RefreshModules();

        // ---------- 3. Regions ----------
        Panel p3 = NewPage(host);
        regionList = new ListView { Dock = DockStyle.Fill };
        AdvTheme.StyleList(regionList);
        regionList.Columns.Add("Base", 130);
        regionList.Columns.Add("Size", 90);
        regionList.Columns.Add("State", 70);
        regionList.Columns.Add("Protect", 140);
        regionList.Columns.Add("Type", 70);
        regionList.Columns.Add("Module", 160);
        Button refresh3 = AdvTheme.Btn("Refresh", 90);
        committedOnly = AdvTheme.Chk("Commit saja", true);
        committedOnly.Margin = new Padding(6, 5, 0, 0);
        p3.Controls.Add(regionList);
        p3.Controls.Add(MakeToolbar(refresh3, committedOnly));
        refresh3.Click += (s, e) => RefreshRegions();

        // ---------- 4. Validate / Read ----------
        Panel p4 = NewPage(host);
        checkOutput = NewOutput();
        addressBox = AdvTheme.Txt(260, "RF_Online.bin+C28B40");
        addressBox.Margin = new Padding(0, 2, 6, 0);
        lengthBox = AdvTheme.Txt(50, "16");
        lengthBox.Margin = new Padding(0, 2, 6, 0);
        Button validateBtn = AdvTheme.Btn("Validate", 80);
        Button readBtn = AdvTheme.Btn("Read", 70);

        var bar4 = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = AdvTheme.Back,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };

        bar4.Controls.Add(addressBox);
        bar4.Controls.Add(AdvTheme.Lbl("Panjang").Also(l => l.Margin = new Padding(0, 6, 4, 0)));
        bar4.Controls.Add(lengthBox);
        bar4.Controls.Add(validateBtn);
        bar4.Controls.Add(readBtn);

        p4.Controls.Add(checkOutput);
        p4.Controls.Add(bar4);
        validateBtn.Click += (s, e) => Validate();
        readBtn.Click += (s, e) => ReadDiagnostics();

        // ---------- 5. Log ----------
        Panel p5 = NewPage(host);
        logBox = NewOutput();
        Button clearBtn = AdvTheme.Btn("Clear", 80);
        Button saveBtn = AdvTheme.Btn("Save Log", 90);
        p5.Controls.Add(logBox);
        p5.Controls.Add(MakeToolbar(clearBtn, saveBtn));
        clearBtn.Click += (s, e) => { DebugLog.Clear(); logBox.Clear(); };
        saveBtn.Click += (s, e) => SaveLog();

        Controls.Add(host);
        Controls.Add(strip);

        DebugLog.LineAdded += OnLogLine;
        ShowPage(0);
        LoadLogHistory();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            DebugLog.LineAdded -= OnLogLine;

        base.Dispose(disposing);
    }

    // ---------------------------------------------------------
    // PEMBANGUN UI
    // ---------------------------------------------------------

    private Panel NewPage(Panel host)
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = AdvTheme.Back, Visible = false };
        host.Controls.Add(p);
        pages.Add(p);
        return p;
    }

    private static TextBox NewOutput()
    {
        return new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = AdvTheme.Mono,
            BackColor = AdvTheme.Input,
            ForeColor = AdvTheme.Fore,
            BorderStyle = BorderStyle.None
        };
    }

    private static FlowLayoutPanel MakeToolbar(params Control[] controls)
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = AdvTheme.Back,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };

        foreach (Control c in controls)
        {
            c.Dock = DockStyle.None;
            bar.Controls.Add(c);
        }

        return bar;
    }

    private void ShowPage(int index)
    {
        for (int i = 0; i < pages.Count; i++)
        {
            pages[i].Visible = i == index;
            tabButtons[i].BackColor = i == index ? AdvTheme.SelectBack : AdvTheme.Input;
        }

        switch (index)
        {
            case 0: RefreshProcess(); break;
            case 1: RefreshModules(); break;
            case 2: RefreshRegions(); break;
        }
    }

    // ---------------------------------------------------------
    // PROCESS
    // ---------------------------------------------------------

    private void RefreshProcess()
    {
        TargetProcess? t = provider.Get();

        if (t == null)
        {
            processBox.Text = "Belum attach ke process." + Environment.NewLine +
                              "Pilih process di bagian atas aplikasi lalu klik ATTACH.";
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Nama            : " + t.Name);
        sb.AppendLine("PID             : " + t.Id);
        sb.AppendLine("Arsitektur      : " + (t.Is32Bit ? "32-bit" : "64-bit"));
        sb.AppendLine("Ukuran pointer  : " + t.PointerSize + " byte");
        sb.AppendLine("Alamat maksimum : 0x" + t.MaxAddress.ToString("X"));

        try
        {
            using Process p = Process.GetProcessById(t.Id);

            Append(sb, "Path            ", () => p.MainModule?.FileName ?? "-");
            Append(sb, "Judul jendela   ", () => string.IsNullOrEmpty(p.MainWindowTitle) ? "-" : p.MainWindowTitle);
            Append(sb, "Waktu mulai     ", () => p.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
            Append(sb, "Thread          ", () => p.Threads.Count.ToString());
            Append(sb, "Handle          ", () => p.HandleCount.ToString());
            Append(sb, "Working set     ", () => (p.WorkingSet64 / (1024 * 1024)) + " MB");
            Append(sb, "Private memory  ", () => (p.PrivateMemorySize64 / (1024 * 1024)) + " MB");
            Append(sb, "Prioritas       ", () => p.PriorityClass.ToString());
            Append(sb, "Sudah exit      ", () => p.HasExited ? "Ya" : "Tidak");
        }
        catch (Exception ex)
        {
            sb.AppendLine("Info tambahan   : gagal (" + ex.Message + ")");
        }

        sb.AppendLine();
        sb.AppendLine("Diagnostik baca/tulis");
        sb.AppendLine("Read gagal      : " + t.ReadFailures);
        sb.AppendLine("Write gagal     : " + t.WriteFailures);
        sb.AppendLine("Error terakhir  : " + t.LastError + " - " + TargetProcess.ErrorText(t.LastError));

        processBox.Text = sb.ToString();
    }

    private static void Append(StringBuilder sb, string label, Func<string> getter)
    {
        string value;

        try
        {
            value = getter();
        }
        catch (Exception ex)
        {
            value = "tidak tersedia (" + ex.Message + ")";
        }

        sb.AppendLine(label + ": " + value);
    }

    // ---------------------------------------------------------
    // MODULES
    // ---------------------------------------------------------

    private void RefreshModules()
    {
        moduleList.Items.Clear();
        TargetProcess? t = provider.Get();

        if (t == null)
            return;

        foreach (ModuleInfo m in t.GetModules(true))
        {
            moduleList.Items.Add(new ListViewItem(new[]
            {
                m.Name,
                t.FormatAddress(m.Base),
                m.Size.ToString("X"),
                m.Path
            }));
        }
    }

    // ---------------------------------------------------------
    // REGIONS
    // ---------------------------------------------------------

    private void RefreshRegions()
    {
        regionList.Items.Clear();
        TargetProcess? t = provider.Get();

        if (t == null)
            return;

        var items = new List<ListViewItem>();

        foreach (MemRegion r in t.EnumerateRegions(0, ulong.MaxValue))
        {
            if (committedOnly.Checked && !r.IsCommitted)
                continue;

            ModuleInfo? m = t.FindModule(r.Base);

            items.Add(new ListViewItem(new[]
            {
                t.FormatAddress(r.Base),
                r.Size.ToString("X"),
                r.StateText,
                r.ProtectText,
                r.TypeText,
                m != null ? m.Name : ""
            }));

            if (items.Count >= 20000)
                break;
        }

        regionList.Items.AddRange(items.ToArray());
    }

    // ---------------------------------------------------------
    // VALIDATE / READ
    // ---------------------------------------------------------

    private bool ResolveInput(out TargetProcess? target, out ulong address, out string error)
    {
        address = 0;
        error = "";
        target = provider.Get();

        if (target == null)
        {
            error = "Belum attach ke process.";
            return false;
        }

        return AddressExpression.TryEvaluate(target, addressBox.Text, out address, out error);
    }

    private void Validate()
    {
        if (!ResolveInput(out TargetProcess? t, out ulong address, out string error) || t == null)
        {
            checkOutput.Text = "Gagal: " + error;
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Input           : " + addressBox.Text.Trim());
        sb.AppendLine("Alamat          : 0x" + t.FormatAddress(address));
        sb.AppendLine("Module+offset   : " + t.Describe(address));

        if (t.QueryRegion(address, out MemRegion r))
        {
            sb.AppendLine("Region base     : 0x" + t.FormatAddress(r.Base));
            sb.AppendLine("Region size     : 0x" + r.Size.ToString("X"));
            sb.AppendLine("State           : " + r.StateText);
            sb.AppendLine("Protect         : " + r.ProtectText);
            sb.AppendLine("Type            : " + r.TypeText);
            sb.AppendLine("Bisa dibaca     : " + (r.IsReadable ? "Ya" : "Tidak"));
            sb.AppendLine("Bisa ditulis    : " + (r.IsWritable ? "Ya" : "Tidak"));
            sb.AppendLine("Executable      : " + (r.IsExecutable ? "Ya" : "Tidak"));
        }
        else
        {
            sb.AppendLine("Region          : tidak dapat di-query (alamat di luar jangkauan?)");
        }

        byte[]? probe = t.ReadBytes(address, 1);
        sb.AppendLine("Uji baca 1 byte : " + (probe != null
            ? "OK (" + probe[0].ToString("X2") + ")"
            : "GAGAL - " + TargetProcess.ErrorText(t.LastError)));

        checkOutput.Text = sb.ToString();
        DebugLog.Info("Validate " + addressBox.Text.Trim() + " -> 0x" + t.FormatAddress(address));
    }

    private void ReadDiagnostics()
    {
        if (!ResolveInput(out TargetProcess? t, out ulong address, out string error) || t == null)
        {
            checkOutput.Text = "Gagal: " + error;
            return;
        }

        if (!int.TryParse(lengthBox.Text.Trim(), out int length) || length < 1 || length > 4096)
        {
            checkOutput.Text = "Panjang harus 1 sampai 4096.";
            return;
        }

        byte[] buffer = new byte[length];
        bool ok = t.TryRead(address, buffer, length, out int got);

        var sb = new StringBuilder();
        sb.AppendLine("Alamat          : 0x" + t.FormatAddress(address));
        sb.AppendLine("Diminta         : " + length + " byte");
        sb.AppendLine("Terbaca         : " + got + " byte");
        sb.AppendLine("Hasil           : " + (ok ? "OK" : "GAGAL (" + t.LastError + " - " + TargetProcess.ErrorText(t.LastError) + ")"));
        sb.AppendLine();

        for (int i = 0; i < got; i += 16)
        {
            sb.Append((address + (ulong)i).ToString(t.Is32Bit ? "X8" : "X16", CultureInfo.InvariantCulture));
            sb.Append("  ");

            var ascii = new StringBuilder();

            for (int j = 0; j < 16; j++)
            {
                if (i + j < got)
                {
                    byte b = buffer[i + j];
                    sb.Append(b.ToString("X2", CultureInfo.InvariantCulture)).Append(' ');
                    ascii.Append(b >= 32 && b < 127 ? (char)b : '.');
                }
                else
                {
                    sb.Append("   ");
                }
            }

            sb.Append(' ').AppendLine(ascii.ToString());
        }

        checkOutput.Text = sb.ToString();
        DebugLog.Info("Read " + got + "/" + length + " byte dari 0x" + t.FormatAddress(address) + (ok ? " OK" : " GAGAL"));
    }

    // ---------------------------------------------------------
    // LOG
    // ---------------------------------------------------------

    private void LoadLogHistory()
    {
        logBox.Text = string.Join(Environment.NewLine, DebugLog.Snapshot());
        logBox.SelectionStart = logBox.TextLength;
        logBox.ScrollToCaret();
    }

    private void OnLogLine(string line)
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        try
        {
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed)
                    return;

                logBox.AppendText(line + Environment.NewLine);
            }));
        }
        catch
        {
        }
    }

    private void SaveLog()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Simpan log pemeriksaan",
            Filter = "Teks (*.txt)|*.txt",
            FileName = "muiz-debug-log.txt",
            InitialDirectory = Application.StartupPath
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            File.WriteAllLines(dialog.FileName, DebugLog.Snapshot());
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Gagal menyimpan log: " + ex.Message, "Debug Tools");
        }
    }
}

internal static class ControlExtensions
{
    public static T Also<T>(this T control, Action<T> action) where T : Control
    {
        action(control);
        return control;
    }
}
