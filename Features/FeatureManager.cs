using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RFTool.Core;

namespace RFTool.Features;

public enum NpcTarget
{
    Bels,
    Cora,
    Acc
}

public sealed class FeatureManager
{
    private const long PlayerPointer = 0xC28B40;

    private readonly MemoryAccess memory;
    private readonly AobScanner scanner;
    private readonly Dictionary<long, byte[]> originals = new();

    private IntPtr combineBase = IntPtr.Zero;
    private IntPtr stage2Address = IntPtr.Zero;
    private IntPtr stage5Address = IntPtr.Zero;
    private IntPtr stage3Address = IntPtr.Zero;
    private IntPtr infernalDragonboneAddress = IntPtr.Zero;

    // =========================================================
    // NPC DYNAMIC ADDRESSES
    // =========================================================

    private IntPtr npcBels = IntPtr.Zero;
    private IntPtr npcCora = IntPtr.Zero;
    private IntPtr npcAcc = IntPtr.Zero;


    // =========================================================
    // ZOOM AOB
    // =========================================================

    private IntPtr zoomAddress = IntPtr.Zero;
    private IntPtr mauKeyboardAddress = IntPtr.Zero;
    private IntPtr reviveAddress = IntPtr.Zero;
    private IntPtr autoPickAddress = IntPtr.Zero;
    private IntPtr lockSpotAddress = IntPtr.Zero;
    private IntPtr showNameAddress = IntPtr.Zero;
    private IntPtr fastLootAddress = IntPtr.Zero;
    private IntPtr distanceAddress = IntPtr.Zero;


    // =========================================================
    // AUTO FOLLOW AOB
    // =========================================================

    private IntPtr autoFollowAddress = IntPtr.Zero;
    private IntPtr autoFollowNickAddress = IntPtr.Zero;


    // =========================================================
    // NPC COMMANDS
    // =========================================================

    private static readonly byte[][] NpcBelsCommands =
    {
        new byte[] { 0x71, 0x31, 0x00, 0x00, 0x01 }, // 7
        new byte[] { 0xA0, 0x30, 0x00, 0x00, 0x12 }, // 0
        new byte[] { 0x60, 0x30, 0x00, 0x00, 0x01 }, // -
        new byte[] { 0xB0, 0x60, 0x00, 0x00, 0x13 }, // =
        new byte[] { 0x10, 0x30, 0x00, 0x00, 0x01 }, // F12
        new byte[] { 0x30, 0x31, 0x00, 0x00, 0x01 }, // 8
        new byte[] { 0xF1, 0x30, 0x00, 0x00, 0x01 }  // F11
    };

    private static readonly byte[][] NpcCoraCommands =
    {
        new byte[] { 0x71, 0x41, 0x00, 0x00, 0x01 }, // 7
        new byte[] { 0xA0, 0x40, 0x00, 0x00, 0x12 }, // 0
        new byte[] { 0x60, 0x40, 0x00, 0x00, 0x01 }, // -
        new byte[] { 0xB0, 0x80, 0x00, 0x00, 0x13 }, // =
        new byte[] { 0x10, 0x40, 0x00, 0x00, 0x01 }, // F12
        new byte[] { 0x30, 0x41, 0x00, 0x00, 0x01 }, // 8
        new byte[] { 0xF1, 0x40, 0x00, 0x00, 0x01 }  // F11
    };

    private static readonly byte[][] NpcAccCommands =
    {
        new byte[] { 0x71, 0x51, 0x00, 0x00, 0x01 }, // 7
        new byte[] { 0xA0, 0x50, 0x00, 0x00, 0x12 }, // 0
        new byte[] { 0x60, 0x50, 0x00, 0x00, 0x01 }, // -
        new byte[] { 0xB0, 0xA0, 0x00, 0x00, 0x13 }, // =
        new byte[] { 0x10, 0x50, 0x00, 0x00, 0x01 }, // F12
        new byte[] { 0x30, 0x51, 0x00, 0x00, 0x01 }, // 8
        new byte[] { 0xF1, 0x50, 0x00, 0x00, 0x01 }  // F11
    };

    private static readonly string[] NpcCommandNames =
    {
        "7",
        "0",
        "-",
        "=",
        "F12",
        "8",
        "F11"
    };


    // =========================================================
    // COMBINE
    // =========================================================

