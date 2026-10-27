using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using NovaIsland.Models;
using Button = System.Windows.Controls.Button;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
namespace NovaIsland;
public partial class MainWindow
{
    private Guid? _focusTaskId;
    private readonly HashSet<Button> _copyFeedbackButtons = new();
    private TodoEntry? FocusTask => _todo.Items.FirstOrDefault(x => x.Id == _focusTaskId && !x.IsCompleted);
    private string FocusTaskTitle => FocusTask?.Text ?? "Odak";
    private TodoEntry? HomeTask => HomeSelection.Task(_todo.Items, _focusTaskId, _timerRunning);
    private void RefreshHome()
    {
        if (HomeOverview is null) return;
        if (_focusTaskId is not null && FocusTask is null)
        {
            _focusTaskId = null;
            TimerLabel.Text = _timerRunning ? "Odaklanma zamanı" : "Biraz alan aç. Odaklan.";
        }
        bool music = _mediaAvailable;
        HomeOverview.Visibility = music ? Visibility.Collapsed : Visibility.Visible;
        foreach (var element in new FrameworkElement[] { SongInfo, TimelineSlider, TrackTimeRow, TransportRow })
            element.Visibility = music ? Visibility.Visible : Visibility.Collapsed;
        var task = HomeTask;
        HomeTaskLabel.Text = _timerRunning ? "ŞİMDİ ODAKTASIN" : "SIRADAKİ ADIM";
        HomeTaskText.Text = task?.Text ?? (_timerRunning ? "Kendine ayırdığın zaman." : "Aklındaki ilk adımı yaz.");
        HomeFocusButton.Content = _timerRunning ? $"Odağa dön · {TimerValue.Text}" : task is null ? "Görev ekle →" : "Odaklan →";
        var reminder = _reminders.Items.Where(x => !x.IsTriggered).OrderBy(x => x.Due).FirstOrDefault();
        HomeReminderLabel.Text = reminder is null ? "HATIRLATICILAR" : $"SIRADAKİ · {reminder.Due.LocalDateTime:dd MMM HH:mm}";
        HomeReminderText.Text = reminder?.Text ?? "Hatırlatıcı ekle";
        MoreBadge.Visibility = NoticeBadge.Visibility;
        MoreButton.Style = (Style)FindResource(_tab is "Files" or "Notifications" or "Controls" or "Settings" ? "DockActive" : "DockTab");
    }
    private void HomeFocus_Click(object sender, RoutedEventArgs e)
    {
        if (_timerRunning) { SetTab("Focus"); return; }
        if (HomeTask is TodoEntry task) StartTaskFocus(task);
        else { SetTab("Todo"); TodoInput.Focus(); }
    }
    private void HomeReminder_Click(object sender, RoutedEventArgs e)
    {
        SetTab("Focus");
        if (!_reminders.Items.Any(x => !x.IsTriggered)) ReminderOpen_Click(sender, e);
    }
    private void TodoFocus_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not TodoEntry task) return;
        if (task.IsCompleted) { StatusText.Text = "Odaklanmak için görevi yeniden aç."; return; }
        StartTaskFocus(task);
    }
    private void StartTaskFocus(TodoEntry task)
    {
        // Moving an active session to another task keeps its remaining time.
        _focusTaskId = task.Id;
        if (!_timerRunning)
        {
            if (_remaining <= TimeSpan.Zero) _remaining = _duration;
            _endAt = DateTimeOffset.UtcNow + _remaining;
            _timerRunning = true;
        }
        TimerStart.Content = "Duraklat";
        TimerLabel.Text = task.Text;
        SetTab("Focus"); RefreshTimerUi();
    }
    private void More_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = MoreButton, Placement = PlacementMode.Top, MinWidth = 190 };
        foreach (var (key, title) in new[] { ("Files", "Dosya rafı"), ("Notifications", NoticeBadge.Visibility == Visibility.Visible ? "Bildirimler · Yeni" : "Bildirimler"), ("Controls", "Kontroller"), ("Settings", "Ayarlar") })
        {
            var item = new MenuItem { Header = title, IsCheckable = true, IsChecked = _tab == key };
            item.Click += (_, _) => { SetTab(key); Activate(); };
            menu.Items.Add(item);
        }
        _dialogOpen = true; SyncActivityPause();
        menu.Closed += (_, _) => { _dialogOpen = false; SyncActivityPause(); };
        menu.IsOpen = true;
    }
    private async Task CopyFeedback(object sender)
    {
        if (sender is not Button button || !_copyFeedbackButtons.Add(button)) return;
        var content = button.Content;
        try
        {
            button.Content = new TextBlock { Text = "✓", FontSize = 15, Foreground = System.Windows.Media.Brushes.LightGreen };
            if (SystemParameters.ClientAreaAnimation)
                button.BeginAnimation(OpacityProperty, new DoubleAnimation(.45, 1, TimeSpan.FromMilliseconds(180)));
            await Task.Delay(850);
        }
        finally { button.Content = content; _copyFeedbackButtons.Remove(button); }
    }
}
