using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NovaIsland.Models;
using NovaIsland.Services;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Button = System.Windows.Controls.Button;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Image = System.Windows.Controls.Image;
using Orientation = System.Windows.Controls.Orientation;

namespace NovaIsland;

public partial class MainWindow
{
    private void RefreshQuickSite()
    {
        _audioTargets.Clear();
        _audioTargets.AddRange(_browserRows);
        _audioTargets.AddRange(_appRows);
        var selected = _audioTargets.FirstOrDefault(t => t.Key == _selectedAudioKey);
        if (selected is null) _selectedAudioKey = "";
        QuickSiteCard.DataContext = selected;
        QuickSiteCard.Visibility = selected is null ? Visibility.Collapsed : Visibility.Visible;
        SystemVolumeRow.Visibility = selected is null ? Visibility.Visible : Visibility.Collapsed;
        AudioSourceTitle.Text = selected is null ? "Sistem" : selected.Name + " · " + selected.Kind;
    }

    private string _selectedAudioKey = "";
    private void AudioSource_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = AudioSourceButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom, MinWidth = 230, MaxHeight = 320 };
        void Add(string name, string key)
        {
            var item = new MenuItem { Header = name, IsCheckable = true, IsChecked = _selectedAudioKey == key };
            item.Click += (_, _) => { _selectedAudioKey = key; RefreshQuickSite(); Activate(); };
            menu.Items.Add(item);
        }
        Add("Sistem", "");
        if (_audioTargets.Count > 0) menu.Items.Add(new Separator());
        foreach (var target in _audioTargets) Add(target.Name + " · " + target.Kind, target.Key);
        _dialogOpen = true; SyncActivityPause();
        menu.Closed += (_, _) => { _dialogOpen = false; SyncActivityPause(); };
        menu.IsOpen = true;
    }

    private void PreviewImage_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ImageEntry entry) return;
        try
        {
            var bitmap = ImageTools.Decode(entry.Png);
            var work = _placementWork;
            var preview = new Window
            {
                Owner = this, Title = "Görsel önizleme", Width = System.Math.Min(900, work.Width - 24),
                Height = System.Math.Min(680, work.Height - 24), WindowStartupLocation = WindowStartupLocation.Manual,
                WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true, Background = Brushes.Transparent, ShowInTaskbar = false,
                Topmost = Topmost
            };
            preview.Left = work.Left + (work.Width - preview.Width) / 2;
            preview.Top = work.Top + (work.Height - preview.Height) / 2;
            var frame = new Border { Background = new SolidColorBrush(Color.FromRgb(20,20,22)), CornerRadius = new CornerRadius(20), Padding = new Thickness(18), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(50,50,54)) };
            var layout = new DockPanel(); frame.Child = layout; preview.Content = frame;
            var toolbar = new DockPanel { Margin = new Thickness(0,0,0,14), LastChildFill = true };
            DockPanel.SetDock(toolbar, Dock.Top); layout.Children.Add(toolbar);
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(actions, Dock.Right); toolbar.Children.Add(actions);
            Button Action(string title) { var b = new Button { Content = title, Style = (Style)FindResource("GhostButton"), Margin = new Thickness(3,0,0,0), Padding = new Thickness(10,7,10,7) }; actions.Children.Add(b); return b; }
            var fit = Action("Sığdır"); var actual = Action("%100"); var copy = Action("Kopyala"); var close = Action("✕");
            close.ToolTip = "Kapat (Esc)"; close.Click += (_,_) => preview.Close();
            copy.Click += async (_,_) => { if (await ClipboardService.CopyImageAsync(entry.Png)) await CopyFeedback(copy); else copy.Content = "Tekrar dene"; };
            var title = new TextBlock { Text = entry.Name, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            toolbar.Children.Add(title);
            var picture = new Image { Source = bitmap, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var scroll = new ScrollViewer { Content = picture, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            layout.Children.Add(scroll);
            fit.Click += (_,_) => { picture.Width = picture.Height = double.NaN; picture.Stretch = Stretch.Uniform; scroll.HorizontalScrollBarVisibility = scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled; };
            actual.Click += (_,_) => { picture.Width = bitmap.PixelWidth; picture.Height = bitmap.PixelHeight; picture.Stretch = Stretch.Fill; scroll.HorizontalScrollBarVisibility = scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; };
            preview.KeyDown += (_,args) => { if (args.Key == Key.Escape) { preview.Close(); args.Handled = true; } };
            _dialogOpen = true; SyncActivityPause();
            try { preview.ShowDialog(); }
            finally { _dialogOpen = false; SyncActivityPause(); Activate(); }
        }
        catch (System.Exception ex) { DiagnosticLog.Write("ImagePreview", ex); StatusText.Text = "Görsel önizlemesi açılamadı."; }
    }
}
