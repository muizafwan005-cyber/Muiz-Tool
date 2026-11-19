using System;
using System.Drawing;
using System.Windows.Forms;

namespace RFTool;

public sealed partial class MainForm
{
    private Label? privateAdvanceProcessStatusLabel;

    // Tab 7: kerangka awal Private Advance.
    // Fitur legacy belum dipindahkan; porting dilakukan setelah audit kompatibilitas.
    private void BuildTab7(TabPage tab)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(12),
            BackColor = BackColorMain
        };

        panel.Controls.Add(new Label
        {
            Text = "PRIVATE ADVANCE",
            AutoSize = true,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = ForeColorMain,
            Location = new Point(12, 12)
        });

        panel.Controls.Add(new Label
        {
            Text = "Modul fitur RF Private • terintegrasi dengan pengelolaan proses Muiz Tool",
            AutoSize = false,
            Location = new Point(12, 43),
            Size = new Size(720, 34),
            ForeColor = ForeColorDim
        });

        var processGroup = new GroupBox
        {
            Text = "PROCESS SESSION",
            Location = new Point(12, 86),
            Size = new Size(720, 92),
            ForeColor = ForeColorMain,
            BackColor = BackColorMain
        };

        privateAdvanceProcessStatusLabel = new Label
        {
            AutoSize = false,
            Location = new Point(14, 27),
            Size = new Size(680, 22),
            ForeColor = ForeColorDim,
            Text = "Status: belum attach"
        };
        processGroup.Controls.Add(privateAdvanceProcessStatusLabel);
        panel.Controls.Add(processGroup);

        var moduleGroup = new GroupBox
        {
            Text = "PRIVATE ADVANCE MODULE",
            Location = new Point(12, 190),
            Size = new Size(720, 142),
            ForeColor = ForeColorMain,
            BackColor = BackColorMain
        };

        moduleGroup.Controls.Add(new Label
        {
            AutoSize = false,
            Location = new Point(14, 27),
            Size = new Size(680, 48),
            ForeColor = Color.Goldenrod,
            Text = "Status: kerangka tab tersedia — fitur legacy belum diintegrasikan."
        });

        moduleGroup.Controls.Add(new Label
        {
            AutoSize = false,
            Location = new Point(14, 79),
            Size = new Size(680, 44),
            ForeColor = ForeColorDim,
            Text = "Fitur dari source KENTUYUK perlu diaudit untuk dependensi, resource, dan kompatibilitas sebelum dipindahkan."
        });
        panel.Controls.Add(moduleGroup);

        panel.Controls.Add(new Label
        {
            AutoSize = false,
            Location = new Point(12, 348),
            Size = new Size(720, 56),
            ForeColor = ForeColorDim,
            Text = "Catatan: modul ini tidak melakukan pencarian proses sendiri. Status sesi mengikuti proses yang dipilih dan di-attach melalui panel utama Muiz Tool."
        });

        tab.Controls.Add(panel);
        UpdatePrivateAdvanceStatus();
    }

    private void UpdatePrivateAdvanceStatus()
    {
        if (privateAdvanceProcessStatusLabel == null)
            return;

        if (selectedProcess == null || memory == null || features == null)
        {
            privateAdvanceProcessStatusLabel.Text = "Status: belum attach";
            privateAdvanceProcessStatusLabel.ForeColor = ForeColorDim;
            return;
        }

        try
        {
            if (selectedProcess.HasExited)
            {
                privateAdvanceProcessStatusLabel.Text = "Status: proses sudah berhenti";
                privateAdvanceProcessStatusLabel.ForeColor = Color.OrangeRed;
                return;
            }

            privateAdvanceProcessStatusLabel.Text =
                $"Status: terhubung ke {selectedProcess.ProcessName} (PID {selectedProcess.Id})";
            privateAdvanceProcessStatusLabel.ForeColor = Color.LightGreen;
        }
        catch (Exception ex)
        {
            privateAdvanceProcessStatusLabel.Text =
                $"Status: sesi tidak tersedia ({ex.Message})";
            privateAdvanceProcessStatusLabel.ForeColor = Color.OrangeRed;
        }
    }
}
