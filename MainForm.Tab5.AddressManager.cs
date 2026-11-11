using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using RFTool.Advanced;
using RFTool.AdvancedSystem;

namespace RFTool;

public sealed partial class MainForm
{
    // =========================================================
    // ADDRESS MANAGER PAGE
    // =========================================================

    private DataGridView addressGrid = null!;
    private Label addressInfoLabel = null!;
    private System.Windows.Forms.Timer? addressInfoTimer;

    private void BuildAddressManagerPage()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = BackColorMain,
            Padding = new Padding(0, 8, 0, 0)
        };

        layout.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 42));

        layout.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100));

        layout.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 46));

        advancedPageHost.Controls.Add(layout);

        // =====================================================
        // TOOLBAR
        // =====================================================

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = BackColorMain,
            Margin = Padding.Empty
        };

        layout.Controls.Add(toolbar, 0, 0);

        AddAddressButton(toolbar, "ADD", AddAddress);
        AddAddressButton(toolbar, "EDIT", EditAddress);
        AddAddressButton(toolbar, "REMOVE", RemoveAddress);
        AddAddressButton(toolbar, "WRITE", WriteSelectedAddress);
        AddAddressButton(toolbar, "READ", ReadSelectedAddress);
        AddAddressButton(toolbar, "BROWSE", BrowseSelectedAddress);
        AddAddressButton(toolbar, "SAVE", SaveAddresses);
        AddAddressButton(toolbar, "LOAD", LoadAddresses);

        // =====================================================
        // ADDRESS GRID
        // =====================================================

        addressGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.FromArgb(25, 25, 30),
            ForeColor = Color.Black,
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            ReadOnly = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            AutoGenerateColumns = false
        };
        
        addressGrid.EnableHeadersVisualStyles = false;
        
        addressGrid.ColumnHeadersDefaultCellStyle.BackColor =
            Color.FromArgb(40, 40, 48);
        
        addressGrid.ColumnHeadersDefaultCellStyle.ForeColor =
            Color.White;
        
        addressGrid.DefaultCellStyle.SelectionBackColor =
            Color.FromArgb(45, 75, 60);
        
        addressGrid.DefaultCellStyle.SelectionForeColor =
            Color.White;
        
        // TEXT COLUMNS
        addressGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Description",
            HeaderText = "Description",
            DataPropertyName = "Description",
            ReadOnly = true,
            FillWeight = 120
        });
        
        addressGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Address",
            HeaderText = "Address",
            DataPropertyName = "Address",
            ReadOnly = true,
            FillWeight = 110
        });
        
        addressGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ValueType",
            HeaderText = "Type",
            DataPropertyName = "ValueType",
            ReadOnly = true,
            FillWeight = 75
        });
        
        addressGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Value",
            HeaderText = "Value",
            DataPropertyName = "Value",
            ReadOnly = true,
            FillWeight = 75
        });
        
        addressGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Group",
            HeaderText = "Group",
            DataPropertyName = "Group",
            ReadOnly = true,
            FillWeight = 85
        });
        
        // FREEZE CHECKBOX
        addressGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Freeze",
            HeaderText = "Freeze",
            DataPropertyName = "Freeze",
            ReadOnly = false,
            FillWeight = 55
        });
        
        // Commit checkbox immediately after click
        addressGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (addressGrid.IsCurrentCellDirty)
            {
                addressGrid.CommitEdit(
                    DataGridViewDataErrorContexts.Commit);
            }
        };
        
        addressGrid.CellValueChanged += AddressGrid_CellValueChanged;

        addressGrid.DataError += (_, e) => e.ThrowException = false;

        BuildAddressGridMenu();

        addressGrid.DataSource = addressManager.Entries;
        
        layout.Controls.Add(addressGrid, 0, 1);

        addressInfoLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = ForeColorDim,
            Text = "Centang Freeze untuk mempertahankan Value. " +
                   "Address boleh hex, module+offset, atau pointer seperti " +
                   "[RF_Online.bin+C28B40]+10."
        };

        layout.Controls.Add(addressInfoLabel, 0, 2);

        EnsureAddressInfoTimer();
    }
    // =========================================================
    // TOOLBAR BUTTON
    // =========================================================

    private void AddAddressButton(
        FlowLayoutPanel toolbar,
        string text,
        Action action)
    {
        var button = new Button
        {
            Text = text,
            Width = 66,
            Height = 30,
            Margin = new Padding(2, 3, 2, 2),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(40, 45, 50),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };

        button.FlatAppearance.BorderSize = 0;

        button.Click += (_, _) => action();

        toolbar.Controls.Add(button);
    }

    // =========================================================
    // ADD ADDRESS
    // =========================================================

    private void AddAddress()
    {
        var entry = ShowAddressEditor(null);

        if (entry == null)
            return;

        addressManager.Add(entry);

        AdvSvc.Freeze.Refresh();
    }

    // =========================================================
    // EDIT ADDRESS
    // =========================================================

    private void EditAddress()
    {
        int index = addressGrid.CurrentRow?.Index ?? -1;

        if (index < 0 ||
            index >= addressManager.Entries.Count)
        {
            MessageBox.Show("Pilih alamat terlebih dahulu.");
            return;
        }

        var entry = ShowAddressEditor(
            addressManager.Entries[index]);

        if (entry == null)
            return;

        addressManager.Edit(index, entry);

        AdvSvc.Freeze.Refresh();
    }

    // =========================================================
    // REMOVE ADDRESS
    // =========================================================

    private void RemoveAddress()
    {
        int index = addressGrid.CurrentRow?.Index ?? -1;

        if (index < 0 ||
            index >= addressManager.Entries.Count)
        {
            MessageBox.Show("Pilih alamat terlebih dahulu.");
            return;
        }

        var result = MessageBox.Show(
            "Hapus alamat yang dipilih?",
            "Address Manager",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
            return;

        addressManager.Remove(index);

        AdvSvc.Freeze.Refresh();
    }

    // =========================================================
    // SAVE JSON
    // =========================================================

    private void SaveAddresses()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            DefaultExt = "json",
            FileName = "addresses.json"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        try
        {
            addressManager.Save(dialog.FileName);

            MessageBox.Show(
                "Address berhasil disimpan.",
                "Address Manager");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Save Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    // =========================================================
    // LOAD JSON
    // =========================================================

    private void LoadAddresses()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        try
        {
            addressManager.Load(dialog.FileName);

            addressGrid.DataSource =
                addressManager.Entries;

            AdvSvc.Freeze.Refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Load Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    // =========================================================
    // ADDRESS EDITOR
    // =========================================================

    private AddressEntry? ShowAddressEditor(
        AddressEntry? existing)
    {
        var draft = new AddressEntry
        {
            Description = existing?.Description ?? "No description",
            Address = existing?.Address ?? "",
            ValueType = existing?.ValueType ?? "4 Bytes",
            Value = existing?.Value ?? "0",
            Group = existing?.Group ?? "Default",
            Hex = existing?.Hex ?? false,
            Signed = existing?.Signed ?? true
        };

        if (!ChangeAddressDialog.Edit(
                this,
                existing == null ? "Add address" : "Change address",
                draft,
                AdvSvc.Target))
        {
            return null;
        }

        draft.Freeze = existing?.Freeze ?? false;
        return draft;
    }

    // =========================================================
    // SELECTED ENTRY
    // =========================================================

    private AddressEntry? SelectedAddressEntry()
    {
        int index = addressGrid?.CurrentRow?.Index ?? -1;

        if (index < 0 || index >= addressManager.Entries.Count)
            return null;

        return addressManager.Entries[index];
    }

    // =========================================================
    // WRITE / READ SATU KALI
    // =========================================================

    private void WriteSelectedAddress()
    {
        AddressEntry? entry = SelectedAddressEntry();

        if (entry == null)
        {
            MessageBox.Show("Pilih alamat terlebih dahulu.");
            return;
        }

        bool ok = EntryIO.TryWrite(
            AdvSvc.Target,
            entry,
            out string message);

        addressInfoLabel.Text =
            (ok ? "Write: " : "Write gagal: ") + message;
    }

    private void ReadSelectedAddress()
    {
        AddressEntry? entry = SelectedAddressEntry();

        if (entry == null)
        {
            MessageBox.Show("Pilih alamat terlebih dahulu.");
            return;
        }

        if (EntryIO.TryRead(
                AdvSvc.Target,
                entry,
                out string text,
                out string message))
        {
            entry.Value = text;
            AdvSvc.Freeze.Refresh();
            addressInfoLabel.Text = "Read: " + text + "  (" + message + ")";
        }
        else
        {
            addressInfoLabel.Text = "Read gagal: " + message;
        }
    }

    // =========================================================
    // FREEZE (CHECKBOX)
    // =========================================================

    private void AddressGrid_CellValueChanged(
        object? sender,
        DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.RowIndex >= addressManager.Entries.Count)
        {
            return;
        }

        if (addressGrid.Columns[e.ColumnIndex].Name != "Freeze")
            return;

        AddressEntry entry = addressManager.Entries[e.RowIndex];

        if (entry.Freeze &&
            !ValidateForFreeze(entry, out string problem))
        {
            addressInfoLabel.Text = "Freeze dibatalkan: " + problem;

            BeginInvoke(new Action(() =>
            {
                entry.Freeze = false;
                AdvSvc.Freeze.Refresh();
            }));

            return;
        }

        AdvSvc.Freeze.Refresh();
    }

    private bool ValidateForFreeze(
        AddressEntry entry,
        out string problem)
    {
        ScanValueType type =
            ValueCodec.TypeFromName(entry.ValueType);

        if (!ValueCodec.TryParse(
                entry.Value,
                type,
                entry.Hex,
                false,
                out _,
                out string parseError))
        {
            problem = "Value tidak valid (" + parseError + ")";
            return false;
        }

        TargetProcess? target = AdvSvc.Target.Get();

        if (target == null)
        {
            problem = "belum attach ke process.";
            return false;
        }

        if (!EntryIO.TryResolve(
                target,
                entry,
                out _,
                out string addressError))
        {
            problem = "alamat tidak bisa di-resolve (" + addressError + ")";
            return false;
        }

        problem = "";
        return true;
    }

    // =========================================================
    // INFO NILAI SAAT INI (BARIS BAWAH)
    // =========================================================

    private void EnsureAddressInfoTimer()
    {
        if (addressInfoTimer != null)
            return;

        addressInfoTimer =
            new System.Windows.Forms.Timer
            {
                Interval = 600
            };

        addressInfoTimer.Tick +=
            (_, _) => UpdateAddressInfo();

        addressInfoTimer.Start();
    }

    private void UpdateAddressInfo()
    {
        if (addressGrid == null ||
            addressGrid.IsDisposed ||
            addressInfoLabel == null ||
            addressInfoLabel.IsDisposed ||
            !addressGrid.Visible)
        {
            return;
        }

        AddressEntry? entry = SelectedAddressEntry();

        if (entry == null)
            return;

        string status =
            entry.Status.Length > 0
                ? "   |   " + entry.Status
                : "";

        if (EntryIO.TryRead(
                AdvSvc.Target,
                entry,
                out string text,
                out string message))
        {
            addressInfoLabel.Text =
                "Nilai saat ini: " + text + "   (" + message + ")" + status;
        }
        else
        {
            addressInfoLabel.Text = message + status;
        }
    }

    // =========================================================
    // BROWSE + MENU KLIK KANAN
    // =========================================================

    private void BrowseSelectedAddress()
    {
        AddressEntry? entry = SelectedAddressEntry();

        if (entry == null)
        {
            MessageBox.Show("Pilih alamat terlebih dahulu.");
            return;
        }

        BrowseInMemoryViewer(entry.Address);
    }

    private void BuildAddressGridMenu()
    {
        ContextMenuStrip menu = AdvTheme.NewMenu();

        menu.Items.Add(AdvTheme.Item(
            "Browse this memory region",
            BrowseSelectedAddress,
            Keys.Control | Keys.B,
            true));

        menu.Items.Add(AdvTheme.Item(
            "Disassemble this memory region",
            DisassembleSelectedAddress,
            Keys.Control | Keys.D));

        menu.Items.Add(AdvTheme.Item(
            "Pointer scan for this address",
            PointerScanSelectedAddress,
            Keys.Control | Keys.P));

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(AdvTheme.Item("Write value now", WriteSelectedAddress));
        menu.Items.Add(AdvTheme.Item("Read current value", ReadSelectedAddress));
        menu.Items.Add(AdvTheme.Item("Edit address", EditAddress, Keys.Control | Keys.E));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(AdvTheme.Item("Remove selected address", RemoveAddress, Keys.Control | Keys.Delete));

        addressGrid.ContextMenuStrip = menu;

        addressGrid.CellMouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0)
                return;

            addressGrid.ClearSelection();
            addressGrid.Rows[e.RowIndex].Selected = true;

            addressGrid.CurrentCell =
                addressGrid.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
        };
    }

    private void DisassembleSelectedAddress()
    {
        AddressEntry? entry = SelectedAddressEntry();

        if (entry == null)
        {
            MessageBox.Show("Pilih alamat terlebih dahulu.");
            return;
        }

        DisassembleInCodeAnalyzer(entry.Address);
    }

    private void PointerScanSelectedAddress()
    {
        AddressEntry? entry = SelectedAddressEntry();

        if (entry == null)
        {
            MessageBox.Show("Pilih alamat terlebih dahulu.");
            return;
        }

        PointerScanFor(entry.Address);
    }
}
