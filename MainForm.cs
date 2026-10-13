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

public sealed class MainForm : Form
{
    private const int FormWidth = 330;
    private const int FormHeight = 600;

    private const int FreezeIntervalMs = 50;

    private MemoryAccess? memory;
    private FeatureManager? features;
    private Process? selectedProcess;

    private ComboBox processCombo = null!;
    private Button refreshButton = null!;
    private Button attachButton = null!;
    private Label statusLabel = null!;

    private TabControl tabs = null!;

    private Panel tab1Scroll = null!;
    private Panel tab1Content = null!;

    private CheckBox autoNpcCheck = null!;
    private RadioButton npcBelsRadio = null!;
    private RadioButton npcCoraRadio = null!;
    private RadioButton npcAccRadio = null!;

    // =========================================================
    // NPC ADDRESS LABEL
    // =========================================================

    private Label npcBelsAddressLabel = null!;
    private Label npcCoraAddressLabel = null!;
    private Label npcAccAddressLabel = null!;

    private CheckBox stage2Check = null!;
    private CheckBox stage5RomanticCheck = null!;
    private CheckBox stage3TrueHeartCheck = null!;
    private CheckBox infernalDragonboneCheck = null!;
    private CheckBox combineCheck = null!;
    private CheckBox combineInitializeCheck = null!;
    
    // =========================================================
    // TAB 4 - ZOOM / AUTO FOLLOW
    // =========================================================

    private CheckBox zoomInitializeCheck = null!;
    private CheckBox zoomCheck = null!;
    private CheckBox autoPickV1Check = null!;
    private CheckBox autoPickV2Check = null!;
    private CheckBox showNameCheck = null!;
    private CheckBox fastLootCheck = null!;
    private CheckBox mauKeyboardCheck = null!;
    private CheckBox reviveInactiveCheck = null!;
    private CheckBox reviveDisableCheck = null!;
    private CheckBox reviveAutoCheck = null!;
    private CheckBox lockSpotCheck = null!;
    private TextBox distanceTextBox = null!;
    private Button distanceApplyButton = null!;

    private CheckBox autoFollowInitializeCheck = null!;
    private TextBox autoFollowNickTextBox = null!;
    private Button autoFollowNickButton = null!;
    private CheckBox autoFollowEnabledCheck = null!;
    private Panel zoomFeaturePanel = null!;
    private Panel autoFollowFeaturePanel = null!;
    // =========================================================
    // TAB 4 - ESP
    // =========================================================
    
    private CheckBox espInitializeCheck = null!;
    private CheckBox espLineCheck = null!;
    private CheckBox espNameCheck = null!;
    private CheckBox espDistanceCheck = null!;
    private CheckBox espAllyCheck = null!;
    private CheckBox espEnemyCheck = null!;
    private CheckBox espMonsterCheck = null!;
    private CheckBox espLockBotCheck = null!;
    private CheckBox espLockSpotLootingCheck = null!;
    private Panel espFeaturePanel = null!;
    private System.Windows.Forms.Timer freezeTimer = null!;

    private readonly List<FreezeEntry> freezeEntries = new();

    // =========================================================
    // HOTKEY
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
    // CONSTRUCTOR
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
    
        ClientSize =
            new Size(
                FormWidth,
                FormHeight);
    
        MinimumSize =
            new Size(
                FormWidth,
                FormHeight);
    
        MaximumSize =
            new Size(
                FormWidth,
                FormHeight);
    
        FormBorderStyle =
            FormBorderStyle.FixedSingle;
    
        MaximizeBox = false;
    
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
    // WINDOW HANDLE
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


    // =========================================================
    // WINDOWS HOTKEY
    // =========================================================

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
                action switch
                {
                    HotkeyAction.Npc7 => "7",
                    HotkeyAction.Npc0 => "0",
                    HotkeyAction.NpcMinus => "-",
                    HotkeyAction.NpcEquals => "=",
                    HotkeyAction.NpcF12 => "F12",
                    HotkeyAction.Npc8 => "8",
                    HotkeyAction.NpcF11 => "F11",
                    _ => "?"
                };

            string targetName =
                target switch
                {
                    NpcTarget.Bels => "BELS",
                    NpcTarget.Cora => "CORA",
                    NpcTarget.Acc => "ACC",
                    _ => "ACC"
                };

