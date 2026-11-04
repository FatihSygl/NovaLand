using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NovaIsland.Models;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
namespace NovaIsland;
public partial class MainWindow
{
    private DateTimeOffset _edgeMotionUntil;
    private bool IsSideDock => _islandEdge is IslandEdge.Left or IslandEdge.Right or IslandEdge.Bottom;
    private void UpdateEdgeHandle()
    {
        if (EdgeRevealHandle is null) return;
        EdgeRevealHandle.HorizontalAlignment = _islandEdge == IslandEdge.Left ? HorizontalAlignment.Right :
            _islandEdge == IslandEdge.Right ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        EdgeRevealHandle.VerticalAlignment = _islandEdge == IslandEdge.Bottom ? VerticalAlignment.Top : VerticalAlignment.Center;
        EdgeRevealHandle.Width = _islandEdge == IslandEdge.Bottom ? 34 : 4;
        EdgeRevealHandle.Height = _islandEdge == IslandEdge.Bottom ? 4 : 32;
        EdgeRevealHandle.Visibility = _dockHidden && IsSideDock ? Visibility.Visible : Visibility.Collapsed;
    }
    private void ResetEdgeVisibility()
    {
        Shell.BeginAnimation(OpacityProperty, null); Shell.Opacity = 1;
        EdgeHideScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        EdgeHideScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        EdgeHideScale.ScaleX = EdgeHideScale.ScaleY = 1;
        Shell.IsHitTestVisible = true;
        UpdateEdgeHandle();
    }
    private void AnimateEdgeVisibility(bool hidden)
    {
        if (!IsSideDock) { ResetEdgeVisibility(); return; }
        _edgeMotionUntil = DateTimeOffset.UtcNow.AddMilliseconds(360);
        UpdateEdgeHandle();
        Shell.IsHitTestVisible = !hidden;
        double opacity = Shell.Opacity;
        Shell.BeginAnimation(OpacityProperty, null);
        Shell.Opacity = hidden ? 0 : 1;
        var property = _islandEdge == IslandEdge.Bottom ? ScaleTransform.ScaleXProperty : ScaleTransform.ScaleYProperty;
        double from = (double)EdgeHideScale.GetValue(property), to = hidden ? .68 : 1;
        EdgeHideScale.BeginAnimation(property, null); EdgeHideScale.SetValue(property, to);
        if (!SystemParameters.ClientAreaAnimation) return;
        Shell.BeginAnimation(OpacityProperty, new DoubleAnimation(hidden ? opacity : 0, hidden ? 0 : 1,
            TimeSpan.FromMilliseconds(hidden ? 250 : 300)) { FillBehavior = FillBehavior.Stop });
        EdgeHideScale.BeginAnimation(property, new DoubleAnimation(hidden ? from : .68, to, TimeSpan.FromMilliseconds(340))
        { EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        EdgeRevealHandle.BeginAnimation(OpacityProperty, new DoubleAnimation(hidden ? 0 : .65, hidden ? .65 : 0, TimeSpan.FromMilliseconds(220)));
    }
}
