using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using RFTool.Advanced;
using RFTool.AdvancedSystem;

namespace RFTool;

public sealed partial class MainForm
{
    // =========================================================
    // ENTRI SCRIPT DI ADDRESS MANAGER
    // Centang Freeze  -> jalankan [ENABLE]
    // Hilangkan centang -> jalankan [DISABLE]
    // =========================================================

    private readonly HashSet<AddressEntry> scriptBusy = new();

    private async void HandleScriptToggle(AddressEntry entry)
    {
        string key = entry.ScriptKey;
        bool want = entry.Freeze;

        // Sudah sesuai, atau sedang diproses -> abaikan (event bisa terpanggil berulang)
        if (scriptBusy.Contains(entry) || want == AdvSvc.IsAaActive(key))
            return;

        if (AdvSvc.Target.Get() == null)
        {
            if (!want)
            {
                // Process sudah lepas: memori script ikut hilang
                AdvSvc.SetAaActive(key, false);
                return;
            }

            entry.Status = "Belum attach ke process.";
            addressInfoLabel.Text = "Script: belum attach ke process.";
            BeginInvoke(new Action(() => entry.Freeze = false));
            return;
        }

        string script = ReadScriptText(entry);

        if (string.IsNullOrWhiteSpace(script))
        {
            entry.Status = "Isi script kosong.";
            addressInfoLabel.Text = "Script kosong, tidak bisa dijalankan.";
            BeginInvoke(new Action(() => entry.Freeze = AdvSvc.IsAaActive(key)));
            return;
        }

        AaSession session = AdvSvc.GetAaSession(key);
        string lastError = "";
        string action = want ? "Enable" : "Disable";

        void Capture(string line)
        {
            if (line.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Dibatalkan", StringComparison.OrdinalIgnoreCase))
            {
                lastError = line;
            }

            AdvSvc.RaiseAaLog(line);
        }

        scriptBusy.Add(entry);
        entry.Status = action + "...";
        addressInfoLabel.Text = action + " script: " + entry.Description + " ...";
        AdvSvc.RaiseAaLog("[AA] " + action + " " + entry.Description + " (Address Manager) ...");

        bool ok;

        try
        {
            ok = await Task.Run(() =>
                new AutoAssembler(
                        AdvSvc.Target,
                        Capture,
                        CancellationToken.None,
                        AdvSvc.Lua)
                    .Run(script, want, session));
        }
        catch (Exception ex)
        {
            ok = false;
            lastError = ex.Message;
        }
        finally
        {
            scriptBusy.Remove(entry);
        }

        if (ok)
        {
            entry.Status = "";
            AdvSvc.SetAaActive(key, want);
            addressInfoLabel.Text = "Script " + entry.Description + ": " +
                                    (want ? "AKTIF" : "nonaktif");
            AdvSvc.RaiseAaLog("[AA] Berhasil.");
        }
        else
        {
            entry.Status = "Script gagal: " + lastError;
            addressInfoLabel.Text = entry.Status;
            AdvSvc.RaiseAaLog("[AA] Gagal (" + action + " " + entry.Description + ").");

            // Kembalikan centang sesuai keadaan sebenarnya
            entry.Freeze = AdvSvc.IsAaActive(key);
        }

        // User mengubah centang lagi saat script masih diproses
        if (entry.Freeze != AdvSvc.IsAaActive(key))
            HandleScriptToggle(entry);
    }

    // Script dibaca ulang dari file (kalau ada) supaya perubahan di Script Manager ikut terpakai.
    private static string ReadScriptText(AddressEntry entry)
    {
        string path = entry.ScriptPath ?? "";

        if (path.Length > 0)
        {
            try
            {
                if (File.Exists(path))
                    return File.ReadAllText(path);
            }
            catch
            {
            }
        }

        return entry.ScriptText ?? "";
    }

    // Script di-enable/disable dari Script Manager -> centang di Address Manager ikut berubah
    private void OnAaStateChanged(string key, bool active)
    {
        foreach (AddressEntry e in addressManager.Entries)
        {
            if (e.IsScript &&
                e.Freeze != active &&
                string.Equals(e.ScriptKey, key, StringComparison.OrdinalIgnoreCase))
            {
                e.Freeze = active;
            }
        }
    }

    private bool ScriptStillActive(AddressEntry entry)
    {
        return entry.IsScript && AdvSvc.IsAaActive(entry.ScriptKey);
    }

    // WRITE / READ / BROWSE / dst tidak berlaku untuk entri script
    private bool RejectScriptEntry(AddressEntry entry)
    {
        if (!entry.IsScript)
            return false;

        addressInfoLabel.Text =
            "Entri script: centang Freeze untuk Enable/Disable. (EDIT hanya untuk ganti nama)";
        return true;
    }

    private void EditScriptEntry(AddressEntry entry)
    {
        string? name = AdvTheme.Prompt(
            this,
            "Ganti nama script",
            "Description:",
            entry.Description);

        if (!string.IsNullOrWhiteSpace(name))
            entry.Description = name.Trim();
    }
}