    private static readonly int[] CombineOffsets =
    {
        0x0000,
        0x0150,
        0x01CC,
        0x0290,
        0x0338,
        0x03E0,
        0x0488,
        0x04EC,
        0x0550,
        0x05B4,
        0x0618,
        0x2318,
        0x23DC
    };


    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    public FeatureManager(MemoryAccess memory)
    {
        this.memory =
            memory ??
            throw new ArgumentNullException(nameof(memory));

        scanner =
            new AobScanner(memory);
    }


    // =========================================================
    // PLAYER POINTER
    // =========================================================

    private IntPtr Player(long offset)
    {
        return memory.ResolvePointer32(
            PlayerPointer,
            offset);
    }


    // =========================================================
    // ORIGINAL MEMORY
    // =========================================================

    private void Capture(
        IntPtr address,
        int size)
    {
        long key =
            address.ToInt64();

        if (!originals.ContainsKey(key))
        {
            originals[key] =
                memory.ReadBytes(
                    address,
                    size);
        }
    }


    private void Restore(
        IntPtr address)
    {
        if (originals.TryGetValue(
            address.ToInt64(),
            out var bytes))
        {
            memory.WriteBytes(
                address,
                bytes);
        }
    }


    // =========================================================
    // ASPD
    // =========================================================

    public void AspdOn()
    {
        var address =
            Player(0x96);

        Capture(
            address,
            1);

        memory.WriteByte(
            address,
            255);
    }


    public void AspdOff()
    {
        Restore(
            Player(0x96));
    }


    // =========================================================
    // SPEED
    // =========================================================

    public void SpeedOn()
    {
        var address =
            Player(0x708);

        Capture(
            address,
            4);

        memory.WriteFloat(
            address,
            10.0f);
    }


    // =========================================================
    // DETECT
    // =========================================================

    public void DetectOn()
    {
        var invis =
            Player(0x801);

        var trap =
            Player(0x802);

        Capture(
            invis,
            1);

        Capture(
            trap,
            1);

        memory.WriteByte(
            invis,
            1);

        memory.WriteByte(
            trap,
            1);
    }


    public void DetectOff()
    {
        Restore(
            Player(0x801));

        Restore(
            Player(0x802));
    }


    // =========================================================
    // ANTI STUN
    // =========================================================

    public void AntiStunOn()
    {
        var first =
            Player(0x804);

        var second =
            Player(0x808);

        Capture(
            first,
            1);

        Capture(
            second,
            1);

        memory.WriteByte(
            first,
            1);

        memory.WriteByte(
            second,
            1);
    }


    public void AntiStunOff()
    {
        Restore(
            Player(0x804));

        Restore(
            Player(0x808));
    }


    // =========================================================
    // PLAYER EDIT / FREEZE
    // =========================================================

    public void WritePlayerString(
        long offset,
        string value,
        int length)
    {
        if (length <= 0)
            return;

        var address =
            Player(offset);

        Capture(
            address,
            length);

        byte[] bytes =
            new byte[length];

        byte[] source =
            Encoding.ASCII.GetBytes(
                value ??
                string.Empty);

        int copy =
            Math.Min(
                length,
                source.Length);

        Buffer.BlockCopy(
            source,
            0,
            bytes,
            0,
            copy);

        memory.WriteBytes(
            address,
            bytes);
    }


    public void WritePlayerByte(
        long offset,
        byte value)
    {
        var address =
            Player(offset);

        Capture(
            address,
            1);

        memory.WriteByte(
            address,
            value);
    }


    public void WritePlayerFloat(
        long offset,
        float value)
    {
        var address =
            Player(offset);

        Capture(
            address,
            4);

        memory.WriteFloat(
            address,
            value);
    }


    public readonly record struct VerifiedByteResult(
        IntPtr Address,
        byte Value);


    public VerifiedByteResult WritePlayerByteVerified(
        long offset,
        byte value)
    {
        var address =
            Player(offset);

        Capture(
            address,
            1);

        memory.WriteByte(
            address,
            value);

        byte readBack =
            memory.ReadByte(
                address);

        return new VerifiedByteResult(
            address,
            readBack);
    }


    // =========================================================
    // COMBINE
    // =========================================================

