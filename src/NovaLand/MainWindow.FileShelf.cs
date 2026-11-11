using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NovaIsland.Models;
using DragEventArgs = System.Windows.DragEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace NovaIsland;

public partial class MainWindow
{
    private const string FileReorderFormat = "NovaLand.FileShelf.Reorder";
    private const double FileWheelStep = 24;
    private const double FileDragScrollZone = 24;
    private Point _fileGripOrigin;
    private FileEntry? _draggedFile;
    private ListBoxItem? _fileDropMarker;
    private Point? _fileDragPosition;
    private int? _fileInsertionIndex;
    private double _fileDragScrollStep;

    private void FileList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var viewport = FindFileScrollViewer(FileList);
        if (viewport is null || viewport.ScrollableHeight <= 0) return;

        ScrollFileListBy(viewport, -e.Delta * FileWheelStep / Mouse.MouseWheelDeltaForOneLine);
        e.Handled = true;
    }

    private void FileGrip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_files.Count < 2 || sender is not FrameworkElement grip) return;
        _fileGripOrigin = e.GetPosition(FileList);
        e.Handled = grip.CaptureMouse();
        if (e.Handled && grip.RenderTransform is ScaleTransform scale) AnimateFileScale(scale, 0.88, 90);
    }

    private void FileGrip_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { IsMouseCaptured: true } grip)
        {
            grip.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void FileGrip_LostCapture(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement { RenderTransform: ScaleTransform scale })
            AnimateFileScale(scale, 1, 125);
    }

    private void FileGrip_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement { IsMouseCaptured: true, DataContext: FileEntry file } grip) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            grip.ReleaseMouseCapture();
            return;
        }

        Point position = e.GetPosition(FileList);
        if (Math.Abs(position.X - _fileGripOrigin.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _fileGripOrigin.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        grip.ReleaseMouseCapture();
        e.Handled = true;
        _draggedFile = file;
        var sourceRow = FileList.ItemContainerGenerator.ContainerFromItem(file) as ListBoxItem;
        var sourceContent = FileRowPart(sourceRow, "FileRowContent");
        if (sourceContent is not null) AnimateFileOpacity(sourceContent, 0.42, 130);
        FileDragName.Text = file.Name;
        MoveFileDragCard(position);
        FileDragCardScale.ScaleX = FileDragCardScale.ScaleY = 0.93;
        AnimateFileScale(FileDragCardScale, 1, 155);
        FileDragCard.Opacity = 0.35;
        AnimateFileOpacity(FileDragCard, 0.96, 145);
        var viewport = FindFileScrollViewer(FileList);
        var autoScroll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(35) };
        autoScroll.Tick += (_, _) =>
        {
            if (viewport is not null && _fileDragScrollStep != 0)
            {
                ScrollFileListBy(viewport, _fileDragScrollStep);
            }
        };
        autoScroll.Start();
        try
        {
            var data = new System.Windows.DataObject(FileReorderFormat, true);
            System.Windows.DragDrop.DoDragDrop(grip, data, System.Windows.DragDropEffects.Move);
        }
        finally
        {
            autoScroll.Stop();
            _fileDragScrollStep = 0;
            _draggedFile = null;
            _fileDragPosition = null;
            FileDragOverlay.Visibility = Visibility.Collapsed;
            SetFileDropMarker(null);
            if (sourceContent is not null) AnimateFileOpacity(sourceContent, 1, 150);
        }
    }

    private void FileList_DragOver(object sender, DragEventArgs e)
    {
        if (!IsFileReorder(e)) return;
        e.Handled = true;
        var position = e.GetPosition(FileList);
        _fileDragPosition = position;
        MoveFileDragCard(position);
        var target = FileDropItem(e.OriginalSource);
        e.Effects = target is null ? System.Windows.DragDropEffects.None : System.Windows.DragDropEffects.Move;
        UpdateFileDropMarker(target, position);

        var viewport = FindFileScrollViewer(FileList);
        _fileDragScrollStep = 0;
        if (viewport is null || viewport.ScrollableHeight <= 0) return;
        double y = e.GetPosition(viewport).Y;
        if (y < FileDragScrollZone && viewport.VerticalOffset > 0) _fileDragScrollStep = -10;
        else if (y > viewport.ViewportHeight - FileDragScrollZone &&
            viewport.VerticalOffset < viewport.ScrollableHeight) _fileDragScrollStep = 10;
    }

    private void FileList_DragLeave(object sender, DragEventArgs e)
    {
        if (!IsFileReorder(e)) return;
        if (GetCursorPos(out var cursor) &&
            new Rect(FileList.RenderSize).Contains(FileList.PointFromScreen(new Point(cursor.X, cursor.Y)))) return;
        _fileDragScrollStep = 0;
        _fileDragPosition = null;
        FileDragOverlay.Visibility = Visibility.Collapsed;
        SetFileDropMarker(null);
    }

    private void FileList_Drop(object sender, DragEventArgs e)
    {
        if (!IsFileReorder(e)) return;
        e.Handled = true;
        e.Effects = System.Windows.DragDropEffects.None;
        if (FileDropItem(e.OriginalSource) is not { DataContext: FileEntry target } row ||
            _draggedFile is not { } file) return;

        int from = _files.IndexOf(file);
        int to = _files.IndexOf(target);
        if (from < 0 || to < 0) return;
        if (IsAfterFileRow(e.GetPosition(FileList), row)) to++;
        if (from < to) to--;

        if (from != to)
        {
            SetFileDropMarker(null);
            _files.Move(from, to);
            SaveFiles();
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => AnimateFileArrival(file, from < to)));
        }
        e.Effects = System.Windows.DragDropEffects.Move;
    }

    private bool IsFileReorder(DragEventArgs e) =>
        _draggedFile is not null && e.Data.GetDataPresent(FileReorderFormat, autoConvert: false);

    private ListBoxItem? FileDropItem(object? hit) =>
        hit is DependencyObject element ? ItemsControl.ContainerFromElement(FileList, element) as ListBoxItem : null;

    private bool IsAfterFileRow(Point position, ListBoxItem row) =>
        FileList.TranslatePoint(position, row).Y >= row.ActualHeight / 2;

    private void FileList_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_draggedFile is not null && _fileDragPosition is { } position && e.VerticalChange != 0)
            UpdateFileDropMarker(FileDropItem(FileList.InputHitTest(position)), position);
    }

    private void MoveFileDragCard(Point position)
    {
        FileDragOverlay.Visibility = Visibility.Visible;
        FileDragCardPosition.X = Math.Clamp(position.X + 17, 0, Math.Max(0, FileList.ActualWidth - FileDragCard.Width - 5));
        FileDragCardPosition.Y = Math.Clamp(position.Y - FileDragCard.Height / 2, 0,
            Math.Max(0, FileList.ActualHeight - FileDragCard.Height));
    }

    private void UpdateFileDropMarker(ListBoxItem? row, Point position)
    {
        if (row?.DataContext is not FileEntry target || _draggedFile is not { } file)
        {
            SetFileDropMarker(null);
            return;
        }

        bool after = IsAfterFileRow(position, row);
        int from = _files.IndexOf(file);
        int targetIndex = _files.IndexOf(target);
        int insertion = targetIndex + (after ? 1 : 0);
        if (from < 0 || targetIndex < 0 || insertion == from || insertion == from + 1)
        {
            SetFileDropMarker(null);
            return;
        }

        if (_fileInsertionIndex == insertion && _fileDropMarker == row) return;
        SetFileDropMarker(row, after, insertion);
    }

    private void SetFileDropMarker(ListBoxItem? row, bool after = false, int? insertion = null)
    {
        if (_fileDropMarker is not null && _fileDropMarker != row)
            _fileDropMarker.ClearValue(FrameworkElement.TagProperty);

        bool animate = _fileInsertionIndex != insertion;
        _fileDropMarker = row;
        _fileInsertionIndex = insertion;
        if (row is null) return;

        row.Tag = after ? "After" : "Before";
        var line = FileRowPart(row, "InsertionLine");
        if (line is null || !animate || !SystemParameters.ClientAreaAnimation) return;
        line.BeginAnimation(OpacityProperty, FileShelfAnimation(0.2, 1, 125));
        if (line.RenderTransform is ScaleTransform scale)
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, FileShelfAnimation(0.78, 1, 145));
    }

    private void AnimateFileArrival(FileEntry file, bool movedDown)
    {
        if (!SystemParameters.ClientAreaAnimation) return;
        var row = FileList.ItemContainerGenerator.ContainerFromItem(file) as ListBoxItem;
        if (FileRowPart(row, "FileRowTint") is { } tint)
            tint.BeginAnimation(OpacityProperty, FileShelfAnimation(1, 0, 320));
        if (FileRowPart(row, "FileRowContent")?.RenderTransform is TranslateTransform offset)
            offset.BeginAnimation(TranslateTransform.YProperty, FileShelfAnimation(movedDown ? -7 : 7, 0, 185));
    }

    private static FrameworkElement? FileRowPart(ListBoxItem? row, string name) =>
        row?.Template?.FindName(name, row) as FrameworkElement;

    private static void AnimateFileOpacity(FrameworkElement element, double target, int milliseconds)
    {
        double current = element.Opacity;
        element.BeginAnimation(OpacityProperty, null);
        element.Opacity = target;
        if (SystemParameters.ClientAreaAnimation)
            element.BeginAnimation(OpacityProperty, FileShelfAnimation(current, target, milliseconds));
    }

    private static void AnimateFileScale(ScaleTransform scale, double target, int milliseconds)
    {
        double current = scale.ScaleX;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        scale.ScaleX = scale.ScaleY = target;
        if (!SystemParameters.ClientAreaAnimation) return;

        var animation = FileShelfAnimation(current, target, milliseconds);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    private static DoubleAnimation FileShelfAnimation(double from, double to, int milliseconds) =>
        new(from, to, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };

    private static void ScrollFileListBy(ScrollViewer viewport, double distance)
    {
        double offset = Math.Clamp(viewport.VerticalOffset + distance, 0, viewport.ScrollableHeight);
        viewport.ScrollToVerticalOffset(offset);
    }

    private static ScrollViewer? FindFileScrollViewer(DependencyObject node)
    {
        if (node is ScrollViewer viewer) return viewer;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            var viewerChild = FindFileScrollViewer(VisualTreeHelper.GetChild(node, i));
            if (viewerChild is not null) return viewerChild;
        }
        return null;
    }
}
