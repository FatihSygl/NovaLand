using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
namespace NovaIsland.Services;
// A compositor overlay: does not change monitor brightness, gamma or saved settings.
public sealed class ScreenDimmer : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Action _restoreIslandOrder;
    private readonly List<DimSurface> _surfaces = new();
    private bool _disposed;
    private double _amount;
    public ScreenDimmer(Dispatcher dispatcher, Action restoreIslandOrder)
    {
        _dispatcher = dispatcher; _restoreIslandOrder = restoreIslandOrder;
        SystemEvents.DisplaySettingsChanged += DisplaysChanged;
    }
    public void SetLevel(double percent)
    {
        if (_disposed) return;
        _amount = double.IsFinite(percent) ? Math.Clamp(percent, 0, 70) / 100 : 0;
        if (_amount == 0) { Clear(); return; }
        if (_surfaces.Count == 0) Rebuild();
        else foreach (var surface in _surfaces) surface.Opacity = _amount;
    }
    private void DisplaysChanged(object? sender, EventArgs e)
    {
        _dispatcher.BeginInvoke(new Action(() => { if (!_disposed && _amount > 0) Rebuild(); }));
    }
    private void Rebuild()
    {
        Clear();
        try
        {
            foreach (var screen in Forms.Screen.AllScreens)
            {
                var surface = new DimSurface(screen.Bounds, _amount);
                _surfaces.Add(surface);
            }
            _restoreIslandOrder();
        }
        catch { Clear(); _amount = 0; throw; }
    }
    private void Clear()
    {
        foreach (var surface in _surfaces) { surface.Dispose(); }
        _surfaces.Clear();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; SystemEvents.DisplaySettingsChanged -= DisplaysChanged; Clear();
    }
    private sealed class DimSurface : IDisposable
    {
        private IntPtr _handle;
        private static readonly WindowProc Procedure = HandleMessage;
        private static bool _registered;
        private const string ClassName = "NovaLand.ScreenDimmer.Native";
        public DimSurface(System.Drawing.Rectangle bounds, double opacity)
        {
            var instance = GetModuleHandle(null);
            if (!_registered)
            {
                var cls = new WindowClass { Size = (uint)Marshal.SizeOf<WindowClass>(), Procedure = Marshal.GetFunctionPointerForDelegate(Procedure),
                    Instance = instance, Background = GetStockObject(4), Name = ClassName };
                if (RegisterClassEx(ref cls) == 0 && Marshal.GetLastWin32Error() != 1410) throw new Win32Exception();
                _registered = true;
            }
            // Disabled popup with NOACTIVATE: it cannot receive keyboard focus or mouse input.
            _handle = CreateWindowEx(0x80000 | 0x20 | 0x80 | 0x08000000, ClassName, "", 0x80000000u | 0x08000000u,
                bounds.X, bounds.Y, bounds.Width, bounds.Height, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
            if (_handle == IntPtr.Zero) throw new Win32Exception();
            try
            {
                Opacity = opacity;
                if (!SetWindowPos(_handle, new IntPtr(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x10 | 0x40)) throw new Win32Exception();
            }
            catch { Dispose(); throw; }
        }
        public double Opacity
        {
            set
            {
                byte alpha = (byte)Math.Round(Math.Clamp(value, 0, .7) * 255);
                if (!SetLayeredWindowAttributes(_handle, 0, alpha, 2)) throw new Win32Exception();
            }
        }
        public void Dispose() { if (_handle != IntPtr.Zero) { DestroyWindow(_handle); _handle = IntPtr.Zero; } }
        private static IntPtr HandleMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
        {
            if (message == 0x21) return new IntPtr(3); // WM_MOUSEACTIVATE -> MA_NOACTIVATE
            if (message == 0x84) return new IntPtr(-1); // WM_NCHITTEST -> HTTRANSPARENT
            return DefWindowProc(hwnd, message, wParam, lParam);
        }
        private delegate IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WindowClass
        {
            public uint Size, Style; public IntPtr Procedure; public int ClassExtra, WindowExtra;
            public IntPtr Instance, Icon, Cursor, Background;
            [MarshalAs(UnmanagedType.LPWStr)] public string? Menu;
            [MarshalAs(UnmanagedType.LPWStr)] public string Name;
            public IntPtr SmallIcon;
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WindowClass cls);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateWindowEx(int exStyle, string cls, string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint color, byte alpha, uint flags);
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
        [DllImport("gdi32.dll")] private static extern IntPtr GetStockObject(int index);
    }
}
