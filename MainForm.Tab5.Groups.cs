using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using RFTool.AdvancedSystem;

namespace RFTool;

public sealed partial class MainForm
{
    // =========================================================
    // GROUP ADDRESS (seperti Cheat Engine)
    //
    // 1. Blok beberapa baris (Ctrl/Shift + klik), klik GROUP.
    //    Baris paling atas = address utama, sisanya jadi anggota group.
    // 2. Dengan HIDE aktif, anggota group tersembunyi sampai address
    //    utama aktif (Freeze dicentang / script enabled).
    // 3. Address utama dimatikan -> anggota group ikut dimatikan
    //    dan kembali tersembunyi.
    // =========================================================

    private sealed class GroupCache
    {
        public readonly Dictionary<string, AddressEntry> ById = new();
        public readonly Dictionary<AddressEntry, int> Depth = new();
        public readonly HashSet<AddressEntry> Parents = new();
    }

    private GroupCache groupCache = new();

    private bool IsEntryActive(AddressEntry entry)
    {
        return entry.IsScript
            ? AdvSvc.IsAaActive(entry.ScriptKey)
            : entry.Freeze;
    }

    private static AddressEntry? ParentOf(AddressEntry entry, GroupCache cache)
    {
        string parentId = entry.ParentId ?? "";

        if (parentId.Length == 0)
            return null;

        if (cache.ById.TryGetValue(parentId, out AddressEntry? parent) &&
            !ReferenceEquals(parent, entry))
        {
            return parent;
        }

        return null;
    }

    private void RebuildGroupCache()
    {
        var cache = new GroupCache();

        foreach (AddressEntry e in addressManager.Entries)
            cache.ById[e.Id] = e;

        foreach (AddressEntry e in addressManager.Entries)
        {
            int depth = 0;
            AddressEntry current = e;

            for (int guard = 0; guard < 32; guard++)
            {
                AddressEntry? parent = ParentOf(current, cache);

                if (parent == null)
                    break;

                depth++;
                cache.Parents.Add(parent);
                current = parent;
            }

            cache.Depth[e] = depth;
        }

        groupCache = cache;
    }

    private bool IsVisibleInGroups(AddressEntry entry, GroupCache cache)
    {
        AddressEntry current = entry;

        for (int guard = 0; guard < 32; guard++)
        {
            AddressEntry? parent = ParentOf(current, cache);

            if (parent == null)
                return true;

            if (parent.HideChildren && !IsEntryActive(parent))
                return false;

            current = parent;
        }

        return true;
    }

    // Menyembunyikan / menampilkan baris sesuai status address utama.
    private void ApplyGroupVisibility()
    {
        if (addressGrid == null || addressGrid.IsDisposed)
            return;

        RebuildGroupCache();

        int count = Math.Min(addressGrid.Rows.Count, addressManager.Entries.Count);
        var changes = new List<(int Index, bool Visible)>();

        for (int i = 0; i < count; i++)
        {
            bool visible = IsVisibleInGroups(addressManager.Entries[i], groupCache);

            if (addressGrid.Rows[i].Visible != visible)
                changes.Add((i, visible));
        }

        if (changes.Count == 0)
            return;

        CurrencyManager? cm = null;

        if (addressGrid.DataSource != null && addressGrid.BindingContext != null)
            cm = addressGrid.BindingContext[addressGrid.DataSource] as CurrencyManager;

        try
        {
            // Baris aktif (current) pada grid yang ter-bind tidak boleh disembunyikan
            // langsung, jadi kursor dilepas dulu dan binding ditahan sementara.
            DataGridViewCell? currentCell = addressGrid.CurrentCell;

            if (currentCell != null &&
                changes.Any(c => !c.Visible && c.Index == currentCell.RowIndex))
            {
                addressGrid.CurrentCell = null;
            }

            cm?.SuspendBinding();

            foreach ((int index, bool visible) in changes)
            {
                DataGridViewRow row = addressGrid.Rows[index];

                if (!visible && row.Selected)
                    row.Selected = false;

                row.Visible = visible;
            }
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            cm?.ResumeBinding();
        }

        addressGrid.Invalidate();
    }

    // =========================================================
    // HELPER
    // =========================================================

    // Baris terpilih, urut sesuai posisi di list
    private List<AddressEntry> SelectedEntries()
    {
        var indexes = new List<int>();

        foreach (DataGridViewRow row in addressGrid.SelectedRows)
        {
            if (row.Index >= 0 && row.Index < addressManager.Entries.Count)
                indexes.Add(row.Index);
        }

        indexes.Sort();

        return indexes.Select(i => addressManager.Entries[i]).ToList();
    }

    private List<AddressEntry> Descendants(AddressEntry parent)
    {
        var result = new List<AddressEntry>();
        var seen = new HashSet<AddressEntry> { parent };
        var queue = new Queue<AddressEntry>();

        queue.Enqueue(parent);

        while (queue.Count > 0)
        {
            AddressEntry current = queue.Dequeue();

            foreach (AddressEntry e in addressManager.Entries)
            {
                if (seen.Contains(e) || (e.ParentId ?? "") != current.Id)
                    continue;

                seen.Add(e);
                result.Add(e);
                queue.Enqueue(e);
            }
        }

        return result;
    }

    // entri + seluruh turunannya, urut sesuai list
    private void CollectBlock(AddressEntry entry, List<AddressEntry> into, HashSet<AddressEntry> seen)
    {
        if (!seen.Add(entry))
            return;

        into.Add(entry);

        foreach (AddressEntry e in addressManager.Entries.ToList())
        {
            if ((e.ParentId ?? "") == entry.Id)
                CollectBlock(e, into, seen);
        }
    }

