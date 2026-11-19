
using System;
using System.Globalization;
using System.Windows.Forms;
using System.Drawing;
using RFTool.PrivateAdvance.Config;

namespace RFTool;

public sealed partial class MainForm
{
    private const string PrivateAdvanceModuleName = "RF_Online.bin";
    private const long PrivateAdvanceBaseOffset = 0x01F8CFBC;

    private Label? privateAdvanceBasePointerStatusLabel;
    private TextBox? privateAdvanceBasePointerTextBox;
    private Button? privateAdvanceSaveBasePointerButton;

    private bool privateAdvanceBasePointerSaved;
    private bool privateAdvanceBasePointerValidForSession;

    // Tab 7 hanya menyediakan area kosong untuk fitur berikutnya.
    private void BuildTab7(TabPage tab)
    {
        LoadPrivateAdvanceBasePointer();

        tab.Controls.Add(new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = BackColorMain
        });

        UpdatePrivateAdvanceStatus();
    }

    private void LoadPrivateAdvanceBasePointer()
    {
        try
        {
            BasePointerSettings settings = BasePointerStore.Load();

            if (privateAdvanceBasePointerTextBox != null)
            {
                privateAdvanceBasePointerTextBox.Text =
                    $"{settings.ModuleName}+{settings.Offset}";
            }

            privateAdvanceBasePointerSaved = BasePointerStore.Exists;

            SetPrivateAdvancePointerStatus(
                privateAdvanceBasePointerSaved
                    ? "Status: Base Pointer berhasil dimuat."
                    : "Status: Base Pointer belum disimpan. Tekan SAVE.",
                Color.Goldenrod);
        }
        catch (Exception ex)
        {
            privateAdvanceBasePointerSaved = false;

            SetPrivateAdvancePointerStatus(
                $"Status: gagal memuat konfigurasi ({ex.Message}).",
                Color.OrangeRed);
        }
    }

    private void SavePrivateAdvanceBasePointer()
    {
        if (privateAdvanceBasePointerTextBox == null)
            return;

        if (!TryParsePrivateAdvanceBasePointer(
                privateAdvanceBasePointerTextBox.Text,
                out long offset))
        {
            privateAdvanceBasePointerSaved = false;
            privateAdvanceBasePointerValidForSession = false;

            SetPrivateAdvancePointerStatus(
                "Format tidak valid. Contoh: RF_Online.bin+01F8CFBC.",
                Color.OrangeRed);

            return;
        }

        try
        {
            BasePointerStore.Save(new BasePointerSettings
            {
                ModuleName = PrivateAdvanceModuleName,
                Offset = offset.ToString(
                    "X8",
                    CultureInfo.InvariantCulture)
            });

            privateAdvanceBasePointerTextBox.Text =
                $"{PrivateAdvanceModuleName}+{offset:X8}";

            privateAdvanceBasePointerSaved = true;
            privateAdvanceBasePointerValidForSession = false;

            if (selectedProcess != null && memory != null)
                ValidatePrivateAdvanceBasePointer();
            else
                SetPrivateAdvancePointerStatus(
                    "Status: Base Pointer berhasil disimpan. Attach RF_Online untuk validasi.",
                    Color.Goldenrod);
        }
        catch (Exception ex)
        {
            privateAdvanceBasePointerSaved = false;

            SetPrivateAdvancePointerStatus(
                $"Status: gagal menyimpan Base Pointer ({ex.Message}).",
                Color.OrangeRed);
        }
    }

    private static bool TryParsePrivateAdvanceBasePointer(
        string? value,
        out long offset)
    {
        offset = 0;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value.Trim().Replace(" ", string.Empty);
        string prefix = PrivateAdvanceModuleName + "+";

        if (!normalized.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
            return false;

        string hex = normalized.Substring(prefix.Length);

        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Substring(2);

        return hex.Length is > 0 and <= 8 &&
               long.TryParse(
                   hex,
                   NumberStyles.HexNumber,
                   CultureInfo.InvariantCulture,
                   out offset) &&
               offset > 0;
    }

    private void ValidatePrivateAdvanceBasePointer()
    {
        privateAdvanceBasePointerValidForSession = false;

        if (!privateAdvanceBasePointerSaved ||
            selectedProcess == null ||
            memory == null)
        {
            SetPrivateAdvancePointerStatus(
                "Status: belum siap. Simpan Base Pointer dan Attach RF_Online.",
                Color.Goldenrod);

            return;
        }

        try
        {
            if (selectedProcess.HasExited ||
                !selectedProcess.ProcessName.Equals(
                    "RF_Online",
                    StringComparison.OrdinalIgnoreCase))
            {
                SetPrivateAdvancePointerStatus(
                    "Status: proses aktif bukan RF_Online.",
                    Color.OrangeRed);

                return;
            }

            if (!TryParsePrivateAdvanceBasePointer(
                    privateAdvanceBasePointerTextBox?.Text,
                    out long offset))
            {
                SetPrivateAdvancePointerStatus(
                    "Status: format Base Pointer tidak valid.",
                    Color.OrangeRed);

                return;
            }

            IntPtr resolvedAddress = new(
                memory.ModuleBase.ToInt64() + offset);

            // Validasi read-only.
            _ = memory.ReadByte(resolvedAddress);

            privateAdvanceBasePointerValidForSession = true;

            SetPrivateAdvancePointerStatus(
                $"Status: valid pada PID {selectedProcess.Id}. Alamat dapat dibaca.",
                Color.LightGreen);
        }
        catch (Exception ex)
        {
            SetPrivateAdvancePointerStatus(
                $"Status: validasi gagal ({ex.Message}).",
                Color.OrangeRed);
        }
    }

    private void SetPrivateAdvancePointerStatus(
        string message,
        Color color)
    {
        if (privateAdvanceBasePointerStatusLabel == null)
            return;

        privateAdvanceBasePointerStatusLabel.Text = message;
        privateAdvanceBasePointerStatusLabel.ForeColor = color;
    }

    private void UpdatePrivateAdvanceStatus()
    {
        if (selectedProcess == null ||
            memory == null ||
            features == null)
        {
            privateAdvanceBasePointerValidForSession = false;

            if (privateAdvanceBasePointerSaved)
            {
                SetPrivateAdvancePointerStatus(
                    "Status: konfigurasi tersimpan. Attach RF_Online untuk validasi.",
                    Color.Goldenrod);
            }

            return;
        }

        try
        {
            if (selectedProcess.HasExited)
            {
                privateAdvanceBasePointerValidForSession = false;

                SetPrivateAdvancePointerStatus(
                    "Status: proses berhenti. Validasi sesi dibatalkan.",
                    Color.OrangeRed);

                return;
            }

            ValidatePrivateAdvanceBasePointer();
        }
        catch (Exception ex)
        {
            privateAdvanceBasePointerValidForSession = false;

            SetPrivateAdvancePointerStatus(
                $"Status: sesi tidak tersedia ({ex.Message}).",
                Color.OrangeRed);
        }
    }
}
