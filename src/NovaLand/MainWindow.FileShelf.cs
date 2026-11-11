using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    }

    private void FileGrip_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { IsMouseCaptured: true } grip)
        {
            grip.ReleaseMouseCapture();
            e.Handled = true;
        }
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
        var viewport = FindFileScrollViewer(FileList);
        var autoScroll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(35) };
        autoScroll.Tick += (_, _) =>
        {
            if (viewport is not null && _fileDragScrollStep != 0)
            {
                ScrollFileListBy(viewport, _fileDragScrollStep);
                SetFileDropMarker(null);
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
            SetFileDropMarker(null);
        }
    }

    private void FileList_DragOver(object sender, DragEventArgs e)
    {
        if (!IsFileReorder(e)) return;
        e.Handled = true;
        e.Effects = System.Windows.DragDropEffects.Move;

        var target = FileDropItem(e);
        SetFileDropMarker(target, target is not null && IsAfterFileRow(e, target));

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
        _fileDragScrollStep = 0;
        SetFileDropMarker(null);
    }

    private void FileList_Drop(object sender, DragEventArgs e)
    {
        if (!IsFileReorder(e)) return;
        e.Handled = true;
        e.Effects = System.Windows.DragDropEffects.None;
        if (FileDropItem(e) is not { DataContext: FileEntry target } row ||
            _draggedFile is not { } file) return;

        int from = _files.IndexOf(file);
        int to = _files.IndexOf(target);
        if (from < 0 || to < 0) return;
        if (IsAfterFileRow(e, row)) to++;
        if (from < to) to--;

        if (from != to)
        {
            _files.Move(from, to);
            SaveFiles();
        }
        e.Effects = System.Windows.DragDropEffects.Move;
    }

    private bool IsFileReorder(DragEventArgs e) =>
        _draggedFile is not null && e.Data.GetDataPresent(FileReorderFormat, autoConvert: false);

    private ListBoxItem? FileDropItem(DragEventArgs e) =>
        e.OriginalSource is DependencyObject hit
            ? ItemsControl.ContainerFromElement(FileList, hit) as ListBoxItem
            : null;

    private static bool IsAfterFileRow(DragEventArgs e, ListBoxItem row) =>
        e.GetPosition(row).Y >= row.ActualHeight / 2;

    private void SetFileDropMarker(ListBoxItem? row, bool after = false)
    {
        if (_fileDropMarker is not null && _fileDropMarker != row)
            _fileDropMarker.ClearValue(FrameworkElement.TagProperty);
        _fileDropMarker = row;
        if (row is not null) row.Tag = after ? "After" : "Before";
    }

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
