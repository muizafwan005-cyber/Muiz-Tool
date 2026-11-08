using System;
using System.Collections.Generic;
using System.Globalization;

namespace RFTool.Advanced;

/// <summary>
/// Penghitung ekspresi alamat. Contoh yang didukung:
///   1A2B3C            (hex)
///   0x1A2B3C
///   RF_Online.bin+C28B40
///   [RF_Online.bin+C28B40]+10      (baca pointer lalu tambah offset)
///   [[base]+10]+4                  (pointer bertingkat)
///   $nama+8                        (variabel script)
/// </summary>
internal static class AddressExpression
{
    private sealed class ExpressionException : Exception
    {
        public ExpressionException(string message) : base(message)
        {
        }
    }

    public static bool TryEvaluate(
        TargetProcess target,
        string? text,
        IDictionary<string, ulong>? variables,
        out ulong value,
        out string error)
    {
        value = 0;
        error = "";

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Alamat kosong.";
            return false;
        }

        try
        {
            var parser = new Parser(target, text, variables);
            value = parser.ParseAll();
            return true;
        }
        catch (ExpressionException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryEvaluate(TargetProcess target, string? text, out ulong value, out string error)
    {
        return TryEvaluate(target, text, null, out value, out error);
    }

    private sealed class Parser
    {
        private readonly TargetProcess target;
        private readonly string s;
        private readonly IDictionary<string, ulong>? vars;
        private int i;

        public Parser(TargetProcess target, string text, IDictionary<string, ulong>? vars)
        {
            this.target = target;
            s = text;
            this.vars = vars;
        }

        public ulong ParseAll()
        {
            ulong v = ParseSum();
            SkipSpaces();

            if (i < s.Length)
                throw new ExpressionException("Karakter tidak dikenal '" + s[i] + "' pada posisi " + (i + 1) + ".");

            return v;
        }

        private void SkipSpaces()
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
                i++;
        }

        private ulong ParseSum()
        {
            ulong v = ParseTerm();

            while (true)
            {
                SkipSpaces();

                if (i >= s.Length)
                    return v;

                char c = s[i];

                if (c == '+')
                {
                    i++;
                    v = unchecked(v + ParseTerm());
                }
                else if (c == '-')
                {
                    i++;
                    v = unchecked(v - ParseTerm());
                }
                else
                {
                    return v;
                }
            }
        }

        private ulong ParseTerm()
        {
            SkipSpaces();

            if (i >= s.Length)
                throw new ExpressionException("Ekspresi tidak lengkap.");

            char c = s[i];

            if (c == '[')
            {
                i++;
                ulong inner = ParseSum();
                SkipSpaces();

                if (i >= s.Length || s[i] != ']')
                    throw new ExpressionException("Tanda ] tidak ditemukan.");

                i++;

                if (!target.TryReadPointer(inner, out ulong pointer))
                    throw new ExpressionException(
                        "Gagal membaca pointer di 0x" + inner.ToString("X") + ".");

                return pointer;
            }

            if (c == '(')
            {
                i++;
                ulong inner = ParseSum();
                SkipSpaces();

                if (i >= s.Length || s[i] != ')')
                    throw new ExpressionException("Tanda ) tidak ditemukan.");

                i++;
                return inner;
            }

            bool isVariable = c == '$';

            if (isVariable)
                i++;

            int start = i;

            while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_' || s[i] == '.'))
                i++;

            if (start == i)
                throw new ExpressionException("Karakter tidak dikenal '" + c + "'.");

            string token = s.Substring(start, i - start);

            if (isVariable)
            {
                if (vars != null && vars.TryGetValue(token, out ulong variable))
                    return variable;

                throw new ExpressionException("Variabel $" + token + " belum didefinisikan.");
            }

            string hex = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? token.Substring(2)
                : token;

            if (ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong number))
                return number;

            ulong moduleBase = target.GetModuleBase(token);

            if (moduleBase != 0)
                return moduleBase;

            throw new ExpressionException("'" + token + "' bukan module yang dimuat atau angka hex.");
        }
    }
}