            statusLabel.Text =
                $"Status: NPC {commandName} → {targetName} updated";
        }
        catch (Exception ex)
        {
            HandleFeatureError(
                "NPC " + HotkeyDisplayName(action),
                ex);
        }
    }


    // =========================================================
    // HOTKEY ENUM
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


    // =========================================================
    // HOTKEY DEFINITION
    // =========================================================

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


    // =========================================================
    // HOTKEY TEXT BOX
    // =========================================================

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


    // =========================================================
    // BUILD INTERFACE
    // =========================================================

    private void BuildInterface()
    {
        SuspendLayout();

        var topPanel = new Panel
        {
            Dock =
                DockStyle.Top,

            Height =
                78,

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
            Text =
                "Status: Not attached",

            AutoSize =
                false,

            Location =
                new Point(
                    10,
                    57),

            Size =
                new Size(
                    310,
                    18),

            ForeColor =
                ForeColorDim
        };

        topPanel.Controls.Add(
            statusLabel);


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


        Controls.Add(
            tabs);

        Controls.Add(
            topPanel);

        ResumeLayout(
            false);
    }


    // =========================================================
    // NPC TARGET CONTROLS
    // =========================================================

    private void SetNpcTargetControlsEnabled(
        bool enabled)
    {
        if (npcBelsRadio != null)
        {
            npcBelsRadio.Enabled =
                enabled;
        }

        if (npcCoraRadio != null)
        {
            npcCoraRadio.Enabled =
                enabled;
        }

        if (npcAccRadio != null)
        {
            npcAccRadio.Enabled =
                enabled;
        }
    }


    private NpcTarget GetSelectedNpcTarget()
    {
        if (
            npcBelsRadio != null &&
            npcBelsRadio.Checked)
        {
            return NpcTarget.Bels;
        }

        if (
            npcCoraRadio != null &&
            npcCoraRadio.Checked)
        {
            return NpcTarget.Cora;
        }

        return NpcTarget.Acc;
    }


    // =========================================================
    // NPC ADDRESS DISPLAY
    // =========================================================

    private void ResetNpcAddressLabels()
    {
        if (npcBelsAddressLabel != null)
        {
            npcBelsAddressLabel.Text =
                "Address: -";
        }

        if (npcCoraAddressLabel != null)
        {
            npcCoraAddressLabel.Text =
                "Address: -";
        }

        if (npcAccAddressLabel != null)
        {
            npcAccAddressLabel.Text =
                "Address: -";
        }
    }


    private void UpdateNpcAddressLabels(
        IntPtr bels,
        IntPtr cora,
        IntPtr acc)
    {
        if (npcBelsAddressLabel != null)
        {
            npcBelsAddressLabel.Text =
                $"Address: 0x{bels.ToInt64():X8}";
        }

        if (npcCoraAddressLabel != null)
        {
            npcCoraAddressLabel.Text =
                $"Address: 0x{cora.ToInt64():X8}";
        }

        if (npcAccAddressLabel != null)
        {
            npcAccAddressLabel.Text =
                $"Address: 0x{acc.ToInt64():X8}";
        }
    }


    // =========================================================
    // TAB 1
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


    // =========================================================
    // TAB 2
    // =========================================================

    private void BuildTab2(
        TabPage tab)
    {
        var panel = new Panel
        {
            Dock =
                DockStyle.Fill,

            BackColor =
                BackColorMain,

            Padding =
                new Padding(12)
        };

        tab.Controls.Add(
            panel);

        int y = 15;

        // =====================================================
        // AUTO NPC
        // =====================================================

        autoNpcCheck =
            CreateFeatureCheckBox(
                panel,
                "Auto NPC",
                y);


        // -----------------------------------------------------
        // NPC TARGET
        // -----------------------------------------------------

        npcBelsRadio =
            new RadioButton
            {
                AutoSize = true,
                Text = "NPC BELS",
                Location =
                    new Point(
                        30,
                        y + 32),
                ForeColor =
                    Color.White,
                BackColor =
                    Color.Transparent,
                Enabled = false
            };

        npcBelsAddressLabel =
            new Label
            {
                AutoSize = false,
                Text = "Address: -",
                Location =
                    new Point(
                        48,
                        y + 53),
                Size =
                    new Size(
                        245,
                        18),
                ForeColor =
                    ForeColorDim,
                BackColor =
                    Color.Transparent,
                Font =
                    new Font(
                        "Segoe UI",
                        8F,
                        FontStyle.Regular)
            };


        npcCoraRadio =
            new RadioButton
            {
                AutoSize = true,
                Text = "NPC CORA",
                Location =
                    new Point(
                        30,
                        y + 80),
                ForeColor =
                    Color.White,
                BackColor =
                    Color.Transparent,
                Enabled = false
            };

        npcCoraAddressLabel =
            new Label
            {
                AutoSize = false,
                Text = "Address: -",
                Location =
                    new Point(
                        48,
                        y + 101),
                Size =
                    new Size(
                        245,
                        18),
                ForeColor =
                    ForeColorDim,
                BackColor =
                    Color.Transparent,
                Font =
                    new Font(
                        "Segoe UI",
                        8F,
                        FontStyle.Regular)
            };


        npcAccRadio =
            new RadioButton
            {
                AutoSize = true,
                Text = "NPC ACC",
                Location =
                    new Point(
                        30,
                        y + 128),
                ForeColor =
                    Color.White,
                BackColor =
                    Color.Transparent,
                Checked = true,
                Enabled = false
            };

        npcAccAddressLabel =
            new Label
            {
                AutoSize = false,
                Text = "Address: -",
                Location =
                    new Point(
                        48,
                        y + 149),
                Size =
                    new Size(
                        245,
                        18),
                ForeColor =
                    ForeColorDim,
                BackColor =
                    Color.Transparent,
                Font =
                    new Font(
                        "Segoe UI",
                        8F,
                        FontStyle.Regular)
            };


        panel.Controls.Add(
            npcBelsRadio);

        panel.Controls.Add(
            npcBelsAddressLabel);

        panel.Controls.Add(
            npcCoraRadio);

        panel.Controls.Add(
            npcCoraAddressLabel);

        panel.Controls.Add(
            npcAccRadio);

        panel.Controls.Add(
            npcAccAddressLabel);


        // =====================================================
        // STAGE 2
        // =====================================================
        
        y += 190;
        
        stage2Check =
            CreateFeatureCheckBox(
                panel,
                "Stage 2 True Heart Flame Launcher",
                y);
        
        
        // =====================================================
        // STAGE 5
        // =====================================================
        
        y += 48;
        
        stage5RomanticCheck =
            CreateFeatureCheckBox(
                panel,
                "Stage 5 Romantic Flame Launcher [30 Days]",
                y);
        
        
        // =====================================================
        // STAGE 3
        // =====================================================
        
        y += 48;
        
        stage3TrueHeartCheck =
            CreateFeatureCheckBox(
                panel,
                "Stage 3 True Heart Flame Launcher",
                y);
        
        
        // =====================================================
        // INFERNAL DRAGONBONE
        // =====================================================
        
        y += 48;
        
        infernalDragonboneCheck =
            CreateFeatureCheckBox(
                panel,
                "Infernal Dragonbone Flame Launcher",
                y);
        
        
        // =====================================================
        // COMBINE
        // =====================================================
        

        // =====================================================
        // COMBINE
        // =====================================================

        y += 48;

        combineInitializeCheck =
            CreateFeatureCheckBox(
                panel,
                "COMBINE AOB - INITIALIZE",
                y);

        y += 34;

        combineCheck =
            CreateFeatureCheckBox(
                panel,
                "Combine Jarak Jauh",
                y);

        combineCheck.Visible = false;
        combineCheck.Enabled = false;

        y += 34;


        // =====================================================
        // EVENTS
        // =====================================================

        autoNpcCheck.CheckedChanged +=
            AutoNpcCheck_CheckedChanged;

        stage2Check.CheckedChanged +=
            Stage2Check_CheckedChanged;

        stage5RomanticCheck.CheckedChanged +=
            Stage5RomanticCheck_CheckedChanged;

        stage3TrueHeartCheck.CheckedChanged +=
            Stage3TrueHeartCheck_CheckedChanged;

        infernalDragonboneCheck.CheckedChanged +=
            InfernalDragonboneCheck_CheckedChanged;

        combineInitializeCheck.CheckedChanged +=
            CombineInitializeCheck_CheckedChanged;

        combineCheck.CheckedChanged +=
            CombineCheck_CheckedChanged;
    }

    // =========================================================
    // TAB 4 - ZOOM / AUTO FOLLOW
    // =========================================================
    
    private void BuildTab4(
        TabPage tab)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BackColorMain,
            Padding = new Padding(10),
            AutoScroll = true
        };
    
        tab.Controls.Add(panel);
    
        int y = 8;
    
        // =========================================================
        // ZOOM INITIALIZE
        // =========================================================
    
        zoomInitializeCheck =
            CreateFeatureCheckBox(
                panel,
                "ZOOM AOB - INITIALIZE",
                y);
    
        y += 42;
    
        // =========================================================
        // ZOOM FEATURE PANEL
        // =========================================================
    
        zoomFeaturePanel = new Panel
        {
            Location = new Point(0, y),
            Size = new Size(300, 0),
            BackColor = BackColorMain,
            Visible = false
        };
    
        panel.Controls.Add(
            zoomFeaturePanel);
    
        int zoomY = 0;
    
        zoomCheck =
            CreateIndentedCheckBox(
                zoomFeaturePanel,
                "Zoom",
                zoomY);
    
        zoomY += 34;
    
        autoPickV1Check =
            CreateIndentedCheckBox(
                zoomFeaturePanel,
                "Auto Pick V1",
                zoomY);
    
        zoomY += 34;
    
        autoPickV2Check =
            CreateIndentedCheckBox(
                zoomFeaturePanel,
                "Auto Pick V2",
                zoomY);
    
        zoomY += 34;
    
        showNameCheck =
            CreateIndentedCheckBox(
                zoomFeaturePanel,
                "Show Name",
                zoomY);
    
        zoomY += 34;
    
        fastLootCheck =
            CreateIndentedCheckBox(
                zoomFeaturePanel,
                "Fast Loot",
                zoomY);
    
        zoomY += 34;
    
        mauKeyboardCheck =
            CreateIndentedCheckBox(
                zoomFeaturePanel,
                "MAU Keyboard Mode",
                zoomY);
    
        zoomY += 36;
    
        // =========================================================
        // REVIVE
        // =========================================================
    
        var reviveLabel = new Label
        {
            Text = "Revive",
            AutoSize = false,
            Location = new Point(28, zoomY),
            Size = new Size(260, 20),
            ForeColor = ForeColorDim,
            BackColor = BackColorMain
        };
    
        zoomFeaturePanel.Controls.Add(
            reviveLabel);
    
        zoomY += 24;
    
        reviveInactiveCheck =
            CreateIndentedCheckBox(
                zoomFeaturePanel,
                "Inactive",
                zoomY);
    
        zoomY += 30;
    
        reviveDisableCheck =
            CreateIndentedCheckBox(
                zoomFeaturePanel,
                "Disable",
                zoomY);
    
        zoomY += 30;
    
        reviveAutoCheck =
            CreateIndentedCheckBox(
                zoomFeaturePanel,
                "Auto",
                zoomY);
    
        zoomY += 36;
    
        // =========================================================
        // DISTANCE + LOCK SPOT
        // =========================================================
    
        var distanceLabel = new Label
        {
            Text = "Distance + Lock Spot",
            AutoSize = false,
            Location = new Point(28, zoomY),
            Size = new Size(260, 20),
            ForeColor = ForeColorDim,
            BackColor = BackColorMain
        };
    
        zoomFeaturePanel.Controls.Add(
            distanceLabel);
    
        zoomY += 24;
    
        var distanceName = new Label
        {
            Text = "Distance",
            AutoSize = false,
            Location = new Point(28, zoomY + 3),
            Size = new Size(75, 24),
            ForeColor = ForeColorMain,
            BackColor = BackColorMain
        };
    
        zoomFeaturePanel.Controls.Add(
            distanceName);
    
        distanceTextBox = new TextBox
        {
            Location = new Point(105, zoomY),
            Size = new Size(105, 25),
            Text = "4.0",
            BackColor = BackColorInput,
            ForeColor = ForeColorMain,
            BorderStyle = BorderStyle.FixedSingle
        };
    
        zoomFeaturePanel.Controls.Add(
            distanceTextBox);
    
        distanceApplyButton = new Button
        {
            Text = "APPLY",
            Location = new Point(215, zoomY),
            Size = new Size(65, 25),
            FlatStyle = FlatStyle.Flat,
            BackColor = BackColorInput,
            ForeColor = ForeColorMain
        };
    
        distanceApplyButton.FlatAppearance.BorderSize = 0;
    
        zoomFeaturePanel.Controls.Add(
            distanceApplyButton);
    
        zoomY += 34;
    
        lockSpotCheck =
            CreateIndentedCheckBox(
                zoomFeaturePanel,
                "Lock Spot = 01",
                zoomY);
    
        // =========================================================
        // AUTO FOLLOW INITIALIZE
        // =========================================================
    
        // Simpan tinggi isi Zoom.
        int zoomContentHeight =
            zoomY + 34;
    
        y += zoomContentHeight;
    
        autoFollowInitializeCheck =
            CreateFeatureCheckBox(
                panel,
                "AUTO FOLLOW AOB - INITIALIZE",
                y);
    
        // =========================================================
        // AUTO FOLLOW FEATURE PANEL
        // =========================================================
    
        autoFollowFeaturePanel = new Panel
        {
            Location = new Point(
                0,
                autoFollowInitializeCheck.Bottom + 6),
    
            Size = new Size(
                300,
                68),
    
            BackColor = BackColorMain,
    
            Visible = false
        };
    
        panel.Controls.Add(
            autoFollowFeaturePanel);
    
        int autoFollowY = 0;
    
        var nickLabel = new Label
        {
            Text = "Nick Follow",
            AutoSize = false,
            Location = new Point(
                28,
                autoFollowY + 3),
    
            Size = new Size(
                75,
                24),
    
            ForeColor = ForeColorMain,
            BackColor = BackColorMain
        };
    
        autoFollowFeaturePanel.Controls.Add(
            nickLabel);
    
        autoFollowNickTextBox = new TextBox
        {
            Location = new Point(
                105,
                autoFollowY),
    
            Size = new Size(
                105,
                25),
    
            BackColor = BackColorInput,
            ForeColor = ForeColorMain,
            BorderStyle = BorderStyle.FixedSingle
        };
    
        autoFollowFeaturePanel.Controls.Add(
            autoFollowNickTextBox);
    
        autoFollowNickButton = new Button
        {
            Text = "SET",
    
            Location = new Point(
                215,
                autoFollowY),
    
            Size = new Size(
                65,
                25),
    
            FlatStyle = FlatStyle.Flat,
            BackColor = BackColorInput,
            ForeColor = ForeColorMain
        };
    
        autoFollowNickButton.FlatAppearance.BorderSize = 0;
    
        autoFollowFeaturePanel.Controls.Add(
            autoFollowNickButton);
    
        autoFollowY += 34;
    
        autoFollowEnabledCheck =
            CreateIndentedCheckBox(
                autoFollowFeaturePanel,
                "Auto Follow",
                autoFollowY);
        // =========================================================
        // ESP INITIALIZE
        // =========================================================
        
        espInitializeCheck =
            CreateFeatureCheckBox(
                panel,
                "ESP AOB - INITIALIZE",
                autoFollowInitializeCheck.Bottom + 6);
        
        // =========================================================
        // ESP FEATURE PANEL
        // =========================================================
        
        espFeaturePanel = new Panel
        {
            Location = new Point(
                0,
                espInitializeCheck.Bottom + 6),
            Size = new Size(300, 270),
            BackColor = BackColorMain,
            Visible = false
        };
        
        panel.Controls.Add(espFeaturePanel);
        
        int espY = 0;
        
        espLineCheck = CreateIndentedCheckBox(
            espFeaturePanel, "Line", espY);
        espY += 30;
        
        espNameCheck = CreateIndentedCheckBox(
            espFeaturePanel, "Name", espY);
        espY += 30;
        
        espDistanceCheck = CreateIndentedCheckBox(
            espFeaturePanel, "Distance", espY);
        espY += 30;
        
        espAllyCheck = CreateIndentedCheckBox(
            espFeaturePanel, "Ally", espY);
        espY += 30;
        
        espEnemyCheck = CreateIndentedCheckBox(
            espFeaturePanel, "Enemy", espY);
        espY += 30;
        
        espMonsterCheck = CreateIndentedCheckBox(
            espFeaturePanel, "Monster", espY);
        espY += 30;
        
        espLockBotCheck = CreateIndentedCheckBox(
            espFeaturePanel, "Lock Bot", espY);
        espY += 30;
        
        espLockSpotLootingCheck = CreateIndentedCheckBox(
            espFeaturePanel, "Lock Spot Looting", espY);
        
        // Kondisi awal
        SetEspFeatureEnabled(false);
        // =========================================================
        // DEFAULT STATE
        // =========================================================
    
        autoFollowNickTextBox.Enabled = false;
        autoFollowNickButton.Enabled = false;
        autoFollowEnabledCheck.Enabled = false;
    
        // =========================================================
        // EVENTS
        // =========================================================
    
        zoomInitializeCheck.CheckedChanged +=
            ZoomInitializeCheck_CheckedChanged;
    
        zoomCheck.CheckedChanged +=
            ZoomCheck_CheckedChanged;
    
        autoPickV1Check.CheckedChanged +=
            AutoPickV1Check_CheckedChanged;
    
        autoPickV2Check.CheckedChanged +=
            AutoPickV2Check_CheckedChanged;
    
        showNameCheck.CheckedChanged +=
            ShowNameCheck_CheckedChanged;
    
        fastLootCheck.CheckedChanged +=
            FastLootCheck_CheckedChanged;
    
        mauKeyboardCheck.CheckedChanged +=
            MauKeyboardCheck_CheckedChanged;
    
        reviveInactiveCheck.CheckedChanged +=
            ReviveInactiveCheck_CheckedChanged;
    
        reviveDisableCheck.CheckedChanged +=
            ReviveDisableCheck_CheckedChanged;
    
        reviveAutoCheck.CheckedChanged +=
            ReviveAutoCheck_CheckedChanged;
    
        lockSpotCheck.CheckedChanged +=
            LockSpotCheck_CheckedChanged;
    
        distanceApplyButton.Click +=
            (_, _) => ApplyDistance();
    
        autoFollowInitializeCheck.CheckedChanged +=
            AutoFollowInitializeCheck_CheckedChanged;
    
        autoFollowNickButton.Click +=
            (_, _) => ApplyAutoFollowNick();
    
        autoFollowEnabledCheck.CheckedChanged +=
            AutoFollowEnabledCheck_CheckedChanged;

        espInitializeCheck.CheckedChanged +=
            EspInitializeCheck_CheckedChanged;
        
        espLineCheck.CheckedChanged +=
            (_, _) => ApplyEspFeature(0, espLineCheck);
        
        espNameCheck.CheckedChanged +=
            (_, _) => ApplyEspFeature(1, espNameCheck);
        
        espDistanceCheck.CheckedChanged +=
            (_, _) => ApplyEspFeature(2, espDistanceCheck);
        
        espAllyCheck.CheckedChanged +=
            (_, _) => ApplyEspFeature(3, espAllyCheck);
        
        espEnemyCheck.CheckedChanged +=
            (_, _) => ApplyEspFeature(4, espEnemyCheck);
        
        espMonsterCheck.CheckedChanged +=
            (_, _) => ApplyEspFeature(5, espMonsterCheck);
        
        espLockBotCheck.CheckedChanged +=
            (_, _) => ApplyEspFeature(6, espLockBotCheck);
        
        espLockSpotLootingCheck.CheckedChanged +=
            (_, _) => ApplyEspFeature(7, espLockSpotLootingCheck);

        // =========================================================
        // INITIAL STATE
        // =========================================================
    
        SetTab4FeatureEnabled(false);
        SetAutoFollowFeatureEnabled(false);
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

    private void SetTab4FeatureEnabled(bool enabled)
    {
        zoomFeaturePanel.Visible = enabled;
    
        zoomCheck.Enabled = enabled;
        autoPickV1Check.Enabled = enabled;
        autoPickV2Check.Enabled = enabled;
        showNameCheck.Enabled = enabled;
        fastLootCheck.Enabled = enabled;
        mauKeyboardCheck.Enabled = enabled;
        reviveInactiveCheck.Enabled = enabled;
        reviveDisableCheck.Enabled = enabled;
        reviveAutoCheck.Enabled = enabled;
        lockSpotCheck.Enabled = enabled;
        distanceTextBox.Enabled = enabled;
        distanceApplyButton.Enabled = enabled;
    
        // =========================================================
        // ZOOM PANEL HEIGHT
        // =========================================================
    
        if (enabled)
        {
            zoomFeaturePanel.Height = 390;
        }
        else
        {
            zoomFeaturePanel.Height = 0;
        }
    
        // =========================================================
        // AUTO FOLLOW HEADER POSITION
        // =========================================================
    
        autoFollowInitializeCheck.Top =
            zoomFeaturePanel.Bottom + 6;
    

        // =========================================================
        // AUTO FOLLOW CONTENT POSITION
        // =========================================================

        autoFollowFeaturePanel.Top =
            autoFollowInitializeCheck.Bottom + 6;

        // =========================================================
        // ESP HEADER DAN PANEL POSITION
        // =========================================================

        espInitializeCheck.Top =
            autoFollowFeaturePanel.Visible
                ? autoFollowFeaturePanel.Bottom + 6
                : autoFollowInitializeCheck.Bottom + 6;
        
        espFeaturePanel.Top =
            espInitializeCheck.Bottom + 6;
    }

    private void SetReviveSelection(
        CheckBox selected)
    {

        if (!selected.Checked)
            return;

        ResetCheckBox(reviveInactiveCheck, false);
        ResetCheckBox(reviveDisableCheck, false);
        ResetCheckBox(reviveAutoCheck, false);
        ResetCheckBox(selected, true);
    }


    private void ZoomInitializeCheck_CheckedChanged(
        object? sender,
        EventArgs e)
    {
        if (features == null)
        {
            ResetCheckBox(zoomInitializeCheck, false);
            SetTab4FeatureEnabled(false);
            return;
        }

        try
        {
            if (zoomInitializeCheck.Checked)
            {
                features.InitializeZoom();
                SetTab4FeatureEnabled(true);
                statusLabel.Text = "Status: Zoom AOB initialized";
            }
            else
            {
                features.ClearZoom();
                SetTab4FeatureEnabled(false);
                ResetCheckBox(zoomCheck, false);
                ResetCheckBox(autoPickV1Check, false);
                ResetCheckBox(autoPickV2Check, false);
                ResetCheckBox(showNameCheck, false);
                ResetCheckBox(fastLootCheck, false);
                ResetCheckBox(mauKeyboardCheck, false);
                ResetCheckBox(reviveInactiveCheck, false);
                ResetCheckBox(reviveDisableCheck, false);
                ResetCheckBox(reviveAutoCheck, false);
                ResetCheckBox(lockSpotCheck, false);
                statusLabel.Text = "Status: Zoom AOB cleared";
            }
        }
        catch (Exception ex)
        {
            HandleFeatureError("Zoom Initialize", ex);
            ResetCheckBox(zoomInitializeCheck, false);
            SetTab4FeatureEnabled(false);
        }
    }


    private void ZoomCheck_CheckedChanged(object? sender, EventArgs e)
    {
        if (!zoomInitializeCheck.Checked || features == null)
        {
            ResetCheckBox(zoomCheck, false);
            return;
        }

        try
        {
            features.SetZoom(zoomCheck.Checked);
        }
        catch (Exception ex)
        {
            HandleFeatureError("Zoom", ex);
            ResetCheckBox(zoomCheck, false);
        }
    }


    private void AutoPickV1Check_CheckedChanged(object? sender, EventArgs e)
    {
        if (autoPickV1Check.Checked)
            ResetCheckBox(autoPickV2Check, false);

        if (!zoomInitializeCheck.Checked || features == null)
            return;

        try
        {
            features.SetAutoPick(
                (byte)(autoPickV1Check.Checked ? 1 : 0));
        }
        catch (Exception ex)
        {
            HandleFeatureError("Auto Pick V1", ex);
            ResetCheckBox(autoPickV1Check, false);
        }
    }


    private void AutoPickV2Check_CheckedChanged(object? sender, EventArgs e)
    {
        if (autoPickV2Check.Checked)
            ResetCheckBox(autoPickV1Check, false);

        if (!zoomInitializeCheck.Checked || features == null)
            return;

        try
        {
            features.SetAutoPick(
                (byte)(autoPickV2Check.Checked ? 2 : 0));
        }
        catch (Exception ex)
        {
            HandleFeatureError("Auto Pick V2", ex);
            ResetCheckBox(autoPickV2Check, false);
        }
    }


    private void ShowNameCheck_CheckedChanged(object? sender, EventArgs e)
    {
        if (!zoomInitializeCheck.Checked || features == null)
            return;

        try { features.SetShowName(showNameCheck.Checked); }
        catch (Exception ex)
        {
            HandleFeatureError("Show Name", ex);
            ResetCheckBox(showNameCheck, false);
        }
    }


    private void FastLootCheck_CheckedChanged(object? sender, EventArgs e)
    {
        if (!zoomInitializeCheck.Checked || features == null)
            return;

        try { features.SetFastLoot(fastLootCheck.Checked); }
        catch (Exception ex)
        {
            HandleFeatureError("Fast Loot", ex);
            ResetCheckBox(fastLootCheck, false);
        }
    }


    private void MauKeyboardCheck_CheckedChanged(object? sender, EventArgs e)
    {
        if (!zoomInitializeCheck.Checked || features == null)
            return;

        try { features.SetMauKeyboardMode(mauKeyboardCheck.Checked); }
        catch (Exception ex)
        {
            HandleFeatureError("MAU Keyboard Mode", ex);
            ResetCheckBox(mauKeyboardCheck, false);
        }
    }


    private void ReviveInactiveCheck_CheckedChanged(object? sender, EventArgs e)
    {
        if (reviveInactiveCheck.Checked)
            SetReviveSelection(reviveInactiveCheck);
        if (!zoomInitializeCheck.Checked || features == null) return;
        try { features.SetRevive(
                (byte)(reviveInactiveCheck.Checked ? 0 : 0)); }
        catch (Exception ex) { HandleFeatureError("Revive", ex); }
    }


    private void ReviveDisableCheck_CheckedChanged(object? sender, EventArgs e)
    {
        if (reviveDisableCheck.Checked)
            SetReviveSelection(reviveDisableCheck);
        if (!zoomInitializeCheck.Checked || features == null) return;
        try { features.SetRevive(
                (byte)(reviveDisableCheck.Checked ? 1 : 0)); }
        catch (Exception ex) { HandleFeatureError("Revive", ex); }
    }


    private void ReviveAutoCheck_CheckedChanged(object? sender, EventArgs e)
    {
        if (reviveAutoCheck.Checked)
            SetReviveSelection(reviveAutoCheck);
        if (!zoomInitializeCheck.Checked || features == null) return;
        try { features.SetRevive(
                (byte)(reviveAutoCheck.Checked ? 2 : 0)); }
        catch (Exception ex) { HandleFeatureError("Revive", ex); }
    }


    private void LockSpotCheck_CheckedChanged(object? sender, EventArgs e)
    {
        if (!zoomInitializeCheck.Checked || features == null)
            return;

        try { features.SetLockSpot(lockSpotCheck.Checked); }
        catch (Exception ex)
        {
            HandleFeatureError("Lock Spot", ex);
            ResetCheckBox(lockSpotCheck, false);
        }
    }


    private void ApplyDistance()
    {
        if (!zoomInitializeCheck.Checked || features == null)
            return;

        if (!float.TryParse(
                distanceTextBox.Text,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float value))
        {
            statusLabel.Text = "Status: Distance tidak valid";
            return;
        }

        try
        {
            features.SetDistance(value);
            statusLabel.Text = $"Status: Distance = {value.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }
        catch (Exception ex)
        {
            HandleFeatureError("Distance", ex);
        }
    }

    private void AutoFollowInitializeCheck_CheckedChanged(
        object? sender,
        EventArgs e)
    {
        if (features == null)
        {
            ResetCheckBox(autoFollowInitializeCheck, false);
            SetAutoFollowFeatureEnabled(false);
            return;
        }
    
        try
        {
            if (autoFollowInitializeCheck.Checked)
            {
                features.InitializeAutoFollow();
    
                autoFollowNickTextBox.Text =
                    features.ReadAutoFollowNick();
    
                SetAutoFollowFeatureEnabled(true);
    
                statusLabel.Text =
                    "Status: Auto Follow AOB initialized";
            }
            else
            {
                features.ClearAutoFollow();
    
                autoFollowNickTextBox.Text = "";
    
                ResetCheckBox(
                    autoFollowEnabledCheck,
                    false);
    
                SetAutoFollowFeatureEnabled(false);
    
                statusLabel.Text =
                    "Status: Auto Follow AOB cleared";
            }
        }
        catch (Exception ex)
        {
            HandleFeatureError(
                "Auto Follow Initialize",
                ex);
    
            ResetCheckBox(
                autoFollowInitializeCheck,
                false);
    
            SetAutoFollowFeatureEnabled(false);
        }
    }


    private void SetAutoFollowFeatureEnabled(bool enabled)
    {
        autoFollowFeaturePanel.Visible = enabled;
        autoFollowFeaturePanel.Height = enabled ? 68 : 0;
    
        autoFollowNickTextBox.Enabled = enabled;
        autoFollowNickButton.Enabled = enabled;
        autoFollowEnabledCheck.Enabled = enabled;
    
        // Pindahkan ESP mengikuti kondisi Auto Follow.
        espInitializeCheck.Top =
            enabled
                ? autoFollowFeaturePanel.Bottom + 6
                : autoFollowInitializeCheck.Bottom + 6;
    
        espFeaturePanel.Top =
            espInitializeCheck.Bottom + 6;
    }

    private void ApplyAutoFollowNick()
    {
        if (!autoFollowInitializeCheck.Checked || features == null)
            return;

        try
        {
            string nick = autoFollowNickTextBox.Text.Trim();
            if (nick.Length == 0)
                return;

            features.SetAutoFollowNick(nick);
            features.SetAutoFollow(true);
            ResetCheckBox(autoFollowEnabledCheck, true);
            statusLabel.Text = $"Status: Auto Follow → {nick}";
        }
        catch (Exception ex)
        {
            HandleFeatureError("Nick Follow", ex);
            ResetCheckBox(autoFollowEnabledCheck, false);
        }
    }


    private void AutoFollowEnabledCheck_CheckedChanged(
        object? sender,
        EventArgs e)
    {
        if (!autoFollowInitializeCheck.Checked || features == null)
        {
            ResetCheckBox(autoFollowEnabledCheck, false);
            return;
        }

        try
        {
            features.SetAutoFollow(autoFollowEnabledCheck.Checked);
        }
        catch (Exception ex)
        {
            HandleFeatureError("Auto Follow", ex);
            ResetCheckBox(autoFollowEnabledCheck, false);
        }
    }
    // =========================================================
    // ESP INITIALIZE
    // =========================================================
    
    private void EspInitializeCheck_CheckedChanged(
        object? sender,
        EventArgs e)
    {
        if (features == null)
        {
            ResetCheckBox(espInitializeCheck, false);
            SetEspFeatureEnabled(false);
            return;
        }
    
        try
        {
            if (espInitializeCheck.Checked)
            {
                features.InitializeEspAob();
                SetEspFeatureEnabled(true);
                statusLabel.Text = "Status: ESP AOB initialized";
            }
            else
            {
                foreach (var check in GetEspChecks())
                    ResetCheckBox(check, false);
    
                features.ClearEsp();
                SetEspFeatureEnabled(false);
                statusLabel.Text = "Status: ESP AOB cleared";
            }
        }
        catch (Exception ex)
        {
            HandleFeatureError("ESP Initialize", ex);
            ResetCheckBox(espInitializeCheck, false);
            SetEspFeatureEnabled(false);
        }
    }
    
    private CheckBox[] GetEspChecks()
    {
        return new[]
        {
            espLineCheck,
            espNameCheck,
            espDistanceCheck,
            espAllyCheck,
            espEnemyCheck,
            espMonsterCheck,
            espLockBotCheck,
            espLockSpotLootingCheck
        };
    }
    
    private void SetEspFeatureEnabled(bool enabled)
    {
        espFeaturePanel.Visible = enabled;
    
        foreach (var check in GetEspChecks())
            check.Enabled = enabled;
    }
    
    private void ApplyEspFeature(int offset, CheckBox check)
    {
        if (!espInitializeCheck.Checked || features == null)
            return;
    
        try
        {
            features.SetEspFeature(offset, check.Checked);
            statusLabel.Text = $"Status: ESP {check.Text} {(check.Checked ? "ON" : "OFF")}";
        }
        catch (Exception ex)
        {
            HandleFeatureError("ESP " + check.Text, ex);
            ResetCheckBox(check, false);
        }
    }

    // =========================================================
    // TAB 3
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
                "NPC 7",
                HotkeyAction.Npc7,
                y);

        y += 72;

        npc0HotkeyBox =
            AddHotkeyRow(
                panel,
                "NPC 0",
                HotkeyAction.Npc0,
                y);

        y += 72;

        npcMinusHotkeyBox =
            AddHotkeyRow(
                panel,
                "NPC -",
                HotkeyAction.NpcMinus,
                y);

        y += 72;

        npcEqualsHotkeyBox =
            AddHotkeyRow(
                panel,
                "NPC =",
                HotkeyAction.NpcEquals,
                y);

        y += 72;

        npcF12HotkeyBox =
            AddHotkeyRow(
                panel,
                "NPC F12",
                HotkeyAction.NpcF12,
                y);

        y += 72;

        npc8HotkeyBox =
            AddHotkeyRow(
                panel,
                "NPC 8",
                HotkeyAction.Npc8,
                y);

        y += 72;

        npcF11HotkeyBox =
            AddHotkeyRow(
                panel,
                "NPC F11",
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


    // =========================================================
    // ADD HOTKEY ROW
    // =========================================================

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
    // HOTKEY CAPTURE
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


    // =========================================================
    // HOTKEY REGISTER
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


    // =========================================================
    // HOTKEY DEFINITIONS
    // =========================================================

    private HotkeyDefinition GetHotkeyDefinition(
        HotkeyAction action)
    {
        return hotkeys[action];
    }


    private static string nameFor(
        HotkeyAction action)
    {
        return action switch
        {
            HotkeyAction.Npc7 =>
                "NPC 7",

            HotkeyAction.Npc0 =>
                "NPC 0",

            HotkeyAction.NpcMinus =>
                "NPC -",

            HotkeyAction.NpcEquals =>
                "NPC =",

            HotkeyAction.NpcF12 =>
                "NPC F12",

            HotkeyAction.Npc8 =>
                "NPC 8",

            HotkeyAction.NpcF11 =>
                "NPC F11",

            _ =>
                "Unknown"
        };
    }


    // =========================================================
    // HOTKEY FORMAT
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


    // =========================================================
    // HOTKEY CONFLICT
    // =========================================================

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
    // LOAD HOTKEY
    // =========================================================

    private void LoadHotkeys()
    {
        hotkeys.Clear();


        // -----------------------------------------------------
        // DEFAULT NPC HOTKEY
        // -----------------------------------------------------

        hotkeys[HotkeyAction.Npc7] =
            new HotkeyDefinition(
                HotkeyIdNpc7,
                HotkeyAction.Npc7,
                Keys.D7,
                Keys.None);

        hotkeys[HotkeyAction.Npc0] =
            new HotkeyDefinition(
                HotkeyIdNpc0,
                HotkeyAction.Npc0,
                Keys.D0,
                Keys.None);

        hotkeys[HotkeyAction.NpcMinus] =
            new HotkeyDefinition(
                HotkeyIdNpcMinus,
                HotkeyAction.NpcMinus,
                Keys.OemMinus,
                Keys.None);

        hotkeys[HotkeyAction.NpcEquals] =
            new HotkeyDefinition(
                HotkeyIdNpcEquals,
                HotkeyAction.NpcEquals,
                Keys.Oemplus,
                Keys.None);

        hotkeys[HotkeyAction.NpcF12] =
            new HotkeyDefinition(
                HotkeyIdNpcF12,
                HotkeyAction.NpcF12,
                Keys.F12,
                Keys.None);

        hotkeys[HotkeyAction.Npc8] =
            new HotkeyDefinition(
                HotkeyIdNpc8,
                HotkeyAction.Npc8,
                Keys.D8,
                Keys.None);

        hotkeys[HotkeyAction.NpcF11] =
            new HotkeyDefinition(
                HotkeyIdNpcF11,
                HotkeyAction.NpcF11,
                Keys.F11,
                Keys.None);


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


    // =========================================================
    // UPDATE HOTKEY BOX
    // =========================================================

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
    // SAVE HOTKEY
    // =========================================================

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


    // =========================================================
    // RESET HOTKEY
    // =========================================================

    private void ResetHotkeysToDefault()
    {
        UnregisterAllHotkeys();


        hotkeys[HotkeyAction.Npc7] =
            new HotkeyDefinition(
                HotkeyIdNpc7,
                HotkeyAction.Npc7,
                Keys.D7,
                Keys.None);

        hotkeys[HotkeyAction.Npc0] =
            new HotkeyDefinition(
                HotkeyIdNpc0,
                HotkeyAction.Npc0,
                Keys.D0,
                Keys.None);

        hotkeys[HotkeyAction.NpcMinus] =
            new HotkeyDefinition(
                HotkeyIdNpcMinus,
                HotkeyAction.NpcMinus,
                Keys.OemMinus,
                Keys.None);

        hotkeys[HotkeyAction.NpcEquals] =
            new HotkeyDefinition(
                HotkeyIdNpcEquals,
                HotkeyAction.NpcEquals,
                Keys.Oemplus,
                Keys.None);

        hotkeys[HotkeyAction.NpcF12] =
            new HotkeyDefinition(
                HotkeyIdNpcF12,
                HotkeyAction.NpcF12,
                Keys.F12,
                Keys.None);

        hotkeys[HotkeyAction.Npc8] =
            new HotkeyDefinition(
                HotkeyIdNpc8,
                HotkeyAction.Npc8,
                Keys.D8,
                Keys.None);

        hotkeys[HotkeyAction.NpcF11] =
            new HotkeyDefinition(
                HotkeyIdNpcF11,
                HotkeyAction.NpcF11,
                Keys.F11,
                Keys.None);


        UpdateHotkeyBoxes();

        RegisterAllHotkeys();

        SaveHotkeys();

        statusLabel.Text =
            "Status: Hotkey kembali ke default";
    }


    // =========================================================
    // SAVED HOTKEY DATA
    // =========================================================

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
    // CREATE CHECKBOX
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


    // =========================================================
    // ADD FREEZE ROW
    // =========================================================

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
    // UPDATE ALL ADDRESSES
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
                        0xC28B40,
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


    // =========================================================
    // APPLY ENTRY
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


    // =========================================================
    // UPDATE ONE ENTRY ADDRESS
    // =========================================================

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
                    0xC28B40,
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


    // =========================================================
    // READ CURRENT VALUE
    // =========================================================

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
                    0xC28B40,
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


    // =========================================================
    // READ ALL CURRENT VALUES
    // =========================================================

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
    // FREEZE TIMER
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


    // =========================================================
    // SILENT FREEZE WRITE
    // =========================================================

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
    // LOAD PROCESSES
    // =========================================================

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


    // =========================================================
    // ATTACH
    // =========================================================

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


    // =========================================================
    // DETACH
    // =========================================================

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


    // =========================================================
    // FORM CLOSING
    // =========================================================

    private void MainForm_FormClosing(
        object? sender,
        FormClosingEventArgs e)
    {
        UnregisterAllHotkeys();

        SaveHotkeys();

        DetachCurrent();
    }


    // =========================================================
    // AUTO NPC
    // =========================================================

    private void AutoNpcCheck_CheckedChanged(
        object? sender,
        EventArgs e)
    {
        if (features == null)
        {
            ResetCheckBox(
                autoNpcCheck,
                false);

            SetNpcTargetControlsEnabled(
                false);

            ResetNpcAddressLabels();

            return;
        }

        try
        {
            bool enabled =
                autoNpcCheck.Checked;


            // =================================================
            // AUTO NPC OFF
            // =================================================

            if (!enabled)
            {
                SetNpcTargetControlsEnabled(
                    false);

                ResetNpcAddressLabels();

                statusLabel.Text =
                    "Status: Auto NPC OFF";

                return;
            }


            // =================================================
            // AUTO NPC ON
            // =================================================

            var result =
                features.FindNpc();


            // Tampilkan address hasil FindNpc()
            UpdateNpcAddressLabels(
                result.Bels,
                result.Cora,
                result.Acc);


            SetNpcTargetControlsEnabled(
                true);


            statusLabel.Text =
                $"Status: Auto NPC ready → " +
                $"B 0x{result.Bels.ToInt64():X8} | " +
                $"C 0x{result.Cora.ToInt64():X8} | " +
                $"A 0x{result.Acc.ToInt64():X8}";


            MessageBox.Show(
                this,
                "AUTO NPC RESULT\r\n\r\n" +
                $"NPC Bels : 0x{result.Bels.ToInt64():X8}\r\n" +
                $"NPC Cora : 0x{result.Cora.ToInt64():X8}\r\n" +
                $"NPC ACC  : 0x{result.Acc.ToInt64():X8}",
                "Auto NPC",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            SetNpcTargetControlsEnabled(
                false);

            ResetNpcAddressLabels();

            HandleFeatureError(
                "Auto NPC",
                ex);

            ResetCheckBox(
                autoNpcCheck,
                false);
        }
    }


    // =========================================================
    // STAGE 2
    // =========================================================

    private void Stage2Check_CheckedChanged(
        object? sender,
        EventArgs e)
    {
        if (features == null)
        {
            ResetCheckBox(
                stage2Check,
                false);

            return;
        }

        try
        {
            bool enabled =
                stage2Check.Checked;

            features.Stage2(
                enabled);

            statusLabel.Text =
                enabled
                    ? "Status: Stage 2 ON"
                    : "Status: Stage 2 OFF";
        }
        catch (Exception ex)
        {
            HandleFeatureError(
                "Stage 2 True Heart Flame Launcher",
                ex);

            ResetCheckBox(
                stage2Check,
                false);
        }
    }
    // =========================================================
    // STAGE 5 ROMANTIC
    // =========================================================
    
    private void Stage5RomanticCheck_CheckedChanged(
        object? sender,
        EventArgs e)
    {
        if (features == null)
        {
            ResetCheckBox(
                stage5RomanticCheck,
                false);
    
            return;
        }
    
        try
        {
            bool enabled =
                stage5RomanticCheck.Checked;
    
            features.Stage5Romantic(
                enabled);
    
            statusLabel.Text =
                enabled
                    ? "Status: Stage 5 Romantic ON"
                    : "Status: Stage 5 Romantic OFF";
        }
        catch (Exception ex)
        {
            HandleFeatureError(
                "Stage 5 Romantic Flame Launcher [30 Days]",
                ex);
    
            ResetCheckBox(
                stage5RomanticCheck,
                false);
        }
    }
    
    // =========================================================
    // STAGE 3
    // =========================================================
    
    private void Stage3TrueHeartCheck_CheckedChanged(
        object? sender,
        EventArgs e)
    {
        if (features == null)
        {
            ResetCheckBox(
                stage3TrueHeartCheck,
                false);
    
            return;
        }
    
        try
        {
            bool enabled =
                stage3TrueHeartCheck.Checked;
    
            features.Stage3(
                enabled);
    
            statusLabel.Text =
                enabled
                    ? "Status: Stage 3 True Heart Flame ON"
                    : "Status: Stage 3 True Heart Flame OFF";
        }
        catch (Exception ex)
        {
            HandleFeatureError(
                "Stage 3 True Heart Flame Launcher",
                ex);
    
            ResetCheckBox(
                stage3TrueHeartCheck,
                false);
        }
    }
    
    // =========================================================
    // INFERNAL DRAGONBONE
    // =========================================================
    
    private void InfernalDragonboneCheck_CheckedChanged(
        object? sender,
        EventArgs e)
    {
        if (features == null)
        {
            ResetCheckBox(
                infernalDragonboneCheck,
                false);
    
            return;
        }
    
        try
        {
            bool enabled =
                infernalDragonboneCheck.Checked;
    
            features.InfernalDragonbone(
                enabled);
    
            statusLabel.Text =
                enabled
                    ? "Status: Infernal Dragonbone ON"
                    : "Status: Infernal Dragonbone OFF";
        }
        catch (Exception ex)
        {
            HandleFeatureError(
                "Infernal Dragonbone Flame Launcher",
                ex);
    
            ResetCheckBox(
                infernalDragonboneCheck,
                false);
        }
    }


        // =========================================================
        // COMBINE
        // =========================================================

        private void CombineCheck_CheckedChanged(
            object? sender,
            EventArgs e)
        {
            if (features == null ||
                !combineInitializeCheck.Checked)
            {
                ResetCheckBox(
                    combineCheck,
                    false);

                return;
            }

            try
            {
                bool enabled = combineCheck.Checked;

                features.Combine(enabled);

                statusLabel.Text =
                    enabled
                        ? "Status: Combine Jarak Jauh ON"
                        : "Status: Combine Jarak Jauh OFF";
            }
            catch (Exception ex)
            {
                HandleFeatureError(
                    "Combine Jarak Jauh",
                    ex);

                ResetCheckBox(
                    combineCheck,
                    false);
            }
        }

        // =========================================================
        // COMBINE AOB - INITIALIZE
        // =========================================================

        private void CombineInitializeCheck_CheckedChanged(
            object? sender,
            EventArgs e)
        {
            if (features == null)
            {
                ResetCheckBox(
                    combineInitializeCheck,
                    false);

                return;
            }

            if (!combineInitializeCheck.Checked)
            {
                ResetCheckBox(
                    combineCheck,
                    false);

                combineCheck.Visible = false;
                combineCheck.Enabled = false;

                statusLabel.Text =
                    "Status: Combine Initialize OFF";

                return;
            }

            try
            {
                IntPtr address =
                    features.InitializeCombineAob();

                combineCheck.Visible = true;
                combineCheck.Enabled = true;

                statusLabel.Text =
                    $"Status: Combine AOB OK → 0x{address.ToInt64():X8}";
            }
            catch (Exception ex)
            {
                combineCheck.Visible = false;
                combineCheck.Enabled = false;

                HandleFeatureError(
                    "COMBINE AOB - INITIALIZE",
                    ex);

                ResetCheckBox(
                    combineInitializeCheck,
                    false);
            }
        }

        // =========================================================
        // SET TAB 2 CHECKBOXES
        // =========================================================
    
        private void SetTab2Checks(
            bool value)
        {
            if (autoNpcCheck != null)
            {
                ResetCheckBox(
                    autoNpcCheck,
                    value);
            }
    
            if (stage2Check != null)
            {
                ResetCheckBox(
                    stage2Check,
                    value);
            }
    
            if (combineInitializeCheck != null)
            {
                ResetCheckBox(
                    combineInitializeCheck,
                    false);
            }
    
            if (combineCheck != null)
            {
                ResetCheckBox(
                    combineCheck,
                    false);
    
                combineCheck.Visible = false;
                combineCheck.Enabled = false;
            }
    
            SetNpcTargetControlsEnabled(
                value &&
                autoNpcCheck != null &&
                autoNpcCheck.Checked);
    
            if (!value)
            {
                ResetNpcAddressLabels();
            }
        }

    // =========================================================
    // ATTACHED CHECK
    // =========================================================

    private bool IsAttached()
    {
        return
            memory != null &&
            features != null &&
            selectedProcess != null;
    }


    // =========================================================
    // FREEZE TIMER CONTROL
    // =========================================================

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


    // =========================================================
    // CHECKBOX RESET
    // =========================================================

    private static void ResetCheckBox(
        CheckBox checkBox,
        bool value)
    {
        if (checkBox.Checked == value)
            return;

        checkBox.Checked =
            value;
    }


    // =========================================================
    // ERROR HANDLER
    // =========================================================

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


    // =========================================================
    // BYTE PARSER
    // =========================================================

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


    // =========================================================
    // FREEZE TYPE
    // =========================================================

    private enum FreezeType
    {
        String6,
        Byte,
        Float
    }


    // =========================================================
    // FREEZE ENTRY
    // =========================================================

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
    // PROCESS ITEM
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
}
