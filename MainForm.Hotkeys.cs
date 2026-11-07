using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using RFTool.Core;
using RFTool.Features;

namespace RFTool;


public sealed partial class MainForm
{
    // =========================================================
    // TAB 3 (HOTKEY) - DAFTAR ISI
    //
    // Urutan bagian di file ini:
    //   1. FIELD & KONSTANTA
    //   2. TIPE DATA
    //   3. WINDOWS API & PESAN HOTKEY
    //   4. EKSEKUSI PERINTAH NPC
    //   5. BUILD TAB 3
    //   6. CAPTURE (TEKAN TOMBOL BARU)
    //   7. REGISTER / UNREGISTER
    //   8. TAMPILAN TEKS HOTKEY
    //   9. LOAD / SAVE / RESET
    //
    // Cari "// 1." sampai "// 9." untuk loncat ke bagiannya.
    // =========================================================


    // =========================================================
    // 1. FIELD & KONSTANTA
    // =========================================================

    private HotkeyTextBox npc7HotkeyBox = null!;
    private HotkeyTextBox npc0HotkeyBox = null!;
    private HotkeyTextBox npcMinusHotkeyBox = null!;
    private HotkeyTextBox npcEqualsHotkeyBox = null!;
    private HotkeyTextBox npcF12HotkeyBox = null!;
    private HotkeyTextBox npc8HotkeyBox = null!;
    private HotkeyTextBox npcF11HotkeyBox = null!;
    private readonly Dictionary<HotkeyAction, HotkeyDefinition> hotkeys = new();
    private const int HotkeyIdNpc7 = 1001;
    private const int HotkeyIdNpc0 = 1002;
    private const int HotkeyIdNpcMinus = 1003;
    private const int HotkeyIdNpcEquals = 1004;
    private const int HotkeyIdNpcF12 = 1005;
    private const int HotkeyIdNpc8 = 1006;
    private const int HotkeyIdNpcF11 = 1007;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_NOREPEAT = 0x4000;
    private const int WM_HOTKEY = 0x0312;

    private readonly string hotkeyFile =
        Path.Combine(
            Application.StartupPath,
            "hotkeys.json");

    // =========================================================
    // 2. TIPE DATA
    // Enum aksi, definisi hotkey, textbox khusus, dan format file hotkeys.json.
    // =========================================================

    private enum HotkeyAction
    {
        Npc7,
        Npc0,
        NpcMinus,
        NpcEquals,
        NpcF12,
        Npc8,
        NpcF11
    }

    private sealed class HotkeyDefinition
    {
        public int Id { get; }

        public HotkeyAction Action { get; }

        public Keys Key { get; set; }

        public Keys Modifiers { get; set; }

        public HotkeyDefinition(
            int id,
            HotkeyAction action,
            Keys key,
            Keys modifiers)
        {
            Id = id;
            Action = action;
            Key = key;
            Modifiers = modifiers;
        }
    }

    private sealed class HotkeyTextBox : TextBox
    {
        public HotkeyTextBox()
        {
            ReadOnly = true;

            TabStop = true;

            ShortcutsEnabled = false;
        }

        protected override bool IsInputKey(
            Keys keyData)
        {
            return true;
        }
    }

    private sealed class HotkeySettings
    {
        public SavedHotkey? Npc7 { get; set; }

        public SavedHotkey? Npc0 { get; set; }

        public SavedHotkey? NpcMinus { get; set; }

        public SavedHotkey? NpcEquals { get; set; }

        public SavedHotkey? NpcF12 { get; set; }

        public SavedHotkey? Npc8 { get; set; }

        public SavedHotkey? NpcF11 { get; set; }
    }

    private sealed class SavedHotkey
    {
        public string Key { get; set; } = "";

        public string Modifiers { get; set; } = "";
    }

    // =========================================================
    // 3. WINDOWS API & PESAN HOTKEY
    // Register ke Windows dan terima pesan WM_HOTKEY.
    // =========================================================

    protected override void OnHandleCreated(
        EventArgs e)
    {
        base.OnHandleCreated(e);

        RegisterAllHotkeys();
    }

    protected override void OnHandleDestroyed(
        EventArgs e)
    {
        UnregisterAllHotkeys();

        base.OnHandleDestroyed(e);
    }

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool RegisterHotKey(
        IntPtr hWnd,
        int id,
        uint fsModifiers,
        uint vk);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern bool UnregisterHotKey(
        IntPtr hWnd,
        int id);

