using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RFTool.Advanced;

/// <summary>Urutan enum HARUS sama dengan ValueCodec.TypeNames.</summary>
internal enum ScanValueType
{
    Byte,
    Int16,
    Int32,
    Int64,
    Float,
    Double,
    String,
    ByteArray
}

internal static class ValueCodec
{
    public static readonly string[] TypeNames =
    {
        "Byte", "2 Bytes", "4 Bytes", "8 Bytes", "Float", "Double", "String", "Array of byte"
    };

    /// <summary>Tipe yang bisa dipakai di Memory Scanner (tanpa Array of byte).</summary>
    public static readonly string[] ScannableTypeNames =
    {
        "Byte", "2 Bytes", "4 Bytes", "8 Bytes", "Float", "Double", "String"
    };

    public static bool IsInteger(ScanValueType type)
    {
        return type == ScanValueType.Byte || type == ScanValueType.Int16 ||
               type == ScanValueType.Int32 || type == ScanValueType.Int64;
    }

    /// <summary>Default tampilan bertanda: 2/4/8 Bytes bertanda, Byte tidak.</summary>
    public static bool DefaultSigned(ScanValueType type)
    {
        return type == ScanValueType.Int16 || type == ScanValueType.Int32 || type == ScanValueType.Int64;
    }

    /// <summary>Mengubah teks tipe ("4 Bytes", "Float", dst) menjadi enum. Default 4 Bytes.</summary>
    public static ScanValueType TypeFromName(string? name)
    {
        string n = (name ?? "").Trim();

        for (int i = 0; i < TypeNames.Length; i++)
        {
            if (string.Equals(TypeNames[i], n, StringComparison.OrdinalIgnoreCase))
                return (ScanValueType)i;
        }

        return ScanValueType.Int32;
    }

    public static string Name(ScanValueType type)
    {
        return TypeNames[(int)type];
    }

    public static int Size(ScanValueType type)
    {
        switch (type)
        {
            case ScanValueType.Byte: return 1;
            case ScanValueType.Int16: return 2;
            case ScanValueType.Int32:
            case ScanValueType.Float: return 4;
            case ScanValueType.Int64:
            case ScanValueType.Double: return 8;
            default: return 0;
        }
    }

    private static bool TryParseInteger(string text, bool hex, out long value)
    {
        value = 0;
        text = text.Trim();

        if (text.Length == 0)
            return false;

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            hex = true;
            text = text.Substring(2);
        }

        if (hex)
        {
            if (ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong u))
            {
                value = unchecked((long)u);
                return true;
            }

            return false;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return true;