    public void Combine(
        bool enabled)
    {
        EnsureCombineBase();

        foreach (int offset in CombineOffsets)
        {
            memory.WriteByte(
                new IntPtr(
                    combineBase.ToInt64() +
                    offset),
                enabled
                    ? (byte)1
                    : (byte)0);
        }
    }


    private void EnsureCombineBase()
    {
        if (combineBase != IntPtr.Zero)
        {
            try
            {
                var check =
                    memory.ReadBytes(
                        new IntPtr(
                            combineBase.ToInt64() +
                            4),
                        4);

                if (check.SequenceEqual(
                    new byte[]
                    {
                        0x00,
                        0x00,
                        0xFB,
                        0x1C
                    }))
                {
                    return;
                }
            }
            catch
            {
            }

            combineBase =
                IntPtr.Zero;
        }

        var hits =
            scanner.ScanWritable(
                "00 00 00 00 1C FB 00 00");

        if (hits.Count != 1)
        {
            throw new InvalidOperationException(
                $"COMBINE: signature tidak unik = {hits.Count}");
        }

        combineBase =
            new IntPtr(
                hits[0].ToInt64() -
                4);
    }


    // =========================================================
    // STAGE 2
    // =========================================================

    public void Stage2(
        bool enabled)
    {
        if (enabled)
        {
            if (stage2Address ==
                IntPtr.Zero)
            {
                var target =
                    Encoding.ASCII.GetBytes(
                        "Stage 2 True Heart Flame Launcher");

                string pattern =
                    string.Join(
                        " ",
                        target.Select(
                            x => x.ToString("X2")));

                var hits =
                    scanner.ScanProcess(
                        pattern);

                foreach (
                    var nameAddress
                    in hits)
                {
                    var rageAddress =
                        new IntPtr(
                            nameAddress.ToInt64() +
                            0xDE);

                    var bytes =
                        memory.ReadBytes(
                            rageAddress,
                            2);

                    if (
                        (bytes[0] == 0x61 &&
                         bytes[1] == 0x43) ||
                        (bytes[0] == 0xB4 &&
                         bytes[1] == 0x43))
                    {
                        stage2Address =
                            rageAddress;

                        break;
                    }
                }

                if (stage2Address ==
                    IntPtr.Zero)
                {
                    throw new InvalidOperationException(
                        "Stage 2 signature tidak ditemukan.");
                }
            }

            memory.WriteBytes(
                stage2Address,
                0xB4,
                0x43);
        }
        else if (
            stage2Address !=
            IntPtr.Zero)
        {
            var bytes =
                memory.ReadBytes(
                    stage2Address,
                    2);

            if (
                bytes[0] == 0xB4 &&
                bytes[1] == 0x43)
            {
                memory.WriteBytes(
                    stage2Address,
                    0x61,
                    0x43);
            }

            stage2Address =
                IntPtr.Zero;
        }
    }
    
    // =========================================================
    // STAGE 5 ROMANTIC FLAME LAUNCHER
    // =========================================================
    
    public void Stage5Romantic(
        bool enabled)
    {
        const string target =
            "Stage 5 Romantic Flame Launcher [30 Days]";
    
        if (!enabled)
            return;
    
        var targetBytes =
            Encoding.ASCII.GetBytes(
                target);
    
        string pattern =
            string.Join(
                " ",
                targetBytes.Select(
                    x => x.ToString("X2")));
    
        var hits =
            scanner.ScanProcess(
                pattern);
    
        if (hits.Count == 0)
        {
            throw new InvalidOperationException(
                "Stage 5 Romantic Flame Launcher [30 Days] tidak ditemukan.");
        }
    
        foreach (var nameAddress in hits)
        {
            var rageAddress =
                new IntPtr(
                    nameAddress.ToInt64() +
                    0xDE);
    
            var bytes =
                memory.ReadBytes(
                    rageAddress,
                    2);
    
            if (
                (bytes[0] == 0x61 &&
                 bytes[1] == 0x43) ||
                (bytes[0] == 0xB4 &&
                 bytes[1] == 0x43))
            {
                memory.WriteBytes(
                    rageAddress,
                    0xB4,
                    0x43);
    
                return;
            }
        }
    
        throw new InvalidOperationException(
            "Stage 5 Romantic: address target tidak ditemukan.");
    }
    
    // =========================================================
    // STAGE 3 TRUE HEART FLAME LAUNCHER
    // =========================================================
    
