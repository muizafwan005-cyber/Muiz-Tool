using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using RFTool.PrivateAdvance.Config;

namespace RFTool;

public sealed partial class MainForm
{
    private const string PrivateAdvanceModuleName = "RF_Online.bin";
    private const long PrivateAdvanceBaseOffset = 0x01F8CFBC;

    private Label? privateAdvanceProcessStatusLabel;
    private Label? privateAdvanceBasePointerStatusLabel;
    private TextBox? privateAdvanceBasePointerTextBox;
    private Button? privateAdvanceSaveBasePointerButton;
    private bool privateAdvanceBasePointerSaved;
    private bool privateAdvanceBasePointerValidForSession;

    // Tab 7: konfigurasi Base Pointer terpisah dari Base Pointer fitur Muiz Tool.
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
            Text = "Modul fitur RF Private • menggunakan sesi proses Muiz Tool",
            AutoSize = false,
            Location = new Point(12, 43),
            Size = new Size(720, 34),
            ForeColor = ForeColorDim
        });

        var processGroup = new GroupBox
        {
            Text = "PROCESS SESSION",
            Location = new Point(12, 86),
            Size = new Size(720, 82),
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

        var pointerGroup = new GroupBox
        {
            Text = "PRIVATE ADVANCE BASE POINTER",
            Location = new Point(12, 180),
            Size = new Size(720, 150),
            ForeColor = ForeColorMain,
            BackColor = BackColorMain
        };

        pointerGroup.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(14, 27),
            ForeColor = ForeColorDim,
            Text = "Base Pointer (module + offset)"
        });

        privateAdvanceBasePointerTextBox = new TextBox
        {
            Location = new Point(14, 52),
            Size = new Size(500, 27),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Text = $"{PrivateAdvanceModuleName}+{PrivateAdvanceBaseOffset:X8}"
        };
        pointerGroup.Controls.Add(privateAdvanceBasePointerTextBox);

        privateAdvanceSaveBasePointerButton = new Button
        {
            Text = "SAVE",
            Location = new Point(526, 50),
            Size = new Size(165, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            UseVisualStyleBackColor = true
        };
        privateAdvanceSaveBasePointerButton.Click += (_, _) => SavePrivateAdvanceBasePointer();
        pointerGroup.Controls.Add(privateAdvanceSaveBasePointerButton);

        privateAdvanceBasePointerStatusLabel = new Label
        {
            AutoSize = false,
            Location = new Point(14, 91),
            Size = new Size(680, 42),
            ForeColor = Color.Goldenrod,
            Text = "Status: konfigurasi default belum disimpan. Attach game untuk validasi."
        };
        pointerGroup.Controls.Add(privateAdvanceBasePointerStatusLabel);
        panel.Controls.Add(pointerGroup);

        var moduleGroup = new GroupBox
        {
            Text = "PRIVATE ADVANCE MODULE",
            Location = new Point(12, 342),
            Size = new Size(720, 112),
            ForeColor = ForeColorMain,
            BackColor = BackColorMain
        };

        moduleGroup.Controls.Add(new Label
        {
            AutoSize = false,
            Location = new Point(14, 27),
            Size = new Size(680, 68),
            ForeColor = ForeColorDim,
            Text = "Fitur Private Advance tetap terkunci sampai Base Pointer dapat di-resolve dan alamatnya berhasil dibaca dari proses RF_Online yang sedang di-attach. Validasi ini hanya membaca memori; belum menjalankan fitur KENTUYUK."
        });
        panel.Controls.Add(moduleGroup);

        panel.Controls.Add(new Label
        {
            AutoSize = false,
            Location = new Point(12, 466),
            Size = new Size(720, 56),
            ForeColor = ForeColorDim,
            Text = "Catatan: Tab ini tidak mencari atau melakukan attach proses sendiri. Proses mengikuti pilihan dan sesi Attach Muiz Tool."
        });

        LoadPrivateAdvanceBasePointer();
        tab.Controls.Add(panel);
        UpdatePrivateAdvanceStatus();
    }

    private void LoadPrivateAdvanceBasePointer()
    {
        try
        {
            BasePointerSettings settings = BasePointerStore.Load();
            if (privateAdvanceBasePointerTextBox != null)
                privateAdvanceBasePointerTextBox.Text = $"{settings.ModuleName}+{settings.Offset}";

            privateAdvanceBasePointerSaved = BasePointerStore.Exists;
            SetPrivateAdvancePointerStatus(
                privateAdvanceBasePointerSaved
                    ? "Status: konfigurasi Base Pointer dimuat dari penyimpanan lokal."
                    : "Status: Base Pointer default terisi. Tekan SAVE untuk menyimpannya.",
                Color.Goldenrod);
        }
        catch (Exception ex)
        {
            privateAdvanceBasePointerSaved = false;
            SetPrivateAdvancePointerStatus($"Status: gagal memuat konfigurasi ({ex.Message}).", Color.OrangeRed);
        }
    }

    private void SavePrivateAdvanceBasePointer()
    {
        if (privateAdvanceBasePointerTextBox == null)
            return;

        if (!TryParsePrivateAdvanceBasePointer(privateAdvanceBasePointerTextBox.Text, out long offset))
        {
            privateAdvanceBasePointerSaved = false;
            privateAdvanceBasePointerValidForSession = false;
            SetPrivateAdvancePointerStatus("Format tidak valid. Gunakan RF_Online.bin+01F8CFBC.", Color.OrangeRed);
            return;
        }

        try
        {
            BasePointerStore.Save(new BasePointerSettings
            {
                ModuleName = PrivateAdvanceModuleName,
                Offset = offset.ToString("X8", CultureInfo.InvariantCulture)
            });

            privateAdvanceBasePointerTextBox.Text = $"{PrivateAdvanceModuleName}+{offset:X8}";
            privateAdvanceBasePointerSaved = true;
            privateAdvanceBasePointerValidForSession = false;

            if (selectedProcess != null && memory != null)
                ValidatePrivateAdvanceBasePointer();
            else
                SetPrivateAdvancePointerStatus("Status: Base Pointer berhasil disimpan. Attach RF_Online untuk validasi.", Color.Goldenrod);
        }
        catch (Exception ex)
        {
            privateAdvanceBasePointerSaved = false;
            SetPrivateAdvancePointerStatus($"Status: gagal menyimpan Base Pointer ({ex.Message}).", Color.OrangeRed);
        }
    }

    private static bool TryParsePrivateAdvanceBasePointer(string? value, out long offset)
    {
        offset = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value.Trim().Replace(" ", string.Empty);
        string prefix = PrivateAdvanceModuleName + "+";
        if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string hex = normalized.Substring(prefix.Length);
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Substring(2);

        return hex.Length is > 0 and <= 8 &&
               long.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offset) &&
               offset > 0;
    }

    private void ValidatePrivateAdvanceBasePointer()
    {
        privateAdvanceBasePointerValidForSession = false;

        if (!privateAdvanceBasePointerSaved || selectedProcess == null || memory == null)
        {
            SetPrivateAdvancePointerStatus("Status: belum siap — simpan Base Pointer dan attach RF_Online.", Color.Goldenrod);
            return;
        }

        try
        {
            if (selectedProcess.HasExited ||
                !selectedProcess.ProcessName.Equals("RF_Online", StringComparison.OrdinalIgnoreCase))
            {
                SetPrivateAdvancePointerStatus("Status: proses aktif bukan RF_Online.", Color.OrangeRed);
                return;
            }

            if (!TryParsePrivateAdvanceBasePointer(privateAdvanceBasePointerTextBox?.Text, out long offset))
            {
                SetPrivateAdvancePointerStatus("Status: format Base Pointer tidak valid.", Color.OrangeRed);
                return;
            }

            IntPtr resolvedAddress = new(memory.ModuleBase.ToInt64() + offset);
            _ = memory.ReadByte(resolvedAddress); // Validasi read-only: pastikan alamat dapat dibaca.

            privateAdvanceBasePointerValidForSession = true;
            SetPrivateAdvancePointerStatus(
                $"Status: pemeriksaan dasar lolos pada PID {selectedProcess.Id}; alamat dapat dibaca. Pointer chain belum diverifikasi.",
                Color.LightGreen);
        }
        catch (Exception ex)
        {
            SetPrivateAdvancePointerStatus($"Status: validasi gagal ({ex.Message}).", Color.OrangeRed);
        }
    }

    private void SetPrivateAdvancePointerStatus(string message, Color color)
    {
        if (privateAdvanceBasePointerStatusLabel == null)
            return;

        privateAdvanceBasePointerStatusLabel.Text = message;
        privateAdvanceBasePointerStatusLabel.ForeColor = color;
    }

    private void UpdatePrivateAdvanceStatus()
    {
        if (privateAdvanceProcessStatusLabel == null)
            return;

        if (selectedProcess == null || memory == null || features == null)
        {
            privateAdvanceProcessStatusLabel.Text = "Status: belum attach";
            privateAdvanceProcessStatusLabel.ForeColor = ForeColorDim;
            privateAdvanceBasePointerValidForSession = false;
            if (privateAdvanceBasePointerSaved)
                SetPrivateAdvancePointerStatus("Status: konfigurasi tersimpan. Attach RF_Online untuk validasi.", Color.Goldenrod);
            return;
        }

        try
        {
            if (selectedProcess.HasExited)
            {
                privateAdvanceProcessStatusLabel.Text = "Status: proses sudah berhenti";
                privateAdvanceProcessStatusLabel.ForeColor = Color.OrangeRed;
                privateAdvanceBasePointerValidForSession = false;
                SetPrivateAdvancePointerStatus("Status: proses berhenti; validasi sesi dibatalkan.", Color.OrangeRed);
                return;
            }

            privateAdvanceProcessStatusLabel.Text =
                $"Status: terhubung ke {selectedProcess.ProcessName} (PID {selectedProcess.Id})";
            privateAdvanceProcessStatusLabel.ForeColor = Color.LightGreen;
            ValidatePrivateAdvanceBasePointer();
        }
        catch (Exception ex)
        {
            privateAdvanceProcessStatusLabel.Text = $"Status: sesi tidak tersedia ({ex.Message})";
            privateAdvanceProcessStatusLabel.ForeColor = Color.OrangeRed;
            privateAdvanceBasePointerValidForSession = false;
        }
    }
}
