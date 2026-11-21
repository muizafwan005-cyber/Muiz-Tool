namespace RFTool.AdvancedSystem;

/// <summary>Mode Freeze: menahan nilai, atau membiarkan nilai naik / turun.</summary>
public enum FreezeMode
{
    Normal,
    AllowIncrease,
    AllowDecrease
}

/// <summary>Aksi hotkey per address (urutan sama dengan daftar di dialog).</summary>
public enum AddressHotkeyAction
{
    ToggleFreeze,
    ToggleFreezeAllowIncrease,
    ToggleFreezeAllowDecrease,
    Freeze,
    Unfreeze,
    SetValue,
    DecreaseValue,
    IncreaseValue
}

public sealed class AddressHotkey
{
    /// <summary>System.Windows.Forms.Keys (tombol utama).</summary>
    public int Key { get; set; }

    /// <summary>System.Windows.Forms.Keys (Ctrl / Alt / Shift).</summary>
    public int Modifiers { get; set; }

    public AddressHotkeyAction Action { get; set; } = AddressHotkeyAction.ToggleFreeze;

    /// <summary>Dipakai oleh Set value / Increase / Decrease.</summary>
    public string Value { get; set; } = "";

    /// <summary>Freeze aktif selama tombol ditahan, mati saat dilepas.</summary>
    public bool DeactivateOnRelease { get; set; }

    public string Description { get; set; } = "";

    public string ActivateSound { get; set; } = "(none)";

    public string DeactivateSound { get; set; } = "(none)";

    public AddressHotkey Clone()
    {
        return (AddressHotkey)MemberwiseClone();
    }
}
