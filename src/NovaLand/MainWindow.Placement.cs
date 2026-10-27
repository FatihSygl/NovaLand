using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using NovaIsland.Models;
using Forms = System.Windows.Forms;
using Color = System.Windows.Media.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Cursors = System.Windows.Input.Cursors;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using TextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;

namespace NovaIsland;

public partial class MainWindow
{
    private IslandEdge _islandEdge = IslandEdge.Top;
    private DockBox _placementBounds, _placementWork;
    private double _anchorX = 0.5, _anchorY = 0.15;
    private bool _dragArmed, _dragging;
    private CursorLocation _pressCursor;
    private double _dragStartLeft, _dragStartTop, _dragScaleX = 1, _dragScaleY = 1;
    private string _monitorName = "";

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorLocation { public int X; public int Y; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out CursorLocation point);

    private void InitializePlacement()
    {
        if (!Enum.TryParse(_settings.IslandEdge, out _islandEdge) || !Enum.IsDefined(_islandEdge)) _islandEdge = IslandEdge.Top;
        _anchorX = DockLayout.Anchor(_settings.IslandAnchorX);
        _anchorY = DockLayout.Anchor(_settings.IslandAnchorY, 0.15);
        _monitorName = _settings.IslandMonitor;
        UpdatePlacementMonitor(); UpdatePlacementChrome(); RefreshPlacementButtons();
    }

