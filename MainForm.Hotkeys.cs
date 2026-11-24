using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using RFTool.Core;
using RFTool.Features;

namespace RFTool;

public sealed partial class MainForm
{
    // =========================================================
    // TAB 3 - HOTKEY NPC
    // Menggunakan WH_KEYBOARD_LL dan selalu meneruskan input
    // keyboard ke aplikasi/game melalui CallNextHookEx.
    // =========================================================

    // 1. FIELD & KONSTANTA

    private HotkeyTextBox npc7HotkeyBox = null!;
    private HotkeyTextBox npc0HotkeyBox = null!;
    private HotkeyTextBox npcMinusHotkeyBox = null!;
    private HotkeyTextBox npcEqualsHotkeyBox = null!;
    private HotkeyTextBox npcF12HotkeyBox = null!;
    private HotkeyTextBox npc8HotkeyBox = null!;
    private HotkeyTextBox npcF11HotkeyBox = null!;
    private HotkeyTextBox npcBabiEmasHotkeyBox = null!;

    private readonly Dictionary<HotkeyAction, HotkeyDefinition> hotkeys = new();

    private const int HotkeyIdNpc7 = 1001;
    private const int HotkeyIdNpc0 = 1002;
    private const int HotkeyIdNpcMinus = 1003;
    private const int HotkeyIdNpcEquals = 1004;
    private const int HotkeyIdNpcF12 = 1005;
    private const int HotkeyIdNpc8 = 1006;
    private const int HotkeyIdNpcF11 = 1007;
    private const int HotkeyIdNpcBabiEmas = 1008;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;

    private const int LLKHF_INJECTED = 0x10;

    private IntPtr keyboardHook = IntPtr.Zero;
    private LowLevelKeyboardProc? keyboardHookProc;
    private HotkeyAction? hotkeyCaptureAction;
    private readonly HashSet<uint> pressedKeys = new();

    private readonly string hotkeyFile =
        Path.Combine(Application.StartupPath, "hotkeys.json");

    // 2. TIPE DATA

    private enum HotkeyAction
    {
        Npc7,
        Npc0,
        NpcMinus,
        NpcEquals,
        NpcF12,
        Npc8,
        NpcF11,
        NpcBabiEmas
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

        protected override bool IsInputKey(Keys keyData)
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
        public SavedHotkey? NpcBabiEmas { get; set; }
    }

    private sealed class SavedHotkey
    {
        public string Key { get; set; } = "";
        public string Modifiers { get; set; } = "";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    // 3. WINDOWS API & KEYBOARD HOOK

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc callback,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hhk,
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RegisterAllHotkeys();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterAllHotkeys();
        base.OnHandleDestroyed(e);
    }

    private bool InstallKeyboardHook()
    {
        if (keyboardHook != IntPtr.Zero)
            return true;

        if (!IsHandleCreated || IsDisposed)
            return false;

        keyboardHookProc = KeyboardHookCallback;

        IntPtr moduleHandle = GetModuleHandle(null);

        keyboardHook = SetWindowsHookEx(
            WH_KEYBOARD_LL,
            keyboardHookProc,
            moduleHandle,
            0);

        return keyboardHook != IntPtr.Zero;
    }

    private void RemoveKeyboardHook()
    {
        if (keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(keyboardHook);
            keyboardHook = IntPtr.Zero;
        }

        pressedKeys.Clear();
        keyboardHookProc = null;
    }

