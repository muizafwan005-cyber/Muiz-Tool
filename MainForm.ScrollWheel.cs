using System.Runtime.InteropServices;

namespace RFTool;

// =============================================================
// SCROLL ZOOM - SCROLL MOUSE DI DALAM GAME
//
// Saat Scroll Zoom di-Initialize dan Freeze dicentang, memutar roda
// mouse di jendela game langsung menggeser slider (dan nilai zoom
// yang di-freeze), tanpa perlu menggeser slider di aplikasi.
// =============================================================

public sealed partial class MainForm
{
    private const int ScrollWheelStep = 50;
    private const int WheelDeltaPerNotch = 120;

    private CheckBox scrollWheelCheck = null!;
    private CheckBox scrollWheelInvertCheck = null!;
    private MouseWheelHook? scrollWheelHook;
    private int scrollWheelRemainder;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    // =========================================================
    // KONTROL (dipanggil dari BuildScrollZoomSection)
    // =========================================================

    private void BuildScrollWheelControls(
        Panel panel,
        ref int y)
    {
        scrollWheelCheck =
            CreateFeatureCheckBox(
                panel,
                "Scroll mouse di game ubah zoom",
                y);

        scrollWheelCheck.Checked = true;
        scrollWheelCheck.Enabled = false;
        scrollWheelCheck.Visible = false;

        y += 30;

        scrollWheelInvertCheck =
            CreateFeatureCheckBox(
                panel,
                "Balik arah scroll",
                y);

        scrollWheelInvertCheck.Enabled = false;
        scrollWheelInvertCheck.Visible = false;

        y += 34;
    }

    private void WireScrollWheelEvents()
    {
        scrollZoomInitializeCheck.CheckedChanged +=
            (_, _) => SyncScrollWheelControls();

        scrollZoomFreezeCheck.CheckedChanged +=
            (_, _) => UpdateScrollWheelHook();

        scrollWheelCheck.CheckedChanged +=
            (_, _) => UpdateScrollWheelHook();
    }

    // Dipanggil dari UpdateTab2Layout.
    private void PositionScrollWheelControls()
    {
        if (scrollWheelCheck == null ||
            scrollWheelInvertCheck == null ||
            scrollZoomFreezeCheck == null)
        {
            return;
        }

        scrollWheelCheck.Top =
            scrollZoomFreezeCheck.Bottom;

        scrollWheelInvertCheck.Top =
            scrollWheelCheck.Bottom - 8;
    }

    private void SyncScrollWheelControls()
    {
        bool on = scrollZoomInitializeCheck.Checked;

        scrollWheelCheck.Visible = on;
        scrollWheelCheck.Enabled = on;
        scrollWheelInvertCheck.Visible = on;
        scrollWheelInvertCheck.Enabled = on;

        UpdateScrollWheelHook();
    }

    // =========================================================
    // HOOK
    // =========================================================

    private bool ScrollWheelActive()
    {
        return
            features != null &&
            selectedProcess != null &&
            scrollZoomInitializeCheck.Checked &&
            scrollZoomFreezeCheck.Checked &&
            scrollWheelCheck.Checked;
    }

    private void UpdateScrollWheelHook()
    {
        bool want = ScrollWheelActive();

        if (want && scrollWheelHook == null)
        {
            var hook = new MouseWheelHook(OnGameMouseWheel);

            if (!hook.Install())
            {
                hook.Dispose();

                statusLabel.Text =
                    "Status: Gagal memasang hook scroll mouse.";

                return;
            }

            scrollWheelHook = hook;
            scrollWheelRemainder = 0;

            statusLabel.Text =
                "Status: Scroll mouse di game aktif untuk Scroll Zoom";
        }
        else if (!want && scrollWheelHook != null)
        {
            DisposeScrollWheelHook();
        }
    }

    private void DisposeScrollWheelHook()
    {
        scrollWheelHook?.Dispose();
        scrollWheelHook = null;
        scrollWheelRemainder = 0;
    }

