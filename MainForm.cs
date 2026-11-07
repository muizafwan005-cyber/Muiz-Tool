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


public sealed partial class MainForm : Form
{
    // =========================================================
    // MAINFORM (UTAMA) - DAFTAR ISI
    //
    // Urutan bagian di file ini:
    //   1. FIELD & KONSTANTA UTAMA
    //   2. TEMA WARNA
    //   3. CONSTRUCTOR
    //   4. BUILD INTERFACE
    //   5. PROCESS, ATTACH & DETACH
    //   6. HELPER UMUM
    //
    // Cari "// 1." sampai "// 6." untuk loncat ke bagiannya.
    // =========================================================


    // =========================================================
    // 1. FIELD & KONSTANTA UTAMA
    // =========================================================

    private const int FormWidth = 330;
    private const int FormHeight = 600;
    private const int FreezeIntervalMs = 30;
    private MemoryAccess? memory;
    private FeatureManager? features;
    private Process? selectedProcess;
    private ComboBox processCombo = null!;
    private Button refreshButton = null!;
    private Button attachButton = null!;
    private Label statusLabel = null!;
    private TextBox basePointerTextBox = null!;
    private Button basePointerApplyButton = null!;
    private Button basePointerLockButton = null!;
    private TabControl tabs = null!;

    // =========================================================
    // 2. TEMA WARNA
    // =========================================================

    private static readonly Color BackColorMain =
        Color.FromArgb(25, 25, 28);

    private static readonly Color BackColorPanel =
        Color.FromArgb(32, 32, 36);

    private static readonly Color BackColorInput =
        Color.FromArgb(42, 42, 47);

    private static readonly Color ForeColorMain =
        Color.FromArgb(235, 235, 235);

    private static readonly Color ForeColorDim =
        Color.FromArgb(170, 170, 175);

    // =========================================================
    // 3. CONSTRUCTOR
    // =========================================================

    public MainForm()
    {
        // =====================================================
        // BRANDING
        // =====================================================
    
        Text = "Muiz Tool";
    
        // Gunakan icon yang sudah tertanam di EXE
        Icon? appIcon =
            Icon.ExtractAssociatedIcon(
                Application.ExecutablePath);
    
        if (appIcon != null)
            Icon = appIcon;
    
        ShowIcon = true;
    
        StartPosition =
            FormStartPosition.CenterScreen;
    
        ClientSize = new Size(850, 650);
        
        MinimumSize = new Size(650, 500);
        
        MaximumSize = Size.Empty;
        
        FormBorderStyle = FormBorderStyle.Sizable;
        
        MaximizeBox = true;
    
        BackColor =
            BackColorMain;
    
        ForeColor =
            ForeColorMain;
    
        Font =
            new Font(
                "Segoe UI",
                9F,
                FontStyle.Regular);
    
        BuildInterface();
    
        freezeTimer =
            new System.Windows.Forms.Timer
            {
                Interval =
                    FreezeIntervalMs
            };
    
        freezeTimer.Tick +=
            FreezeTimer_Tick;
    
        LoadHotkeys();
    
        LoadProcesses();
    
        FormClosing +=
            MainForm_FormClosing;
    }

    // =========================================================
    // 4. BUILD INTERFACE
    // Panel atas (process, attach, base pointer) dan pembuatan 4 tab.
    // =========================================================

