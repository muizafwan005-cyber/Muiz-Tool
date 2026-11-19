using System;
using System.IO;
using System.Text.Json;

namespace RFTool.PrivateAdvance.Config;

internal sealed class BasePointerSettings
{
    public string ModuleName { get; set; } = "RF_Online.bin";
    public string Offset { get; set; } = "01F8CFBC";
}

internal static class BasePointerStore
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MuizTool",
        "PrivateAdvance",
        "base-pointer.json");

    public static bool Exists => File.Exists(SettingsPath);

    public static BasePointerSettings Load()
    {
        if (!File.Exists(SettingsPath))
            return new BasePointerSettings();

        string json = File.ReadAllText(SettingsPath);
        return JsonSerializer.Deserialize<BasePointerSettings>(json)
            ?? new BasePointerSettings();
    }

    public static void Save(BasePointerSettings settings)
    {
        string? directory = Path.GetDirectoryName(SettingsPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Folder konfigurasi Base Pointer tidak valid.");

        Directory.CreateDirectory(directory);
        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(SettingsPath, json);
    }
}