    private IntPtr KeyboardHookCallback(
        int nCode,
        IntPtr wParam,
        IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 &&
                hotkeyCaptureAction == null &&
                lParam != IntPtr.Zero)
            {
                int message = wParam.ToInt32();

                bool isKeyDown =
                    message == WM_KEYDOWN ||
                    message == WM_SYSKEYDOWN;

                bool isKeyUp =
                    message == WM_KEYUP ||
                    message == WM_SYSKEYUP;

                if (isKeyDown || isKeyUp)
                {
                    KBDLLHOOKSTRUCT keyInfo =
                        Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                    // Jangan jalankan hotkey dari input sintetis.
                    // Input sintetis tetap diteruskan ke aplikasi berikutnya.
                    if ((keyInfo.flags & LLKHF_INJECTED) == 0)
                    {
                        if (isKeyUp)
                        {
                            pressedKeys.Remove(keyInfo.vkCode);
                        }
                        else if (pressedKeys.Add(keyInfo.vkCode))
                        {
                            CheckAndRunHotkey(keyInfo.vkCode);
                        }
                    }
                }
            }
        }
        catch
        {
            // Hook tidak boleh menghentikan aliran input keyboard.
        }

        // PENTING: jangan mengembalikan nilai non-zero.
        // Semua tombol tetap diteruskan ke RF Online/aplikasi lain.
        return CallNextHookEx(
            keyboardHook,
            nCode,
            wParam,
            lParam);
    }

    private void CheckAndRunHotkey(uint virtualKey)
    {
        // Multi karakter: hanya window yang game-nya aktif yang menjalankan hotkey.
        if (!HotkeyAllowedHere())
            return;

        Keys pressedKey = (Keys)virtualKey;
        Keys currentModifiers = GetCurrentModifiers();

        foreach (HotkeyDefinition definition in hotkeys.Values)
        {
            if (definition.Key != pressedKey)
                continue;

            if (definition.Modifiers != currentModifiers)
                continue;

            HotkeyAction action = definition.Action;

            // Eksekusi dipindahkan ke UI thread agar callback hook tetap cepat.
            if (!IsDisposed && IsHandleCreated)
            {
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (!IsDisposed)
                            ExecuteNpcHotkey(action);
                    }));
                }
                catch
                {
                    // Form mungkin sedang ditutup.
                }
            }

            break;
        }
    }

    private static Keys GetCurrentModifiers()
    {
        Keys result = Keys.None;

        if (IsKeyDown(VK_CONTROL))
            result |= Keys.Control;

        if (IsKeyDown(VK_MENU))
            result |= Keys.Alt;

        if (IsKeyDown(VK_SHIFT))
            result |= Keys.Shift;

        return result;
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    // 4. EKSEKUSI PERINTAH NPC

    private void ExecuteNpcHotkey(HotkeyAction action)
    {
        if (!IsAttached() || features == null)
        {
            statusLabel.Text =
                "Status: Attach process terlebih dahulu.";
            return;
        }

        try
        {
            int commandIndex = action switch
            {
                HotkeyAction.Npc7 => 0,
                HotkeyAction.Npc0 => 1,
                HotkeyAction.NpcMinus => 2,
                HotkeyAction.NpcEquals => 3,
                HotkeyAction.NpcF12 => 4,
                HotkeyAction.Npc8 => 5,
                HotkeyAction.NpcF11 => 6,
                HotkeyAction.NpcBabiEmas => 7,
                _ => -1
            };

            if (commandIndex < 0)
                return;

            NpcTarget target = GetSelectedNpcTarget();

            // NPC Babi Emas hanya tersedia untuk target ACC.
            if (action == HotkeyAction.NpcBabiEmas &&
                target != NpcTarget.Acc)
            {
                statusLabel.Text =
                    "Status: NPC Babi Emas hanya untuk target ACC. Pilih ACC dulu.";
                return;
            }

            features.ExecuteNpcCommand(commandIndex, target);

            string commandName = nameFor(action);

            string targetName = target switch
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
            HandleFeatureError(nameFor(action), ex);
        }
    }

    private static string nameFor(HotkeyAction action)
    {
        return action switch
        {
            HotkeyAction.Npc7 => "NPC Amunisi",
            HotkeyAction.Npc0 => "NPC Bank",
            HotkeyAction.NpcMinus => "NPC TOOL",
            HotkeyAction.NpcEquals => "NPC UMT",
            HotkeyAction.NpcF12 => "NPC Potion",
            HotkeyAction.Npc8 => "NPC Vendor",
            HotkeyAction.NpcF11 => "NPC Hero",
            HotkeyAction.NpcBabiEmas => "NPC Babi Emas",
            _ => "Unknown"
        };
    }

    private static string HotkeyDisplayName(HotkeyAction action)
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
            HotkeyAction.NpcBabiEmas => "Babi Emas",
            _ => action.ToString()
        };
    }

    // 5. BUILD TAB 3

    private void BuildTab3(TabPage tab)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BackColorMain,
            Padding = new Padding(10),
            AutoScroll = true
        };

        tab.Controls.Add(panel);

        var title = new Label
        {
            Text = "SETTING HOTKEY",
            AutoSize = true,
            Location = new Point(8, 10),
            ForeColor = ForeColorMain,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        panel.Controls.Add(title);

        var info = new Label
        {
            Text = "Klik kotak lalu tekan tombol yang diinginkan.",
            AutoSize = false,
            Location = new Point(8, 36),
            Size = new Size(295, 36),
            ForeColor = ForeColorDim
        };

        panel.Controls.Add(info);

        int y = 82;

        npc7HotkeyBox = AddHotkeyRow(
            panel, "NPC Amunisi", HotkeyAction.Npc7, y);
        y += 72;

        npc0HotkeyBox = AddHotkeyRow(
            panel, "NPC Bank", HotkeyAction.Npc0, y);
        y += 72;

        npcMinusHotkeyBox = AddHotkeyRow(
            panel, "NPC TOOL", HotkeyAction.NpcMinus, y);
        y += 72;

        npcEqualsHotkeyBox = AddHotkeyRow(
            panel, "NPC UMT", HotkeyAction.NpcEquals, y);
        y += 72;

        npcF12HotkeyBox = AddHotkeyRow(
            panel, "NPC Potion", HotkeyAction.NpcF12, y);
        y += 72;

        npc8HotkeyBox = AddHotkeyRow(
            panel, "NPC Vendor", HotkeyAction.Npc8, y);
        y += 72;

        npcF11HotkeyBox = AddHotkeyRow(
            panel, "NPC Hero", HotkeyAction.NpcF11, y);
        y += 72;

        npcBabiEmasHotkeyBox = AddHotkeyRow(
            panel, "NPC Babi Emas (ACC)", HotkeyAction.NpcBabiEmas, y);
        // Jarak ekstra agar RESET HOTKEY / SIMPAN berada di bawah slot terakhir
        y += 100;

        var resetButton = new Button
        {
            Text = "RESET HOTKEY",
            Location = new Point(8, y),
            Size = new Size(130, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = BackColorInput,
            ForeColor = ForeColorMain
        };

        resetButton.FlatAppearance.BorderSize = 0;
        resetButton.Click += (_, _) => ResetHotkeysToDefault();
        panel.Controls.Add(resetButton);

        var saveButton = new Button
        {
            Text = "SIMPAN",
            Location = new Point(145, y),
            Size = new Size(130, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = BackColorInput,
            ForeColor = ForeColorMain
        };

        saveButton.FlatAppearance.BorderSize = 0;
        saveButton.Click += (_, _) =>
        {
            SaveHotkeys();
            statusLabel.Text = "Status: Setting hotkey tersimpan";
        };

        panel.Controls.Add(saveButton);

        y += 48;

        var note = new Label
        {
            Text = "Hotkey bekerja secara global selama RF Tool aktif.",
            AutoSize = false,
            Location = new Point(8, y),
            Size = new Size(295, 45),
            ForeColor = ForeColorDim
        };

        panel.Controls.Add(note);
    }

    private HotkeyTextBox AddHotkeyRow(
        Control parent,
        string name,
        HotkeyAction action,
        int y)
    {
        var label = new Label
        {
            Text = name,
            AutoSize = false,
            Location = new Point(8, y),
            Size = new Size(170, 22),
            ForeColor = ForeColorMain
        };

        parent.Controls.Add(label);

        var hotkeyBox = new HotkeyTextBox
        {
            Location = new Point(8, y + 25),
            Size = new Size(180, 27),
            BackColor = BackColorInput,
            ForeColor = ForeColorMain,
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = HorizontalAlignment.Center
        };

        parent.Controls.Add(hotkeyBox);

        hotkeyBox.Enter += (_, _) =>
            BeginHotkeyCapture(action, hotkeyBox);

        hotkeyBox.KeyDown += (_, e) =>
            CaptureHotkey(action, hotkeyBox, e);

        hotkeyBox.Leave += (_, _) =>
            EndHotkeyCapture(action, hotkeyBox);

        return hotkeyBox;
    }

    // 6. CAPTURE HOTKEY

    private void BeginHotkeyCapture(
        HotkeyAction action,
        HotkeyTextBox box)
    {
        hotkeyCaptureAction = action;
        box.Text = "Tekan tombol...";
        box.SelectAll();
    }

    private void EndHotkeyCapture(
        HotkeyAction action,
        HotkeyTextBox box)
    {
        if (hotkeyCaptureAction == action)
            hotkeyCaptureAction = null;

        if (!hotkeys.TryGetValue(action, out HotkeyDefinition? definition))
        {
            box.Text = "Belum diset";
            return;
        }

        box.Text = FormatHotkey(definition.Modifiers, definition.Key);
    }

    private void CaptureHotkey(
        HotkeyAction action,
        HotkeyTextBox box,
        KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;

        Keys key = e.KeyCode;

        if (key == Keys.Escape)
        {
            EndHotkeyCapture(action, box);
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
            box.Text = "Tekan tombol...";
            return;
        }

        Keys modifiers = Keys.None;

        if (e.Control)
            modifiers |= Keys.Control;

        if (e.Alt)
            modifiers |= Keys.Alt;

        if (e.Shift)
            modifiers |= Keys.Shift;

        if (IsHotkeyUsedByAnotherFeature(action, modifiers, key))
        {
            MessageBox.Show(
                this,
                "Hotkey tersebut sudah digunakan fitur lain.",
                "Hotkey bentrok",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            box.Text = "Tekan tombol...";
            return;
        }

        HotkeyDefinition definition = GetHotkeyDefinition(action);

        Keys oldKey = definition.Key;
        Keys oldModifiers = definition.Modifiers;

        definition.Key = key;
        definition.Modifiers = modifiers;

        if (!RegisterHotkey(definition))
        {
            definition.Key = oldKey;
            definition.Modifiers = oldModifiers;

            MessageBox.Show(
                this,
                "Keyboard hook tidak aktif. Hotkey tidak dapat disimpan.",
                "Hotkey gagal",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            box.Text = FormatHotkey(oldModifiers, oldKey);
            hotkeyCaptureAction = null;
            return;
        }

        hotkeyCaptureAction = null;

        box.Text = FormatHotkey(modifiers, key);

        SaveHotkeys();

        statusLabel.Text =
            $"Status: Hotkey {nameFor(action)} → {box.Text}";

        BeginInvoke(new Action(() =>
        {
            SelectNextControl(box, true, true, true, true);
        }));
    }

    private bool IsHotkeyUsedByAnotherFeature(
        HotkeyAction currentAction,
        Keys modifiers,
        Keys key)
    {
        foreach (HotkeyDefinition definition in hotkeys.Values)
        {
            if (definition.Action == currentAction)
                continue;

            if (definition.Key == key &&
                definition.Modifiers == modifiers)
            {
                return true;
            }
        }

        return false;
    }

    // 7. REGISTER / UNREGISTER
    // Tidak memakai RegisterHotKey lagi karena API tersebut mengambil
    // kombinasi tombol dari aplikasi lain. Hook hanya mengamati input.

    private bool RegisterHotkey(HotkeyDefinition definition)
    {
        if (definition.Key == Keys.None)
            return false;

        if (!IsHandleCreated)
            return false;

        return InstallKeyboardHook();
    }

    private void RegisterAllHotkeys()
    {
        if (!IsHandleCreated)
            return;

        bool hasHotkey = false;

        foreach (HotkeyDefinition definition in hotkeys.Values)
        {
            if (definition.Key != Keys.None)
            {
                hasHotkey = true;
                break;
            }
        }

        if (hasHotkey && !InstallKeyboardHook())
        {
            statusLabel.Text =
                "Status: Gagal memasang keyboard hook";
        }
    }

    private void UnregisterHotkey(HotkeyAction action)
    {
        // Saat satu kotak sedang menangkap tombol, hook tidak menjalankan
        // hotkey NPC apa pun sampai proses capture selesai.
        hotkeyCaptureAction = action;
    }

    private void UnregisterAllHotkeys()
    {
        hotkeyCaptureAction = null;
        RemoveKeyboardHook();
    }

    private HotkeyDefinition GetHotkeyDefinition(HotkeyAction action)
    {
        return hotkeys[action];
    }

    // 8. TAMPILAN TEKS HOTKEY

    private static string FormatHotkey(Keys modifiers, Keys key)
    {
        if (key == Keys.None)
            return "Belum diset";

        var parts = new List<string>();

        if ((modifiers & Keys.Control) != 0)
            parts.Add("Ctrl");

        if ((modifiers & Keys.Alt) != 0)
            parts.Add("Alt");

        if ((modifiers & Keys.Shift) != 0)
            parts.Add("Shift");

        parts.Add(GetKeyDisplayName(key));

        return string.Join(" + ", parts);
    }

    private static string GetKeyDisplayName(Keys key)
    {
        return key switch
        {
            Keys.PageUp => "Page Up",
            Keys.PageDown => "Page Down",
            Keys.PrintScreen => "Print Screen",
            Keys.Scroll => "Scroll Lock",
            Keys.CapsLock => "Caps Lock",
            Keys.NumLock => "Num Lock",
            Keys.Insert => "Insert",
            Keys.Delete => "Delete",
            Keys.Home => "Home",
            Keys.End => "End",
            Keys.Up => "Up",
            Keys.Down => "Down",
            Keys.Left => "Left",
            Keys.Right => "Right",
            Keys.Space => "Space",
            Keys.Enter => "Enter",
            Keys.Back => "Backspace",
            Keys.Tab => "Tab",
            Keys.Oemtilde => "`",
            Keys.D0 => "0",
            Keys.D1 => "1",
            Keys.D2 => "2",
            Keys.D3 => "3",
            Keys.D4 => "4",
            Keys.D5 => "5",
            Keys.D6 => "6",
            Keys.D7 => "7",
            Keys.D8 => "8",
            Keys.D9 => "9",
            Keys.OemMinus => "-",
            Keys.Oemplus => "=",
            _ => key.ToString()
        };
    }

    private void UpdateHotkeyBoxes()
    {
        if (npc7HotkeyBox == null)
            return;

        npc7HotkeyBox.Text = FormatHotkey(
            hotkeys[HotkeyAction.Npc7].Modifiers,
            hotkeys[HotkeyAction.Npc7].Key);

        npc0HotkeyBox.Text = FormatHotkey(
            hotkeys[HotkeyAction.Npc0].Modifiers,
            hotkeys[HotkeyAction.Npc0].Key);

        npcMinusHotkeyBox.Text = FormatHotkey(
            hotkeys[HotkeyAction.NpcMinus].Modifiers,
            hotkeys[HotkeyAction.NpcMinus].Key);

        npcEqualsHotkeyBox.Text = FormatHotkey(
            hotkeys[HotkeyAction.NpcEquals].Modifiers,
            hotkeys[HotkeyAction.NpcEquals].Key);

        npcF12HotkeyBox.Text = FormatHotkey(
            hotkeys[HotkeyAction.NpcF12].Modifiers,
            hotkeys[HotkeyAction.NpcF12].Key);

        npc8HotkeyBox.Text = FormatHotkey(
            hotkeys[HotkeyAction.Npc8].Modifiers,
            hotkeys[HotkeyAction.Npc8].Key);

        npcF11HotkeyBox.Text = FormatHotkey(
            hotkeys[HotkeyAction.NpcF11].Modifiers,
            hotkeys[HotkeyAction.NpcF11].Key);

        npcBabiEmasHotkeyBox.Text = FormatHotkey(
            hotkeys[HotkeyAction.NpcBabiEmas].Modifiers,
            hotkeys[HotkeyAction.NpcBabiEmas].Key);
    }

    // 9. LOAD / SAVE / RESET

    private void LoadHotkeys()
    {
        hotkeys[HotkeyAction.Npc7] = new HotkeyDefinition(
            HotkeyIdNpc7, HotkeyAction.Npc7, Keys.F1, Keys.Control);

        hotkeys[HotkeyAction.Npc0] = new HotkeyDefinition(
            HotkeyIdNpc0, HotkeyAction.Npc0, Keys.F2, Keys.Control);

        hotkeys[HotkeyAction.NpcMinus] = new HotkeyDefinition(
            HotkeyIdNpcMinus, HotkeyAction.NpcMinus, Keys.F3, Keys.Control);

        hotkeys[HotkeyAction.NpcEquals] = new HotkeyDefinition(
            HotkeyIdNpcEquals, HotkeyAction.NpcEquals, Keys.F4, Keys.Control);

        hotkeys[HotkeyAction.NpcF12] = new HotkeyDefinition(
            HotkeyIdNpcF12, HotkeyAction.NpcF12, Keys.F5, Keys.Control);

        hotkeys[HotkeyAction.Npc8] = new HotkeyDefinition(
            HotkeyIdNpc8, HotkeyAction.Npc8, Keys.F6, Keys.Control);

        hotkeys[HotkeyAction.NpcF11] = new HotkeyDefinition(
            HotkeyIdNpcF11, HotkeyAction.NpcF11, Keys.F7, Keys.Control);

        hotkeys[HotkeyAction.NpcBabiEmas] = new HotkeyDefinition(
            HotkeyIdNpcBabiEmas, HotkeyAction.NpcBabiEmas, Keys.F8, Keys.Control);

        try
        {
            if (!File.Exists(hotkeyFile))
            {
                UpdateHotkeyBoxes();
                return;
            }

            string json = File.ReadAllText(hotkeyFile);

            HotkeySettings? data =
                JsonSerializer.Deserialize<HotkeySettings>(json);

            if (data == null)
            {
                UpdateHotkeyBoxes();
                return;
            }

            ApplySavedHotkey(HotkeyAction.Npc7, data.Npc7);
            ApplySavedHotkey(HotkeyAction.Npc0, data.Npc0);
            ApplySavedHotkey(HotkeyAction.NpcMinus, data.NpcMinus);
            ApplySavedHotkey(HotkeyAction.NpcEquals, data.NpcEquals);
            ApplySavedHotkey(HotkeyAction.NpcF12, data.NpcF12);
            ApplySavedHotkey(HotkeyAction.Npc8, data.Npc8);
            ApplySavedHotkey(HotkeyAction.NpcF11, data.NpcF11);
            ApplySavedHotkey(HotkeyAction.NpcBabiEmas, data.NpcBabiEmas);
        }
        catch
        {
            // Jika file rusak, gunakan default.
        }

        UpdateHotkeyBoxes();
    }

    private void ApplySavedHotkey(
        HotkeyAction action,
        SavedHotkey? saved)
    {
        if (saved == null)
            return;

        if (!Enum.TryParse(saved.Key, true, out Keys key))
            return;

        if (!Enum.TryParse(saved.Modifiers, true, out Keys modifiers))
            modifiers = Keys.None;

        if (key == Keys.None)
            return;

        HotkeyDefinition definition = GetHotkeyDefinition(action);
        definition.Key = key;
        definition.Modifiers = modifiers;
    }

    private void SaveHotkeys()
    {
        try
        {
            var data = new HotkeySettings
            {
                Npc7 = CreateSavedHotkey(HotkeyAction.Npc7),
                Npc0 = CreateSavedHotkey(HotkeyAction.Npc0),
                NpcMinus = CreateSavedHotkey(HotkeyAction.NpcMinus),
                NpcEquals = CreateSavedHotkey(HotkeyAction.NpcEquals),
                NpcF12 = CreateSavedHotkey(HotkeyAction.NpcF12),
                Npc8 = CreateSavedHotkey(HotkeyAction.Npc8),
                NpcF11 = CreateSavedHotkey(HotkeyAction.NpcF11),
                NpcBabiEmas = CreateSavedHotkey(HotkeyAction.NpcBabiEmas)
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(hotkeyFile, json);
        }
        catch (Exception ex)
        {
            statusLabel.Text = "Error: gagal menyimpan hotkey";

            MessageBox.Show(
                this,
                $"Gagal menyimpan setting hotkey:\r\n\r\n{ex.Message}",
                "RF Tool",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private SavedHotkey CreateSavedHotkey(HotkeyAction action)
    {
        HotkeyDefinition definition = GetHotkeyDefinition(action);

        return new SavedHotkey
        {
            Key = definition.Key.ToString(),
            Modifiers = definition.Modifiers.ToString()
        };
    }

    private void ResetHotkeysToDefault()
    {
        hotkeys[HotkeyAction.Npc7] = new HotkeyDefinition(
            HotkeyIdNpc7, HotkeyAction.Npc7, Keys.F1, Keys.Control);

        hotkeys[HotkeyAction.Npc0] = new HotkeyDefinition(
            HotkeyIdNpc0, HotkeyAction.Npc0, Keys.F2, Keys.Control);

        hotkeys[HotkeyAction.NpcMinus] = new HotkeyDefinition(
            HotkeyIdNpcMinus, HotkeyAction.NpcMinus, Keys.F3, Keys.Control);

        hotkeys[HotkeyAction.NpcEquals] = new HotkeyDefinition(
            HotkeyIdNpcEquals, HotkeyAction.NpcEquals, Keys.F4, Keys.Control);

        hotkeys[HotkeyAction.NpcF12] = new HotkeyDefinition(
            HotkeyIdNpcF12, HotkeyAction.NpcF12, Keys.F5, Keys.Control);

        hotkeys[HotkeyAction.Npc8] = new HotkeyDefinition(
            HotkeyIdNpc8, HotkeyAction.Npc8, Keys.F6, Keys.Control);

        hotkeys[HotkeyAction.NpcF11] = new HotkeyDefinition(
            HotkeyIdNpcF11, HotkeyAction.NpcF11, Keys.F7, Keys.Control);

        hotkeys[HotkeyAction.NpcBabiEmas] = new HotkeyDefinition(
            HotkeyIdNpcBabiEmas, HotkeyAction.NpcBabiEmas, Keys.F8, Keys.Control);

        UpdateHotkeyBoxes();
        RegisterAllHotkeys();
        SaveHotkeys();

        statusLabel.Text = "Status: Hotkey kembali ke default";
    }
}
