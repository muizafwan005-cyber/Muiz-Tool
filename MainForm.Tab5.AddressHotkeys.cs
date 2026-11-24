using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using RFTool.Advanced;
using RFTool.AdvancedSystem;

namespace RFTool;

public sealed partial class MainForm
{
    // =========================================================
    // HOTKEY PER ADDRESS (seperti Cheat Engine: Set/Change hotkey)
    // + EDIT VALUE LANGSUNG KE MEMORI
    // =========================================================

    private IntPtr addressKeyboardHook = IntPtr.Zero;
    private LowLevelKeyboardProc? addressKeyboardProc;
    private bool addressHotkeyCapturing;

    private readonly HashSet<uint> addressKeysDown = new();
    private readonly List<(AddressEntry Entry, AddressHotkey Hotkey)> addressHeld = new();

    private static readonly (AddressHotkeyAction Action, string Text)[] AddressHotkeyActions =
    {
        (AddressHotkeyAction.ToggleFreeze, "Toggle freeze"),
        (AddressHotkeyAction.ToggleFreezeAllowIncrease, "Toggle freeze and allow increase"),
        (AddressHotkeyAction.ToggleFreezeAllowDecrease, "Toggle freeze and allow decrease"),
        (AddressHotkeyAction.Freeze, "Freeze"),
        (AddressHotkeyAction.Unfreeze, "Unfreeze"),
        (AddressHotkeyAction.SetValue, "Set value to:"),
        (AddressHotkeyAction.DecreaseValue, "Decrease value with:"),
        (AddressHotkeyAction.IncreaseValue, "Increase value with:")
    };

    private static readonly string[] AddressSoundNames =
    {
        "(none)", "Beep", "Asterisk", "Exclamation", "Hand", "Question"
    };

    private static string AddressActionText(AddressHotkeyAction action)
    {
        return AddressHotkeyActions[(int)action].Text;
    }

    private static string HotkeySummary(AddressEntry entry)
    {
        if (entry.Hotkeys == null || entry.Hotkeys.Count == 0)
            return "";

        return string.Join(
            ", ",
            entry.Hotkeys.Select(h => FormatHotkey((Keys)h.Modifiers, (Keys)h.Key)));
    }

    private static void PlayAddressSound(string? name)
    {
        switch (name)
        {
            case "Beep": SystemSounds.Beep.Play(); break;
            case "Asterisk": SystemSounds.Asterisk.Play(); break;
            case "Exclamation": SystemSounds.Exclamation.Play(); break;
            case "Hand": SystemSounds.Hand.Play(); break;
            case "Question": SystemSounds.Question.Play(); break;
        }
    }

    // =========================================================
    // KEYBOARD HOOK (pasif, input selalu diteruskan ke game)
    // =========================================================

    private bool EnsureAddressHotkeyHook()
    {
        if (addressKeyboardHook != IntPtr.Zero)
            return true;

        if (!IsHandleCreated || IsDisposed)
            return false;

        addressKeyboardProc = AddressKeyboardHookCallback;

        addressKeyboardHook = SetWindowsHookEx(
            WH_KEYBOARD_LL,
            addressKeyboardProc,
            GetModuleHandle(null),
            0);

        if (addressKeyboardHook == IntPtr.Zero)
            return false;

        HandleDestroyed += (_, _) => RemoveAddressHotkeyHook();
        return true;
    }

    private void RemoveAddressHotkeyHook()
    {
        if (addressKeyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(addressKeyboardHook);
            addressKeyboardHook = IntPtr.Zero;
        }

        addressKeyboardProc = null;
        addressKeysDown.Clear();
        addressHeld.Clear();

        lock (addressHoldLock)
            addressHoldItems.Clear();
    }

