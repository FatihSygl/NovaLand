using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using NovaIsland.Services;
internal static class Program
{
    [STAThread] private static void Main()
    {
        int count = 0;
        void Check(bool result, string label) { if (!result) throw new Exception(label); count++; }
        using (var dimmer = new ScreenDimmer(Dispatcher.CurrentDispatcher, () => { }))
        {
            foreach (double level in new[] { 20d, 55d, 0d, 35d, 70d, 0d })
            {
                var foreground = GetForegroundWindow(); var active = GetActiveWindow(); var capture = GetCapture();
                dimmer.SetLevel(level);
                Check(GetForegroundWindow() == foreground, "dimming must not steal foreground");
                Check(GetActiveWindow() == active, "dimming must not change active window");
                Check(GetCapture() == capture, "dimming must not capture pointer");
                var windows = Surfaces();
                Check(windows.Count == (level == 0 ? 0 : System.Windows.Forms.Screen.AllScreens.Length), "surface lifecycle");
                foreach (var hwnd in windows)
                {
                    long ex = GetWindowLongPtr(hwnd, -20).ToInt64(), style = GetWindowLongPtr(hwnd, -16).ToInt64();
                    Check((ex & 0x08080020) == 0x08080020 && (style & 0x08000000) != 0, "surface remains layered, transparent, disabled and nonactivating");
                    Check(GetLayeredWindowAttributes(hwnd, out _, out byte alpha, out _) && alpha == (byte)Math.Round(level / 100 * 255), "requested dim level applied");
                }
            }
            dimmer.SetLevel(35);
        }
        Check(Surfaces().Count == 0, "dispose restores desktop");
        Console.WriteLine($"{count} native dimmer checks passed");
    }
    private static List<IntPtr> Surfaces()
    {
        var result = new List<IntPtr>();
        EnumThreadWindows(GetCurrentThreadId(), (hwnd, _) =>
        {
            var name = new StringBuilder(128); GetClassName(hwnd, name, name.Capacity);
            if (name.ToString() == "NovaLand.ScreenDimmer.Native") result.Add(hwnd);
            return true;
        }, IntPtr.Zero);
        return result;
    }
    private delegate bool EnumCallback(IntPtr hwnd, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint thread, EnumCallback callback, IntPtr parameter);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetCapture();
    [DllImport("user32.dll")] private static extern bool GetLayeredWindowAttributes(IntPtr hwnd, out uint color, out byte alpha, out uint flags);
}
