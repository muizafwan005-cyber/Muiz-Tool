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
    // TAB 4 (ZOOM / FOLLOW) - DAFTAR ISI
    //
    // Urutan bagian di file ini:
    //   1. BUILD TAB 4
    //   2. GENE TUMPUK - INITIALIZE
    //   3. ZOOM AOB
    //   4. AUTO FOLLOW AOB
    //   5. ESP AOB
    //   6. PURSE CHARGER AOB
    //   7. LAYOUT TAB 4
    //
    // Cari "// 1." sampai "// 7." untuk loncat ke bagiannya.
    // =========================================================


    // =========================================================
    // 1. BUILD TAB 4
    // Memanggil tiap bagian sesuai urutan tampilan.
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

        BuildGeneTumpukSection(panel, ref y);   // bagian 2
        BuildZoomSection(panel, ref y);         // bagian 3
        BuildAutoFollowSection(panel, y);       // bagian 4
        BuildEspSection(panel);                 // bagian 5
        BuildPurseChargerSection(panel);        // bagian 6

        // Kondisi awal: semua sub-fitur AOB disembunyikan sampai di-Initialize.
        SetTab4FeatureEnabled(false);
        SetAutoFollowFeatureEnabled(false);
    }

    // =========================================================
    // 2. GENE TUMPUK - INITIALIZE
    // =========================================================

    private CheckBox geneTumpukInitializeCheck = null!;

    private void BuildGeneTumpukSection(
        Panel panel,
        ref int y)
    {
        geneTumpukInitializeCheck =
            CreateFeatureCheckBox(
                panel,
                "GENE TUMPUK - INITIALIZE",
                y);

        y += 42;

        geneTumpukInitializeCheck.CheckedChanged +=
            GeneTumpukInitializeCheck_CheckedChanged;
    }

    private void GeneTumpukInitializeCheck_CheckedChanged(
        object? sender,
        EventArgs e)
    {
        // ===== BAGIAN BARU: mulai =====
        if (features == null)
        {
            geneTumpukInitializeCheck.CheckedChanged -= GeneTumpukInitializeCheck_CheckedChanged;
            geneTumpukInitializeCheck.Checked = false;
            geneTumpukInitializeCheck.CheckedChanged += GeneTumpukInitializeCheck_CheckedChanged;
    
            statusLabel.Text = "Status: Attach process terlebih dahulu.";
            return;
        }
        // ===== BAGIAN BARU: selesai =====
    
        try
        {
            // Kirim status checkbox (true saat dicentang, false saat dimatikan)
            features.SetGeneTumpuk(geneTumpukInitializeCheck.Checked);
    
            System.Diagnostics.Trace.WriteLine(
                $"GENE TUMPUK: {(geneTumpukInitializeCheck.Checked ? "AKTIF" : "NONAKTIF")}");
        }
        catch (Exception ex)
        {
            // Jika gagal/crash saat scan/tulis memori:
            // Lepas event sementara agar tidak memicu loop rekursif
            geneTumpukInitializeCheck.CheckedChanged -= GeneTumpukInitializeCheck_CheckedChanged;
            geneTumpukInitializeCheck.Checked = false;
            geneTumpukInitializeCheck.CheckedChanged += GeneTumpukInitializeCheck_CheckedChanged;
    
            System.Diagnostics.Trace.WriteLine($"GENE TUMPUK ERROR: {ex.Message}");
        }
    }

    // =========================================================
    // 3. ZOOM AOB
    // Initialize -> Zoom, Auto Pick, Show Name, Fast Loot, MAU, Revive, Distance, Lock Spot.
    // =========================================================

    private CheckBox zoomInitializeCheck = null!;
    private Panel zoomFeaturePanel = null!;
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

    private void BuildZoomSection(
        Panel panel,
        ref int y)
    {
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

        // Simpan tinggi isi Zoom.
        int zoomContentHeight =
            zoomY + 34;

        y += zoomContentHeight;

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

    private void SetTab4FeatureEnabled(bool enabled)
    {
        zoomFeaturePanel.Visible = enabled;
        zoomFeaturePanel.Height = enabled ? 390 : 0;

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

        UpdateTab4Layout();
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

    // =========================================================
    // 4. AUTO FOLLOW AOB
    // Initialize -> Nick Follow + Auto Follow.
    // =========================================================

    private CheckBox autoFollowInitializeCheck = null!;
    private TextBox autoFollowNickTextBox = null!;
    private Button autoFollowNickButton = null!;
    private CheckBox autoFollowEnabledCheck = null!;
    private Panel autoFollowFeaturePanel = null!;

    private void BuildAutoFollowSection(
        Panel panel,
        int y)
    {
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

        // Default: kontrol Auto Follow nonaktif sampai Initialize.
        autoFollowNickTextBox.Enabled = false;
        autoFollowNickButton.Enabled = false;
        autoFollowEnabledCheck.Enabled = false;

        autoFollowInitializeCheck.CheckedChanged +=
            AutoFollowInitializeCheck_CheckedChanged;

        autoFollowNickButton.Click +=
            (_, _) => ApplyAutoFollowNick();

        autoFollowEnabledCheck.CheckedChanged +=
            AutoFollowEnabledCheck_CheckedChanged;
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

        UpdateTab4Layout();
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
    // 5. ESP AOB
    // Initialize -> Line, Name, Distance, Ally, Enemy, Monster, Lock Bot, Lock Spot Looting.
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

    private void BuildEspSection(
        Panel panel)
    {
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
    }

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
        espFeaturePanel.Height = enabled ? 270 : 0;

        foreach (var check in GetEspChecks())
            check.Enabled = enabled;

        UpdateTab4Layout();
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
    // 6. PURSE CHARGER AOB
    // Initialize -> Purse Charger Always On (timer 50 ms).
    // =========================================================

    private CheckBox purseChargerInitializeCheck = null!;
    private CheckBox purseChargerAlwaysOnCheck = null!;
    private Panel purseChargerFeaturePanel = null!;
    private System.Windows.Forms.Timer purseChargerTimer = null!;

    private void BuildPurseChargerSection(
        Panel panel)
    {
        purseChargerInitializeCheck =
            CreateFeatureCheckBox(
                panel,
                "PURSE CHARGER AOB - INITIALIZE",
                espInitializeCheck.Bottom + 6);

        // =========================================================
        // PURSE CHARGER FEATURE PANEL
        // =========================================================

        purseChargerFeaturePanel = new Panel
        {
            Location = new Point(
                0,
                purseChargerInitializeCheck.Bottom + 6),
            Size = new Size(300, 34),
            BackColor = BackColorMain,
            Visible = false
        };

        panel.Controls.Add(purseChargerFeaturePanel);

        purseChargerAlwaysOnCheck =
            CreateIndentedCheckBox(
                purseChargerFeaturePanel,
                "Purse Charger Always On",
                0);

        // Timer Purse Charger: interval 50 ms.
        purseChargerTimer =
            new System.Windows.Forms.Timer
            {
                Interval = 50
            };

        purseChargerInitializeCheck.CheckedChanged +=
            PurseChargerInitializeCheck_CheckedChanged;

        purseChargerAlwaysOnCheck.CheckedChanged +=
            PurseChargerAlwaysOnCheck_CheckedChanged;

        purseChargerTimer.Tick +=
            PurseChargerTimer_Tick;
    }

    private void PurseChargerInitializeCheck_CheckedChanged(
        object? sender,
        EventArgs e)
    {
        if (features == null)
        {
            ResetCheckBox(
                purseChargerInitializeCheck,
                false);

            return;
        }

        if (!purseChargerInitializeCheck.Checked)
        {
            purseChargerTimer.Stop();

            ResetCheckBox(
                purseChargerAlwaysOnCheck,
                false);

            features.ClearPurseCharger();

            purseChargerFeaturePanel.Visible = false;
            purseChargerFeaturePanel.Height = 0;
            purseChargerAlwaysOnCheck.Enabled = false;
            UpdateTab4Layout();

            statusLabel.Text =
                "Status: Purse Charger AOB cleared";

            return;
        }

        try
        {
            IntPtr address =
                features.InitializePurseChargerAob();

            purseChargerFeaturePanel.Visible = true;
            purseChargerFeaturePanel.Height = 34;
            purseChargerAlwaysOnCheck.Enabled = true;
            
            UpdateTab4Layout();

            statusLabel.Text =
                $"Status: Purse Charger AOB OK → 0x{address.ToInt64():X8}";
        }
        catch (Exception ex)
        {
            purseChargerTimer.Stop();

            purseChargerFeaturePanel.Visible = false;
            purseChargerFeaturePanel.Height = 0;
            purseChargerAlwaysOnCheck.Enabled = false;
            
            HandleFeatureError(
                "Purse Charger Initialize",
                ex);

            ResetCheckBox(
                purseChargerInitializeCheck,
                false);

            UpdateTab4Layout();
        }
    }

    private void PurseChargerAlwaysOnCheck_CheckedChanged(
        object? sender,
        EventArgs e)
    {
        if (features == null ||
            !purseChargerInitializeCheck.Checked)
        {
            purseChargerTimer.Stop();

            if (features != null)
                features.RestorePurseCharger();

            ResetCheckBox(
                purseChargerAlwaysOnCheck,
                false);

            return;
        }

        try
        {
            if (purseChargerAlwaysOnCheck.Checked)
            {
                features.ForcePurseChargerOn();

                purseChargerTimer.Interval = 50;
                purseChargerTimer.Start();

                statusLabel.Text =
                    "Status: Purse Charger Always On";
            }
            else
            {
                purseChargerTimer.Stop();

                features.RestorePurseCharger();

                statusLabel.Text =
                    "Status: Purse Charger OFF";
            }
        }
        catch (Exception ex)
        {
            purseChargerTimer.Stop();

            features.RestorePurseCharger();

            HandleFeatureError(
                "Purse Charger Always On",
                ex);

            ResetCheckBox(
                purseChargerAlwaysOnCheck,
                false);
        }
    }

    private void PurseChargerTimer_Tick(
        object? sender,
        EventArgs e)
    {
        if (features == null ||
            !purseChargerInitializeCheck.Checked ||
            !purseChargerAlwaysOnCheck.Checked)
        {
            purseChargerTimer.Stop();
            return;
        }

        try
        {
            features.ForcePurseChargerOn();
        }
        catch (Exception ex)
        {
            purseChargerTimer.Stop();

            features.RestorePurseCharger();

            HandleFeatureError(
                "Purse Charger Timer",
                ex);

            ResetCheckBox(
                purseChargerAlwaysOnCheck,
                false);
        }
    }

    // =========================================================
    // 7. LAYOUT TAB 4
    // Posisi dinamis semua bagian AOB saat panel dibuka/ditutup.
    // =========================================================

    private void UpdateTab4Layout()
    {
        zoomFeaturePanel.Top =
            zoomInitializeCheck.Bottom + 6;

        autoFollowInitializeCheck.Top =
            zoomFeaturePanel.Visible
                ? zoomFeaturePanel.Bottom + 6
                : zoomInitializeCheck.Bottom + 6;

        autoFollowFeaturePanel.Top =
            autoFollowInitializeCheck.Bottom + 6;

        espInitializeCheck.Top =
            autoFollowFeaturePanel.Visible
                ? autoFollowFeaturePanel.Bottom + 6
                : autoFollowInitializeCheck.Bottom + 6;

        espFeaturePanel.Top =
            espInitializeCheck.Bottom + 6;

        purseChargerInitializeCheck.Top =
            espFeaturePanel.Visible
                ? espFeaturePanel.Bottom + 6
                : espInitializeCheck.Bottom + 6;

        purseChargerFeaturePanel.Top =
            purseChargerInitializeCheck.Bottom + 6;

        Control? parent = purseChargerInitializeCheck.Parent;

        if (parent != null)
        {
            parent.PerformLayout();
            parent.Invalidate();
        }
    }
}