    protected override void WndProc(
        ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            int id =
                m.WParam.ToInt32();

            HandleGlobalHotkey(id);
        }

        base.WndProc(
            ref m);
    }

    private void HandleGlobalHotkey(
        int id)
    {
        if (id == HotkeyIdNpc7)
        {
            ExecuteNpcHotkey(
                HotkeyAction.Npc7);

            return;
        }

        if (id == HotkeyIdNpc0)
        {
            ExecuteNpcHotkey(
                HotkeyAction.Npc0);

            return;
        }

        if (id == HotkeyIdNpcMinus)
        {
            ExecuteNpcHotkey(
                HotkeyAction.NpcMinus);

            return;
        }

        if (id == HotkeyIdNpcEquals)
        {
            ExecuteNpcHotkey(
                HotkeyAction.NpcEquals);

            return;
        }

        if (id == HotkeyIdNpcF12)
        {
            ExecuteNpcHotkey(
                HotkeyAction.NpcF12);

            return;
        }

        if (id == HotkeyIdNpc8)
        {
            ExecuteNpcHotkey(
                HotkeyAction.Npc8);

            return;
        }

        if (id == HotkeyIdNpcF11)
        {
            ExecuteNpcHotkey(
                HotkeyAction.NpcF11);
        }
    }

    // =========================================================
    // 4. EKSEKUSI PERINTAH NPC
    // Hotkey menjalankan perintah NPC (Amunisi, Bank, Tool, UMT, Potion, Vendor, Hero).
    // =========================================================

    private void ExecuteNpcHotkey(
        HotkeyAction action)
    {
        if (!IsAttached() ||
            features == null)
        {
            statusLabel.Text =
                "Status: Attach process terlebih dahulu.";

            return;
        }

        try
        {
            int commandIndex =
                action switch
                {
                    HotkeyAction.Npc7 => 0,
                    HotkeyAction.Npc0 => 1,
                    HotkeyAction.NpcMinus => 2,
                    HotkeyAction.NpcEquals => 3,
                    HotkeyAction.NpcF12 => 4,
                    HotkeyAction.Npc8 => 5,
                    HotkeyAction.NpcF11 => 6,
                    _ => -1
                };

            if (commandIndex < 0)
                return;

            NpcTarget target =
                GetSelectedNpcTarget();

            var result =
                features.ExecuteNpcCommand(
                    commandIndex,
                    target);

            string commandName =
                nameFor(action);

            string targetName =
                target switch
                {
                    NpcTarget.Bels => "BELS",
                    NpcTarget.Cora => "CORA",
                    NpcTarget.Acc => "ACC",
                    _ => "ACC"
                };

            statusLabel.Text =
                $"Status: {commandName} → {targetName} updated";
        }
        catch (Exception ex)
        {
            HandleFeatureError(
                nameFor(action),
                ex);
        }
    }

    private static string nameFor(
        HotkeyAction action)
    {
        return action switch
        {
            HotkeyAction.Npc7 =>
                "NPC Amunisi",
    
            HotkeyAction.Npc0 =>
                "NPC Bank",
    
            HotkeyAction.NpcMinus =>
                "NPC TOOL",
    
            HotkeyAction.NpcEquals =>
                "NPC UMT",
    
            HotkeyAction.NpcF12 =>
                "NPC Potion",
    
            HotkeyAction.Npc8 =>
                "NPC Vendor",
    
            HotkeyAction.NpcF11 =>
                "NPC Hero",
    
            _ =>
                "Unknown"
        };
    }

    private static string HotkeyDisplayName(
        HotkeyAction action)
    {
        return action switch
        {
            HotkeyAction.Npc7 => "7",
            HotkeyAction.Npc0 => "0",
            HotkeyAction.NpcMinus => "-",
            HotkeyAction.NpcEquals => "=",
            HotkeyAction.NpcF12 => "F12",
            HotkeyAction.Npc8 => "8",
            HotkeyAction.NpcF11 => "F11",
            _ => action.ToString()
        };
    }

    // =========================================================
    // 5. BUILD TAB 3
    // Daftar baris hotkey ada di BuildTab3.
    // =========================================================

    private void BuildTab3(
        TabPage tab)
    {
        var panel = new Panel
        {
            Dock =
                DockStyle.Fill,

            BackColor =
                BackColorMain,

            Padding =
                new Padding(10),

            AutoScroll =
                true
        };

        tab.Controls.Add(
            panel);


        var title = new Label
        {
            Text =
                "SETTING HOTKEY",

            AutoSize =
                true,

            Location =
                new Point(
                    8,
                    10),

            ForeColor =
                ForeColorMain,

            Font =
                new Font(
                    "Segoe UI",
                    10F,
                    FontStyle.Bold)
        };

        panel.Controls.Add(
            title);


        var info = new Label
        {
            Text =
                "Klik kotak lalu tekan tombol yang diinginkan.",

            AutoSize =
                false,

            Location =
                new Point(
                    8,
                    36),

            Size =
                new Size(
                    295,
                    36),

            ForeColor =
                ForeColorDim
        };

        panel.Controls.Add(
            info);


        int y = 82;



        // -----------------------------------------------------
        // NPC COMMANDS
        // -----------------------------------------------------
        
        npc7HotkeyBox =
            AddHotkeyRow(
                panel,
                "NPC Amunisi",
                HotkeyAction.Npc7,
                y);
        
        y += 72;
        
        npc0HotkeyBox =
            AddHotkeyRow(
                panel,
                "NPC Bank",
                HotkeyAction.Npc0,
                y);
        
        y += 72;
        
        npcMinusHotkeyBox =
            AddHotkeyRow(
                panel,
                "NPC TOOL",
                HotkeyAction.NpcMinus,
                y);
        
        y += 72;
        
        npcEqualsHotkeyBox =
            AddHotkeyRow(
                panel,
                "NPC UMT",
                HotkeyAction.NpcEquals,
                y);
        
        y += 72;
        
        npcF12HotkeyBox =
            AddHotkeyRow(
                panel,
                "NPC Potion",
                HotkeyAction.NpcF12,
                y);
        
        y += 72;
        
        npc8HotkeyBox =
            AddHotkeyRow(
                panel,
                "NPC Vendor",
                HotkeyAction.Npc8,
                y);
        
        y += 72;
        
        npcF11HotkeyBox =
            AddHotkeyRow(
                panel,
                "NPC Hero",
                HotkeyAction.NpcF11,
                y);
        
        y += 82;


        // -----------------------------------------------------
        // RESET
        // -----------------------------------------------------

        var resetButton = new Button
        {
            Text =
                "RESET HOTKEY",

            Location =
                new Point(
                    8,
                    y),

            Size =
                new Size(
                    130,
                    30),

            FlatStyle =
                FlatStyle.Flat,

            BackColor =
                BackColorInput,

            ForeColor =
                ForeColorMain
        };

        resetButton.FlatAppearance.BorderSize =
            0;

        resetButton.Click +=
            (_, _) =>
            {
                ResetHotkeysToDefault();
            };

        panel.Controls.Add(
            resetButton);


        // -----------------------------------------------------
        // SAVE
        // -----------------------------------------------------

        var saveButton = new Button
        {
            Text =
                "SIMPAN",

            Location =
                new Point(
                    145,
                    y),

            Size =
                new Size(
                    130,
                    30),

            FlatStyle =
                FlatStyle.Flat,

            BackColor =
                BackColorInput,

            ForeColor =
                ForeColorMain
        };

        saveButton.FlatAppearance.BorderSize =
            0;

        saveButton.Click +=
            (_, _) =>
            {
                SaveHotkeys();

                statusLabel.Text =
                    "Status: Setting hotkey tersimpan";
            };

        panel.Controls.Add(
            saveButton);


        y += 48;


        var note = new Label
        {
            Text =
                "Hotkey bekerja secara global selama RF Tool aktif.",

            AutoSize =
                false,

            Location =
                new Point(
                    8,
                    y),

            Size =
                new Size(
                    295,
                    45),

            ForeColor =
                ForeColorDim
        };

        panel.Controls.Add(
            note);
    }

    private HotkeyTextBox AddHotkeyRow(
        Control parent,
        string name,
        HotkeyAction action,
        int y)
    {
        var label = new Label
        {
            Text =
                name,

            AutoSize =
                false,

            Location =
                new Point(
                    8,
                    y),

            Size =
                new Size(
                    170,
                    22),

            ForeColor =
                ForeColorMain
        };

        parent.Controls.Add(
            label);


        var hotkeyBox = new HotkeyTextBox
        {
            Location =
                new Point(
                    8,
                    y + 25),

            Size =
                new Size(
                    180,
                    27),

            BackColor =
                BackColorInput,

            ForeColor =
                ForeColorMain,

            BorderStyle =
                BorderStyle.FixedSingle,

            TextAlign =
                HorizontalAlignment.Center
        };

        parent.Controls.Add(
            hotkeyBox);


        hotkeyBox.Enter +=
            (_, _) =>
            {
                BeginHotkeyCapture(
                    action,
                    hotkeyBox);
            };


        hotkeyBox.KeyDown +=
            (_, e) =>
            {
                CaptureHotkey(
                    action,
                    hotkeyBox,
                    e);
            };


        hotkeyBox.Leave +=
            (_, _) =>
            {
                EndHotkeyCapture(
                    action,
                    hotkeyBox);
            };


        return hotkeyBox;
    }

    // =========================================================
    // 6. CAPTURE (TEKAN TOMBOL BARU)
    // =========================================================

    private void BeginHotkeyCapture(
        HotkeyAction action,
        HotkeyTextBox box)
    {
        UnregisterHotkey(
            action);

        box.Text =
            "Tekan tombol...";

        box.SelectAll();
    }

    private void EndHotkeyCapture(
        HotkeyAction action,
        HotkeyTextBox box)
    {
        if (!hotkeys.TryGetValue(
                action,
                out var definition))
        {
            box.Text =
                "Belum diset";

            return;
        }

        box.Text =
            FormatHotkey(
                definition.Modifiers,
                definition.Key);
    }

    private void CaptureHotkey(
        HotkeyAction action,
        HotkeyTextBox box,
        KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;


        Keys key =
            e.KeyCode;


        if (key == Keys.Escape)
        {
            EndHotkeyCapture(
                action,
                box);

            return;
        }


        if (key == Keys.ControlKey ||
            key == Keys.ShiftKey ||
            key == Keys.Menu ||
            key == Keys.LControlKey ||
            key == Keys.RControlKey ||
            key == Keys.LShiftKey ||
            key == Keys.RShiftKey ||
            key == Keys.LMenu ||
            key == Keys.RMenu)
        {
            box.Text =
                "Tekan tombol...";

            return;
        }


        Keys modifiers =
            Keys.None;

        if (e.Control)
            modifiers |= Keys.Control;

        if (e.Alt)
            modifiers |= Keys.Alt;

        if (e.Shift)
            modifiers |= Keys.Shift;


        if (IsHotkeyUsedByAnotherFeature(
                action,
                modifiers,
                key))
        {
            MessageBox.Show(
                this,
                "Hotkey tersebut sudah digunakan fitur lain.",
                "Hotkey bentrok",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            box.Text =
                "Tekan tombol...";

            return;
        }


        var definition =
            GetHotkeyDefinition(
                action);


        Keys oldKey =
            definition.Key;

        Keys oldModifiers =
            definition.Modifiers;


        definition.Key =
            key;

        definition.Modifiers =
            modifiers;


        if (!RegisterHotkey(
                definition))
        {
            definition.Key =
                oldKey;

            definition.Modifiers =
                oldModifiers;

            if (oldKey != Keys.None)
            {
                RegisterHotkey(
                    definition);
            }

            MessageBox.Show(
                this,
                "Hotkey tidak dapat digunakan.\r\n" +
                "Kemungkinan sedang dipakai aplikasi lain.",
                "Hotkey gagal",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            box.Text =
                FormatHotkey(
                    oldModifiers,
                    oldKey);

            return;
        }


        box.Text =
            FormatHotkey(
                modifiers,
                key);


        SaveHotkeys();


        statusLabel.Text =
            $"Status: Hotkey {nameFor(action)} → {box.Text}";


        BeginInvoke(
            new Action(
                () =>
                {
                    SelectNextControl(
                        box,
                        true,
                        true,
                        true,
                        true);
                }));
    }

    private bool IsHotkeyUsedByAnotherFeature(
        HotkeyAction currentAction,
        Keys modifiers,
        Keys key)
    {
        foreach (
            var definition
            in hotkeys.Values)
        {
            if (definition.Action ==
                currentAction)
            {
                continue;
            }

            if (definition.Key ==
                    key &&
                definition.Modifiers ==
                    modifiers)
            {
                return true;
            }
        }

        return false;
    }

    // =========================================================
    // 7. REGISTER / UNREGISTER
    // =========================================================

    private bool RegisterHotkey(
        HotkeyDefinition definition)
    {
        if (!IsHandleCreated)
            return false;

        if (definition.Key == Keys.None)
            return false;


        uint modifiers =
            GetNativeModifiers(
                definition.Modifiers);


        bool result =
            RegisterHotKey(
                Handle,
                definition.Id,
                modifiers,
                (uint)definition.Key);


        return result;
    }

    private void RegisterAllHotkeys()
    {
        if (!IsHandleCreated)
            return;

        foreach (
            var definition
            in hotkeys.Values)
        {
            if (definition.Key == Keys.None)
                continue;

            if (!RegisterHotkey(
                    definition))
            {
                statusLabel.Text =
                    $"Status: Hotkey {nameFor(definition.Action)} gagal register";
            }
        }
    }

    private void UnregisterHotkey(
        HotkeyAction action)
    {
        if (!hotkeys.TryGetValue(
                action,
                out var definition))
        {
            return;
        }

        if (!IsHandleCreated)
            return;

        UnregisterHotKey(
            Handle,
            definition.Id);
    }

    private void UnregisterAllHotkeys()
    {
        if (!IsHandleCreated)
            return;

        foreach (
            var definition
            in hotkeys.Values)
        {
            UnregisterHotKey(
                Handle,
                definition.Id);
        }
    }

    private static uint GetNativeModifiers(
        Keys modifiers)
    {
        uint result = 0;

        if ((modifiers & Keys.Control) != 0)
            result |= MOD_CONTROL;

        if ((modifiers & Keys.Alt) != 0)
            result |= MOD_ALT;

        if ((modifiers & Keys.Shift) != 0)
            result |= MOD_SHIFT;

        result |= MOD_NOREPEAT;

        return result;
    }

    private HotkeyDefinition GetHotkeyDefinition(
        HotkeyAction action)
    {
        return hotkeys[action];
    }

    // =========================================================
    // 8. TAMPILAN TEKS HOTKEY
    // =========================================================

    private static string FormatHotkey(
        Keys modifiers,
        Keys key)
    {
        if (key == Keys.None)
            return "Belum diset";

        var parts =
            new List<string>();

        if ((modifiers & Keys.Control) != 0)
            parts.Add("Ctrl");

        if ((modifiers & Keys.Alt) != 0)
            parts.Add("Alt");

        if ((modifiers & Keys.Shift) != 0)
            parts.Add("Shift");


        string keyName =
            GetKeyDisplayName(
                key);

        parts.Add(
            keyName);

        return string.Join(
            " + ",
            parts);
    }

    private static string GetKeyDisplayName(
        Keys key)
    {
        return key switch
        {
            Keys.PageUp =>
                "Page Up",

            Keys.PageDown =>
                "Page Down",

            Keys.PrintScreen =>
                "Print Screen",

            Keys.Scroll =>
                "Scroll Lock",

            Keys.CapsLock =>
                "Caps Lock",

            Keys.NumLock =>
                "Num Lock",

            Keys.Insert =>
                "Insert",

            Keys.Delete =>
                "Delete",

            Keys.Home =>
                "Home",

            Keys.End =>
                "End",

            Keys.Up =>
                "Up",

            Keys.Down =>
                "Down",

            Keys.Left =>
                "Left",

            Keys.Right =>
                "Right",

            Keys.Space =>
                "Space",

            Keys.Enter =>
                "Enter",

            Keys.Back =>
                "Backspace",

            Keys.Tab =>
                "Tab",

            Keys.Oemtilde =>
                "`",

            Keys.D0 =>
                "0",

            Keys.D1 =>
                "1",

            Keys.D2 =>
                "2",

            Keys.D3 =>
                "3",

            Keys.D4 =>
                "4",

            Keys.D5 =>
                "5",

            Keys.D6 =>
                "6",

            Keys.D7 =>
                "7",

            Keys.D8 =>
                "8",

            Keys.D9 =>
                "9",

            Keys.OemMinus =>
                "-",

            Keys.Oemplus =>
                "=",

            _ =>
                key.ToString()
        };
    }

    private void UpdateHotkeyBoxes()
    {
        if (npc7HotkeyBox == null)
            return;


        npc7HotkeyBox.Text =
            FormatHotkey(
                hotkeys[HotkeyAction.Npc7].Modifiers,
                hotkeys[HotkeyAction.Npc7].Key);

        npc0HotkeyBox.Text =
            FormatHotkey(
                hotkeys[HotkeyAction.Npc0].Modifiers,
                hotkeys[HotkeyAction.Npc0].Key);

        npcMinusHotkeyBox.Text =
            FormatHotkey(
                hotkeys[HotkeyAction.NpcMinus].Modifiers,
                hotkeys[HotkeyAction.NpcMinus].Key);

        npcEqualsHotkeyBox.Text =
            FormatHotkey(
                hotkeys[HotkeyAction.NpcEquals].Modifiers,
                hotkeys[HotkeyAction.NpcEquals].Key);

        npcF12HotkeyBox.Text =
            FormatHotkey(
                hotkeys[HotkeyAction.NpcF12].Modifiers,
                hotkeys[HotkeyAction.NpcF12].Key);

        npc8HotkeyBox.Text =
            FormatHotkey(
                hotkeys[HotkeyAction.Npc8].Modifiers,
                hotkeys[HotkeyAction.Npc8].Key);

        npcF11HotkeyBox.Text =
            FormatHotkey(
                hotkeys[HotkeyAction.NpcF11].Modifiers,
                hotkeys[HotkeyAction.NpcF11].Key);
    }

    // =========================================================
    // 9. LOAD / SAVE / RESET
    // Default hotkey ada di LoadHotkeys dan ResetHotkeysToDefault.
    // =========================================================

    private void LoadHotkeys()
    {
        // -----------------------------------------------------
        // DEFAULT NPC HOTKEY
        // -----------------------------------------------------

        hotkeys[HotkeyAction.Npc7] =
            new HotkeyDefinition(
                HotkeyIdNpc7,
                HotkeyAction.Npc7,
                Keys.F1,
                Keys.Control);

        hotkeys[HotkeyAction.Npc0] =
            new HotkeyDefinition(
                HotkeyIdNpc0,
                HotkeyAction.Npc0,
                Keys.F2,
                Keys.Control);

        hotkeys[HotkeyAction.NpcMinus] =
            new HotkeyDefinition(
                HotkeyIdNpcMinus,
                HotkeyAction.NpcMinus,
                Keys.F3,
                Keys.Control);

        hotkeys[HotkeyAction.NpcEquals] =
            new HotkeyDefinition(
                HotkeyIdNpcEquals,
                HotkeyAction.NpcEquals,
                Keys.F4,
                Keys.Control);

        hotkeys[HotkeyAction.NpcF12] =
            new HotkeyDefinition(
                HotkeyIdNpcF12,
                HotkeyAction.NpcF12,
                Keys.F5,
                Keys.Control);

        hotkeys[HotkeyAction.Npc8] =
            new HotkeyDefinition(
                HotkeyIdNpc8,
                HotkeyAction.Npc8,
                Keys.F6,
                Keys.Control);

        hotkeys[HotkeyAction.NpcF11] =
            new HotkeyDefinition(
                HotkeyIdNpcF11,
                HotkeyAction.NpcF11,
                Keys.F7,
                Keys.Control);


        // -----------------------------------------------------
        // LOAD FILE
        // -----------------------------------------------------

        try
        {
            if (!File.Exists(
                    hotkeyFile))
            {
                UpdateHotkeyBoxes();

                return;
            }


            string json =
                File.ReadAllText(
                    hotkeyFile);


            var data =
                JsonSerializer.Deserialize<
                    HotkeySettings>(
                    json);


            if (data == null)
            {
                UpdateHotkeyBoxes();

                return;
            }


            ApplySavedHotkey(
                HotkeyAction.Npc7,
                data.Npc7);

            ApplySavedHotkey(
                HotkeyAction.Npc0,
                data.Npc0);

            ApplySavedHotkey(
                HotkeyAction.NpcMinus,
                data.NpcMinus);

            ApplySavedHotkey(
                HotkeyAction.NpcEquals,
                data.NpcEquals);

            ApplySavedHotkey(
                HotkeyAction.NpcF12,
                data.NpcF12);

            ApplySavedHotkey(
                HotkeyAction.Npc8,
                data.Npc8);

            ApplySavedHotkey(
                HotkeyAction.NpcF11,
                data.NpcF11);
        }
        catch
        {
            // Jika file rusak,
            // gunakan default.
        }


        UpdateHotkeyBoxes();
    }

    private void ApplySavedHotkey(
        HotkeyAction action,
        SavedHotkey? saved)
    {
        if (saved == null)
            return;


        if (!Enum.TryParse(
                saved.Key,
                true,
                out Keys key))
        {
            return;
        }


        if (!Enum.TryParse(
                saved.Modifiers,
                true,
                out Keys modifiers))
        {
            modifiers =
                Keys.None;
        }


        if (key == Keys.None)
            return;


        var definition =
            GetHotkeyDefinition(
                action);

        definition.Key =
            key;

        definition.Modifiers =
            modifiers;
    }

    private void SaveHotkeys()
    {
        try
        {
            var data =
                new HotkeySettings
                {
                    Npc7 =
                        CreateSavedHotkey(
                            HotkeyAction.Npc7),

                    Npc0 =
                        CreateSavedHotkey(
                            HotkeyAction.Npc0),

                    NpcMinus =
                        CreateSavedHotkey(
                            HotkeyAction.NpcMinus),

                    NpcEquals =
                        CreateSavedHotkey(
                            HotkeyAction.NpcEquals),

                    NpcF12 =
                        CreateSavedHotkey(
                            HotkeyAction.NpcF12),

                    Npc8 =
                        CreateSavedHotkey(
                            HotkeyAction.Npc8),

                    NpcF11 =
                        CreateSavedHotkey(
                            HotkeyAction.NpcF11)
                };


            var options =
                new JsonSerializerOptions
                {
                    WriteIndented = true
                };


            string json =
                JsonSerializer.Serialize(
                    data,
                    options);


            File.WriteAllText(
                hotkeyFile,
                json);
        }
        catch (Exception ex)
        {
            statusLabel.Text =
                "Error: gagal menyimpan hotkey";

            MessageBox.Show(
                this,
                $"Gagal menyimpan setting hotkey:\r\n\r\n{ex.Message}",
                "RF Tool",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private SavedHotkey CreateSavedHotkey(
        HotkeyAction action)
    {
        var definition =
            GetHotkeyDefinition(
                action);

        return new SavedHotkey
        {
            Key =
                definition.Key.ToString(),

            Modifiers =
                definition.Modifiers.ToString()
        };
    }

    private void ResetHotkeysToDefault()
    {
        UnregisterAllHotkeys();
    
        hotkeys[HotkeyAction.Npc7] =
            new HotkeyDefinition(
                HotkeyIdNpc7,
                HotkeyAction.Npc7,
                Keys.F1,
                Keys.Control);
    
        hotkeys[HotkeyAction.Npc0] =
            new HotkeyDefinition(
                HotkeyIdNpc0,
                HotkeyAction.Npc0,
                Keys.F2,
                Keys.Control);
    
        hotkeys[HotkeyAction.NpcMinus] =
            new HotkeyDefinition(
                HotkeyIdNpcMinus,
                HotkeyAction.NpcMinus,
                Keys.F3,
                Keys.Control);
    
        hotkeys[HotkeyAction.NpcEquals] =
            new HotkeyDefinition(
                HotkeyIdNpcEquals,
                HotkeyAction.NpcEquals,
                Keys.F4,
                Keys.Control);
    
        hotkeys[HotkeyAction.NpcF12] =
            new HotkeyDefinition(
                HotkeyIdNpcF12,
                HotkeyAction.NpcF12,
                Keys.F5,
                Keys.Control);
    
        hotkeys[HotkeyAction.Npc8] =
            new HotkeyDefinition(
                HotkeyIdNpc8,
                HotkeyAction.Npc8,
                Keys.F6,
                Keys.Control);
    
        hotkeys[HotkeyAction.NpcF11] =
            new HotkeyDefinition(
                HotkeyIdNpcF11,
                HotkeyAction.NpcF11,
                Keys.F7,
                Keys.Control);
    
        UpdateHotkeyBoxes();
    
        RegisterAllHotkeys();
    
        SaveHotkeys();
    
        statusLabel.Text =
            "Status: Hotkey kembali ke default";
    }
}