    private void UpdatePlacementMonitor(Forms.Screen? chosen = null)
    {
        var screen = chosen ?? Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == _monitorName)
            ?? Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
        _monitorName = screen.DeviceName;
        var transform = (PresentationSource.FromVisual(this) as HwndSource)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        DockBox Convert(System.Drawing.Rectangle box)
        {
            var origin = transform.Transform(new System.Windows.Point(box.Left, box.Top));
            var end = transform.Transform(new System.Windows.Point(box.Right, box.Bottom));
            return new(origin.X, origin.Y, end.X - origin.X, end.Y - origin.Y);
        }
        _placementBounds = Convert(screen.Bounds); _placementWork = Convert(screen.WorkingArea);
    }

    private double PlacementExtraHeight => _islandEdge is IslandEdge.Top or IslandEdge.Bottom ? 0 : 12;
    private void UpdatePlacementChrome()
    {
        if (IslandRoot is null) return;
        IslandRoot.Margin = _islandEdge switch
        {
            IslandEdge.Top => new Thickness(12, 0, 12, 12),
            IslandEdge.Bottom => new Thickness(12, 12, 12, 0),
            IslandEdge.Left => new Thickness(0, 12, 12, 12),
            IslandEdge.Right => new Thickness(12, 12, 0, 12),
            _ => new Thickness(12)
        };
        UpdateShellCorners((double)GetValue(ShellRadiusProperty));
        HideDirection.Data = Geometry.Parse(_islandEdge switch
        {
            IslandEdge.Left => "M14,5 L7,12 14,19",
            IslandEdge.Right => "M7,5 L14,12 7,19",
            IslandEdge.Bottom => "M5,7 L12,14 19,7",
            _ => "M5,14 L12,7 19,14"
        });
        HideButton.ToolTip = _islandEdge == IslandEdge.Floating ? "Küçük adaya dön" : "Kenara gizle";
        HideDirection.Width = _islandEdge is IslandEdge.Left or IslandEdge.Right ? 8 : 14;
        HideDirection.Height = _islandEdge is IslandEdge.Left or IslandEdge.Right ? 14 : 8;
    }
    private void UpdateShellCorners(double radius)
    {
        if (Shell is null) return;
        Shell.CornerRadius = _islandEdge switch
        {
            IslandEdge.Top => new CornerRadius(0, 0, radius, radius),
            IslandEdge.Right => new CornerRadius(radius, 0, 0, radius),
            IslandEdge.Bottom => new CornerRadius(radius, radius, 0, 0),
            IslandEdge.Left => new CornerRadius(0, radius, radius, 0),
            _ => new CornerRadius(radius)
        };
    }

    private void ReflowPlacement()
    {
        if (_dragging) return;
        UpdatePlacementMonitor(); UpdatePlacementChrome();
        RefreshCompact(); ApplyDockPosition(false);
    }
    private void MovePlacement(bool animated, double width, double height)
    {
        if (_dragging || _placementBounds.Width <= 0) return;
        var target = DockLayout.Place(_placementBounds, _placementWork, _islandEdge, _anchorX, _anchorY, width, height, _dockHidden);
        void Move(DependencyProperty property, double from, double to)
        {
            if (animated && double.IsFinite(from)) BeginAnimation(property, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(280))
                { EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut } });
            else { BeginAnimation(property, null); SetValue(property, to); }
        }
        Move(LeftProperty, Left, target.X); Move(TopProperty, Top, target.Y);
    }

    private bool IsPlacementControl(object source)
    {
        for (DependencyObject? node = source as DependencyObject; node is not null && node != Pill;)
        {
            if (node is ButtonBase or Slider or TextBoxBase) return true;
            node = node is FrameworkContentElement content ? content.Parent : VisualTreeHelper.GetParent(node);
        }
        return false;
    }
    private void Pill_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.IslandPositionLocked || IsPlacementControl(e.OriginalSource) || !GetCursorPos(out _pressCursor)) return;
        _dragArmed = true; _autoHideTimer.Stop(); _hoverRevealTimer.Stop();
        var matrix = (PresentationSource.FromVisual(this) as HwndSource)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        _dragScaleX = matrix.M11; _dragScaleY = matrix.M22;
        if (!Mouse.Capture(Pill, CaptureMode.Element)) _dragArmed = false;
    }
    private void Placement_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragArmed || e.LeftButton != MouseButtonState.Pressed || !GetCursorPos(out var cursor)) return;
        if (!_dragging)
        {
            if (Math.Abs(cursor.X - _pressCursor.X) * _dragScaleX < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(cursor.Y - _pressCursor.Y) * _dragScaleY < SystemParameters.MinimumVerticalDragDistance) return;
            _dockHidden = false;
            double width = ActualWidth, height = ActualHeight;
            BeginAnimation(WidthProperty, null); BeginAnimation(HeightProperty, null);
            Width = _targetWidth = width; Height = _targetHeight = height;
            ApplyDockPosition(false);
            _dragStartLeft = Left; _dragStartTop = Top;
            var matrix = (PresentationSource.FromVisual(this) as HwndSource)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            _dragScaleX = matrix.M11; _dragScaleY = matrix.M22;
            _dragging = true; Pill.Cursor = Cursors.SizeAll; SyncActivityPause();
        }
        Left = _dragStartLeft + (cursor.X - _pressCursor.X) * _dragScaleX;
        Top = _dragStartTop + (cursor.Y - _pressCursor.Y) * _dragScaleY;
        e.Handled = true;
    }
    private void Pill_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragArmed) return;
        bool moved = _dragging;
        FinishPlacementDrag();
        if (moved) e.Handled = true;
    }
    private void Pill_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_dragArmed) FinishPlacementDrag();
    }
    private void FinishPlacementDrag(bool cancel = false)
    {
        bool moved = _dragging;
        _dragging = false; _dragArmed = false;
        if (Mouse.Captured == Pill) Mouse.Capture(null);
        Pill.Cursor = Cursors.Hand;
        if (moved && !cancel)
        {
            if (GetCursorPos(out var cursor)) UpdatePlacementMonitor(Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y)));
            _islandEdge = DockLayout.Snap(_placementBounds, new DockBox(Left, Top, ActualWidth, ActualHeight));
            _anchorX = DockLayout.Anchor((Left + ActualWidth / 2 - _placementBounds.Left) / _placementBounds.Width);
            _anchorY = DockLayout.Anchor((Top - _placementBounds.Top) / _placementBounds.Height, 0.15);
            SavePlacement();
        }
        if (moved)
        {
            UpdatePlacementChrome(); RefreshCompact(); ApplyDockPosition(true);
            SyncActivityPause();
        }
        QueueAutoHide(1);
    }
    private void SavePlacement()
    {
        _settings.IslandEdge = _islandEdge.ToString(); _settings.IslandMonitor = _monitorName;
        _settings.IslandAnchorX = _anchorX; _settings.IslandAnchorY = _anchorY; SaveSettings();
    }
    private void RefreshPlacementButtons()
    {
        PlacementLockIcon.Data = Geometry.Parse(_settings.IslandPositionLocked
            ? "M6,10 L18,10 18,21 6,21 Z M8,10 V6 A4,4 0 0 1 16,6 V10 M12,14 V17"
            : "M6,10 L18,10 18,21 6,21 Z M8,10 V6 A4,4 0 0 1 16,6 M12,14 V17");
        PlacementLockIcon.Stroke = new SolidColorBrush(_settings.IslandPositionLocked ? Color.FromRgb(220, 190, 156) : Color.FromRgb(120, 120, 130));
        PlacementLockButton.ToolTip = _settings.IslandPositionLocked ? "Konum kilitli · açmak için tıkla" : "Konumu kilitle";
    }
    private void PlacementLock_Click(object sender, RoutedEventArgs e)
    {
        _settings.IslandPositionLocked = !_settings.IslandPositionLocked;
        SaveSettings(); RefreshPlacementButtons(); e.Handled = true;
    }
    private void PlacementReset_Click(object sender, RoutedEventArgs e)
    {
        if (_dragArmed) FinishPlacementDrag(true);
        _islandEdge = IslandEdge.Top; _anchorX = 0.5; _anchorY = 0.15; _dockHidden = false;
        UpdatePlacementMonitor(Forms.Screen.PrimaryScreen); SavePlacement();
        UpdatePlacementChrome(); RefreshCompact(); ApplyDockPosition(true); e.Handled = true;
    }
}
