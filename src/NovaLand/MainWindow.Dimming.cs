using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using NovaIsland.Services;
namespace NovaIsland;
public partial class MainWindow
{
    private ScreenDimmer? _screenDimmer;
    private bool _changingDim;
    private DateTime _dimInteractionUntil;
    private void ExtraDim_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ExtraDimValue is null || !_ready) return;
        int level = (int)Math.Round(e.NewValue);
        ExtraDimValue.Text = level == 0 ? "Kapalı" : $"%{level}";
        _dimInteractionUntil = DateTime.UtcNow.AddMilliseconds(500);
        _changingDim = true;
        try
        {
            _screenDimmer ??= new ScreenDimmer(Dispatcher, () =>
            {
                if (Topmost && IsVisible)
                    DimSetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), 0, 0, 0, 0, 0x1 | 0x2 | 0x10);
            });
            _screenDimmer.SetLevel(level);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Extra dim", ex);
            _screenDimmer?.Dispose(); _screenDimmer = null;
            ExtraDimSlider.Value = 0;
            StatusText.Text = "Ek karartma açılamadı. Tekrar deneyebilirsin.";
        }
        finally { _changingDim = false; }
    }
    private void ExtraDimOff_Click(object sender, RoutedEventArgs e) => ExtraDimSlider.Value = 0;
    [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DimSetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
