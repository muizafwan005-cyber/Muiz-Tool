
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using RFTool.RFClicker;

namespace RFTool;

public sealed partial class MainForm
{
    // =====================================================
    // TAB 6 - RF CLICKER
    // =====================================================

    private readonly List<TextBox> rfClickerHotkeyBoxes = new();
    private readonly List<NumericUpDown> rfClickerDelayInputs = new();

    private Label rfClickerStatusLabel = null!;

    private void BuildTab6(TabPage tab)
    {
        var root = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BackColorMain,
            Padding = new Padding(8)
        };

        tab.Controls.Add(root);

        // HEADER
        var header = new Label
        {
            Text = "RF CLICKER",
            Location = new Point(8, 8),
            Size = new Size(280, 25),
            ForeColor = ForeColorMain,
            Font = new Font(
                "Segoe UI",
                11F,
                FontStyle.Bold)
        };

        root.Controls.Add(header);

        var description = new Label
        {
            Text = "Keyboard automation settings",
            Location = new Point(8, 33),
            Size = new Size(280, 20),
            ForeColor = ForeColorDim
        };

        root.Controls.Add(description);

        // GROUP BOX
        var group = new GroupBox
        {
            Text = "Key (Non Movement)",
            Location = new Point(5, 65),
            Size = new Size(290, 255),
            ForeColor = ForeColorDim,
            BackColor = BackColorMain
        };

        root.Controls.Add(group);

        // FOUR HOTKEY ROWS
        for (int i = 0; i < 4; i++)
        {
            int rowY = 28 + (i * 48);

            var hotkeyBox = new TextBox
            {
                Location = new Point(10, rowY),
                Size = new Size(75, 27),
                ReadOnly = true,
                ShortcutsEnabled = false,
                TextAlign = HorizontalAlignment.Center,
                BackColor = BackColorInput,
                ForeColor = ForeColorMain,
                Cursor = Cursors.Hand,
                TabStop = true
            };

            hotkeyBox.KeyDown +=
                (_, e) =>
                {
                    string keyName =
                        GetRFClickerKeyName(e);

                    if (!string.IsNullOrEmpty(keyName))
                        hotkeyBox.Text = keyName;

                    e.SuppressKeyPress = true;
                    e.Handled = true;
                };

            hotkeyBox.Enter += (_, _) =>
            {
                hotkeyBox.BackColor =
                    Color.FromArgb(55, 65, 75);
            };

            hotkeyBox.Leave += (_, _) =>
            {
                hotkeyBox.BackColor = BackColorInput;
            };

            group.Controls.Add(hotkeyBox);

            rfClickerHotkeyBoxes.Add(hotkeyBox);

            var clearButton = new Button
            {
                Text = "Clear",
                Location = new Point(90, rowY),
                Size = new Size(48, 27),
                FlatStyle = FlatStyle.Flat,
                BackColor = BackColorInput,
                ForeColor = ForeColorMain
            };

            clearButton.FlatAppearance.BorderSize = 0;

            clearButton.Click += (_, _) =>
            {
                hotkeyBox.Clear();
            };

            group.Controls.Add(clearButton);

            var delayInput = new NumericUpDown
            {
                Location = new Point(145, rowY),
                Size = new Size(90, 27),
                Minimum = 1,
                Maximum = 60000,
                Increment = 1,
                Value = 100,
                BackColor = BackColorInput,
                ForeColor = ForeColorMain,
                TextAlign = HorizontalAlignment.Center
            };

            group.Controls.Add(delayInput);

            rfClickerDelayInputs.Add(delayInput);

            var msLabel = new Label
            {
                Text = "ms",
                Location = new Point(242, rowY + 4),
                Size = new Size(30, 20),
                ForeColor = ForeColorDim
            };

            group.Controls.Add(msLabel);
        }

        // START / STOP
        var stopButton = new Button
        {
            Text = "STOP",
            Location = new Point(8, 335),
            Size = new Size(135, 35),
            FlatStyle = FlatStyle.Flat,
            BackColor = BackColorInput,
            ForeColor = ForeColorMain,
            Enabled = false
        };

        stopButton.FlatAppearance.BorderSize = 0;
        root.Controls.Add(stopButton);

        var startButton = new Button
        {
            Text = "START",
            Location = new Point(151, 335),
            Size = new Size(135, 35),
            FlatStyle = FlatStyle.Flat,
            BackColor = BackColorInput,
            ForeColor = ForeColorMain,
            Enabled = false
        };

        startButton.FlatAppearance.BorderSize = 0;
        root.Controls.Add(startButton);

        // STATUS
        rfClickerStatusLabel = new Label
        {
            Text = "Status: UI Ready",
            Location = new Point(8, 385),
            Size = new Size(280, 22),
            ForeColor = ForeColorDim
        };

        root.Controls.Add(rfClickerStatusLabel);
    }

    // =====================================================
    // HOTKEY DISPLAY
    // =====================================================

    private static string GetRFClickerKeyName(
        KeyEventArgs e)
    {
        Keys key = e.KeyCode;

        // Modifier tunggal
        if (key == Keys.ControlKey)
            return "CTRL";

        if (key == Keys.ShiftKey)
            return "SHIFT";

        if (key == Keys.Menu)
            return "ALT";

        // Modifier + tombol utama
        string result = "";

        if (e.Control)
            result += "CTRL + ";

        if (e.Alt)
            result += "ALT + ";

        if (e.Shift)
            result += "SHIFT + ";

        result += key switch
        {
            Keys.Space => "SPACE",
            Keys.Return => "ENTER",
            Keys.Escape => "ESC",
            Keys.Back => "BACKSPACE",
            Keys.Delete => "DELETE",
            Keys.Insert => "INSERT",
            Keys.Prior => "PAGE UP",
            Keys.Next => "PAGE DOWN",
            _ => key.ToString().ToUpperInvariant()
        };

        return result;
    }
}