    public void Stage3(
        bool enabled)
    {
        const string target =
            "Stage 3 True Heart Flame Launcher";
    
        if (!enabled)
            return;
    
        var targetBytes =
            Encoding.ASCII.GetBytes(
                target);
    
        string pattern =
            string.Join(
                " ",
                targetBytes.Select(
                    x => x.ToString("X2")));
    
        var hits =
            scanner.ScanProcess(
                pattern);
    
        if (hits.Count == 0)
        {
            throw new InvalidOperationException(
                "Stage 3 True Heart Flame Launcher tidak ditemukan.");
        }
    
        foreach (var nameAddress in hits)
        {
            var rageAddress =
                new IntPtr(
                    nameAddress.ToInt64() +
                    0xDE);
    
            var bytes =
                memory.ReadBytes(
                    rageAddress,
                    2);
    
            if (
                (bytes[0] == 0x61 &&
                 bytes[1] == 0x43) ||
                (bytes[0] == 0xB4 &&
                 bytes[1] == 0x43))
            {
                memory.WriteBytes(
                    rageAddress,
                    0xB4,
                    0x43);
    
                return;
            }
        }
    
        throw new InvalidOperationException(
            "Stage 3: address target tidak ditemukan.");
    }
    
    // =========================================================
    // INFERNAL DRAGONBONE FLAME LAUNCHER
    // =========================================================
    
    public void InfernalDragonbone(
        bool enabled)
    {
        const string target =
            "Infernal Dragonbone Flame Launcher [Rare M]";
    
        if (!enabled)
            return;
    
        var targetBytes =
            Encoding.ASCII.GetBytes(
                target);
    
        string pattern =
            string.Join(
                " ",
                targetBytes.Select(
                    x => x.ToString("X2")));
    
        var hits =
            scanner.ScanProcess(
                pattern);
    
        if (hits.Count == 0)
        {
            throw new InvalidOperationException(
                "Infernal Dragonbone Flame Launcher [Rare M] tidak ditemukan.");
        }
    
        foreach (var nameAddress in hits)
        {
            var rageAddress =
                new IntPtr(
                    nameAddress.ToInt64() +
                    0xDE);
    
            var bytes =
                memory.ReadBytes(
                    rageAddress,
                    2);
    
            if (
                (bytes[0] == 0x61 &&
                 bytes[1] == 0x43) ||
                (bytes[0] == 0xB4 &&
                 bytes[1] == 0x43))
            {
                memory.WriteBytes(
                    rageAddress,
                    0xB4,
                    0x43);
    
                return;
            }
        }
    
        throw new InvalidOperationException(
            "Infernal Dragonbone: address target tidak ditemukan.");
    }
    // =========================================================
    // AUTO NPC
    // =========================================================

    public (
        IntPtr Bels,
        IntPtr Cora,
        IntPtr Acc)
        FindNpc()
    {
        const string pattern =
            "55 4D 54 20 44 69 73 70 6F 73 61 62 6C 65 " +
            "20 50 61 67 65 72";

        var hits =
            scanner.ScanProcess(
                pattern);

        if (hits.Count == 0)
        {
            throw new InvalidOperationException(
                "AUTO NPC: signature tidak ditemukan.");
        }

        foreach (var address in hits)
        {
            long baseAddress =
                address.ToInt64();

            bool hasFirst =
                hits.Any(
                    x =>
                        x.ToInt64() ==
                        baseAddress +
                        0x1A8);

            bool hasSecond =
                hits.Any(
                    x =>
                        x.ToInt64() ==
                        baseAddress +
                        0x350);

            if (!hasFirst ||
                !hasSecond)
            {
                continue;
            }

            npcBels =
                new IntPtr(
                    baseAddress +
                    0x98);

            npcCora =
                new IntPtr(
                    baseAddress +
                    0x240);

            npcAcc =
                new IntPtr(
                    baseAddress +
                    0x3E8);

            return (
                npcBels,
                npcCora,
                npcAcc);
        }

        throw new InvalidOperationException(
            "AUTO NPC: base tidak ditemukan.");
    }


    // =========================================================
    // GET NPC COMMAND NAMES
    // =========================================================

    public string[] GetNpcCommandNames()
    {
        return NpcCommandNames.ToArray();
    }


    // =========================================================
    // GET CURRENT NPC ADDRESSES
    // =========================================================