    private void BuildInterface()
    {
        SuspendLayout();

        var topPanel = new Panel
        {
            Dock =
                DockStyle.Top,

            Height =
                124,

            BackColor =
                BackColorPanel
        };

        var processLabel = new Label
        {
            Text =
                "PROCESS",

            AutoSize =
                true,

            Location =
                new Point(
                    10,
                    9),

            ForeColor =
                ForeColorDim
        };

        topPanel.Controls.Add(
            processLabel);

        processCombo = new ComboBox
        {
            Location =
                new Point(
                    10,
                    28),

            Size =
                new Size(
                    185,
                    27),

            DropDownStyle =
                ComboBoxStyle.DropDownList,

            BackColor =
                BackColorInput,

            ForeColor =
                ForeColorMain,

            FlatStyle =
                FlatStyle.Flat
        };

        topPanel.Controls.Add(
            processCombo);

        refreshButton = new Button
        {
            Text =
                "REFRESH",

            Location =
                new Point(
                    200,
                    28),

            Size =
                new Size(
                    55,
                    27),

            FlatStyle =
                FlatStyle.Flat,

            BackColor =
                BackColorInput,

            ForeColor =
                ForeColorMain
        };

        refreshButton.FlatAppearance.BorderSize =
            0;

        refreshButton.Click +=
            (_, _) =>
            {
                LoadProcesses();
            };

        topPanel.Controls.Add(
            refreshButton);

        attachButton = new Button
        {
            Text =
                "ATTACH",

            Location =
                new Point(
                    260,
                    28),

            Size =
                new Size(
                    60,
                    27),

            FlatStyle =
                FlatStyle.Flat,

            BackColor =
                BackColorInput,

            ForeColor =
                ForeColorMain
        };

        attachButton.FlatAppearance.BorderSize =
            0;

        attachButton.Click +=
            (_, _) =>
            {
                AttachSelectedProcess();
            };

        topPanel.Controls.Add(
            attachButton);
        
        statusLabel = new Label
        {
            Text = "Status: Not attached",
            AutoSize = false,
            Location = new Point(10, 104),
            Size = new Size(310, 18),
            ForeColor = ForeColorDim
        };
        
        topPanel.Controls.Add(statusLabel);

        var basePointerLabel = new Label
        {
            Text = "BASE POINTER",
            AutoSize = true,
            Location = new Point(10, 57),
            ForeColor = ForeColorDim
        };
        
        topPanel.Controls.Add(basePointerLabel);
        
        basePointerTextBox = new TextBox
        {
            Text = "RF_Online.bin+C28B40",
            Location = new Point(10, 74),
            Size = new Size(185, 25),
            BackColor = BackColorInput,
            ForeColor = ForeColorMain
        };
        
        topPanel.Controls.Add(basePointerTextBox);
        
        basePointerApplyButton = new Button
        {
            Text = "OK",
            Location = new Point(200, 73),
            Size = new Size(55, 27),
            FlatStyle = FlatStyle.Flat,
            BackColor = BackColorInput,
            ForeColor = ForeColorMain
        };
        
        basePointerApplyButton.FlatAppearance.BorderSize = 0;
        basePointerApplyButton.Click += (_, _) => ApplyBasePointer();
        
        topPanel.Controls.Add(basePointerApplyButton);

        // Tombol kunci Base Pointer (butuh password untuk membuka).
        basePointerLockButton = new Button
        {
            Text = "UNLOCK",
            Location = new Point(260, 73),
            Size = new Size(60, 27),
            FlatStyle = FlatStyle.Flat,
            BackColor = BackColorInput,
            ForeColor = ForeColorMain
        };

        basePointerLockButton.FlatAppearance.BorderSize = 0;
        basePointerLockButton.Click += BasePointerLockButton_Click;

        topPanel.Controls.Add(basePointerLockButton);

        // Saat aplikasi dibuka, Base Pointer selalu terkunci.
        SetBasePointerLocked(true);


        // =====================================================
        // TABS
        // =====================================================

        tabs = new TabControl
        {
            Dock =
                DockStyle.Fill,

            Appearance =
                TabAppearance.Normal,

            DrawMode =
                TabDrawMode.Normal
        };


        // =====================================================
        // TAB 1
        // =====================================================

        var tab1 = new TabPage
        {
            Text =
                "Edit + Freeze",

            BackColor =
                BackColorMain,

            ForeColor =
                ForeColorMain
        };

        tabs.TabPages.Add(
            tab1);

        BuildTab1(
            tab1);


        // =====================================================
        // TAB 2
        // =====================================================

        var tab2 = new TabPage
        {
            Text =
                "ON / OFF",

            BackColor =
                BackColorMain,

            ForeColor =
                ForeColorMain
        };

        tabs.TabPages.Add(
            tab2);

        BuildTab2(
            tab2);


        // =====================================================
        // TAB 3
        // =====================================================

        var tab3 = new TabPage
        {
            Text =
                "HOTKEY",

            BackColor =
                BackColorMain,

            ForeColor =
                ForeColorMain
        };

        tabs.TabPages.Add(
            tab3);

        BuildTab3(
            tab3);


        // =====================================================
        // TAB 4
        // =====================================================

        var tab4 = new TabPage
        {
            Text =
                "ZOOM / FOLLOW",

            BackColor =
                BackColorMain,

            ForeColor =
                ForeColorMain
        };

        tabs.TabPages.Add(
            tab4);

        BuildTab4(
            tab4);
            
        // =====================================================
        // TAB 5
        // =====================================================
        
        var tab5 = new TabPage
        {
            Text = "ADVANCED",
        
            BackColor = BackColorMain,
        
            ForeColor = ForeColorMain
        };
        
        tabs.TabPages.Add(tab5);
        
        BuildTab5(tab5);
        
                Controls.Add(
                    tabs);
        
                Controls.Add(
                    topPanel);
        
                ResumeLayout(
                    false);
            }