    private IntPtr AddressKeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 && !addressHotkeyCapturing && lParam != IntPtr.Zero)
            {
                int message = wParam.ToInt32();

                bool isDown = message == WM_KEYDOWN || message == WM_SYSKEYDOWN;
                bool isUp = message == WM_KEYUP || message == WM_SYSKEYUP;

                if (isDown || isUp)
                {
                    KBDLLHOOKSTRUCT info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                    // Abaikan input sintetis; semua input tetap diteruskan.
                    if ((info.flags & LLKHF_INJECTED) == 0)
                        HandleAddressHotkeyKey(info.vkCode, isDown);
                }
            }
        }
        catch
        {
            // Hook tidak boleh menghentikan aliran input keyboard.
        }

        return CallNextHookEx(addressKeyboardHook, nCode, wParam, lParam);
    }

    private void HandleAddressHotkeyKey(uint virtualKey, bool isDown)
    {
        if (isDown)
        {
            // Abaikan auto-repeat saat tombol ditahan
            if (!addressKeysDown.Add(virtualKey))
                return;

            // Multi karakter: hanya window yang game-nya aktif yang menjalankan hotkey.
            if (!HotkeyAllowedHere())
                return;

            Keys key = (Keys)virtualKey;
            Keys modifiers = GetCurrentModifiers();

            foreach (AddressEntry entry in addressManager.Entries.ToList())
            {
                foreach (AddressHotkey hotkey in entry.Hotkeys.ToList())
                {
                    if (hotkey.Key != (int)key || hotkey.Modifiers != (int)modifiers)
                        continue;

                    if (hotkey.DeactivateOnRelease)
                        addressHeld.Add((entry, hotkey));

                    PostAddressHotkey(entry, hotkey, true);

                    // Tombol ditahan -> nilai terus dipertahankan / diulang
                    StartAddressHold(entry, hotkey);
                }
            }
        }
        else
        {
            addressKeysDown.Remove(virtualKey);

            lock (addressHoldLock)
                addressHoldItems.RemoveAll(x => x.Key == (Keys)virtualKey);

            for (int i = addressHeld.Count - 1; i >= 0; i--)
            {
                (AddressEntry entry, AddressHotkey hotkey) = addressHeld[i];

                if (hotkey.Key != (int)(Keys)virtualKey)
                    continue;

                addressHeld.RemoveAt(i);
                PostAddressHotkey(entry, hotkey, false);
            }
        }
    }

    // Eksekusi di UI thread supaya callback hook tetap cepat
    private void PostAddressHotkey(AddressEntry entry, AddressHotkey hotkey, bool pressed)
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        try
        {
            BeginInvoke(new Action(() =>
            {
                if (!IsDisposed)
                    RunAddressHotkey(entry, hotkey, pressed);
            }));
        }
        catch
        {
            // Form mungkin sedang ditutup.
        }
    }

    // =========================================================
    // HOLD: selama tombol hotkey ditahan, nilai dipertahankan
    //
    // Set value to       -> ditulis ulang terus (tiap ~4 ms) di thread terpisah,
    //                       sehingga nilai bertahan walau game menimpanya
    //                       (mis. speed saat karakter berjalan).
    // Increase / Decrease -> diulang tiap 100 ms setelah jeda 300 ms.
    // Berhenti otomatis saat tombol dilepas.
    // =========================================================

    private sealed class HoldItem
    {
        public AddressEntry Entry = null!;
        public AddressHotkey Hotkey = null!;
        public Keys Key;
        public string Expression = "";
        public byte[] Data = Array.Empty<byte>();
        public long NextTick;
    }

    private readonly object addressHoldLock = new();
    private readonly List<HoldItem> addressHoldItems = new();
    private Thread? addressHoldThread;
    private TargetProvider? addressHoldProvider;

    private void StartAddressHold(AddressEntry entry, AddressHotkey hotkey)
    {
        bool isSet = hotkey.Action == AddressHotkeyAction.SetValue;

        bool isDelta =
            hotkey.Action == AddressHotkeyAction.DecreaseValue ||
            hotkey.Action == AddressHotkeyAction.IncreaseValue;

        if ((!isSet && !isDelta) || entry.IsScript)
            return;

        var item = new HoldItem
        {
            Entry = entry,
            Hotkey = hotkey,
            Key = (Keys)hotkey.Key,
            Expression = entry.Address ?? "",
            NextTick = Environment.TickCount64 + 300
        };

        if (isSet)
        {
            // Sedang di-freeze: mesin Freeze sudah menahan nilainya.
            if (entry.Freeze)
                return;

            ScanValueType type = ValueCodec.TypeFromName(entry.ValueType);

            if (!ValueCodec.TryParse(hotkey.Value, type, entry.Hex, false, out byte[] data, out _))
                return;

            item.Data = data;
        }

        addressHoldProvider = AdvSvc.Target;

        lock (addressHoldLock)
        {
            addressHoldItems.RemoveAll(x =>
                ReferenceEquals(x.Entry, entry) &&
                ReferenceEquals(x.Hotkey, hotkey));

            addressHoldItems.Add(item);

            if (addressHoldThread == null)
            {
                addressHoldThread = new Thread(AddressHoldLoop)
                {
                    IsBackground = true,
                    Name = "MuizHotkeyHold"
                };

                addressHoldThread.Start();
            }
        }
    }

    private void AddressHoldLoop()
    {
        while (true)
        {
            HoldItem[] snapshot;

            lock (addressHoldLock)
            {
                if (addressHoldItems.Count == 0)
                {
                    addressHoldThread = null;
                    return;
                }

                snapshot = addressHoldItems.ToArray();
            }

            TargetProcess? target = addressHoldProvider?.Get();
            long now = Environment.TickCount64;

            foreach (HoldItem item in snapshot)
            {
                // Pengaman: tombol sudah tidak ditekan (event lepas mungkin terlewat)
                if ((GetAsyncKeyState((int)item.Key) & 0x8000) == 0)
                {
                    lock (addressHoldLock)
                        addressHoldItems.Remove(item);

                    continue;
                }

                if (item.Hotkey.Action == AddressHotkeyAction.SetValue)
                {
                    if (target != null &&
                        AddressExpression.TryEvaluate(
                            target,
                            item.Expression,
                            null,
                            out ulong address,
                            out _))
                    {
                        target.Write(address, item.Data);
                    }
                }
                else if (now >= item.NextTick)
                {
                    item.NextTick = now + 100;
                    PostAddressHotkeyRepeat(item.Entry, item.Hotkey);
                }
            }

            Thread.Sleep(4);
        }
    }

    // Increase/Decrease diulang lewat UI thread agar tetap sinkron dengan Freeze
    private void PostAddressHotkeyRepeat(AddressEntry entry, AddressHotkey hotkey)
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        try
        {
            BeginInvoke(new Action(() =>
            {
                if (!IsDisposed && addressManager.Entries.Contains(entry))
                    ApplyValueHotkey(entry, hotkey, true);
            }));
        }
        catch
        {
            // Form mungkin sedang ditutup.
        }
    }

    // =========================================================
    // EKSEKUSI AKSI HOTKEY
    // =========================================================

    private void RunAddressHotkey(AddressEntry entry, AddressHotkey hotkey, bool pressed)
    {
        if (!addressManager.Entries.Contains(entry))
            return;

        try
        {
            switch (hotkey.Action)
            {
                case AddressHotkeyAction.ToggleFreeze:
                case AddressHotkeyAction.ToggleFreezeAllowIncrease:
                case AddressHotkeyAction.ToggleFreezeAllowDecrease:
                case AddressHotkeyAction.Freeze:
                case AddressHotkeyAction.Unfreeze:
                {
                    FreezeMode mode = hotkey.Action switch
                    {
                        AddressHotkeyAction.ToggleFreezeAllowIncrease => FreezeMode.AllowIncrease,
                        AddressHotkeyAction.ToggleFreezeAllowDecrease => FreezeMode.AllowDecrease,
                        _ => FreezeMode.Normal
                    };

                    bool on;

                    if (!pressed)
                    {
                        // Tombol dilepas: hanya berlaku untuk "Deactivate on release"
                        if (!hotkey.DeactivateOnRelease || hotkey.Action == AddressHotkeyAction.Unfreeze)
                            return;

                        on = false;
                    }
                    else if (hotkey.Action == AddressHotkeyAction.Freeze)
                    {
                        on = true;
                    }
                    else if (hotkey.Action == AddressHotkeyAction.Unfreeze)
                    {
                        on = false;
                    }
                    else
                    {
                        on = !IsEntryActive(entry);
                    }

                    if (SetEntryFreezeFromHotkey(entry, on, mode))
                        PlayAddressSound(on ? hotkey.ActivateSound : hotkey.DeactivateSound);

                    break;
                }

                default:
                    if (!pressed)
                        return;

                    ApplyValueHotkey(entry, hotkey);
                    break;
            }
        }
        catch (Exception ex)
        {
            addressInfoLabel.Text = "Hotkey error: " + ex.Message;
        }
    }

    private bool SetEntryFreezeFromHotkey(AddressEntry entry, bool on, FreezeMode mode)
    {
        if (entry.IsScript)
        {
            entry.Freeze = on;
            HandleScriptToggle(entry);

            addressInfoLabel.Text =
                "Hotkey: script " + entry.Description + (on ? " -> Enable" : " -> Disable");
            return true;
        }

        if (on)
        {
            entry.FreezeMode = mode;
            entry.Freeze = true;

            if (!ValidateForFreeze(entry, out string problem))
            {
                entry.Freeze = false;
                entry.FreezeMode = FreezeMode.Normal;
                addressInfoLabel.Text = "Hotkey freeze dibatalkan: " + problem;
                return false;
            }
        }
        else
        {
            entry.Freeze = false;
            entry.FreezeMode = FreezeMode.Normal;
            CascadeDeactivate(entry);
        }

        AdvSvc.Freeze.Refresh();
        ApplyGroupVisibility();

        string modeText = mode switch
        {
            FreezeMode.AllowIncrease => " (boleh naik)",
            FreezeMode.AllowDecrease => " (boleh turun)",
            _ => ""
        };

        addressInfoLabel.Text =
            "Hotkey: " + entry.Description + (on ? " FREEZE" + modeText : " unfreeze");

        return true;
    }

    private void ApplyValueHotkey(AddressEntry entry, AddressHotkey hotkey, bool repeat = false)
    {
        if (entry.IsScript)
        {
            addressInfoLabel.Text =
                "Hotkey: entri script hanya mendukung Toggle freeze / Freeze / Unfreeze.";
            return;
        }

        if (hotkey.Action == AddressHotkeyAction.SetValue)
        {
            if (WriteEntryValueNow(entry, hotkey.Value) && !repeat)
                PlayAddressSound(hotkey.ActivateSound);

            return;
        }

        TargetProcess? target = AdvSvc.Target.Get();

        if (target == null)
        {
            addressInfoLabel.Text = "Hotkey: belum attach ke process.";
            return;
        }

        ScanValueType type = ValueCodec.TypeFromName(entry.ValueType);

        if (type == ScanValueType.String || type == ScanValueType.ByteArray)
        {
            addressInfoLabel.Text = "Hotkey: Increase/Decrease hanya untuk tipe angka.";
            return;
        }

        if (!EntryIO.TryResolve(target, entry, out ulong address, out string addressError))
        {
            addressInfoLabel.Text = "Hotkey: " + addressError;
            return;
        }

        byte[]? current = target.ReadBytes(address, ValueCodec.Size(type));

        if (current == null)
        {
            addressInfoLabel.Text = "Hotkey: gagal membaca memori.";
            return;
        }

        bool decrease = hotkey.Action == AddressHotkeyAction.DecreaseValue;

        if (!TryAddToBytes(current, type, hotkey.Value, decrease, out byte[] result))
        {
            addressInfoLabel.Text = "Hotkey: nilai '" + hotkey.Value + "' tidak valid.";
            return;
        }

        if (!target.Write(address, result))
        {
            addressInfoLabel.Text =
                "Hotkey: write gagal: " + TargetProcess.ErrorText(target.LastError);
            return;
        }

        string newText = ValueCodec.Format(result, type, false, entry.Signed, entry.Hex);

        // Kalau sedang di-freeze, nilai tahan ikut berubah agar tidak dikembalikan
        if (entry.Freeze)
        {
            entry.Value = newText;
            AdvSvc.Freeze.Refresh();
        }

        addressInfoLabel.Text =
            "Hotkey: " + entry.Description + (decrease ? " - " : " + ") +
            hotkey.Value + " = " + newText;

        if (!repeat)
            PlayAddressSound(hotkey.ActivateSound);
    }

    // Tambah / kurang nilai angka sesuai tipe (integer dibungkus sesuai ukuran tipe)
    private static bool TryAddToBytes(
        byte[] current,
        ScanValueType type,
        string deltaText,
        bool negate,
        out byte[] result)
    {
        result = current;
        deltaText = deltaText.Trim();

        switch (type)
        {
            case ScanValueType.Float:
            {
                if (!float.TryParse(deltaText, NumberStyles.Float, CultureInfo.InvariantCulture, out float d))
                    return false;

                float value = BitConverter.ToSingle(current, 0);
                result = BitConverter.GetBytes(negate ? value - d : value + d);
                return true;
            }

            case ScanValueType.Double:
            {
                if (!double.TryParse(deltaText, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    return false;

                double value = BitConverter.ToDouble(current, 0);
                result = BitConverter.GetBytes(negate ? value - d : value + d);
                return true;
            }

            case ScanValueType.Byte:
            case ScanValueType.Int16:
            case ScanValueType.Int32:
            case ScanValueType.Int64:
            {
                if (!long.TryParse(deltaText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long d))
                    return false;

                long value = type switch
                {
                    ScanValueType.Byte => current[0],
                    ScanValueType.Int16 => BitConverter.ToInt16(current, 0),
                    ScanValueType.Int32 => BitConverter.ToInt32(current, 0),
                    _ => BitConverter.ToInt64(current, 0)
                };

                long updated = unchecked(negate ? value - d : value + d);

                byte[] full = BitConverter.GetBytes(updated);
                result = new byte[current.Length];
                Array.Copy(full, result, current.Length);
                return true;
            }

            default:
                return false;
        }
    }

    // =========================================================
    // EDIT VALUE LANGSUNG (double-click kolom Value)
    // =========================================================

    // Ubah value dan tulis ke memori saat itu juga. Freeze hanya untuk menahan nilai.
    private bool WriteEntryValueNow(AddressEntry entry, string newValue)
    {
        string old = entry.Value;
        entry.Value = newValue;

        if (!EntryIO.TryWrite(AdvSvc.Target, entry, out string message))
        {
            entry.Value = old;
            addressInfoLabel.Text = "Write gagal: " + message;
            return false;
        }

        // Sedang di-freeze -> nilai tahan ikut diperbarui
        if (entry.Freeze)
            AdvSvc.Freeze.Refresh();

        addressInfoLabel.Text = "Value diubah: " + entry.Description + " = " + newValue;
        return true;
    }

    private void AddressGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.RowIndex >= addressManager.Entries.Count ||
            addressGrid.Columns[e.ColumnIndex].Name != "Live")
        {
            return;
        }

        AddressEntry entry = addressManager.Entries[e.RowIndex];

        if (entry.IsScript)
            return;

        string shown = addressGrid.Rows[e.RowIndex].Cells["Live"].Value as string ?? "";
        string initial = shown.Length > 0 && shown != "??" ? shown : entry.Value;

        string? text = AdvTheme.Prompt(
            this,
            "Change value",
            "Value baru untuk " + entry.Description + ":",
            initial);

        if (text == null)
            return;

        WriteEntryValueNow(entry, text.Trim());
    }

    // =========================================================
    // DIALOG: SET/CHANGE HOTKEY
    // =========================================================

    private void ShowAddressHotkeyDialog()
    {
        AddressEntry? entry = SelectedAddressEntry();

        if (entry == null)
        {
            MessageBox.Show("Pilih alamat terlebih dahulu.");
            return;
        }

        List<AddressHotkey> work = entry.Hotkeys.Select(h => h.Clone()).ToList();

        Keys capturedKey = Keys.None;
        Keys capturedModifiers = Keys.None;
        int editing = -1;

        using var form = new Form
        {
            Text = "Set/Change hotkey : " + entry.Description,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(568, 416),
            BackColor = AdvTheme.Back,
            ForeColor = AdvTheme.Fore
        };

        var list = new ListView
        {
            Location = new Point(6, 6),
            Size = new Size(556, 168),
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = false,
            GridLines = true,
            BackColor = AdvTheme.Input,
            ForeColor = AdvTheme.Fore,
            BorderStyle = BorderStyle.FixedSingle
        };

        list.Columns.Add("Hotkey", 120);
        list.Columns.Add("On Hotkey", 170);
        list.Columns.Add("Value", 80);
        list.Columns.Add("Description", 160);

        var keyBox = new HotkeyTextBox
        {
            Location = new Point(6, 204),
            Width = 236,
            BackColor = AdvTheme.Input,
            ForeColor = AdvTheme.Fore
        };

        Button clearBtn = AdvTheme.Btn("Clear", 66).At(248, 203);

        var actionBox = new ComboBox
        {
            Location = new Point(6, 232),
            Width = 236,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = AdvTheme.Input,
            ForeColor = AdvTheme.Fore
        };

        foreach (var item in AddressHotkeyActions)
            actionBox.Items.Add(item.Text);

        actionBox.SelectedIndex = 0;

        var releaseCheck = new CheckBox
        {
            Text = "Deactivate on release",
            Location = new Point(6, 260),
            AutoSize = true,
            ForeColor = AdvTheme.Fore
        };

        TextBox valueBox = AdvTheme.Txt(236).At(6, 284);
        valueBox.Enabled = false;

        Label descLabel = AdvTheme.Lbl("Description (Optional)").At(6, 312);
        TextBox descBox = AdvTheme.Txt(236).At(6, 332);

        Button applyBtn = AdvTheme.Btn("Apply", 74).At(6, 362);
        Button cancelBtn = AdvTheme.Btn("Cancel", 74).At(86, 362);

        Label activateLabel = AdvTheme.Lbl("Activate sound").At(330, 184);

        var activateSound = new ComboBox
        {
            Location = new Point(330, 204),
            Width = 170,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = AdvTheme.Input,
            ForeColor = AdvTheme.Fore
        };

        Button activatePlay = AdvTheme.Btn("▶", 28).At(506, 203);

        Label deactivateLabel = AdvTheme.Lbl("Deactivate sound").At(330, 236);

        var deactivateSound = new ComboBox
        {
            Location = new Point(330, 256),
            Width = 170,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = AdvTheme.Input,
            ForeColor = AdvTheme.Fore
        };

        Button deactivatePlay = AdvTheme.Btn("▶", 28).At(506, 255);

        activateSound.Items.AddRange(AddressSoundNames);
        deactivateSound.Items.AddRange(AddressSoundNames);
        activateSound.SelectedIndex = 0;
        deactivateSound.SelectedIndex = 0;

        Label hint = AdvTheme.Lbl(
            "Pilih baris lalu Apply untuk mengubah.\nTekan Delete untuk menghapus hotkey terpilih.",
            true).At(330, 300);

        Button okBtn = AdvTheme.Btn("OK", 90).At(239, 384);
        okBtn.DialogResult = DialogResult.OK;

        form.Controls.AddRange(new Control[]
        {
            list, keyBox, clearBtn, actionBox, releaseCheck, valueBox,
            descLabel, descBox, applyBtn, cancelBtn,
            activateLabel, activateSound, activatePlay,
            deactivateLabel, deactivateSound, deactivatePlay,
            hint, okBtn
        });

        form.Controls.Add(AdvTheme.Lbl("Type the keys you want to set the hotkey to").At(6, 184));

        // ---------- helper lokal ----------

        void UpdateEnabled()
        {
            int idx = actionBox.SelectedIndex;

            valueBox.Enabled = idx >= (int)AddressHotkeyAction.SetValue;
            releaseCheck.Enabled = idx <= (int)AddressHotkeyAction.Freeze;

            if (!valueBox.Enabled)
                valueBox.Text = "";

            if (!releaseCheck.Enabled)
                releaseCheck.Checked = false;
        }

        void ClearEditor()
        {
            editing = -1;
            capturedKey = Keys.None;
            capturedModifiers = Keys.None;
            keyBox.Text = "";
            actionBox.SelectedIndex = 0;
            valueBox.Text = "";
            descBox.Text = "";
            releaseCheck.Checked = false;
            activateSound.SelectedIndex = 0;
            deactivateSound.SelectedIndex = 0;
            list.SelectedItems.Clear();
            UpdateEnabled();
        }

        void Refill()
        {
            list.Items.Clear();

            foreach (AddressHotkey h in work)
            {
                list.Items.Add(new ListViewItem(new[]
                {
                    FormatHotkey((Keys)h.Modifiers, (Keys)h.Key),
                    AddressActionText(h.Action),
                    h.Action >= AddressHotkeyAction.SetValue ? h.Value : "",
                    h.Description
                }));
            }
        }

        actionBox.SelectedIndexChanged += (_, _) => UpdateEnabled();

        keyBox.KeyDown += (_, e) =>
        {
            e.SuppressKeyPress = true;
            e.Handled = true;

            Keys key = e.KeyCode;

            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu ||
                key == Keys.LControlKey || key == Keys.RControlKey ||
                key == Keys.LShiftKey || key == Keys.RShiftKey ||
                key == Keys.LMenu || key == Keys.RMenu ||
                key == Keys.Escape)
            {
                return;
            }

            Keys modifiers = Keys.None;

            if (e.Control) modifiers |= Keys.Control;
            if (e.Alt) modifiers |= Keys.Alt;
            if (e.Shift) modifiers |= Keys.Shift;

            capturedKey = key;
            capturedModifiers = modifiers;
            keyBox.Text = FormatHotkey(modifiers, key);
        };

        clearBtn.Click += (_, _) =>
        {
            capturedKey = Keys.None;
            capturedModifiers = Keys.None;
            keyBox.Text = "";
        };

        list.SelectedIndexChanged += (_, _) =>
        {
            if (list.SelectedIndices.Count == 0)
                return;

            int idx = list.SelectedIndices[0];

            if (idx < 0 || idx >= work.Count)
                return;

            AddressHotkey h = work[idx];

            editing = idx;
            capturedKey = (Keys)h.Key;
            capturedModifiers = (Keys)h.Modifiers;
            keyBox.Text = FormatHotkey(capturedModifiers, capturedKey);
            actionBox.SelectedIndex = (int)h.Action;
            UpdateEnabled();
            valueBox.Text = h.Value;
            releaseCheck.Checked = h.DeactivateOnRelease && releaseCheck.Enabled;
            descBox.Text = h.Description;

            int a = Array.IndexOf(AddressSoundNames, h.ActivateSound);
            int d = Array.IndexOf(AddressSoundNames, h.DeactivateSound);
            activateSound.SelectedIndex = a < 0 ? 0 : a;
            deactivateSound.SelectedIndex = d < 0 ? 0 : d;
        };

        list.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Delete || list.SelectedIndices.Count == 0)
                return;

            int idx = list.SelectedIndices[0];

            if (idx >= 0 && idx < work.Count)
                work.RemoveAt(idx);

            Refill();
            ClearEditor();
            e.Handled = true;
        };

        activatePlay.Click += (_, _) => PlayAddressSound(activateSound.Text);
        deactivatePlay.Click += (_, _) => PlayAddressSound(deactivateSound.Text);

        cancelBtn.Click += (_, _) => ClearEditor();

        applyBtn.Click += (_, _) =>
        {
            if (capturedKey == Keys.None)
            {
                MessageBox.Show(
                    form,
                    "Tekan kombinasi tombol pada kotak hotkey terlebih dahulu.",
                    "Hotkey");
                return;
            }

            // Hindari bentrok dengan hotkey NPC (Tab 3)
            foreach (HotkeyDefinition npc in hotkeys.Values)
            {
                if (npc.Key == capturedKey && npc.Modifiers == capturedModifiers)
                {
                    MessageBox.Show(
                        form,
                        "Hotkey tersebut sudah dipakai fitur NPC (Tab 3).",
                        "Hotkey bentrok",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }

            var action = (AddressHotkeyAction)actionBox.SelectedIndex;
            string value = valueBox.Enabled ? valueBox.Text.Trim() : "";
            ScanValueType type = ValueCodec.TypeFromName(entry.ValueType);

            if (action >= AddressHotkeyAction.SetValue)
            {
                if (value.Length == 0)
                {
                    MessageBox.Show(form, "Isi Value terlebih dahulu.", "Hotkey");
                    return;
                }

                if (action == AddressHotkeyAction.SetValue)
                {
                    if (!ValueCodec.TryParse(value, type, entry.Hex, false, out _, out string parseError))
                    {
                        MessageBox.Show(form, "Value tidak valid: " + parseError, "Hotkey");
                        return;
                    }
                }
                else
                {
                    if (type == ScanValueType.String || type == ScanValueType.ByteArray)
                    {
                        MessageBox.Show(form, "Increase/Decrease hanya untuk tipe angka.", "Hotkey");
                        return;
                    }

                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        MessageBox.Show(form, "Value harus berupa angka.", "Hotkey");
                        return;
                    }
                }
            }

            var hotkey = new AddressHotkey
            {
                Key = (int)capturedKey,
                Modifiers = (int)capturedModifiers,
                Action = action,
                Value = value,
                DeactivateOnRelease = releaseCheck.Enabled && releaseCheck.Checked,
                Description = descBox.Text.Trim(),
                ActivateSound = activateSound.Text,
                DeactivateSound = deactivateSound.Text
            };

            if (editing >= 0 && editing < work.Count)
                work[editing] = hotkey;
            else
                work.Add(hotkey);

            Refill();
            ClearEditor();
        };

        Refill();
        UpdateEnabled();

        // Selama dialog terbuka, hotkey address dijeda supaya tidak ikut tereksekusi
        addressHotkeyCapturing = true;

        DialogResult result;

        try
        {
            result = form.ShowDialog(this);
        }
        finally
        {
            addressHotkeyCapturing = false;
            addressKeysDown.Clear();
            addressHeld.Clear();

            lock (addressHoldLock)
                addressHoldItems.Clear();
        }

        if (result != DialogResult.OK)
            return;

        entry.Hotkeys = work;
        addressGrid.Invalidate();

        if (work.Count > 0 && !EnsureAddressHotkeyHook())
        {
            addressInfoLabel.Text = "Hotkey tersimpan, tetapi keyboard hook gagal dipasang.";
            return;
        }

        addressInfoLabel.Text =
            "Hotkey " + entry.Description + ": " + work.Count + " hotkey diatur.";
    }
}
