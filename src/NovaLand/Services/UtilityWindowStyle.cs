using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace NovaIsland.Services;
public static class UtilityWindowStyle
{
    private const int ExtendedStyle = -20;
    private const long ToolWindow = 0x80, AppWindow = 0x40000;
    public static void Attach(Window window)
    {
        window.ShowInTaskbar = false;
        void ApplyCurrent()
        {
            try { Apply(new WindowInteropHelper(window).Handle); }
            catch (Win32Exception ex) { DiagnosticLog.Write("Task switcher style", ex); }
        }
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero) { ApplyCurrent(); return; }
        EventHandler? initialized = null;
        initialized = (_, _) => { window.SourceInitialized -= initialized; ApplyCurrent(); };
        window.SourceInitialized += initialized;
    }
    private static void Apply(IntPtr hwnd)
    {
        Marshal.SetLastPInvokeError(0);
        long style = GetWindowLongPtr(hwnd, ExtendedStyle).ToInt64();
        if (style == 0 && Marshal.GetLastPInvokeError() != 0) throw new Win32Exception();
        Marshal.SetLastPInvokeError(0);
        if (SetWindowLongPtr(hwnd, ExtendedStyle, new IntPtr((style | ToolWindow) & ~AppWindow)) == IntPtr.Zero &&
            Marshal.GetLastPInvokeError() != 0) throw new Win32Exception();
        // Refresh shell styling without moving, resizing, activating or reordering the island.
        if (!SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, 0x1 | 0x2 | 0x4 | 0x10 | 0x20)) throw new Win32Exception();
    }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
