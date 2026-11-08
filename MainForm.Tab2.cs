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
    // TAB 2 (ON / OFF) - DAFTAR ISI
    //
    // Urutan fitur di file ini sama dengan urutan di aplikasi:
    //   1. Auto NPC
    //   2. Stage 2 True Heart Flame Launcher
    //   3. Stage 5 Romantic Flame Launcher
    //   4. Stage 3 True Heart Flame Launcher
    //   5. Infernal Dragonbone Flame Launcher
    //   6. Combine AOB (Initialize + Combine Jarak Jauh)
    //   7. Scroll Zoom (Initialize + Slider + Freeze)
    //   8. Layout & Reset Tab 2
    //
    // Tiap bagian berisi: field -> BuildXxxSection (UI + event) -> handler.
    // Cari "// 1." sampai "// 8." untuk loncat ke bagiannya.
    // =========================================================


    // =========================================================
    // 0. BUILD TAB 2
    // Memanggil tiap bagian sesuai urutan tampilan.
    // =========================================================

    private void BuildTab2(
        TabPage tab)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
        
            BackColor = BackColorMain,
        
            Padding = new Padding(12),
        
            AutoScroll = true
        };

        tab.Controls.Add(
            panel);
        int y = 15;

        BuildAutoNpcSection(panel, ref y);          // 1
        BuildStage2Section(panel, ref y);           // 2
        BuildStage5Section(panel, ref y);           // 3
        BuildStage3Section(panel, ref y);           // 4
        BuildInfernalDragonboneSection(panel, ref y); // 5
        BuildCombineSection(panel, ref y);          // 6
        BuildScrollZoomSection(panel, ref y);       // 7

        UpdateTab2Layout();                         // 8
    }

    // =========================================================
    // 1. AUTO NPC
    // Cari NPC Bels / Cora / Acc, lalu pilih target.
    // =========================================================

    private CheckBox autoNpcCheck = null!;
    private RadioButton npcBelsRadio = null!;
    private RadioButton npcCoraRadio = null!;
    private RadioButton npcAccRadio = null!;
    private Label npcBelsAddressLabel = null!;
    private Label npcCoraAddressLabel = null!;
    private Label npcAccAddressLabel = null!;

    private void BuildAutoNpcSection(
        Panel panel,
        ref int y)
    {
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

        y += 190;

        autoNpcCheck.CheckedChanged +=
            AutoNpcCheck_CheckedChanged;
    }

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
    // 2. STAGE 2 - True Heart Flame Launcher
    // =========================================================

    private CheckBox stage2Check = null!;

    private void BuildStage2Section(
        Panel panel,
        ref int y)
    {
        stage2Check =
            CreateFeatureCheckBox(
                panel,
                "Stage 2 True Heart Flame Launcher",
                y);

        y += 48;

        stage2Check.CheckedChanged +=
            Stage2Check_CheckedChanged;
    }

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
    // 3. STAGE 5 - Romantic Flame Launcher [30 Days]
    // =========================================================

    private CheckBox stage5RomanticCheck = null!;

    private void BuildStage5Section(
        Panel panel,
        ref int y)
    {
        stage5RomanticCheck =
            CreateFeatureCheckBox(
                panel,
                "Stage 5 Romantic Flame Launcher [30 Days]",
                y);

        y += 48;

        stage5RomanticCheck.CheckedChanged +=
            Stage5RomanticCheck_CheckedChanged;
    }

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
    // 4. STAGE 3 - True Heart Flame Launcher
    // =========================================================

    private CheckBox stage3TrueHeartCheck = null!;

    private void BuildStage3Section(
        Panel panel,
        ref int y)
    {
        stage3TrueHeartCheck =
            CreateFeatureCheckBox(
                panel,
                "Stage 3 True Heart Flame Launcher",
                y);

        y += 48;

        stage3TrueHeartCheck.CheckedChanged +=
            Stage3TrueHeartCheck_CheckedChanged;
    }

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
    // 5. INFERNAL DRAGONBONE - Flame Launcher
    // =========================================================

    private CheckBox infernalDragonboneCheck = null!;

    private void BuildInfernalDragonboneSection(
        Panel panel,
        ref int y)
    {
        infernalDragonboneCheck =
            CreateFeatureCheckBox(
                panel,
                "Infernal Dragonbone Flame Launcher",
                y);

        y += 48;

        infernalDragonboneCheck.CheckedChanged +=
            InfernalDragonboneCheck_CheckedChanged;
    }

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
    // 6. COMBINE AOB
    // Initialize dulu (AOB scan), baru Combine Jarak Jauh muncul.
    // =========================================================

    private CheckBox combineInitializeCheck = null!;
    private CheckBox combineCheck = null!;

    private void BuildCombineSection(
        Panel panel,
        ref int y)
    {
        combineInitializeCheck =
            CreateFeatureCheckBox(
                panel,
                "COMBINE AOB - INITIALIZE",
                y);

        y += 25;

        combineCheck =
            CreateFeatureCheckBox(
                panel,
                "Combine Jarak Jauh",
                y);

        combineCheck.Visible = false;
        combineCheck.Enabled = false;

        y += 34;

        combineInitializeCheck.CheckedChanged +=
            CombineInitializeCheck_CheckedChanged;

        combineCheck.CheckedChanged +=
            CombineCheck_CheckedChanged;
    }

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
    
            SetScrollZoomPosition(false);
    
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
    
            SetScrollZoomPosition(true);
    
            statusLabel.Text =
                $"Status: Combine AOB OK → 0x{address.ToInt64():X8}";
        }
        catch (Exception ex)
        {
            combineCheck.Visible = false;
            combineCheck.Enabled = false;
    
            SetScrollZoomPosition(false);
    
            HandleFeatureError(
                "COMBINE AOB - INITIALIZE",
                ex);
    
            ResetCheckBox(
                combineInitializeCheck,
                false);
        }
    }

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
    // 7. SCROLL ZOOM
    // Initialize -> slider zoom -> Scroll Zoom Freeze.
    // =========================================================

    private CheckBox scrollZoomInitializeCheck = null!;
    private Label scrollZoomValueLabel = null!;
    private TrackBar scrollZoomTrackBar = null!;
    private CheckBox scrollZoomFreezeCheck = null!;
    private float scrollZoomCurrentValue = 0f;
    private readonly System.Windows.Forms.Timer scrollZoomFreezeTimer =
        new System.Windows.Forms.Timer
        {
            Interval = 50
        };

    private void BuildScrollZoomSection(
        Panel panel,
        ref int y)
    {
        scrollZoomInitializeCheck =
            CreateFeatureCheckBox(
                panel,
                "SCROLL ZOOM - INITIALIZE",
                y);

        y += 35;

        scrollZoomValueLabel = new Label
        {
            Text = "Zoom: Belum Initialize",
            Location = new Point(10, y),
            AutoSize = true
        };

        panel.Controls.Add(scrollZoomValueLabel);

        y += 30;

        scrollZoomTrackBar = new TrackBar
        {
            Minimum = 0,
            Maximum = 1500,
            Value = 0,
            TickFrequency = 50,
            SmallChange = 50,
            LargeChange = 50,
            Location = new Point(5, y),
            Width = 290,
            Height = 45,
            Enabled = false
        };

        panel.Controls.Add(scrollZoomTrackBar);

        y += 15;

        scrollZoomFreezeCheck =
            CreateFeatureCheckBox(
                panel,
                "Scroll Zoom Freeze",
                y);

        scrollZoomFreezeCheck.Enabled = false;
        scrollZoomFreezeCheck.Visible = false;

        y += 34;

        scrollZoomInitializeCheck.CheckedChanged +=
            ScrollZoomInitializeCheck_CheckedChanged;

        scrollZoomTrackBar.Scroll +=
            ScrollZoomTrackBar_Scroll;

        scrollZoomFreezeCheck.CheckedChanged +=
            ScrollZoomFreezeCheck_CheckedChanged;

        scrollZoomFreezeTimer.Tick +=
            ScrollZoomFreezeTimer_Tick;
    }

    private void ScrollZoomInitializeCheck_CheckedChanged(
        object? sender,
        EventArgs e)
    {
        if (features == null)
        {
            scrollZoomFreezeTimer.Stop();
    
            scrollZoomFreezeCheck.Visible = false;
            scrollZoomFreezeCheck.Enabled = false;
    
            ResetCheckBox(scrollZoomInitializeCheck, false);
            return;
        }
    
        if (!scrollZoomInitializeCheck.Checked)
        {
            scrollZoomFreezeTimer.Stop();
    
            ResetCheckBox(scrollZoomFreezeCheck, false);
    
            scrollZoomTrackBar.Enabled = false;
            scrollZoomFreezeCheck.Visible = false;
            scrollZoomFreezeCheck.Enabled = false;
    
            features.ClearScrollZoom();
    
            scrollZoomValueLabel.Text =
                "Zoom: Belum Initialize";
    
            statusLabel.Text =
                "Status: Scroll Zoom Initialize OFF";
            return;
        }
    
        try
        {
            features.InitializeScrollZoom();
    
            float currentValue =
                features.ReadScrollZoom();
    
            scrollZoomCurrentValue = currentValue;
    
            int sliderValue =
                (int)Math.Round(currentValue / 50f) * 50;
    
            sliderValue = Math.Max(
                0,
                Math.Min(1500, sliderValue));
    
            scrollZoomTrackBar.Value = sliderValue;
    
            scrollZoomTrackBar.Enabled = true;
            scrollZoomFreezeCheck.Visible = true;
            scrollZoomFreezeCheck.Enabled = true;
    
            scrollZoomValueLabel.Text =
                $"Zoom: {currentValue:0.##}";
    
            statusLabel.Text =
                "Status: Scroll Zoom Initialize OK";
        }
        catch (Exception ex)
        {
            scrollZoomTrackBar.Enabled = false;
            scrollZoomFreezeCheck.Visible = false;
            scrollZoomFreezeCheck.Enabled = false;
    
            HandleFeatureError(
                "SCROLL ZOOM - INITIALIZE",
                ex);
    
            ResetCheckBox(
                scrollZoomInitializeCheck,
                false);
        }
    }

    private void ScrollZoomTrackBar_Scroll(
        object? sender,
        EventArgs e)
    {
        if (features == null ||
            !scrollZoomInitializeCheck.Checked)
            return;
    
        try
        {
            int snappedValue =
                (int)Math.Round(
                    scrollZoomTrackBar.Value / 50.0) * 50;
    
            snappedValue = Math.Max(
                0,
                Math.Min(1500, snappedValue));
    
            scrollZoomTrackBar.Value = snappedValue;
    
            scrollZoomCurrentValue = snappedValue;
    
            features.SetScrollZoom(
                scrollZoomCurrentValue);
    
            scrollZoomValueLabel.Text =
                $"Zoom: {scrollZoomCurrentValue:0}";
        }
        catch (Exception ex)
        {
            HandleFeatureError(
                "SCROLL ZOOM",
                ex);
        }
    }

    private void ScrollZoomFreezeCheck_CheckedChanged(
        object? sender,
        EventArgs e)
    {
        if (!scrollZoomInitializeCheck.Checked ||
            features == null)
        {
            ResetCheckBox(
                scrollZoomFreezeCheck,
                false);
    
            scrollZoomFreezeTimer.Stop();
            return;
        }
    
        if (scrollZoomFreezeCheck.Checked)
        {
            try
            {
                scrollZoomCurrentValue =
                    scrollZoomTrackBar.Value;
    
                features.SetScrollZoom(
                    scrollZoomCurrentValue);
    
                scrollZoomFreezeTimer.Start();
    
                statusLabel.Text =
                    "Status: Scroll Zoom Freeze ON";
            }
            catch (Exception ex)
            {
                scrollZoomFreezeTimer.Stop();
    
                HandleFeatureError(
                    "SCROLL ZOOM FREEZE",
                    ex);
    
                ResetCheckBox(
                    scrollZoomFreezeCheck,
                    false);
            }
        }
        else
        {
            scrollZoomFreezeTimer.Stop();
    
            statusLabel.Text =
                "Status: Scroll Zoom Freeze OFF";
        }
    }

    private void ScrollZoomFreezeTimer_Tick(
        object? sender,
        EventArgs e)
    {
        if (features == null ||
            !scrollZoomInitializeCheck.Checked ||
            !scrollZoomFreezeCheck.Checked)
        {
            scrollZoomFreezeTimer.Stop();
            return;
        }
    
        try
        {
            features.SetScrollZoom(
                scrollZoomTrackBar.Value);
        }
        catch (Exception ex)
        {
            scrollZoomFreezeTimer.Stop();
    
            HandleFeatureError(
                "SCROLL ZOOM FREEZE",
                ex);
    
            ResetCheckBox(
                scrollZoomFreezeCheck,
                false);
        }
    }

    // =========================================================
    // 8. LAYOUT & RESET TAB 2
    // Posisi kontrol (Combine / Scroll Zoom) dan reset saat attach/detach.
    // =========================================================

    private void UpdateTab2Layout()
    {
        if (combineInitializeCheck == null ||
            combineCheck == null ||
            scrollZoomInitializeCheck == null ||
            scrollZoomValueLabel == null ||
            scrollZoomTrackBar == null ||
            scrollZoomFreezeCheck == null)
        {
            return;
        }
    
        // Combine Jarak Jauh tepat di bawah Combine Initialize
        combineCheck.Top =
            combineInitializeCheck.Bottom;
    
        // Jika Combine Jarak Jauh tampil, Scroll Zoom turun di bawahnya.
        // Jika tidak, Scroll Zoom langsung di bawah Combine Initialize.
        int nextTop =
            combineCheck.Visible
                ? combineCheck.Bottom
                : combineInitializeCheck.Bottom;
    
        scrollZoomInitializeCheck.Top =
            nextTop + 10;
    
        scrollZoomValueLabel.Top =
            scrollZoomInitializeCheck.Bottom;
    
        scrollZoomTrackBar.Top =
            scrollZoomValueLabel.Bottom + 5;
    
        // Freeze tepat di bawah trackbar
        scrollZoomFreezeCheck.Top =
            scrollZoomTrackBar.Bottom + 2;
    }

    private void SetScrollZoomPosition(bool combineVisible)
    {
        UpdateTab2Layout();
    }

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
    
        // -----------------------------------------------------
        // SCROLL ZOOM RESET
        // -----------------------------------------------------
    
        if (scrollZoomInitializeCheck != null)
        {
            ResetCheckBox(
                scrollZoomInitializeCheck,
                false);
        }
    
        if (scrollZoomFreezeCheck != null)
        {
            ResetCheckBox(
                scrollZoomFreezeCheck,
                false);
    
            scrollZoomFreezeCheck.Enabled = false;
        }
    
        if (scrollZoomTrackBar != null)
        {
            scrollZoomTrackBar.Enabled = false;
        }
    
        if (scrollZoomValueLabel != null)
        {
            scrollZoomValueLabel.Text =
                "Zoom: Belum Initialize";
        }
    
        scrollZoomFreezeTimer?.Stop();
    
        features?.ClearScrollZoom();
    
        scrollZoomCurrentValue = 0f;
    
        // -----------------------------------------------------
    
        SetNpcTargetControlsEnabled(
            value &&
            autoNpcCheck != null &&
            autoNpcCheck.Checked);
    
        if (!value)
        {
            ResetNpcAddressLabels();
        }
    }
}