        if (ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong u2))
        {
            value = unchecked((long)u2);
            return true;
        }

        return false;
    }

    public static bool TryParse(
        string? text,
        ScanValueType type,
        bool hex,
        bool unicode,
        out byte[] bytes,
        out string error)
    {
        bytes = Array.Empty<byte>();
        error = "";
        text ??= "";

        if (type == ScanValueType.String)
        {
            if (text.Length == 0)
            {
                error = "Teks tidak boleh kosong.";
                return false;
            }

            if (text.Length > 256)
            {
                error = "Teks maksimal 256 karakter.";
                return false;
            }

            bytes = unicode ? Encoding.Unicode.GetBytes(text) : Encoding.Latin1.GetBytes(text);
            return true;
        }

        if (type == ScanValueType.ByteArray)
        {
            var list = new List<byte>();

            foreach (string token in text.Split(
                         new[] { ' ', ',', '\t', '\r', '\n' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                string hexText = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? token.Substring(2)
                    : token;

                if (hexText.Length == 0 || hexText.Length > 2 ||
                    !byte.TryParse(hexText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                {
                    error = "Byte '" + token + "' bukan hex 00-FF.";
                    return false;
                }

                list.Add(b);
            }

            if (list.Count == 0)
            {
                error = "Isi minimal satu byte hex, contoh: 90 90 90.";
                return false;
            }

            if (list.Count > 4096)
            {
                error = "Array of byte maksimal 4096 byte.";
                return false;
            }

            bytes = list.ToArray();
            return true;
        }

        string s = text.Trim();

        if (s.Length == 0)
        {
            error = "Nilai belum diisi.";
            return false;
        }

        if (type == ScanValueType.Float)
        {
            if (!float.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            {
                error = "'" + text + "' bukan angka Float yang valid.";
                return false;
            }

            bytes = BitConverter.GetBytes(f);
            return true;
        }

        if (type == ScanValueType.Double)
        {
            if (!double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            {
                error = "'" + text + "' bukan angka Double yang valid.";
                return false;
            }

            bytes = BitConverter.GetBytes(d);
            return true;
        }

        if (!TryParseInteger(s, hex, out long v))
        {
            error = "'" + text + "' bukan angka " + (hex ? "hex" : "bulat") + " yang valid.";
            return false;
        }

        switch (type)
        {
            case ScanValueType.Byte:
                if (v < -128 || v > 255)
                {
                    error = "Nilai Byte harus -128 sampai 255.";
                    return false;
                }

                bytes = new[] { unchecked((byte)v) };
                return true;

            case ScanValueType.Int16:
                if (v < short.MinValue || v > ushort.MaxValue)
                {
                    error = "Nilai 2 Bytes harus -32768 sampai 65535.";
                    return false;
                }

                bytes = BitConverter.GetBytes(unchecked((short)v));
                return true;

            case ScanValueType.Int32:
                if (v < int.MinValue || v > uint.MaxValue)
                {
                    error = "Nilai 4 Bytes di luar jangkauan.";
                    return false;
                }

                bytes = BitConverter.GetBytes(unchecked((int)v));
                return true;

            default:
                bytes = BitConverter.GetBytes(v);
                return true;
        }
    }

    /// <summary>
    /// signed: null = bawaan (Byte tanpa tanda, 2/4/8 Bytes bertanda); true/false memaksa.
    /// hex: tampilkan bilangan bulat sebagai heksadesimal.
    /// </summary>
    public static string Format(
        ReadOnlySpan<byte> b,
        ScanValueType type,
        bool unicode = false,
        bool? signed = null,
        bool hex = false)
    {
        try
        {
            if (hex && IsInteger(type))
            {
                ulong v = 0;

                for (int i = 0; i < b.Length && i < 8; i++)
                    v |= (ulong)b[i] << (8 * i);

                return v.ToString("X" + (Size(type) * 2), CultureInfo.InvariantCulture);
            }

            switch (type)
            {
                case ScanValueType.Byte:
                    return signed == true
                        ? ((sbyte)b[0]).ToString(CultureInfo.InvariantCulture)
                        : b[0].ToString(CultureInfo.InvariantCulture);

                case ScanValueType.Int16:
                    return signed == false
                        ? BitConverter.ToUInt16(b).ToString(CultureInfo.InvariantCulture)
                        : BitConverter.ToInt16(b).ToString(CultureInfo.InvariantCulture);

                case ScanValueType.Int32:
                    return signed == false
                        ? BitConverter.ToUInt32(b).ToString(CultureInfo.InvariantCulture)
                        : BitConverter.ToInt32(b).ToString(CultureInfo.InvariantCulture);

                case ScanValueType.Int64:
                    return signed == false
                        ? BitConverter.ToUInt64(b).ToString(CultureInfo.InvariantCulture)
                        : BitConverter.ToInt64(b).ToString(CultureInfo.InvariantCulture);

                case ScanValueType.Float:
                    return BitConverter.ToSingle(b).ToString(CultureInfo.InvariantCulture);

                case ScanValueType.Double:
                    return BitConverter.ToDouble(b).ToString(CultureInfo.InvariantCulture);

                case ScanValueType.ByteArray:
                    return BitConverter.ToString(b.ToArray()).Replace('-', ' ');

                default:
                    string text = unicode ? Encoding.Unicode.GetString(b) : Encoding.Latin1.GetString(b);
                    int nul = text.IndexOf('\0');
                    return nul >= 0 ? text.Substring(0, nul) : text;
            }
        }
        catch
        {
            return "??";
        }
    }

    public static int Compare(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, ScanValueType type)
    {
        switch (type)
        {
            case ScanValueType.Byte:
                return a[0].CompareTo(b[0]);

            case ScanValueType.Int16:
                return BitConverter.ToInt16(a).CompareTo(BitConverter.ToInt16(b));

            case ScanValueType.Int32:
                return BitConverter.ToInt32(a).CompareTo(BitConverter.ToInt32(b));

            case ScanValueType.Int64:
                return BitConverter.ToInt64(a).CompareTo(BitConverter.ToInt64(b));

            case ScanValueType.Float:
                return BitConverter.ToSingle(a).CompareTo(BitConverter.ToSingle(b));

            case ScanValueType.Double:
                return BitConverter.ToDouble(a).CompareTo(BitConverter.ToDouble(b));

            default:
                return a.SequenceCompareTo(b);
        }
    }

    /// <summary>Ukuran default alignment untuk tipe nilai (Fast Scan).</summary>
    public static int DefaultAlignment(ScanValueType type, bool unicode)
    {
        if (type == ScanValueType.String)
            return unicode ? 2 : 1;

        if (type == ScanValueType.ByteArray)
            return 1;

        return Size(type);
    }
}
