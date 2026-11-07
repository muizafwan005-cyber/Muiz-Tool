
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using RFTool.AdvancedSystem;

namespace RFTool;

public sealed partial class MainForm
{
    // =========================================================
    // ADDRESS MANAGER PAGE
    // =========================================================

    private DataGridView addressGrid = null!;

    private void BuildAddressManagerPage()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = BackColorMain,
            Padding = new Padding(0, 8, 0, 0)
        };

        layout.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 42));

        layout.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100));

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
        
        addressGrid.DataSource = addressManager.Entries;
        
        layout.Controls.Add(addressGrid, 0, 1);

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
            Width = 72,
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
        using var dialog = new Form
        {
            Text = existing == null
                ? "Add Address"
                : "Edit Address",

            Size = new Size(400, 370),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(30, 30, 35),
            ForeColor = Color.White
        };

        Label CreateLabel(string text, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(15, y + 4),
                Size = new Size(100, 22),
                ForeColor = Color.White
            };
        }

        TextBox CreateTextBox(string value, int y)
        {
            return new TextBox
            {
                Text = value,
                Location = new Point(120, y),
                Size = new Size(245, 24)
            };
        }

        dialog.Controls.Add(CreateLabel("Description", 20));

        var descriptionBox = CreateTextBox(
            existing?.Description ?? "",
            20);

        dialog.Controls.Add(descriptionBox);

        dialog.Controls.Add(CreateLabel("Address", 60));

        var addressBox = CreateTextBox(
            existing?.Address ?? "",
            60);

        dialog.Controls.Add(addressBox);

        dialog.Controls.Add(CreateLabel("Value Type", 100));

        var typeBox = new ComboBox
        {
            Location = new Point(120, 100),
            Size = new Size(245, 24),
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        typeBox.Items.AddRange(new object[]
        {
            "Byte",
            "2 Bytes",
            "4 Bytes",
            "Float",
            "Double",
            "String"
        });

        typeBox.SelectedItem =
            existing?.ValueType ?? "4 Bytes";

        if (typeBox.SelectedIndex < 0)
            typeBox.SelectedIndex = 2;

        dialog.Controls.Add(typeBox);

        dialog.Controls.Add(CreateLabel("Value", 140));
        
        var valueBox = CreateTextBox(
            existing?.Value ?? "0",
            140);
        
        dialog.Controls.Add(valueBox);
        
        dialog.Controls.Add(CreateLabel("Group", 180));
        
        var groupBox = CreateTextBox(
            existing?.Group ?? "Default",
            180);
        
        dialog.Controls.Add(groupBox);
        
        var saveButton = new Button
        {
            Text = "SAVE",
            Location = new Point(190, 250),
            Size = new Size(80, 32),
            DialogResult = DialogResult.OK
        };

        var cancelButton = new Button
        {
            Text = "CANCEL",
            Location = new Point(280, 250),
            Size = new Size(80, 32),
            DialogResult = DialogResult.Cancel
        };

        dialog.Controls.Add(saveButton);
        dialog.Controls.Add(cancelButton);

        dialog.AcceptButton = saveButton;
        dialog.CancelButton = cancelButton;

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return null;

        if (string.IsNullOrWhiteSpace(
            descriptionBox.Text))
        {
            MessageBox.Show("Description wajib diisi.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(addressBox.Text))
        {
            MessageBox.Show("Address wajib diisi.");
            return null;
        }

        return new AddressEntry
        {
            Description = descriptionBox.Text.Trim(),
            Address = addressBox.Text.Trim(),
            ValueType = typeBox.Text,
            Value = valueBox.Text.Trim(),
            Group = string.IsNullOrWhiteSpace(groupBox.Text)
                ? "Default"
                : groupBox.Text.Trim(),
            Freeze = existing?.Freeze ?? false
        };
    }
}
