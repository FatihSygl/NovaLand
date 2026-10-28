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
    private string FocusTaskTitle => FocusTask?.Text ?? "Bir adım daha.";
    private TodoEntry? HomeTask => HomeSelection.Task(_todo.Items, _focusTaskId, _timerRunning);
    private void RefreshHome()
    {
        if (HomeOverview is null) return;
        if (_focusTaskId is not null && FocusTask is null)
        {
            _focusTaskId = null;
            TimerLabel.Text = _timerRunning ? "Odaklanma zamanı" : "Odak seansı";
        }
        if (!_expanded || _tab != "Music") return;
        bool music = _mediaAvailable;
        HomeOverview.Visibility = music ? Visibility.Collapsed : Visibility.Visible;
        foreach (var element in new FrameworkElement[] { SongInfo, TimelineSlider, TrackTimeRow, TransportRow })
            element.Visibility = music ? Visibility.Visible : Visibility.Collapsed;
        var task = HomeTask;
        HomeTaskLabel.Text = _timerRunning ? "ŞİMDİ ODAKTASIN" : "SIRADAKİ ADIM";
        HomeTaskText.Text = task?.Text ?? (_timerRunning ? "Kendine ayırdığın zaman." : "Henüz görev yok.");
        HomeFocusButton.Content = _timerRunning ? $"Odağa dön · {TimerValue.Text}" : task is null ? "Görev ekle →" : "Odaklan →";
        ReminderEntry? reminder = null;
        foreach (var candidate in _reminders.Items)
            if (!candidate.IsTriggered && (reminder is null || candidate.Due < reminder.Due)) reminder = candidate;
        HomeReminderLabel.Text = reminder is null ? "HATIRLATICILAR" : $"SIRADAKİ · {reminder.Due.LocalDateTime:dd MMM HH:mm}";
        HomeReminderText.Text = reminder?.Text ?? "Hatırlatıcı ekle";
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
    private async Task CopyFeedback(object sender)
    {
        if (sender is not Button button || !_copyFeedbackButtons.Add(button)) return;
        var content = button.Content;
        double minWidth = button.MinWidth;
        button.MinWidth = Math.Max(minWidth, button.ActualWidth);
        try
        {
            var tick = new System.Windows.Controls.Grid { Width = 25, Height = 21,
                RenderTransformOrigin = new System.Windows.Point(.5, .5), RenderTransform = new System.Windows.Media.ScaleTransform(1, 1) };
            var ring = new System.Windows.Shapes.Ellipse { Width = 19, Height = 19, StrokeThickness = 1,
                Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(170, 239, 190)), Opacity = .35,
                RenderTransformOrigin = new System.Windows.Point(.5, .5), RenderTransform = new System.Windows.Media.ScaleTransform(1, 1) };
            var check = new System.Windows.Shapes.Path { Data = System.Windows.Media.Geometry.Parse("M4,10 L9,15 L20,4"),
                Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(176, 244, 195)),
                StrokeThickness = 2, StrokeStartLineCap = System.Windows.Media.PenLineCap.Round,
                StrokeEndLineCap = System.Windows.Media.PenLineCap.Round, StrokeLineJoin = System.Windows.Media.PenLineJoin.Round };
            tick.Children.Add(ring); tick.Children.Add(check);
            button.Content = tick;
            if (SystemParameters.ClientAreaAnimation)
            {
                tick.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
                check.StrokeDashArray = new System.Windows.Media.DoubleCollection { 25, 25 };
                check.BeginAnimation(System.Windows.Shapes.Shape.StrokeDashOffsetProperty, new DoubleAnimation(25, 0, TimeSpan.FromMilliseconds(320)));
                ring.BeginAnimation(OpacityProperty, new DoubleAnimation(.5, 0, TimeSpan.FromMilliseconds(650)));
                var ringScale = (System.Windows.Media.ScaleTransform)ring.RenderTransform;
                ringScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation(.6, 1.7, TimeSpan.FromMilliseconds(650)));
                ringScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation(.6, 1.7, TimeSpan.FromMilliseconds(650)));
                var scale = (System.Windows.Media.ScaleTransform)tick.RenderTransform;
                foreach (var property in new[] { System.Windows.Media.ScaleTransform.ScaleXProperty, System.Windows.Media.ScaleTransform.ScaleYProperty })
                    scale.BeginAnimation(property, new DoubleAnimation(.5, 1, TimeSpan.FromMilliseconds(320))
                    { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = .3 } });
            }
            await Task.Delay(1700);
            if (SystemParameters.ClientAreaAnimation)
            {
                tick.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180)));
                await Task.Delay(180);
            }
        }
        finally { button.Content = content; button.MinWidth = minWidth; _copyFeedbackButtons.Remove(button); }
    }
}
