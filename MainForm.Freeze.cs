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
    // TAB 1 (EDIT + FREEZE) & BASE POINTER - DAFTAR ISI
    //
    // Urutan bagian di file ini:
    //   1. FIELD
    //   2. TIPE DATA
    //   3. BASE POINTER
    //   4. BUILD TAB 1
    //   5. ADDRESS & BACA NILAI
    //   6. APPLY / TULIS NILAI
    //   7. FREEZE TIMER
    //
    // Cari "// 1." sampai "// 7." untuk loncat ke bagiannya.
    // =========================================================


    // =========================================================
    // 1. FIELD
    // =========================================================

    private Panel tab1Scroll = null!;
    private Panel tab1Content = null!;
    private System.Windows.Forms.Timer freezeTimer = null!;
    private readonly List<FreezeEntry> freezeEntries = new();

    // =========================================================
    // 2. TIPE DATA
    // Jenis nilai dan satu baris data freeze.
    // =========================================================

    private enum FreezeType
    {
        String6,
        Byte,
        Float
    }

    private sealed class FreezeEntry
    {
        public string Name { get; }

        public long Offset { get; }

        public FreezeType Type { get; }

        public TextBox TextBox { get; }

        public CheckBox Freeze { get; }

        public Label AddressLabel { get; }

        public IntPtr Address { get; set; }


        public FreezeEntry(
            string name,
            long offset,
            FreezeType type,
            TextBox textBox,
            CheckBox freeze,
            Label addressLabel)
        {
            Name =
                name;

            Offset =
                offset;

            Type =
                type;

            TextBox =
                textBox;

            Freeze =
                freeze;

            AddressLabel =
                addressLabel;

            Address =
                IntPtr.Zero;
        }
    }

    // =========================================================
    // 3. BASE POINTER
    // Kotak BASE POINTER di bagian atas aplikasi.
    // =========================================================

    private bool TryParseBasePointerOffset(
        string input,
        out long offset)
    {
        offset = 0;
    
        string value = input.Trim();
        const string prefix = "RF_Online.bin+";
    
        if (value.StartsWith(
            prefix,
            StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(prefix.Length);
        }
    
        if (value.StartsWith(
            "0x",
            StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(2);
        }
    
        return long.TryParse(
            value,
            System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture,
            out offset)
            && offset >= 0;
    }

    private void ApplyBasePointer()
    {
        if (features == null || memory == null || !IsAttached())
        {
            statusLabel.Text = "Status: Attach process terlebih dahulu.";
            return;
        }
    
        if (!TryParseBasePointerOffset(
            basePointerTextBox.Text,
            out long newOffset))
        {
            statusLabel.Text = "Status: Format Base Pointer tidak valid.";
            return;
        }
    
        try
        {
            features.SetPlayerPointerOffset(newOffset);
    
            UpdateAllAddresses();
    
            statusLabel.Text =
                $"Status: Base Pointer diterapkan → +{newOffset:X}";
    
        }
        catch (Exception ex)
        {
            statusLabel.Text =
                "Status: Gagal menerapkan Base Pointer.";
    
            MessageBox.Show(
                this,
                ex.Message,
                "Base Pointer gagal",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    // =========================================================
    // 4. BUILD TAB 1
    // Daftar baris Nick, aspd, Speed, dll ada di BuildTab1.
    // =========================================================

    private void BuildTab1(
        TabPage tab)
    {
        tab1Scroll = new Panel
        {
            Dock =
                DockStyle.Fill,

            AutoScroll =
                true,

            BackColor =
                BackColorMain,

            Padding =
                new Padding(5)
        };

        tab.Controls.Add(
            tab1Scroll);

        tab1Content = new Panel
        {
            Location =
                new Point(
                    0,
                    0),

            Width =
                300,

            Height =
                700,

            BackColor =
                BackColorMain
        };

        tab1Scroll.Controls.Add(
            tab1Content);

        int y = 5;


        AddFreezeRow(
            tab1Content,
            "Nick",
            0x10,
            FreezeType.String6,
            ref y);

        AddFreezeRow(
            tab1Content,
            "aspd 255",
            0x96,
            FreezeType.Byte,
            ref y);

        AddFreezeRow(
            tab1Content,
            "aspd 65",
            0x97,
            FreezeType.Byte,
            ref y);

        AddFreezeRow(
            tab1Content,
            "Speed Float",
            0x708,
            FreezeType.Float,
            ref y);

        AddFreezeRow(
            tab1Content,
            "Detect Trap",
            0x802,
            FreezeType.Byte,
            ref y);

        AddFreezeRow(
            tab1Content,
            "Detect Invis",
            0x801,
            FreezeType.Byte,
            ref y);

        AddFreezeRow(
            tab1Content,
            "AntiStun",
            0x804,
            FreezeType.Byte,
            ref y);

        AddFreezeRow(
            tab1Content,
            "Anti Stun 2",
            0x808,
            FreezeType.Byte,
            ref y);

        AddFreezeRow(
            tab1Content,
            "TAB Bell[01]Cora[23]Acc[4]",
            0x4524,
            FreezeType.Byte,
            ref y);

        AddFreezeRow(
            tab1Content,
            "buta",
            0x816,
            FreezeType.Byte,
            ref y);


        tab1Content.Height =
            Math.Max(
                700,
                y + 10);
    }

    private void AddFreezeRow(
        Control parent,
        string name,
        long offset,
        FreezeType type,
        ref int y)
    {
        var label = new Label
        {
            Text =
                name,

            AutoSize =
                false,

            Location =
                new Point(
                    5,
                    y),

            Size =
                new Size(
                    290,
                    20),

            ForeColor =
                ForeColorMain
        };

        parent.Controls.Add(
            label);

        y += 19;


        var addressLabel = new Label
        {
            Text =
                "Address: -",

            AutoSize =
                false,

            Location =
                new Point(
                    5,
                    y),

            Size =
                new Size(
                    290,
                    18),

            ForeColor =
                ForeColorDim
        };

        parent.Controls.Add(
            addressLabel);

        y += 19;


        var textBox = new TextBox
        {
            Location =
                new Point(
                    5,
                    y),

            Size =
                new Size(
                    145,
                    25),

            BackColor =
                BackColorInput,

            ForeColor =
                ForeColorMain,

            BorderStyle =
                BorderStyle.FixedSingle
        };

        parent.Controls.Add(
            textBox);


        var apply = new Button
        {
            Text =
                "APPLY",

            Location =
                new Point(
                    155,
                    y),

            Size =
                new Size(
                    58,
                    25),

            FlatStyle =
                FlatStyle.Flat,

            BackColor =
                BackColorInput,

            ForeColor =
                ForeColorMain
        };

        apply.FlatAppearance.BorderSize =
            0;

        parent.Controls.Add(
            apply);


        var freeze = new CheckBox
        {
            Text =
                "FREEZE",

            AutoSize =
                false,

            Location =
                new Point(
                    215,
                    y),

            Size =
                new Size(
                    75,
                    25),

            ForeColor =
                ForeColorMain,

            BackColor =
                BackColorMain
        };

        parent.Controls.Add(
            freeze);


        var entry =
            new FreezeEntry(
                name,
                offset,
                type,
                textBox,
                freeze,
                addressLabel);

        freezeEntries.Add(
            entry);


        apply.Click +=
            (_, _) =>
            {
                ApplyEntry(
                    entry);
            };


        freeze.CheckedChanged +=
            (_, _) =>
            {
                if (freeze.Checked)
                {
                    if (
                        string.IsNullOrWhiteSpace(
                            textBox.Text))
                    {
                        TryReadCurrentValue(
                            entry);
                    }

                    if (
                        string.IsNullOrWhiteSpace(
                            textBox.Text))
                    {
                        ResetCheckBox(
                            freeze,
                            false);

                        return;
                    }

                    if (!IsAttached())
                    {
                        MessageBox.Show(
                            this,
                            "Attach process terlebih dahulu.",
                            "RF Tool",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);

                        ResetCheckBox(
                            freeze,
                            false);

                        return;
                    }

                    if (!ApplyEntry(
                        entry))
                    {
                        ResetCheckBox(
                            freeze,
                            false);

                        return;
                    }

                    EnsureFreezeTimer();
                }
                else
                {
                    StopFreezeTimerIfNeeded();
                }
            };


        y += 31;
    }

    // =========================================================
    // 5. ADDRESS & BACA NILAI
    // Resolve pointer, tampilkan address, baca nilai saat ini.
    // =========================================================

    private void UpdateAllAddresses()
    {
        if (!IsAttached() ||
            memory == null)
        {
            foreach (
                var entry
                in freezeEntries)
            {
                entry.AddressLabel.Text =
                    "Address: -";
            }

            return;
        }

        foreach (
            var entry
            in freezeEntries)
        {
            try
            {
                IntPtr address =
                    memory.ResolvePointer32(
                        features!.PlayerPointerOffset,
                        entry.Offset);

                entry.Address =
                    address;

                entry.AddressLabel.Text =
                    $"Address: 0x{address.ToInt64():X8}";
            }
            catch
            {
                entry.Address =
                    IntPtr.Zero;

                entry.AddressLabel.Text =
                    "Address: ERROR";
            }
        }
    }

    private void UpdateEntryAddress(
        FreezeEntry entry)
    {
        if (!IsAttached() ||
            memory == null)
        {
            entry.Address =
                IntPtr.Zero;

            entry.AddressLabel.Text =
                "Address: -";

            return;
        }

        try
        {
            IntPtr address =
                memory.ResolvePointer32(
                    features!.PlayerPointerOffset,
                    entry.Offset);

            entry.Address =
                address;

            entry.AddressLabel.Text =
                $"Address: 0x{address.ToInt64():X8}";
        }
        catch
        {
            entry.Address =
                IntPtr.Zero;

            entry.AddressLabel.Text =
                "Address: ERROR";
        }
    }

    private void TryReadCurrentValue(
        FreezeEntry entry)
    {
        if (!IsAttached() ||
            memory == null)
        {
            return;
        }

        try
        {
            IntPtr address =
                memory.ResolvePointer32(
                    features!.PlayerPointerOffset,
                    entry.Offset);

            entry.Address =
                address;

            entry.AddressLabel.Text =
                $"Address: 0x{address.ToInt64():X8}";


            switch (entry.Type)
            {
                case FreezeType.String6:
                {
                    byte[] data =
                        memory.ReadBytes(
                            address,
                            6);

                    int length = 0;

                    while (
                        length < data.Length &&
                        data[length] != 0)
                    {
                        length++;
                    }

                    entry.TextBox.Text =
                        System.Text.Encoding.ASCII.GetString(
                            data,
                            0,
                            length);

                    break;
                }


                case FreezeType.Byte:

                    entry.TextBox.Text =
                        memory.ReadByte(
                            address)
                        .ToString();

                    break;


                case FreezeType.Float:
                {
                    byte[] data =
                        memory.ReadBytes(
                            address,
                            4);

                    float value =
                        BitConverter.ToSingle(
                            data,
                            0);

                    entry.TextBox.Text =
                        value.ToString(
                            System.Globalization.CultureInfo.InvariantCulture);

                    break;
                }
            }
        }
        catch
        {
            entry.AddressLabel.Text =
                "Address: ERROR";
        }
    }

    private void ReadAllPlayerValues()
    {
        if (!IsAttached() ||
            memory == null)
        {
            return;
        }

        foreach (
            var entry
            in freezeEntries)
        {
            TryReadCurrentValue(
                entry);
        }
    }

    // =========================================================
    // 6. APPLY / TULIS NILAI
    // =========================================================

    private bool ApplyEntry(
        FreezeEntry entry)
    {
        if (!IsAttached() ||
            features == null)
        {
            MessageBox.Show(
                this,
                "Attach process terlebih dahulu.",
                "RF Tool",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return false;
        }

        try
        {
            string value =
                entry.TextBox.Text.Trim();


            switch (entry.Type)
            {
                case FreezeType.String6:

                    features.WritePlayerString(
                        entry.Offset,
                        value,
                        6);

                    break;


                case FreezeType.Byte:

                    if (!TryParseByte(
                        value,
                        out byte byteValue))
                    {
                        MessageBox.Show(
                            this,
                            $"Value untuk {entry.Name} harus 0-255.",
                            "Value tidak valid",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);

                        return false;
                    }


                    if (
                        entry.Name ==
                        "TAB Bell[01]Cora[23]Acc[4]" ||

                        entry.Name ==
                        "buta")
                    {
                        var result =
                            features.WritePlayerByteVerified(
                                entry.Offset,
                                byteValue);

                        entry.Address =
                            result.Address;

                        entry.AddressLabel.Text =
                            $"Address: 0x{result.Address.ToInt64():X8}";

                        statusLabel.Text =
                            $"Status: {entry.Name} → " +
                            $"0x{result.Address.ToInt64():X8} = " +
                            $"{result.Value}";

                        if (
                            result.Value !=
                            byteValue)
                        {
                            MessageBox.Show(
                                this,
                                $"{entry.Name} gagal read-back.\r\n\r\n" +
                                $"Address : 0x{result.Address.ToInt64():X8}\r\n" +
                                $"Write   : {byteValue}\r\n" +
                                $"ReadBack: {result.Value}",
                                "Write tidak sesuai",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);

                            return false;
                        }

                        return true;
                    }


                    features.WritePlayerByte(
                        entry.Offset,
                        byteValue);

                    break;


                case FreezeType.Float:

                    if (!float.TryParse(
                        value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float floatValue))
                    {
                        MessageBox.Show(
                            this,
                            $"Nilai FLOAT tidak valid: {value}",
                            "Value tidak valid",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);

                        return false;
                    }

                    features.WritePlayerFloat(
                        entry.Offset,
                        floatValue);

                    break;


                default:

                    return false;
            }


            UpdateEntryAddress(
                entry);


            statusLabel.Text =
                $"Status: Applied {entry.Name}";

            return true;
        }
        catch (Exception ex)
        {
            HandleFeatureError(
                entry.Name,
                ex);

            return false;
        }
    }

    private void ApplyEntrySilently(
        FreezeEntry entry)
    {
        if (features == null)
            return;

        string value =
            entry.TextBox.Text.Trim();

        switch (entry.Type)
        {
            case FreezeType.String6:

                features.WritePlayerString(
                    entry.Offset,
                    value,
                    6);

                break;


            case FreezeType.Byte:

                if (
                    TryParseByte(
                        value,
                        out byte byteValue))
                {
                    features.WritePlayerByte(
                        entry.Offset,
                        byteValue);
                }

                break;


            case FreezeType.Float:

                if (
                    float.TryParse(
                        value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float floatValue))
                {
                    features.WritePlayerFloat(
                        entry.Offset,
                        floatValue);
                }

                break;
        }
    }

    // =========================================================
    // 7. FREEZE TIMER
    // =========================================================

    private void FreezeTimer_Tick(
        object? sender,
        EventArgs e)
    {
        if (!IsAttached() ||
            features == null)
        {
            freezeTimer.Stop();

            return;
        }

        bool anyActive = false;

        foreach (
            var entry
            in freezeEntries)
        {
            if (!entry.Freeze.Checked)
                continue;

            anyActive = true;

            try
            {
                ApplyEntrySilently(
                    entry);
            }
            catch
            {
            }
        }

        if (!anyActive)
        {
            freezeTimer.Stop();
        }
    }

    private void EnsureFreezeTimer()
    {
        if (!freezeTimer.Enabled)
        {
            freezeTimer.Start();
        }
    }

    private void StopFreezeTimerIfNeeded()
    {
        bool active =
            freezeEntries.Any(
                x => x.Freeze.Checked);

        if (!active)
        {
            freezeTimer.Stop();
        }
    }
}
