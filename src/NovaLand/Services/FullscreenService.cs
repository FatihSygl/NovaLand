using System.Runtime.InteropServices;
using NovaIsland.Models;
namespace NovaIsland.Services;
public sealed class FullscreenService : IDisposable
{
    private readonly WinEventCallback _callback;
    private IntPtr _hook;
    public event Action? Changed;
    public FullscreenService()
    {
        _callback = (_, _, _, _, _, _, _) => Changed?.Invoke();
        _hook = SetWinEventHook(3, 3, IntPtr.Zero, _callback, 0, 0, 0);
    }
    public static bool ShouldHide()
    {
        try
        {
            var foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero || foreground == GetShellWindow() || foreground == GetDesktopWindow()) return false;
            GetWindowThreadProcessId(foreground, out uint pid);
            if (pid == Environment.ProcessId || !IsWindowVisible(foreground)) return false;
            if (SHQueryUserNotificationState(out int state) == 0 && state is 3 or 4) return true;
            // Maximized captioned windows are ordinary desktop windows, even on a monitor without a taskbar.
            if (IsZoomed(foreground) && (GetWindowLongPtr(foreground,-16).ToInt64() & 0x00C00000) != 0) return false;
            var monitor = MonitorFromWindow(foreground, 2);
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetWindowRect(foreground, out var rect) && GetMonitorInfo(monitor, ref info)
                && FullscreenPolicy.Covers(rect.Box, info.Monitor.Box)) return true;
            return false;
        }
        catch (Exception ex) { DiagnosticLog.Write("Fullscreen detection", ex); return false; }
    }
    public void Dispose() { if (_hook != IntPtr.Zero) { UnhookWinEvent(_hook); _hook = IntPtr.Zero; } }
    [StructLayout(LayoutKind.Sequential)] private struct Rect
    {
        public int Left, Top, Right, Bottom;
        public DockBox Box => new(Left, Top, Right - Left, Bottom - Top);
    }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    private delegate void WinEventCallback(IntPtr hook, uint ev, IntPtr hwnd, int obj, int child, uint thread, uint time);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hwnd);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", EntryPoint="GetMonitorInfoW")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("shell32.dll")] private static extern int SHQueryUserNotificationState(out int state);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventCallback callback, uint pid, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
}