    public (
        IntPtr Bels,
        IntPtr Cora,
        IntPtr Acc)
        GetNpcAddresses()
    {
        if (
            npcBels == IntPtr.Zero ||
            npcCora == IntPtr.Zero ||
            npcAcc == IntPtr.Zero)
        {
            return FindNpc();
        }

        return (
            npcBels,
            npcCora,
            npcAcc);
    }


    // =========================================================
    // NPC COMMAND - LEGACY
    // =========================================================

    public (
        IntPtr Bels,
        IntPtr Cora,
        IntPtr Acc)
        ExecuteNpcCommand(
            int commandIndex)
    {
        return ExecuteNpcCommand(
            commandIndex,
            NpcTarget.Acc);
    }


    // =========================================================
    // NPC COMMAND - SELECTED TARGET
    // =========================================================

    public (
        IntPtr Bels,
        IntPtr Cora,
        IntPtr Acc)
        ExecuteNpcCommand(
            int commandIndex,
            NpcTarget target)
    {
        if (
            commandIndex < 0 ||
            commandIndex >=
            NpcCommandNames.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(commandIndex));
        }

        if (
            npcBels == IntPtr.Zero ||
            npcCora == IntPtr.Zero ||
            npcAcc == IntPtr.Zero)
        {
            FindNpc();
        }

