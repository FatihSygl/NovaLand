using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using NovaIsland.Services;
using Button = System.Windows.Controls.Button;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
namespace NovaIsland;
public partial class MainWindow
{
    private bool _dockScrubbing;
    private int _pageTransitionVersion;
    private static readonly string[] DockOrder = ["Music", "Focus", "Todo", "Files", "Clipboard", "Controls", "Settings"];
    private IEnumerable<Button> NavigationButtons => DockButtons.Children.OfType<Button>();
    private void NavigationDock_Loaded(object sender, RoutedEventArgs e) => UpdateDockSelection(false);
    private void NavigationDock_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateDockSelection(false);
    private void UpdateDockSelection(bool animate)
    {
        if (_dockScrubbing || NavigationDock.ActualWidth <= 0) return;
        var button = NavigationButtons.FirstOrDefault(x => (string?)x.Tag == _tab);
        if (button is null) return;
        MoveDockSelection(button.TranslatePoint(new Point(), NavigationDock).X, animate ? 260 : 0);
    }
    private void MoveDockSelection(double x, int milliseconds)
    {
        double from = DockSelectionTranslate.X;
        DockSelectionTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        DockSelectionTranslate.X = x;
        if (SystemParameters.ClientAreaAnimation && milliseconds > 0)
            DockSelectionTranslate.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(from, x, TimeSpan.FromMilliseconds(milliseconds))
                { EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
    }
    private Button? DockButtonAt(Point point)
    {
        return NavigationButtons.FirstOrDefault(button =>
            new Rect(button.TranslatePoint(new Point(), NavigationDock), button.RenderSize).Contains(point));
    }
    private void NavigationDock_Down(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(NavigationDock);
        var button = DockButtonAt(point);
        if (button is null || !NavigationDock.CaptureMouse()) return;
        _dockScrubbing = true;
        button.Focus();
        ScaleDockSelection(1.07, .95);
        TrackDock(point);
        e.Handled = true;
    }
    private void NavigationDock_Move(object sender, MouseEventArgs e)
    {
        if (!_dockScrubbing) return;
        if (e.LeftButton != MouseButtonState.Pressed) { EndDockScrub(); return; }
        TrackDock(e.GetPosition(NavigationDock));
        e.Handled = true;
    }
    private void TrackDock(Point point)
    {
        if (point.Y < -8 || point.Y > NavigationDock.ActualHeight + 8) return;
        var button = DockButtonAt(point);
        if (button?.Tag is string key && key != _tab) SetTab(key);
        double left = NavigationButtons.First().TranslatePoint(new Point(), NavigationDock).X;
        double right = NavigationButtons.Last().TranslatePoint(new Point(), NavigationDock).X;
        MoveDockSelection(Math.Clamp(point.X - DockSelection.Width / 2, left, right), 65);
    }
    private void NavigationDock_Up(object sender, MouseButtonEventArgs e)
    {
        if (!_dockScrubbing) return;
        EndDockScrub(); e.Handled = true;
    }
    private void NavigationDock_LostCapture(object sender, MouseEventArgs e)
    {
        if (e.OriginalSource == NavigationDock) EndDockScrub();
    }
    private void EndDockScrub()
    {
        if (!_dockScrubbing) return;
        _dockScrubbing = false;
        if (NavigationDock.IsMouseCaptured) NavigationDock.ReleaseMouseCapture();
        ScaleDockSelection(1, 1);
        UpdateDockSelection(true);
    }
    private void ScaleDockSelection(double x, double y)
    {
        foreach (var (property, value) in new[] { (ScaleTransform.ScaleXProperty, x), (ScaleTransform.ScaleYProperty, y) })
        {
            double from = (double)DockSelectionScale.GetValue(property);
            DockSelectionScale.BeginAnimation(property, null);
            DockSelectionScale.SetValue(property, value);
            if (SystemParameters.ClientAreaAnimation)
                DockSelectionScale.BeginAnimation(property, new DoubleAnimation(from, value, TimeSpan.FromMilliseconds(200))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        }
    }
    private BitmapSource? CapturePageSurface()
    {
        if (!SystemParameters.ClientAreaAnimation || PageSurface.ActualWidth <= 0 || PageSurface.ActualHeight <= 0) return null;
        try
        {
            var dpi = VisualTreeHelper.GetDpi(PageSurface);
            int width = (int)Math.Ceiling(PageSurface.ActualWidth * dpi.DpiScaleX);
            int height = (int)Math.Ceiling(PageSurface.ActualHeight * dpi.DpiScaleY);
            if ((long)width * height > 4_000_000) return null;
            var frame = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            frame.Render(PageSurface); frame.Freeze(); return frame;
        }
        catch (Exception ex) { DiagnosticLog.Write("Page transition", ex); return null; }
    }
    private void TransitionPage(BitmapSource? previous, string oldTab, string newTab)
    {
        int version = ++_pageTransitionVersion;
        ViewHost.BeginAnimation(OpacityProperty, null); ViewHost.Opacity = 1;
        ViewTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        ViewTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        ViewTranslate.X = ViewTranslate.Y = 0;
        PreviousPage.BeginAnimation(OpacityProperty, null);
        PreviousPageTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        PreviousPage.Source = previous;
        PreviousPage.Visibility = previous is null ? Visibility.Collapsed : Visibility.Visible;
        if (!SystemParameters.ClientAreaAnimation) return;
        double direction = Array.IndexOf(DockOrder, newTab) >= Array.IndexOf(DockOrder, oldTab) ? 1 : -1;
        ViewHost.BeginAnimation(OpacityProperty, new DoubleAnimation(previous is null ? .7 : 0, 1, TimeSpan.FromMilliseconds(220)) { FillBehavior = FillBehavior.Stop });
        ViewTranslate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(direction * 8, 0, TimeSpan.FromMilliseconds(280))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        if (previous is null) return;
        PreviousPage.Opacity = 1;
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220)) { FillBehavior = FillBehavior.Stop };
        fade.Completed += (_, _) =>
        {
            if (version != _pageTransitionVersion) return;
            PreviousPage.Visibility = Visibility.Collapsed; PreviousPage.Source = null;
        };
        PreviousPage.BeginAnimation(OpacityProperty, fade);
        PreviousPageTranslate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, -direction * 6, TimeSpan.FromMilliseconds(220))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
}
