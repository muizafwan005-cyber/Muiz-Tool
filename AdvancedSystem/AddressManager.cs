
using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;

namespace RFTool.AdvancedSystem;

public class AddressManager
{
    public BindingList<AddressEntry> Entries { get; private set; }
        = new();

    // =====================================================
    // ADD ADDRESS
    // =====================================================

    public void Add(AddressEntry entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        Entries.Add(entry);
    }

    // =====================================================
    // EDIT ADDRESS
    // =====================================================

    public void Edit(
        int index,
        AddressEntry entry)
    {
        if (index < 0 || index >= Entries.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        Entries[index] = entry;
    }

    // =====================================================
    // REMOVE ADDRESS
    // =====================================================

    public void Remove(int index)
    {
        if (index < 0 || index >= Entries.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        Entries.RemoveAt(index);
    }

    // =====================================================
    // CLEAR
    // =====================================================

    public void Clear()
    {
        Entries.Clear();
    }

    // =====================================================
    // SAVE JSON
    // =====================================================

    public void Save(string filePath)
    {
        string json = JsonSerializer.Serialize(
            Entries,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(filePath, json);
    }

    // =====================================================
    // LOAD JSON
    // =====================================================

    public void Load(string filePath)
    {
        string json = File.ReadAllText(filePath);

        var loaded =
            JsonSerializer.Deserialize<AddressEntry[]>(json);

        if (loaded == null)
            throw new InvalidDataException(
                "Address data tidak valid.");

        // Freeze selalu dimatikan saat memuat agar tidak langsung menulis memori.
        foreach (var entry in loaded)
            entry.Freeze = false;

        Entries = new BindingList<AddressEntry>(loaded.ToList());
    }
}