    // =========================================================
    // 5. PROCESS, ATTACH & DETACH
    // =========================================================

    private sealed class ProcessItem
    {
        public int Id { get; }

        public string Name { get; }

        public string Title { get; }


        public ProcessItem(
            int id,
            string name,
            string title)
        {
            Id =
                id;

            Name =
                name;

            Title =
                title;
        }


        public override string ToString()
        {
            return
                $"{Name} ({Id})";
        }
    }

    private void LoadProcesses()
    {
        processCombo.Items.Clear();

        var processes =
            Process.GetProcesses()
                .Where(
                    p =>
                    {
                        try
                        {
                            return
                                !string.IsNullOrWhiteSpace(
                                    p.MainWindowTitle);
                        }
                        catch
                        {
                            return false;
                        }
                    })
                .OrderBy(
                    p => p.ProcessName)
                .ToList();

        foreach (
            var process
            in processes)
        {
            try
            {
                processCombo.Items.Add(
                    new ProcessItem(
                        process.Id,
                        process.ProcessName,
                        process.MainWindowTitle));
            }
            finally
            {
                process.Dispose();
            }
        }

        int rfIndex = -1;

        for (
            int i = 0;
            i < processCombo.Items.Count;
            i++)
        {
            if (
                processCombo.Items[i]
                is ProcessItem item &&
                item.Name.Equals(
                    "RF_Online",
                    StringComparison.OrdinalIgnoreCase))
            {
                rfIndex = i;
                break;
            }
        }

        if (rfIndex >= 0)
        {
            processCombo.SelectedIndex =
                rfIndex;
        }
        else if (
            processCombo.Items.Count > 0)
        {
            processCombo.SelectedIndex =
                0;
        }

        statusLabel.Text =
            $"Status: {processCombo.Items.Count} process";
    }

