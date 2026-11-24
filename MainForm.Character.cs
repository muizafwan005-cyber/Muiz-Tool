using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace RFTool;

public sealed partial class MainForm
{
    // =========================================================
    // MULTI KARAKTER
    //
    // Header menampilkan nickname karakter (dibaca dari Tab 1: Nick)
    // dan tombol "+" untuk membuka window baru yang bisa di-attach ke
    // process lain. Tiap window = 1 karakter dengan fitur, freeze,
    // hotkey, dan Address Manager sendiri, tetapi tetap dalam 1 aplikasi.
    // =========================================================

    private static readonly List<MainForm> OpenWindows = new();

    private Label characterLabel = null!;
    private Button addCharacterButton = null!;
    private System.Windows.Forms.Timer characterTimer = null!;

    private void BuildCharacterBar(Panel topPanel)
    {
        characterLabel = new Label
        {
            Text = "Nickname: -",
            AutoSize = false,
            Location = new Point(10, 127),
            Size = new Size(250, 20),
            ForeColor = ForeColorMain
        };

        topPanel.Controls.Add(characterLabel);

        addCharacterButton = new Button
        {
            Text = "+",
            Location = new Point(264, 124),
            Size = new Size(30, 24),
            FlatStyle = FlatStyle.Flat,
            BackColor = BackColorInput,
            ForeColor = ForeColorMain,
            Cursor = Cursors.Hand
        };

        addCharacterButton.FlatAppearance.BorderSize = 0;
        addCharacterButton.Click += (_, _) => OpenNewCharacterWindow();

        topPanel.Controls.Add(addCharacterButton);

        new ToolTip().SetToolTip(
            addCharacterButton,
            "Tambah karakter: buka window baru untuk attach process lain");

        OpenWindows.Add(this);

        characterTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000
        };

        characterTimer.Tick += (_, _) => UpdateCharacterInfo();
        characterTimer.Start();

        FormClosed += (_, _) =>
        {
            characterTimer.Stop();
            OpenWindows.Remove(this);

            // Window terakhir ditutup -> aplikasi selesai.
            if (OpenWindows.Count == 0)
                Application.ExitThread();
        };
    }

    private void OpenNewCharacterWindow()
    {
        var window = new MainForm
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(Left + 32, Top + 32),
            Size = Size
        };

        window.Show();
        window.Activate();
    }

    private bool IsProcessUsedByOtherWindow(int processId)
    {
        foreach (MainForm window in OpenWindows)
        {
            if (ReferenceEquals(window, this))
                continue;

            try
            {
                if (window.selectedProcess != null &&
                    window.selectedProcess.Id == processId)
                {
                    return true;
                }
            }
            catch
            {
                // Process sudah keluar.
            }
        }

        return false;
    }

    // Saat lebih dari satu window ter-attach, hotkey hanya berlaku
    // untuk window yang game-nya sedang aktif di layar.
    private bool HotkeyAllowedHere()
    {
        int attachedWindows = 0;

        foreach (MainForm window in OpenWindows)
        {
            if (window.selectedProcess != null)
                attachedWindows++;
        }

        return attachedWindows <= 1 || IsGameForeground();
    }

    // =========================================================
    // NICKNAME
    // =========================================================

    private string ReadCharacterNick()
    {
        if (!IsAttached() || memory == null || features == null)
            return "";

        try
        {
            // Sama dengan baris Nick di Tab 1: Player + 0x10
            IntPtr address = memory.ResolvePointer32(features.PlayerPointerOffset, 0x10);
            byte[] data = memory.ReadBytes(address, 16);

            int count = 0;

            while (count < data.Length && data[count] >= 0x20 && data[count] <= 0x7E)
                count++;

            bool terminated = count < data.Length && data[count] == 0;

            // Tab 1 membaca 6 karakter; kalau tidak ada terminator, jangan tampilkan sampah.
            if (!terminated)
                count = Math.Min(count, 6);

            return Encoding.ASCII.GetString(data, 0, count);
        }
        catch
        {
            return "";
        }
    }

    private void UpdateCharacterInfo()
    {
        if (!IsAttached() || selectedProcess == null)
        {
            characterLabel.Text = "Nickname: -";
            Text = "Muiz Tool";
            return;
        }

        int pid;

        try
        {
            pid = selectedProcess.Id;
        }
        catch
        {
            return;
        }

        string nick = ReadCharacterNick();
        string shown = nick.Length > 0 ? nick : "?";

        characterLabel.Text = $"Nickname: {shown}  (PID {pid})";
        Text = $"Muiz Tool - {shown} (PID {pid})";
    }
}