        switch (target)
        {
            case NpcTarget.Bels:

                WriteNpcBytes(
                    npcBels,
                    NpcBelsCommands[
                        commandIndex]);

                return (
                    npcBels,
                    IntPtr.Zero,
                    IntPtr.Zero);

            case NpcTarget.Cora:

                WriteNpcBytes(
                    npcCora,
                    NpcCoraCommands[
                        commandIndex]);

                return (
                    IntPtr.Zero,
                    npcCora,
                    IntPtr.Zero);

            case NpcTarget.Acc:

                WriteNpcBytes(
                    npcAcc,
                    NpcAccCommands[
                        commandIndex]);

                return (
                    IntPtr.Zero,
                    IntPtr.Zero,
                    npcAcc);

            default:

                throw new ArgumentOutOfRangeException(
                    nameof(target));
        }
    }


    // =========================================================
    // NPC WRITE
    // =========================================================

    private void WriteNpcBytes(
        IntPtr address,
        byte[] value)
    {
        Capture(
            address,
            value.Length);

        memory.WriteBytes(
            address,
            value);
    }


    // =========================================================
    // ZOOM AOB
    // =========================================================
    //
    // Signature:
    //
    // 90 27 ?? 04 00 ?? 00 00 00 00 00 00
    //
    // Relative:
    //
    // +05 = Zoom
    // +06 = MAU Keyboard
    // +08 = Revive
    // +0A = Auto Pick
    // +0B = Lock Spot
    // +0C = Show Name
    // +0D = Fast Loot
    // +28 = Distance
    // =========================================================

    public (
        IntPtr Zoom,
        IntPtr MauKeyboard,
        IntPtr Revive,
        IntPtr AutoPick,
        IntPtr LockSpot,
        IntPtr ShowName,
        IntPtr FastLoot,
        IntPtr Distance)
        InitializeZoomAob()
    {
        const string pattern =
            "90 27 ?? 04 00 ?? 00 00 00 00 00 00";

        var hits =
            scanner.ScanProcess(
                pattern);

        if (hits.Count != 1)
        {
            throw new InvalidOperationException(
                $"ZOOM AOB: signature tidak unik = {hits.Count}");
        }

        IntPtr baseAddress =
            hits[0];

        zoomAddress =
            new IntPtr(
                baseAddress.ToInt64() +
                0x05);

        mauKeyboardAddress =
            new IntPtr(
                baseAddress.ToInt64() +
                0x06);

        reviveAddress =
            new IntPtr(
                baseAddress.ToInt64() +
                0x08);

        autoPickAddress =
            new IntPtr(
                baseAddress.ToInt64() +
                0x0A);

        lockSpotAddress =
            new IntPtr(
                baseAddress.ToInt64() +
                0x0B);

        showNameAddress =
            new IntPtr(
                baseAddress.ToInt64() +
                0x0C);

        fastLootAddress =
            new IntPtr(
                baseAddress.ToInt64() +
                0x0D);

        distanceAddress =
            new IntPtr(
                baseAddress.ToInt64() +
                0x28);

        return (
            zoomAddress,
            mauKeyboardAddress,
            reviveAddress,
            autoPickAddress,
            lockSpotAddress,
            showNameAddress,
            fastLootAddress,
            distanceAddress);
    }


    // =========================================================
    // TAB 4 - ZOOM INITIALIZE
    // =========================================================

    public void InitializeZoom()
    {
        InitializeZoomAob();
    }


    // =========================================================
    // TAB 4 - CLEAR ZOOM
    // =========================================================

    public void ClearZoom()
    {
        if (zoomAddress != IntPtr.Zero)
        {
            try
            {
                Restore(zoomAddress);
            }
            catch
            {
            }
        }

        if (mauKeyboardAddress != IntPtr.Zero)
        {
            try
            {
                Restore(mauKeyboardAddress);
            }
            catch
            {
            }
        }

        if (reviveAddress != IntPtr.Zero)
        {
            try
            {
                Restore(reviveAddress);
            }
            catch
            {
            }
        }

        if (autoPickAddress != IntPtr.Zero)
        {
            try
            {
                Restore(autoPickAddress);
            }
            catch
            {
            }
        }

        if (lockSpotAddress != IntPtr.Zero)
        {
            try
            {
                Restore(lockSpotAddress);
            }
            catch
            {
            }
        }

        if (showNameAddress != IntPtr.Zero)
        {
            try
            {
                Restore(showNameAddress);
            }
            catch
            {
            }
        }

        if (fastLootAddress != IntPtr.Zero)
        {
            try
            {
                Restore(fastLootAddress);
            }
            catch
            {
            }
        }

        if (distanceAddress != IntPtr.Zero)
        {
            try
            {
                Restore(distanceAddress);
            }
            catch
            {
            }
        }

        zoomAddress =
            IntPtr.Zero;

        mauKeyboardAddress =
            IntPtr.Zero;

        reviveAddress =
            IntPtr.Zero;

        autoPickAddress =
            IntPtr.Zero;

        lockSpotAddress =
            IntPtr.Zero;

        showNameAddress =
            IntPtr.Zero;

        fastLootAddress =
            IntPtr.Zero;

        distanceAddress =
            IntPtr.Zero;
    }


    // =========================================================
    // GET CURRENT ZOOM ADDRESSES
    // =========================================================

    public (
        IntPtr Zoom,
        IntPtr MauKeyboard,
        IntPtr Revive,
        IntPtr AutoPick,
        IntPtr LockSpot,
        IntPtr ShowName,
        IntPtr FastLoot,
        IntPtr Distance)
        GetZoomAddresses()
    {
        if (zoomAddress == IntPtr.Zero)
        {
            return InitializeZoomAob();
        }

        return (
            zoomAddress,
            mauKeyboardAddress,
            reviveAddress,
            autoPickAddress,
            lockSpotAddress,
            showNameAddress,
            fastLootAddress,
            distanceAddress);
    }


    // =========================================================
    // ZOOM
    // =========================================================

    public void SetZoom(
        bool enabled)
    {
        GetZoomAddresses();

        Capture(
            zoomAddress,
            1);

        memory.WriteByte(
            zoomAddress,
            enabled
                ? (byte)0x01
                : (byte)0x00);
    }


    // =========================================================
    // AUTO PICK
    // =========================================================

    public void SetAutoPick(
        byte mode)
    {
        if (mode > 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mode));
        }

        GetZoomAddresses();

        Capture(
            autoPickAddress,
            1);

        memory.WriteByte(
            autoPickAddress,
            mode);
    }


    // =========================================================
    // SHOW NAME
    // =========================================================

    public void SetShowName(
        bool enabled)
    {
        GetZoomAddresses();

        Capture(
            showNameAddress,
            1);

        memory.WriteByte(
            showNameAddress,
            enabled
                ? (byte)0x01
                : (byte)0x00);
    }


    // =========================================================
    // FAST LOOT
    // =========================================================

    public void SetFastLoot(
        bool enabled)
    {
        GetZoomAddresses();

        Capture(
            fastLootAddress,
            1);

        memory.WriteByte(
            fastLootAddress,
            enabled
                ? (byte)0x01
                : (byte)0x00);
    }


    // =========================================================
    // MAU KEYBOARD MODE
    // =========================================================

    public void SetMauKeyboardMode(
        bool enabled)
    {
        GetZoomAddresses();

        Capture(
            mauKeyboardAddress,
            1);

        memory.WriteByte(
            mauKeyboardAddress,
            enabled
                ? (byte)0x01
                : (byte)0x00);
    }


    // =========================================================
    // REVIVE
    // =========================================================
    //
    // 0 = Inactive
    // 1 = Disable
    // 2 = Auto
    // =========================================================

    public void SetRevive(
        byte mode)
    {
        if (mode > 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mode));
        }

        GetZoomAddresses();

        Capture(
            reviveAddress,
            1);

        memory.WriteByte(
            reviveAddress,
            mode);
    }


    // =========================================================
    // LOCK SPOT
    // =========================================================

    public void SetLockSpot(
        bool enabled)
    {
        GetZoomAddresses();

        Capture(
            lockSpotAddress,
            1);

        memory.WriteByte(
            lockSpotAddress,
            enabled
                ? (byte)0x01
                : (byte)0x00);
    }


    // =========================================================
    // DISTANCE
    // =========================================================

    public void SetDistance(
        float value)
    {
        GetZoomAddresses();

        Capture(
            distanceAddress,
            4);

        memory.WriteFloat(
            distanceAddress,
            value);
    }


    // =========================================================
    // AUTO FOLLOW AOB
    // =========================================================
    //
    // Signature:
    //
    // 00 28 00 88
    //
    // Validation:
    //
    // base + 0x1A = 00 08 70 27
    //
    // AutoFollow     = base + 0x38
    // AutoFollowNick = AutoFollow + 0x1F4
    // =========================================================

    public (
        IntPtr AutoFollow,
        IntPtr AutoFollowNick)
        InitializeAutoFollowAob()
    {
        const string pattern =
            "00 28 00 88";

        var hits =
            scanner.ScanProcess(
                pattern);

        if (hits.Count == 0)
        {
            throw new InvalidOperationException(
                "AUTO FOLLOW AOB: signature tidak ditemukan.");
        }

        IntPtr found =
            IntPtr.Zero;

        int count =
            0;

        foreach (var baseAddress in hits)
        {
            try
            {
                var marker =
                    memory.ReadBytes(
                        new IntPtr(
                            baseAddress.ToInt64() +
                            0x1A),
                        4);

                if (
                    marker[0] != 0x00 ||
                    marker[1] != 0x08 ||
                    marker[2] != 0x70 ||
                    marker[3] != 0x27)
                {
                    continue;
                }

                IntPtr autoFollow =
                    new IntPtr(
                        baseAddress.ToInt64() +
                        0x38);

                byte value =
                    memory.ReadByte(
                        autoFollow);

                if (
                    value != 0x00 &&
                    value != 0x01)
                {
                    continue;
                }

                found =
                    autoFollow;

                count++;
            }
            catch
            {
            }
        }

        if (count != 1)
        {
            throw new InvalidOperationException(
                $"AUTO FOLLOW AOB: candidate valid = {count}");
        }

        autoFollowAddress =
            found;

        autoFollowNickAddress =
            new IntPtr(
                found.ToInt64() +
                0x1F4);

        return (
            autoFollowAddress,
            autoFollowNickAddress);
    }


    // =========================================================
    // TAB 4 - AUTO FOLLOW INITIALIZE
    // =========================================================

    public void InitializeAutoFollow()
    {
        InitializeAutoFollowAob();
    }


    // =========================================================
    // TAB 4 - CLEAR AUTO FOLLOW
    // =========================================================

    public void ClearAutoFollow()
    {
        if (autoFollowAddress != IntPtr.Zero)
        {
            try
            {
                Restore(
                    autoFollowAddress);
            }
            catch
            {
            }
        }

        if (autoFollowNickAddress != IntPtr.Zero)
        {
            try
            {
                Restore(
                    autoFollowNickAddress);
            }
            catch
            {
            }
        }

        autoFollowAddress =
            IntPtr.Zero;

        autoFollowNickAddress =
            IntPtr.Zero;
    }


    // =========================================================
    // GET CURRENT AUTO FOLLOW ADDRESSES
    // =========================================================

    public (
        IntPtr AutoFollow,
        IntPtr AutoFollowNick)
        GetAutoFollowAddresses()
    {
        if (
            autoFollowAddress == IntPtr.Zero ||
            autoFollowNickAddress == IntPtr.Zero)
        {
            return InitializeAutoFollowAob();
        }

        return (
            autoFollowAddress,
            autoFollowNickAddress);
    }


    // =========================================================
    // READ AUTO FOLLOW NICK
    // =========================================================

    public string ReadAutoFollowNick()
    {
        var addresses =
            GetAutoFollowAddresses();

        byte[] bytes =
            memory.ReadBytes(
                addresses.AutoFollowNick,
                64);

        int length =
            Array.IndexOf(
                bytes,
                (byte)0);

        if (length < 0)
            length = bytes.Length;

        return Encoding.ASCII.GetString(
            bytes,
            0,
            length);
    }


    // =========================================================
    // SET AUTO FOLLOW NICK
    // =========================================================

    public void SetAutoFollowNick(
        string nick)
    {
        if (string.IsNullOrEmpty(nick))
            return;

        var addresses =
            GetAutoFollowAddresses();

        byte[] bytes =
            Encoding.ASCII.GetBytes(
                nick);

        const int NickBufferSize = 64;

        if (bytes.Length >= NickBufferSize)
        {
            throw new ArgumentException(
                "Nick terlalu panjang. Maksimal 63 karakter.",
                nameof(nick));
        }

        byte[] buffer =
            new byte[NickBufferSize];

        Buffer.BlockCopy(
            bytes,
            0,
            buffer,
            0,
            bytes.Length);

        Capture(
            addresses.AutoFollowNick,
            NickBufferSize);

        memory.WriteBytes(
            addresses.AutoFollowNick,
            buffer);
    }


    // =========================================================
    // AUTO FOLLOW ON / OFF
    // =========================================================

    public void SetAutoFollow(
        bool enabled)
    {
        var addresses =
            GetAutoFollowAddresses();

        Capture(
            addresses.AutoFollow,
            1);

        memory.WriteByte(
            addresses.AutoFollow,
            enabled
                ? (byte)0x01
                : (byte)0x00);
    }


    // =========================================================
    // RESET AOB CACHE
    // =========================================================

    private void ResetAobAddresses()
    {
        zoomAddress =
            IntPtr.Zero;

        mauKeyboardAddress =
            IntPtr.Zero;

        reviveAddress =
            IntPtr.Zero;

        autoPickAddress =
            IntPtr.Zero;

        lockSpotAddress =
            IntPtr.Zero;

        showNameAddress =
            IntPtr.Zero;

        fastLootAddress =
            IntPtr.Zero;

        distanceAddress =
            IntPtr.Zero;

        autoFollowAddress =
            IntPtr.Zero;

        autoFollowNickAddress =
            IntPtr.Zero;
    }


    // =========================================================
    // RESTORE ALL
    // =========================================================

    public void RestoreAll()
    {
        foreach (
            var item
            in originals.ToArray())
        {
            try
            {
                memory.WriteBytes(
                    new IntPtr(item.Key),
                    item.Value);
            }
            catch
            {
            }
        }

        originals.Clear();


        // -----------------------------------------------------
        // COMBINE
        // -----------------------------------------------------

        if (combineBase !=
            IntPtr.Zero)
        {
            foreach (
                int offset
                in CombineOffsets)
            {
                try
                {
                    memory.WriteByte(
                        new IntPtr(
                            combineBase.ToInt64() +
                            offset),
                        0);
                }
                catch
                {
                }
            }
        }


        // -----------------------------------------------------
        // STAGE 2
        // -----------------------------------------------------

        if (stage2Address !=
            IntPtr.Zero)
        {
            try
            {
                var bytes =
                    memory.ReadBytes(
                        stage2Address,
                        2);

                if (
                    bytes[0] == 0xB4 &&
                    bytes[1] == 0x43)
                {
                    memory.WriteBytes(
                        stage2Address,
                        0x61,
                        0x43);
                }
            }
            catch
            {
            }

            stage2Address =
                IntPtr.Zero;
        }


        // -----------------------------------------------------
        // NPC ADDRESS CACHE
        // -----------------------------------------------------

        npcBels =
            IntPtr.Zero;

        npcCora =
            IntPtr.Zero;

        npcAcc =
            IntPtr.Zero;


        // -----------------------------------------------------
        // AOB ADDRESS CACHE
        // -----------------------------------------------------

        ResetAobAddresses();
    }
}
