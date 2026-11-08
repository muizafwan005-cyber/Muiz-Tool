using System;
using System.Drawing;
using System.Windows.Forms;

namespace RFTool.Advanced;

/// <summary>Warna dan pembuat kontrol bertema gelap untuk semua panel Advanced.</summary>
internal static class AdvTheme
{
    public static readonly Color Back = Color.FromArgb(25, 25, 28);
    public static readonly Color Side = Color.FromArgb(32, 32, 36);
    public static readonly Color Input = Color.FromArgb(42, 42, 47);
    public static readonly Color Hover = Color.FromArgb(58, 58, 64);
    public static readonly Color Line = Color.FromArgb(62, 62, 68);
    public static readonly Color Fore = Color.FromArgb(235, 235, 235);
    public static readonly Color Dim = Color.FromArgb(170, 170, 175);
    public static readonly Color Accent = Color.FromArgb(0, 168, 132);
    public static readonly Color SelectBack = Color.FromArgb(0, 110, 90);

    public static readonly Font Mono = new Font("Consolas", 9.5F);

    private sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Side;
        public override Color ImageMarginGradientBegin => Side;
        public override Color ImageMarginGradientMiddle => Side;
        public override Color ImageMarginGradientEnd => Side;
        public override Color MenuBorder => Line;
        public override Color MenuItemBorder => SelectBack;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemPressedGradientBegin => Hover;
        public override Color MenuItemPressedGradientMiddle => Hover;
        public override Color MenuItemPressedGradientEnd => Hover;
        public override Color SeparatorDark => Line;
        public override Color SeparatorLight => Line;
        public override Color CheckBackground => SelectBack;
        public override Color CheckSelectedBackground => SelectBack;
        public override Color CheckPressedBackground => SelectBack;
    }

    /// <summary>Menu klik kanan bertema gelap.</summary>
    public static ContextMenuStrip NewMenu()
    {
        return new ContextMenuStrip
        {
            BackColor = Side,
            ForeColor = Fore,
            Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors())
        };
    }

    public static ToolStripMenuItem Item(string text, Action onClick, Keys shortcut = Keys.None, bool bold = false)
    {
        var item = new ToolStripMenuItem(text)
        {
            ForeColor = Fore,
            BackColor = Side
        };

        if (shortcut != Keys.None)
            item.ShortcutKeys = shortcut;

        if (bold)
            item.Font = new Font(item.Font, FontStyle.Bold);

        item.Click += (s, e) => onClick();
        return item;
    }

    /// <summary>Dialog satu isian. Mengembalikan null jika dibatalkan.</summary>
    public static string? Prompt(IWin32Window owner, string title, string label, string initial = "")
    {
        using var form = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(360, 112),
            BackColor = Back,
            ForeColor = Fore
        };

        TextBox box = Txt(336, initial).At(12, 38);
        Button ok = Btn("OK", 90).At(168, 74);
        Button cancel = Btn("Batal", 90).At(264, 74);
        ok.DialogResult = DialogResult.OK;
        cancel.DialogResult = DialogResult.Cancel;

        form.Controls.Add(Lbl(label).At(12, 12));
        form.Controls.Add(box);
        form.Controls.Add(ok);
        form.Controls.Add(cancel);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        form.Shown += (s, e) => { box.Focus(); box.SelectAll(); };

        return form.ShowDialog(owner) == DialogResult.OK ? box.Text : null;
    }

    public static Button Btn(string text, int width = 90)
    {
        var b = new Button
        {
            Text = text,
            Size = new Size(width, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = Input,
            ForeColor = Fore,
            UseVisualStyleBackColor = false,
            Margin = new Padding(0, 0, 6, 4)
        };

        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Hover;
        return b;
    }

    public static Label Lbl(string text, bool dim = false)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = dim ? Dim : Fore,
            BackColor = Color.Transparent
        };
    }

    public static TextBox Txt(int width, string text = "")
    {
        return new TextBox
        {
            Width = width,
            Text = text,
            BackColor = Input,
            ForeColor = Fore,
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    public static CheckBox Chk(string text, bool isChecked = false)
    {
        return new CheckBox
        {
            Text = text,
            Checked = isChecked,
            AutoSize = true,
            ForeColor = Fore,
            BackColor = Color.Transparent
        };
    }

    public static ComboBox Cmb(int width, params string[] items)
    {
        var c = new ComboBox
        {
            Width = width,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Input,
            ForeColor = Fore
        };

        c.Items.AddRange(items);

        if (items.Length > 0)
            c.SelectedIndex = 0;

        return c;
    }

    public static T At<T>(this T control, int x, int y) where T : Control
    {
        control.Location = new Point(x, y);
        return control;
    }

    public static void StyleList(ListView lv)
    {
        lv.View = View.Details;
        lv.FullRowSelect = true;
        lv.HideSelection = false;
        lv.BorderStyle = BorderStyle.None;
        lv.BackColor = Back;
        lv.ForeColor = Fore;
        lv.OwnerDraw = true;

        lv.DrawColumnHeader += (s, e) =>
        {
            using (var br = new SolidBrush(Side))
                e.Graphics.FillRectangle(br, e.Bounds);

            var textRect = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 6), e.Bounds.Height);

            TextRenderer.DrawText(
                e.Graphics,
                e.Header?.Text ?? "",
                lv.Font,
                textRect,
                Dim,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            using (var pen = new Pen(Line))
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        };

        lv.DrawItem += (s, e) => e.DrawDefault = true;
        lv.DrawSubItem += (s, e) => e.DrawDefault = true;
    }

    public static void StyleGrid(DataGridView g)
    {
        g.BackgroundColor = Back;
        g.BorderStyle = BorderStyle.None;
        g.GridColor = Line;
        g.EnableHeadersVisualStyles = false;
        g.RowHeadersVisible = false;
        g.AllowUserToAddRows = false;
        g.AllowUserToResizeRows = false;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        g.ColumnHeadersHeight = 28;

        g.ColumnHeadersDefaultCellStyle.BackColor = Side;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Dim;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Side;
        g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Dim;

        g.DefaultCellStyle.BackColor = Back;
        g.DefaultCellStyle.ForeColor = Fore;
        g.DefaultCellStyle.SelectionBackColor = SelectBack;
        g.DefaultCellStyle.SelectionForeColor = Color.White;
    }
}
