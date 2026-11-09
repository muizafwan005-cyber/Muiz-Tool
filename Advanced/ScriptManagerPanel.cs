using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RFTool.Advanced;

/// <summary>
/// Halaman "Script Manager": kelola skrip Lua Advance (.lua) dan Auto Assemble (.aa)
/// yang disimpan di folder "scripts" di samping aplikasi.
/// </summary>
internal sealed class ScriptManagerPanel : UserControl
{
    private const string LuaTemplate =
        "-- Lua Advance (fungsi bergaya Cheat Engine)\r\n" +
        "-- Contoh: print(readInteger(\"RF_Online.bin+C28B40\"))\r\n\r\n" +
        "print(\"Process ID: \" .. tostring(getOpenedProcessID()))\r\n";

    private const string AaTemplate =
        "// Auto Assemble (angka default HEX)\r\n" +
        "[ENABLE]\r\n" +
        "// aobscanmodule(INJECT,RF_Online.bin,8B 46 ?? 89)\r\n" +
        "// alloc(newmem,1000,INJECT)\r\n" +
        "// label(returnhere)\r\n" +
        "// registersymbol(INJECT)\r\n" +
        "//\r\n" +
        "// newmem:\r\n" +
        "//   jmp returnhere\r\n" +
        "//\r\n" +
        "// INJECT:\r\n" +
        "//   jmp newmem\r\n" +
        "//   returnhere:\r\n\r\n" +
        "[DISABLE]\r\n" +
        "// INJECT:\r\n" +
        "//   db 8B 46 04 89\r\n" +
        "// unregistersymbol(INJECT)\r\n" +
        "// dealloc(newmem)\r\n";

    private readonly AdvancedServices services;
    private readonly string folder;

    private readonly ListView scriptList;
    private readonly TextBox editor;
    private readonly TextBox console;
    private readonly Label fileLabel;
    private readonly Button runBtn;
    private readonly Button enableBtn;
    private readonly Button disableBtn;
    private readonly Button stopBtn;

    private readonly Dictionary<string, AaSession> aaSessions =
        new Dictionary<string, AaSession>(StringComparer.OrdinalIgnoreCase);

    private readonly AaSession luaSession = new AaSession();

    private string? currentPath;
    private bool dirty;
    private bool loading;
    private bool running;
    private CancellationTokenSource? cts;

    public ScriptManagerPanel(AdvancedServices services)
    {
        this.services = services;
        folder = Path.Combine(Application.StartupPath, "scripts");

        Dock = DockStyle.Fill;
        BackColor = AdvTheme.Back;
        ForeColor = AdvTheme.Fore;

        // ---------- kiri: daftar skrip ----------
        var left = new Panel { Dock = DockStyle.Left, Width = 236, BackColor = AdvTheme.Back };

        scriptList = new ListView { Dock = DockStyle.Fill, MultiSelect = false };
        AdvTheme.StyleList(scriptList);
        scriptList.Columns.Add("Skrip", 130);
        scriptList.Columns.Add("Jenis", 90);

        var leftBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 66,
            BackColor = AdvTheme.Back,
            WrapContents = true,
            Padding = new Padding(0, 2, 0, 0)
        };

        Button newLuaBtn = AdvTheme.Btn("+ Lua Advance", 112);
        Button newAaBtn = AdvTheme.Btn("+ Auto Assemble", 112);
        Button importBtn = AdvTheme.Btn("Import...", 112);
        Button deleteBtn = AdvTheme.Btn("Delete", 112);

        leftBar.Controls.Add(newLuaBtn);
        leftBar.Controls.Add(newAaBtn);
        leftBar.Controls.Add(importBtn);
        leftBar.Controls.Add(deleteBtn);

        left.Controls.Add(scriptList);
        left.Controls.Add(leftBar);

        // ---------- kanan: editor + konsol ----------
        var right = new Panel { Dock = DockStyle.Fill, BackColor = AdvTheme.Back, Padding = new Padding(8, 0, 0, 0) };

