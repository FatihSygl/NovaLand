using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Input;

namespace NovaIsland;

public partial class MainWindow
{
    private const double PageWheelStep = 24;

    private void PageScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer viewport || e.OriginalSource is not DependencyObject source) return;

        // The innermost scrollable viewport owns the gesture, including in audio popups.
        for (DependencyObject? element = source; element is not null;)
        {
            if (element is System.Windows.Controls.TextBox) return;
            if (element == viewport)
            {
                ScrollPageWheel(viewport, e);
                return;
            }
            if (element is ScrollViewer { ScrollableHeight: > 0 }) return;
            element = element is Visual or Visual3D ? VisualTreeHelper.GetParent(element)
                : (element as FrameworkContentElement)?.Parent;
        }
    }

    private static void ScrollPageWheel(ScrollViewer viewport, MouseWheelEventArgs e)
    {
        if (viewport.ScrollableHeight <= 0 || e.Delta == 0) return;

        ScrollPageBy(viewport, -e.Delta * PageWheelStep / Mouse.MouseWheelDeltaForOneLine);
        e.Handled = true;
    }

    private static void ScrollPageBy(ScrollViewer viewport, double distance)
    {
        double offset = Math.Clamp(viewport.VerticalOffset + distance, 0, viewport.ScrollableHeight);
        viewport.ScrollToVerticalOffset(offset);
    }
}
