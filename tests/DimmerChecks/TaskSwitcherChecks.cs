using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using NovaIsland.Services;
internal static class TaskSwitcherChecks
{
    public static void Run()
    {
        int count = 0;
        void Check(bool result, string label) { if (!result) throw new Exception(label); count++; }
        void CheckStyle(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            long style = GetWindowLongPtr(hwnd, -20).ToInt64();
            Check((style & 0x80) != 0, "utility window must be excluded from Alt+Tab");
            Check((style & 0x40000) == 0, "utility window must not force an application entry");
            Check((style & 0x80000) != 0, "layered island transparency preserved");
            Check((style & 0x08000000) == 0, "utility style must still allow keyboard activation");
        }
        var input = new System.Windows.Controls.TextBox { Text = "Test task" };
        var window = new Window { Title = "NovaLand task-switcher check", WindowStyle = WindowStyle.None,
            AllowsTransparency = true, Background = System.Windows.Media.Brushes.Transparent,
            Width = 240, Height = 80, ShowActivated = false, Content = input };
        try
        {
            UtilityWindowStyle.Attach(window);
            window.Show(); CheckStyle(window);
            Check(!window.ShowInTaskbar, "no taskbar entry");
            window.Hide(); window.Show(); CheckStyle(window);
            window.Topmost = true; window.Topmost = false; CheckStyle(window);
            input.Text = "Reminder text";
            Check(input.Text == "Reminder text" && input.Focusable && input.IsEnabled, "task and reminder input remains enabled");
            var preview = new Window { Owner = window, WindowStyle = WindowStyle.None, AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent, Width = 100, Height = 80, ShowActivated = false };
            try { UtilityWindowStyle.Attach(preview); preview.Show(); CheckStyle(preview); }
            finally { preview.Close(); }
        }
        finally { window.Close(); }
        Console.WriteLine(count + " native task-switcher checks passed");
    }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
}
