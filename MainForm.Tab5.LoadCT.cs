using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using System.Xml.Linq;
using RFTool.Advanced;
using RFTool.AdvancedSystem;

namespace RFTool;

public sealed partial class MainForm
{
    // =========================================================
    // LOAD CHEAT TABLE (.CT)
    // =========================================================

    // Tipe CE yang dikenali Address Manager (lihat ValueCodec.TypeNames)
    private static readonly HashSet<string> CtSupportedTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Byte", "2 Bytes", "4 Bytes", "8 Bytes",
            "Float", "Double", "String", "Array of byte"
        };

    private int ctImportedScripts;

    private void LoadCheatTable()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Cheat Table (*.CT)|*.CT|All Files (*.*)|*.*",
            Title = "Load Cheat Table"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        try
        {
            XDocument doc = XDocument.Load(dialog.FileName);
            XElement? root = doc.Root?.Element("CheatEntries");

            if (root == null)
            {
                MessageBox.Show(
                    "File CT tidak berisi CheatEntries.",
                    "Load CT",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            int added = 0;
            ctImportedScripts = 0;
            var skipped = new List<string>();

            ImportCtEntries(root, "Default", ref added, skipped);

            AdvSvc.Freeze.Refresh();

            addressInfoLabel.Text =
                "Load CT: " + added + " address dimuat, " +
                skipped.Count + " dilewati.";

            string msg = added + " entri dimuat dari CT" +
                         (ctImportedScripts > 0 ? " (termasuk " + ctImportedScripts + " script)." : ".");

            if (skipped.Count > 0)
            {
                int show = Math.Min(10, skipped.Count);

                msg += "\n\n" + skipped.Count +
                       " entry dilewati (tipe tidak didukung / alamat tidak valid):\n- " +
                       string.Join("\n- ", skipped.GetRange(0, show));

                if (skipped.Count > show)
                    msg += "\n- ...";
            }

            MessageBox.Show(
                msg,
                "Load CT",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Load CT Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ImportCtEntries(
        XElement entries,
        string group,
        ref int added,
        List<string> skipped)
    {
        foreach (XElement e in entries.Elements("CheatEntry"))
        {
            string desc = (e.Element("Description")?.Value ?? "")
                .Trim().Trim('"');

            if (desc.Length == 0)
                desc = "No description";

            XElement? children = e.Element("CheatEntries");
            bool isGroup = e.Element("GroupHeader")?.Value == "1";

            if (isGroup || children != null)
            {
                if (children != null)
                    ImportCtEntries(children, isGroup ? desc : group, ref added, skipped);

                continue;
            }

            string type = (e.Element("VariableType")?.Value ?? "4 Bytes").Trim();

            // Auto Assembler script -> entri script (Freeze = Enable/Disable)
            string? scriptText = e.Element("AssemblerScript")?.Value;

            if (!string.IsNullOrWhiteSpace(scriptText))
            {
                addressManager.Add(new AddressEntry
                {
                    Description = desc,
                    Address = "(script)",
                    ValueType = AddressEntry.ScriptTypeName,
                    Value = "",
                    Group = group,
                    ScriptText = scriptText.Trim(),
                    Freeze = false
                });

                added++;
                ctImportedScripts++;
                continue;
            }

            // Tipe lain (Binary, Pointer, dst) dilewati
            if (!CtSupportedTypes.Contains(type))
            {
                skipped.Add(desc + " [" + type + "]");
                continue;
            }

            string baseAddr = (e.Element("Address")?.Value ?? "")
                .Trim().Replace("\"", "");

            if (baseAddr.Length == 0)
            {
                skipped.Add(desc + " [alamat kosong]");
                continue;
            }

            if (!TryBuildCtAddress(baseAddr, e.Element("Offsets"), out string address))
            {
                skipped.Add(desc + " [offset tidak valid]");
                continue;
            }

            ScanValueType scanType = ValueCodec.TypeFromName(type);
            bool isNumber = ValueCodec.IsInteger(scanType);

            bool showHex = isNumber && e.Element("ShowAsHex")?.Value == "1";

            string? signedText = e.Element("ShowAsSigned")?.Value;
            bool signed = signedText != null
                ? signedText == "1"
                : ValueCodec.DefaultSigned(scanType);

            string value = e.Element("LastState")?.Attribute("Value")?.Value ?? "0";

            addressManager.Add(new AddressEntry
            {
                Description = desc,
                Address = address,
                ValueType = ValueCodec.Name(scanType),
                Value = value,
                Group = group,
                Hex = showHex,
                Signed = signed,
                Freeze = false
            });

            added++;
        }
    }

    // CE menyimpan <Offset> terbalik: yang pertama adalah offset TERAKHIR.
    // Hasil: [[base]+off1]+off2 ... sesuai AddressExpression.
    private static bool TryBuildCtAddress(
        string baseAddr,
        XElement? offsets,
        out string address)
    {
        address = baseAddr;

        if (offsets == null)
            return true;

        var list = new List<string>();

        foreach (XElement o in offsets.Elements("Offset"))
            list.Add(o.Value.Trim());

        list.Reverse();

        foreach (string raw in list)
        {
            bool neg = raw.StartsWith("-");
            string hex = neg ? raw.Substring(1) : raw;

            if (!int.TryParse(
                    hex,
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out int off))
            {
                return false;
            }

            address = "[" + address + "]" + (neg ? "-" : "+") + off.ToString("X");
        }

        return true;
    }
}