    protected override void OnFormClosed(
        FormClosedEventArgs e)
    {
        DisposeScrollWheelHook();

        base.OnFormClosed(e);
    }

    private bool IsGameForeground()
    {
        try
        {
            if (selectedProcess == null)
                return false;

            IntPtr window = GetForegroundWindow();

            if (window == IntPtr.Zero)
                return false;

            GetWindowThreadProcessId(window, out uint pid);

            return pid == (uint)selectedProcess.Id;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Dipanggil dari hook mouse global. Mengembalikan true jika roda mouse sudah
    /// dipakai untuk zoom (event tidak diteruskan ke game).
    /// </summary>
    private bool OnGameMouseWheel(int delta)
    {
        if (!ScrollWheelActive() || !IsGameForeground())
            return false;

        scrollWheelRemainder += delta;

        int notches =
            scrollWheelRemainder / WheelDeltaPerNotch;

        if (notches == 0)
            return true;

        scrollWheelRemainder -=
            notches * WheelDeltaPerNotch;

        // Default: scroll ke atas = zoom mendekat (nilai turun). "Balik arah" membaliknya.
        int direction =
            scrollWheelInvertCheck.Checked ? -1 : 1;

        int value =
            scrollZoomTrackBar.Value -
            direction * notches * ScrollWheelStep;

        value =
            Math.Max(
                scrollZoomTrackBar.Minimum,
                Math.Min(
                    scrollZoomTrackBar.Maximum,
                    (int)Math.Round(value / (double)ScrollWheelStep) * ScrollWheelStep));

        ApplyScrollZoomValue(value);

        return true;
    }

    private void ApplyScrollZoomValue(int value)
    {
        if (features == null)
            return;

        try
        {
            scrollZoomTrackBar.Value = value;
            scrollZoomCurrentValue = value;

            features.SetScrollZoom(
                scrollZoomCurrentValue);

            scrollZoomValueLabel.Text =
                $"Zoom: {scrollZoomCurrentValue:0}";
        }
        catch (Exception ex)
        {
            // Jangan menampilkan MessageBox di dalam hook (bisa menahan input mouse seluruh Windows).
            statusLabel.Text =
                "Error: Scroll Zoom (scroll mouse): " + ex.Message;
        }
    }
}

/// <summary>
/// Hook mouse global (WH_MOUSE_LL) khusus roda mouse. Dipasang hanya saat diperlukan
/// dan harus dipasang dari UI thread. Handler harus cepat dan tidak boleh memblokir.
/// </summary>
internal sealed class MouseWheelHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEWHEEL = 0x020A;

    private delegate IntPtr LowLevelMouseProc(
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelMouseProc lpfn,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hhk,
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    // Delegate disimpan di field agar tidak dibersihkan GC selama hook aktif.
    private readonly LowLevelMouseProc proc;
    private readonly Func<int, bool> handler;
    private IntPtr hook = IntPtr.Zero;

    public MouseWheelHook(Func<int, bool> handler)
    {
        this.handler = handler;
        proc = Callback;
    }

    public bool Install()
    {
        if (hook != IntPtr.Zero)
            return true;

        hook = SetWindowsHookEx(
            WH_MOUSE_LL,
            proc,
            GetModuleHandle(null),
            0);

        return hook != IntPtr.Zero;
    }

    private IntPtr Callback(
        int nCode,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (nCode >= 0 &&
            wParam.ToInt64() == WM_MOUSEWHEEL)
        {
            try
            {
                // MSLLHOOKSTRUCT: POINT pt (8 byte), lalu mouseData (DWORD) pada offset 8.
                int mouseData = Marshal.ReadInt32(lParam, 8);
                int delta = (short)((mouseData >> 16) & 0xFFFF);

                if (handler(delta))
                    return (IntPtr)1;
            }
            catch
            {
            }
        }

        return CallNextHookEx(hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(hook);
            hook = IntPtr.Zero;
        }
    }
}
