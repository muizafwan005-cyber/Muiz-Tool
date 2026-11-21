using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;
using RFTool.Advanced;
using RFTool.AdvancedSystem;

namespace RFTool;

public sealed partial class MainForm
{
    // =========================================================
    // NILAI LIVE (REAL TIME) DI ADDRESS MANAGER
    // Kolom "Value" = nilai yang sedang ada di memori game.
    // Kolom "Target" = nilai tujuan untuk WRITE / FREEZE.
    // =========================================================

    private System.Windows.Forms.Timer? addressLiveTimer;

    private void EnsureAddressLiveTimer()
    {
        if (addressLiveTimer != null)
            return;

        addressLiveTimer = new System.Windows.Forms.Timer
        {
            Interval = 250
        };

        addressLiveTimer.Tick += (_, _) => UpdateAddressLiveValues();
        addressLiveTimer.Start();
    }

    private void UpdateAddressLiveValues()
    {
        if (addressGrid == null ||
            addressGrid.IsDisposed ||
            !addressGrid.Visible ||
            addressGrid.Columns["Live"] == null)
        {
            return;
        }

        ApplyGroupVisibility();

        int count = Math.Min(addressGrid.Rows.Count, addressManager.Entries.Count);

        if (count == 0)
            return;

        // Hanya baris yang terlihat di layar yang dibaca
        int first = Math.Max(0, addressGrid.FirstDisplayedScrollingRowIndex);
        int budget = addressGrid.DisplayedRowCount(true) + 1;

        TargetProcess? target = AdvSvc.Target.Get();

        for (int i = first; i < count && budget > 0; i++)
        {
            if (!addressGrid.Rows[i].Visible)
                continue;

            budget--;

            AddressEntry entry = addressManager.Entries[i];
            string text;

            if (entry.IsScript)
            {
                text = AdvSvc.IsAaActive(entry.ScriptKey) ? "ENABLED" : "disabled";
            }
            else if (target == null)
            {
                UpdatePointerDisplay(entry, i, null);
                text = "??";
            }
            else
            {
                bool resolved = EntryIO.TryResolve(target, entry, out ulong address, out _);

                UpdatePointerDisplay(entry, i, resolved ? target.FormatAddress(address) : null);

                text = resolved ? ReadLiveValue(target, entry, address) : "??";
            }

            DataGridViewCell hotkeyCell = addressGrid.Rows[i].Cells["Hotkey"];
            string hotkeyText = HotkeySummary(entry);

            if (!Equals(hotkeyCell.Value, hotkeyText))
                hotkeyCell.Value = hotkeyText;

            DataGridViewCell cell = addressGrid.Rows[i].Cells["Live"];

            if (!Equals(cell.Value, text))
                cell.Value = text;
        }
    }

    private static string ReadLiveValue(TargetProcess target, AddressEntry entry, ulong address)
    {
        try
        {
            ScanValueType type = ValueCodec.TypeFromName(entry.ValueType);

            if (type == ScanValueType.String)
                return ReadLiveString(target, address, entry);

            byte[]? data = target.ReadBytes(address, EntryIO.ValueSize(entry));

            if (data == null)
                return "??";

            return ValueCodec.Format(data, type, false, entry.Signed, entry.Hex);
        }
        catch
        {
            return "??";
        }
    }

    // Panjang string tidak diketahui (Value hanya teks target), jadi baca blok
    // tetap lalu potong di karakter NUL. Cocok untuk Nickname.
    private static string ReadLiveString(TargetProcess target, ulong address, AddressEntry entry)
    {
        int want = Math.Clamp(Math.Max(entry.Value?.Length ?? 0, 64), 16, 256);

        byte[]? data = null;

        // Kalau blok besar melewati batas halaman memori, coba lebih kecil
        for (int size = want; size >= 4 && data == null; size /= 2)
            data = target.ReadBytes(address, size);

        if (data == null)
            return "??";

        // Deteksi UTF-16 (huruf, 00, huruf, 00) otomatis
        bool utf16 = data.Length >= 4 &&
                     data[0] != 0 && data[1] == 0 &&
                     data[2] != 0 && data[3] == 0;

        string text = utf16
            ? Encoding.Unicode.GetString(data)
            : Encoding.Latin1.GetString(data);

        int nul = text.IndexOf('\0');

        return nul >= 0 ? text.Substring(0, nul) : text;
    }

    // =========================================================
    // TAMPILAN KOLOM ADDRESS (seperti Cheat Engine)
    // Pointer  -> "P->alamat tujuan" (di-resolve live)
    // Lainnya  -> teks address apa adanya
    // Teks asli (ekspresi pointer) tampil sebagai tooltip.
    // =========================================================

    private sealed class AddressDisplay
    {
        public string Expression = "";
        public bool IsPointer;
        public string Text = "";
    }

    private readonly ConditionalWeakTable<AddressEntry, AddressDisplay> addressDisplays = new();

    private AddressDisplay GetAddressDisplay(AddressEntry entry)
    {
        AddressDisplay d = addressDisplays.GetOrCreateValue(entry);
        string expression = entry.Address ?? "";

        if (d.Text.Length == 0 || d.Expression != expression)
        {
            d.Expression = expression;
            d.IsPointer = PointerExpression.TryParse(expression, out _, out _);
            d.Text = d.IsPointer ? "P->????????" : expression;
        }

        return d;
    }

    private void UpdatePointerDisplay(AddressEntry entry, int rowIndex, string? resolvedHex)
    {
        AddressDisplay d = GetAddressDisplay(entry);

        if (!d.IsPointer)
            return;

        string text = "P->" + (resolvedHex ?? "????????");

        if (d.Text == text)
            return;

        d.Text = text;

        DataGridViewColumn? column = addressGrid.Columns["Address"];

        if (column != null)
            addressGrid.InvalidateCell(column.Index, rowIndex);
    }

    private void AddressGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.RowIndex >= addressManager.Entries.Count ||
            addressGrid.Columns[e.ColumnIndex].Name != "Address")
        {
            return;
        }

        AddressEntry entry = addressManager.Entries[e.RowIndex];

        if (entry.IsScript)
            return;

        AddressDisplay d = GetAddressDisplay(entry);

        if (d.IsPointer)
        {
            e.Value = d.Text;
            e.FormattingApplied = true;
        }
    }

    private void AddressGrid_CellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.RowIndex >= addressManager.Entries.Count ||
            addressGrid.Columns[e.ColumnIndex].Name != "Address")
        {
            return;
        }

        AddressEntry entry = addressManager.Entries[e.RowIndex];

        if (!entry.IsScript)
            e.ToolTipText = entry.Address;
    }
}