        editor = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            AcceptsTab = true,
            AcceptsReturn = true,
            WordWrap = false,
            ScrollBars = ScrollBars.Both,
            Font = AdvTheme.Mono,
            BackColor = AdvTheme.Input,
            ForeColor = AdvTheme.Fore,
            BorderStyle = BorderStyle.None,
            Enabled = false
        };

        console = new TextBox
        {
            Dock = DockStyle.Bottom,
            Height = 170,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = AdvTheme.Mono,
            BackColor = Color.FromArgb(20, 20, 23),
            ForeColor = Color.FromArgb(150, 230, 170),
            BorderStyle = BorderStyle.None
        };

        var runBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = AdvTheme.Back,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };

        Button saveBtn = AdvTheme.Btn("Save", 60);
        runBtn = AdvTheme.Btn("Run (Lua Advance)", 130);
        enableBtn = AdvTheme.Btn("Enable (AA)", 90);
        disableBtn = AdvTheme.Btn("Disable (AA)", 94);
        stopBtn = AdvTheme.Btn("Stop", 56);
        Button clearBtn = AdvTheme.Btn("Clear", 56);

        runBar.Controls.Add(saveBtn);
        runBar.Controls.Add(runBtn);
        runBar.Controls.Add(enableBtn);
        runBar.Controls.Add(disableBtn);
        runBar.Controls.Add(stopBtn);
        runBar.Controls.Add(clearBtn);

        fileLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            ForeColor = AdvTheme.Dim,
            Text = "Pilih atau buat skrip."
        };

        right.Controls.Add(editor);
        right.Controls.Add(console);
        right.Controls.Add(runBar);
        right.Controls.Add(fileLabel);

        Controls.Add(right);
        Controls.Add(left);

        // ---------- event ----------
        newLuaBtn.Click += (s, e) => NewScript(true);
        newAaBtn.Click += (s, e) => NewScript(false);
        importBtn.Click += (s, e) => ImportScript();
        deleteBtn.Click += (s, e) => DeleteScript();
        saveBtn.Click += (s, e) => SaveScript();
        runBtn.Click += (s, e) => RunLua();
        enableBtn.Click += (s, e) => RunAa(true);
        disableBtn.Click += (s, e) => RunAa(false);
        stopBtn.Click += (s, e) => cts?.Cancel();
        clearBtn.Click += (s, e) => console.Clear();

        scriptList.SelectedIndexChanged += (s, e) => OnSelectionChanged();

        editor.TextChanged += (s, e) =>
        {
            if (loading || currentPath == null)
                return;

            dirty = true;
            UpdateLabel();
        };

        EnsureSamples();
        ReloadList(null);
        UpdateButtons();

        Log("Lua Advance siap. Folder skrip: " + folder);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            cts?.Cancel();

        base.Dispose(disposing);
    }

    // ---------------------------------------------------------
    // KONSOL
    // ---------------------------------------------------------

    private void Log(string line)
    {
        PostUi(() => console.AppendText(line + Environment.NewLine));
    }

    private void PostUi(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        try
        {
            BeginInvoke(action);
        }
        catch
        {
        }
    }

    // ---------------------------------------------------------
    // FILE
    // ---------------------------------------------------------

    private void EnsureSamples()
    {
        try
        {
            Directory.CreateDirectory(folder);

            if (Directory.GetFiles(folder).Length == 0)
            {
                File.WriteAllText(Path.Combine(folder, "contoh_lua_advance.lua"), LuaTemplate);
                File.WriteAllText(Path.Combine(folder, "contoh_auto_assemble.aa"), AaTemplate);
            }
        }
        catch (Exception ex)
        {
            DebugLog.Warn("Gagal menyiapkan folder skrip: " + ex.Message);
        }
    }

    private static bool IsLua(string path)
    {
        return string.Equals(Path.GetExtension(path), ".lua", StringComparison.OrdinalIgnoreCase);
    }

    private void ReloadList(string? select)
    {
        loading = true;
        scriptList.Items.Clear();

        try
        {
            IEnumerable<string> files = Directory.GetFiles(folder)
                .Where(f => IsLua(f) || string.Equals(Path.GetExtension(f), ".aa", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                var item = new ListViewItem(new[]
                {
                    Path.GetFileNameWithoutExtension(file),
                    IsLua(file) ? "Lua Advance" : "Auto Assemble"
                })
                {
                    Tag = file
                };

                scriptList.Items.Add(item);

                if (select != null && string.Equals(file, select, StringComparison.OrdinalIgnoreCase))
                    item.Selected = true;
            }
        }
        catch (Exception ex)
        {
            Log("Gagal membaca folder skrip: " + ex.Message);
        }

        loading = false;

        if (select != null)
            OnSelectionChanged();
    }

    private void OnSelectionChanged()
    {
        if (loading)
            return;

        if (scriptList.SelectedItems.Count == 0)
            return;

        string path = (string)scriptList.SelectedItems[0].Tag!;

        if (string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase))
            return;

        SaveIfDirty();

        try
        {
            loading = true;
            editor.Text = File.ReadAllText(path);
            editor.Enabled = true;
            currentPath = path;
            dirty = false;
        }
        catch (Exception ex)
        {
            Log("Gagal membuka skrip: " + ex.Message);
        }
        finally
        {
            loading = false;
        }

        UpdateLabel();
        UpdateButtons();
    }

    private void UpdateLabel()
    {
        fileLabel.Text = currentPath == null
            ? "Pilih atau buat skrip."
            : Path.GetFileName(currentPath) + (dirty ? " *" : "") +
              (IsLua(currentPath) ? "   [Lua Advance]" : "   [Auto Assemble]");
    }

    private void UpdateButtons()
    {
        bool has = currentPath != null;
        bool lua = has && IsLua(currentPath!);

        runBtn.Enabled = has && lua && !running;
        enableBtn.Enabled = has && !lua && !running;
        disableBtn.Enabled = has && !lua && !running;
        stopBtn.Enabled = running;
    }

    private void SaveIfDirty()
    {
        if (dirty)
            SaveScript();
    }

    private void SaveScript()
    {
        if (currentPath == null)
            return;

        try
        {
            File.WriteAllText(currentPath, editor.Text);
            dirty = false;
            UpdateLabel();
        }
        catch (Exception ex)
        {
            Log("Gagal menyimpan: " + ex.Message);
        }
    }

    private static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return name.Trim();
    }

    private void NewScript(bool lua)
    {
        string? name = AdvTheme.Prompt(
            this,
            lua ? "Skrip Lua Advance baru" : "Skrip Auto Assemble baru",
            "Nama skrip (tanpa ekstensi):",
            "skrip_baru");

        if (name == null)
            return;

        name = Sanitize(name);

        if (name.Length == 0)
            return;

        string path = Path.Combine(folder, name + (lua ? ".lua" : ".aa"));

        if (File.Exists(path))
        {
            Log("File sudah ada: " + Path.GetFileName(path));
            return;
        }

        try
        {
            File.WriteAllText(path, lua ? LuaTemplate : AaTemplate);
            ReloadList(path);
        }
        catch (Exception ex)
        {
            Log("Gagal membuat skrip: " + ex.Message);
        }
    }

    private void ImportScript()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Import skrip",
            Filter = "Skrip (*.lua;*.aa;*.cea;*.txt)|*.lua;*.aa;*.cea;*.txt|Semua file (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            string ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            string target = ext == ".lua" ? ".lua" : ".aa";
            string destination = Path.Combine(folder, Sanitize(Path.GetFileNameWithoutExtension(dialog.FileName)) + target);

            File.Copy(dialog.FileName, destination, true);
            ReloadList(destination);
            Log("Diimpor: " + Path.GetFileName(destination));
        }
        catch (Exception ex)
        {
            Log("Gagal import: " + ex.Message);
        }
    }

    private void DeleteScript()
    {
        if (currentPath == null)
            return;

        if (MessageBox.Show(
                this,
                "Hapus skrip " + Path.GetFileName(currentPath) + "?",
                "Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            File.Delete(currentPath);
            aaSessions.Remove(currentPath);

            loading = true;
            editor.Clear();
            editor.Enabled = false;
            loading = false;

            currentPath = null;
            dirty = false;

            ReloadList(null);
            UpdateLabel();
            UpdateButtons();
        }
        catch (Exception ex)
        {
            Log("Gagal menghapus: " + ex.Message);
        }
    }

    // ---------------------------------------------------------
    // EKSEKUSI
    // ---------------------------------------------------------

    private async void RunLua()
    {
        if (currentPath == null || running)
            return;

        SaveIfDirty();

        string code = editor.Text;
        string name = Path.GetFileName(currentPath);

        running = true;
        UpdateButtons();
        cts = new CancellationTokenSource();
        CancellationToken token = cts.Token;

        Log("[Lua Advance] Menjalankan " + name + " ...");

        try
        {
            await Task.Run(() =>
            {
                var engine = new LuaAdvance(services.Target, services.Manager, Log, PostUi, luaSession, token);
                engine.Run(code);
            });
        }
        finally
        {
            running = false;
            cts?.Dispose();
            cts = null;

            if (!IsDisposed)
                UpdateButtons();

            Log("[Lua Advance] Selesai: " + name);
        }
    }

    private async void RunAa(bool enable)
    {
        if (currentPath == null || running)
            return;

        SaveIfDirty();

        string text = editor.Text;
        string path = currentPath;

        if (!aaSessions.TryGetValue(path, out AaSession? session))
        {
            session = new AaSession();
            aaSessions[path] = session;
        }

        running = true;
        UpdateButtons();
        cts = new CancellationTokenSource();
        CancellationToken token = cts.Token;

        Log("[AA] " + (enable ? "Enable" : "Disable") + " " + Path.GetFileName(path) + " ...");

        try
        {
            bool ok = await Task.Run(() =>
                new AutoAssembler(services.Target, Log, token).Run(text, enable, session));

            Log(ok ? "[AA] Berhasil." : "[AA] Gagal (lihat pesan di atas).");
        }
        finally
        {
            running = false;
            cts?.Dispose();
            cts = null;

            if (!IsDisposed)
                UpdateButtons();
        }
    }
}