    private void AttachSelectedProcess()
    {
        if (
            processCombo.SelectedItem
            is not ProcessItem item)
        {
            MessageBox.Show(
                this,
                "Pilih process terlebih dahulu.",
                "RF Tool",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return;
        }

        try
        {
            DetachCurrent();

            selectedProcess =
                Process.GetProcessById(
                    item.Id);

            memory =
                new MemoryAccess(
                    selectedProcess);

            features =
                new FeatureManager(
                    memory);

            SetTab2Checks(
                false);


            ReadAllPlayerValues();

            UpdateAllAddresses();


            statusLabel.Text =
                $"Status: Attached → {item.Name}";

            attachButton.Text =
                "ATTACHED";
        }
        catch (Exception ex)
        {
            DetachCurrent();

            MessageBox.Show(
                this,
                $"Gagal attach:\r\n\r\n{ex.Message}",
                "RF Tool",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void DetachCurrent()
    {
        freezeTimer.Stop();

        try
        {
            features?.RestoreAll();
        }
        catch
        {
        }

        features = null;

        SetTab2Checks(
            false);

        if (zoomInitializeCheck != null)
        {
            ResetCheckBox(zoomInitializeCheck, false);
            SetTab4FeatureEnabled(false);
        }

        if (autoFollowInitializeCheck != null)
        {
            ResetCheckBox(autoFollowInitializeCheck, false);
            ResetCheckBox(autoFollowEnabledCheck, false);
            autoFollowNickTextBox.Text = "";
            autoFollowNickTextBox.Enabled = false;
            autoFollowNickButton.Enabled = false;
            autoFollowEnabledCheck.Enabled = false;
        }

        // Reset address NPC
        ResetNpcAddressLabels();

        foreach (
            var entry
            in freezeEntries)
        {
            ResetCheckBox(
                entry.Freeze,
                false);

            entry.Address =
                IntPtr.Zero;

            entry.AddressLabel.Text =
                "Address: -";
        }

        try
        {
            memory?.Dispose();
        }
        catch
        {
        }

        memory = null;

        try
        {
            selectedProcess?.Dispose();
        }
        catch
        {
        }

        selectedProcess = null;

        attachButton.Text =
            "ATTACH";

        statusLabel.Text =
            "Status: Not attached";
    }

    private void MainForm_FormClosing(
        object? sender,
        FormClosingEventArgs e)
    {
        UnregisterAllHotkeys();

        SaveHotkeys();

        DetachCurrent();
    }

    private bool IsAttached()
    {
        return
            memory != null &&
            features != null &&
            selectedProcess != null;
    }

    // =========================================================
    // 6. HELPER UMUM
    // Dipakai oleh semua tab.
    // =========================================================

    private static CheckBox CreateFeatureCheckBox(
        Control parent,
        string text,
        int y)
    {
        var check = new CheckBox
        {
            Text =
                text,

            AutoSize =
                false,

            Location =
                new Point(
                    8,
                    y),

            Size =
                new Size(
                    285,
                    35),

            ForeColor =
                ForeColorMain,

            BackColor =
                BackColorMain,

            FlatStyle =
                FlatStyle.Standard
        };

        parent.Controls.Add(
            check);

        return check;
    }

    private static CheckBox CreateIndentedCheckBox(
        Control parent,
        string text,
        int y)
    {
        var check = new CheckBox
        {
            Text = text,
            AutoSize = false,
            Location = new Point(28, y),
            Size = new Size(255, 28),
            ForeColor = ForeColorMain,
            BackColor = BackColorMain,
            FlatStyle = FlatStyle.Standard,
            Enabled = false
        };

        parent.Controls.Add(check);
        return check;
    }

    private static void ResetCheckBox(
        CheckBox checkBox,
        bool value)
    {
        if (checkBox.Checked == value)
            return;

        checkBox.Checked =
            value;
    }

    private void HandleFeatureError(
        string featureName,
        Exception ex)
    {
        statusLabel.Text =
            $"Error: {featureName}";

        MessageBox.Show(
            this,
            $"{featureName} gagal.\r\n\r\n{ex.Message}",
            "RF Tool",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private static bool TryParseByte(
        string text,
        out byte value)
    {
        text =
            text.Trim();

        if (
            text.StartsWith(
                "0x",
                StringComparison.OrdinalIgnoreCase))
        {
            return byte.TryParse(
                text[2..],
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
        }

        return byte.TryParse(
            text,
            out value);
    }
}
