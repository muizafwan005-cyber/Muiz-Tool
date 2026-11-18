
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RFTool.RFClicker;

public sealed class RFClickerEngine : IDisposable
{
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    private CancellationTokenSource? cancellation;
    private Task[] workers = Array.Empty<Task>();

    public bool IsRunning =>
        cancellation != null &&
        !cancellation.IsCancellationRequested;

    public sealed record ClickerBinding(
        string Hotkey,
        int DelayMs);

    public void Start(
        IntPtr targetWindow,
        IReadOnlyList<ClickerBinding> bindings)
    {
        if (IsRunning)
            throw new InvalidOperationException(
                "RF Clicker sedang berjalan.");

        if (targetWindow == IntPtr.Zero ||
            !IsWindow(targetWindow))
        {
            throw new InvalidOperationException(
                "Jendela target RF Online tidak valid.");
        }

        var validBindings = bindings
            .Where(x => !string.IsNullOrWhiteSpace(x.Hotkey))
            .ToArray();

        if (validBindings.Length == 0)
            throw new InvalidOperationException(
                "Belum ada hotkey yang diatur.");

        foreach (var binding in validBindings)
        {
            if (binding.DelayMs < 1 ||
                binding.DelayMs > 60000)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bindings));
            }

            ParseHotkey(binding.Hotkey);
        }

        cancellation = new CancellationTokenSource();

        workers = validBindings
            .Select(binding => Task.Run(
                () => RunWorker(
                    targetWindow,
                    binding,
                    cancellation.Token)))
            .ToArray();
    }

    private static async Task RunWorker(
        IntPtr targetWindow,
        ClickerBinding binding,
        CancellationToken token)
    {
        Keys[] keys = ParseHotkey(binding.Hotkey);

        while (!token.IsCancellationRequested)
        {
            token.ThrowIfCancellationRequested();

            SendKey(targetWindow, keys, true);

            await Task.Delay(
                binding.DelayMs,
                token);
        }
    }

    private static void SendKey(
        IntPtr targetWindow,
        Keys[] keys,
        bool send)
    {
        Keys mainKey = keys[^1];

        Keys[] modifiers = keys
            .Take(keys.Length - 1)
            .ToArray();

        if (send)
        {
            foreach (Keys modifier in modifiers)
                SendSingleKey(targetWindow, modifier, true);

            SendSingleKey(targetWindow, mainKey, true);
            SendSingleKey(targetWindow, mainKey, false);

            foreach (Keys modifier in modifiers.Reverse())
                SendSingleKey(targetWindow, modifier, false);
        }
    }

    private static void SendSingleKey(
        IntPtr targetWindow,
        Keys key,
        bool down)
    {
        uint message = down
            ? WM_KEYDOWN
            : WM_KEYUP;

        long lParam = down
            ? 1
            : unchecked((long)0xC0000001);

        if (!PostMessage(
            targetWindow,
            message,
            (IntPtr)(int)key,
            (IntPtr)lParam))
        {
            throw new InvalidOperationException(
                "Pengiriman pesan keyboard gagal.");
        }
    }

    private static Keys[] ParseHotkey(string hotkey)
    {
        string[] parts = hotkey
            .Split(
                new[] { '+' },
                StringSplitOptions.TrimEntries |
                StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            throw new FormatException("Hotkey kosong.");

        var result = new List<Keys>();

        foreach (string part in parts)
        {
            string name = part.Trim().ToUpperInvariant();

            Keys key = name switch
            {
                "CTRL" => Keys.ControlKey,
                "SHIFT" => Keys.ShiftKey,
                "ALT" => Keys.Menu,
                "SPACE" => Keys.Space,
                "ENTER" => Keys.Return,
                "ESC" => Keys.Escape,
                "BACKSPACE" => Keys.Back,
                "DELETE" => Keys.Delete,
                "INSERT" => Keys.Insert,
                "PAGE UP" => Keys.Prior,
                "PAGE DOWN" => Keys.Next,
                _ when Enum.TryParse(
                    name,
                    true,
                    out Keys parsed) => parsed,
                _ => throw new FormatException(
                    $"Hotkey tidak dikenal: {name}")
            };

            result.Add(key);
        }

        if (result.Count > 1 &&
            !result.Take(result.Count - 1).All(
                k => k == Keys.ControlKey ||
                     k == Keys.ShiftKey ||
                     k == Keys.Menu))
        {
            throw new FormatException(
                "Kombinasi modifier tidak valid.");
        }

        return result.ToArray();
    }

    public void Stop()
    {
        if (cancellation == null)
            return;

        cancellation.Cancel();

        cancellation.Dispose();
        cancellation = null;

        workers = Array.Empty<Task>();
    }

    public void Dispose()
    {
        Stop();
    }
}
