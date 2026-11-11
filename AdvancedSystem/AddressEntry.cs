using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace RFTool.AdvancedSystem;

public class AddressEntry : INotifyPropertyChanged
{
    private string description = "";
    private string address = "";
    private string valueType = "4 Bytes";
    private string valueText = "0";
    private string group = "Default";
    private bool freeze;
    private bool hex;
    private bool signed;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public string Description
    {
        get => description;
        set { if (description != value) { description = value; Raise(); } }
    }

    /// <summary>Hex, module+offset, atau pointer path, contoh: [RF_Online.bin+C28B40]+10</summary>
    public string Address
    {
        get => address;
        set { if (address != value) { address = value; Raise(); } }
    }

    public string ValueType
    {
        get => valueType;
        set { if (valueType != value) { valueType = value; Raise(); } }
    }

    /// <summary>Nilai target yang dipertahankan saat Freeze aktif.</summary>
    public string Value
    {
        get => valueText;
        set { if (valueText != value) { valueText = value; Raise(); } }
    }

    public string Group
    {
        get => group;
        set { if (group != value) { group = value; Raise(); } }
    }

    public bool Freeze
    {
        get => freeze;
        set { if (freeze != value) { freeze = value; Raise(); } }
    }

    /// <summary>Value ditulis/ditampilkan sebagai heksadesimal (bilangan bulat).</summary>
    public bool Hex
    {
        get => hex;
        set { if (hex != value) { hex = value; Raise(); } }
    }

    /// <summary>Tampilkan bilangan bulat sebagai bertanda (signed).</summary>
    public bool Signed
    {
        get => signed;
        set { if (signed != value) { signed = value; Raise(); } }
    }

    /// <summary>Status mesin Freeze (tidak disimpan ke JSON).</summary>
    [JsonIgnore]
    public string Status { get; set; } = "";
}