    private bool IsAncestorOf(AddressEntry ancestor, AddressEntry node)
    {
        AddressEntry current = node;

        for (int guard = 0; guard < 32; guard++)
        {
            AddressEntry? parent = ParentOf(current, groupCache);

            if (parent == null)
                return false;

            if (ReferenceEquals(parent, ancestor))
                return true;

            current = parent;
        }

        return false;
    }

    // Address utama dimatikan -> semua anggota group dimatikan
    private void CascadeDeactivate(AddressEntry parent)
    {
        bool any = false;

        foreach (AddressEntry child in Descendants(parent))
        {
            if (!child.Freeze)
                continue;

            child.Freeze = false;
            any = true;

            if (child.IsScript)
                HandleScriptToggle(child);
        }

        if (any)
            AdvSvc.Freeze.Refresh();
    }

    private void DetachChildren(AddressEntry parent)
    {
        foreach (AddressEntry e in addressManager.Entries)
        {
            if (!ReferenceEquals(e, parent) && (e.ParentId ?? "") == parent.Id)
                e.ParentId = "";
        }
    }

    // =========================================================
    // AKSI: GROUP / UNGROUP / HIDE
    // =========================================================

    private void GroupSelected()
    {
        List<AddressEntry> selected = SelectedEntries();

        if (selected.Count < 2)
        {
            MessageBox.Show(
                "Blok minimal 2 baris (Ctrl / Shift + klik).\n\n" +
                "Baris paling atas = address utama.\n" +
                "Baris di bawahnya masuk ke dalam group address utama.",
                "Group",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        RebuildGroupCache();

        AddressEntry parent = selected[0];

        var children = selected
            .Skip(1)
            .Where(e => !IsAncestorOf(e, parent)) // cegah group melingkar
            .ToList();

        if (children.Count == 0)
        {
            MessageBox.Show(
                "Tidak ada baris yang bisa dimasukkan ke group ini.",
                "Group");
            return;
        }

        bool parentAlreadyHasChildren = groupCache.Parents.Contains(parent);

        foreach (AddressEntry child in children)
            child.ParentId = parent.Id;

        if (!parentAlreadyHasChildren)
            parent.HideChildren = true;

        // Pindahkan anggota group (beserta turunannya) tepat di bawah address utama
        var moved = new List<AddressEntry>();
        var seen = new HashSet<AddressEntry>();

        foreach (AddressEntry child in children)
            CollectBlock(child, moved, seen);

        var list = addressManager.Entries;

        foreach (AddressEntry m in moved)
            list.Remove(m);

        var block = new List<AddressEntry>();
        CollectBlock(parent, block, new HashSet<AddressEntry>());

        int position = block.Max(b => list.IndexOf(b)) + 1;

        foreach (AddressEntry m in moved)
            list.Insert(position++, m);

        ApplyGroupVisibility();
        addressGrid.Invalidate();

        addressInfoLabel.Text =
            "Group dibuat: " + parent.Description + " + " + children.Count + " anggota." +
            (parent.HideChildren
                ? " Anggota tampil setelah address utama diaktifkan."
                : "");
    }

    private void UngroupSelected()
    {
        List<AddressEntry> selected = SelectedEntries();

        if (selected.Count == 0)
        {
            MessageBox.Show("Pilih baris terlebih dahulu.", "Ungroup");
            return;
        }

        int detached = 0;

        foreach (AddressEntry e in selected)
        {
            // Address utama dipilih -> seluruh group dibubarkan
            foreach (AddressEntry child in addressManager.Entries.ToList())
            {
                if (!ReferenceEquals(child, e) && (child.ParentId ?? "") == e.Id)
                {
                    child.ParentId = "";
                    detached++;
                }
            }

            // Anggota dipilih -> keluar dari group
            if ((e.ParentId ?? "").Length > 0)
            {
                e.ParentId = "";
                detached++;
            }
        }

        ApplyGroupVisibility();
        addressGrid.Invalidate();

        addressInfoLabel.Text = detached > 0
            ? "Ungroup: " + detached + " entri dilepas dari group."
            : "Baris yang dipilih tidak berada di dalam group.";
    }

    private void ToggleHideChildren()
    {
        RebuildGroupCache();

        List<AddressEntry> parents = SelectedEntries()
            .Where(e => groupCache.Parents.Contains(e))
            .ToList();

        if (parents.Count == 0)
        {
            MessageBox.Show(
                "Pilih address utama (baris yang punya group) lalu klik HIDE.",
                "Hide group",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        bool hide = !parents[0].HideChildren;

        foreach (AddressEntry p in parents)
        {
            p.HideChildren = hide;

            // Disembunyikan padahal address utama belum aktif -> matikan anggotanya
            if (hide && !IsEntryActive(p))
                CascadeDeactivate(p);
        }

        ApplyGroupVisibility();
        addressGrid.Invalidate();

        addressInfoLabel.Text = hide
            ? "HIDE aktif: group tampil setelah address utama diaktifkan."
            : "HIDE mati: group selalu tampil.";
    }

    // Indentasi + penanda group di kolom Description
    private void AddressGrid_DescriptionFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 ||
            e.RowIndex >= addressManager.Entries.Count ||
            addressGrid.Columns[e.ColumnIndex].Name != "Description")
        {
            return;
        }

        AddressEntry entry = addressManager.Entries[e.RowIndex];

        groupCache.Depth.TryGetValue(entry, out int depth);

        string prefix = depth > 0
            ? new string(' ', depth * 3) + "└ "
            : "";

        if (groupCache.Parents.Contains(entry))
        {
            prefix += entry.HideChildren && !IsEntryActive(entry)
                ? "► "
                : "▼ ";
        }

        if (prefix.Length == 0)
            return;

        e.Value = prefix + entry.Description;
        e.FormattingApplied = true;
    }
}
