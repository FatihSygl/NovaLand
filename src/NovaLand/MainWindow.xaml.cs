using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Rectangle = System.Windows.Shapes.Rectangle;
using Color = System.Windows.Media.Color;
using System.Windows.Threading;
using NovaIsland.Models;
using NovaIsland.Services;
using Forms = System.Windows.Forms;

namespace NovaIsland;

public partial class MainWindow : Window
{
    private readonly LocalSettings _settings;
    private readonly ObservableCollection<FileEntry> _files = new();
    private readonly MediaService _media = new();
    private BrowserAudioBridge? _browserAudio;
    private readonly ObservableCollection<BrowserAudioRow> _browserRows = new();
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _mediaPoll = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _brightnessDebounce = new() { Interval = TimeSpan.FromMilliseconds(320) };
    private Forms.NotifyIcon? _tray;
    private System.Drawing.Icon? _trayBrandIcon;
    private bool _ready;
    private bool _expanded;
    private bool _exiting;
    private bool _mediaBusy;
    private bool _brightnessSupported;
    private DateTime _volumeChangedUntil = DateTime.MinValue;
    private DateTimeOffset _lastWaterReminder = DateTimeOffset.UtcNow;
    private DateTimeOffset _endAt;
    private TimeSpan _duration = TimeSpan.FromMinutes(25);
    private TimeSpan _remaining = TimeSpan.FromMinutes(25);
    private bool _timerRunning;
    private bool _mediaAvailable;
    private bool _mediaPlaying;
    private bool _loaded;
    private bool _dialogOpen;
    private readonly AudioSpectrumService _spectrum = new();
    private readonly DispatcherTimer _waveTick = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly double[] _waveLevels = new double[4];
    private string _tab = "Music";
    private double _targetWidth = 244;
    private double _targetHeight = 56;
    private LaunchGreeting? _launchGreeting;
    private readonly ReminderBoard _reminders = new();
    private readonly ReminderStore _reminderStore = new();
    private bool _reminderComposerOpen;
    private bool _settingReminderClock;
    private bool _reminderNotice;
    private string _reminderClock = "";
    private const string AlarmGeometry = "M12,5 A8,8 0 1 1 11.99,5 M12,9 L12,13 15,15 M3,5 L6,2 M18,2 L21,5 M6,20 L4,23 M18,20 L20,23";
    private readonly TodoBoard _todo = new();
    private readonly TodoStore _todoStore = new();
    private readonly ClipboardHistory _clipboardHistory = new();
    private readonly ImageHistory _images = new();
    private readonly ObservableCollection<NoticeEntry> _notices = new();
    private readonly BluetoothAudioService _bluetooth = new();
    private readonly ScreenshotService _screenshots = new();
    private readonly DispatcherTimer _devicePoll = new() { Interval = TimeSpan.FromSeconds(4) };
    private bool _deviceBusy;
    private readonly ActivityQueue<Action> _activities = new();
    private readonly DispatcherTimer _activityTick = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly PinnedClipboardStore _pins = new();
    private bool _showingNotice, _wasVolume;
    private NovaIsland.Services.MediaState? _activitySong;
    private DateTimeOffset _volumeUntil;
    private int _wheelRemainder, _compactVolume;
    private string _noticeTitle = "", _noticeBody = "";
    private AudioDeviceNotice? _audioNotice;
    private const string BellGeometry = "M5,17 L19,17 17,14 17,9 A5,5 0 0 0 7,9 L7,14 Z M10,20 A2,2 0 0 0 14,20";
    private const string HeadphoneGeometry = "M4,14 V10 A8,8 0 0 1 20,10 V14 M4,12 L7,12 7,21 4,21 Q2,21 2,19 L2,15 Q2,12 4,12 M20,12 L17,12 17,21 20,21 Q22,21 22,19 L22,15 Q22,12 20,12";
    private ClipboardService? _clipboard;
    private const int AutoHideDelayMs = 5000;
    private readonly DispatcherTimer _autoHideTimer = new() { Interval = TimeSpan.FromMilliseconds(AutoHideDelayMs) };
    private readonly DispatcherTimer _hoverRevealTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private readonly DispatcherTimer _seekDebounce = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool _dockHidden;
    private bool _forceHide;
    private bool _updatingTimeline;
    private TimeSpan _mediaPosition, _mediaDuration;
    private DateTimeOffset _mediaReadAt;
    private string _songKey = "";
    private bool? _previousPlaying;
    private Color _mediaColor = Color.FromRgb(185, 248, 121);
    public static readonly DependencyProperty ShellRadiusProperty = DependencyProperty.Register(
        "ShellRadius", typeof(double), typeof(MainWindow), new PropertyMetadata(22.0,
        (d, e) => { var w = (MainWindow)d; w.UpdateShellCorners((double)e.NewValue); }));

    public MainWindow()
    {
        _settings = SettingsStore.Load();
        InitializeComponent();
        InitializePlacement();

        PinnedTextList.ItemsSource = _clipboardHistory.Pinned;
        PinnedImageList.ItemsSource = _images.Pinned;
        _activities.Changed += action =>
        {
            _updateNotice = false; _showingNotice = false; _audioNotice = null; _activitySong = null; _reminderNotice = false; _launchGreeting = null;
            NoticeIcon.RenderTransform = Transform.Identity;
            GreetingOrbit.BeginAnimation(RotateTransform.AngleProperty, null);
            GreetingSpark.BeginAnimation(OpacityProperty, null);
            GreetingHalo.BeginAnimation(OpacityProperty, null);
            if (action is not null)
            {
                action(); RevealFromEdge(); _autoHideTimer.Stop(); RefreshCompact();
            }
            else { RefreshCompact(); if (!_expanded && !VolumeOverlay) QueueAutoHide(1); }
        };
        _activityTick.Tick += (_, _) =>
        {
            SyncActivityPause(); _activities.Tick(DateTimeOffset.UtcNow);
            bool volume = VolumeOverlay;
            if (_wasVolume && !volume) { RefreshCompact(); if (!_activities.HasWork) QueueAutoHide(1); }
            _wasVolume = volume;
        };
        _activityTick.Start();
        _clipboardHistory.Pinned.CollectionChanged += (_, _) => UpdateClipboardEmpty();
        _images.Pinned.CollectionChanged += (_, _) => UpdateClipboardEmpty();
        _duration = _remaining = TimeSpan.FromMinutes(Math.Clamp(_settings.FocusMinutes, 1, 1440));
        FocusMinutesInput.Text = ((int)_duration.TotalMinutes).ToString();
        ReminderList.ItemsSource = _reminders.Items;
        foreach (var reminder in _reminderStore.Load()) _reminders.Items.Add(reminder);
        _reminders.Items.CollectionChanged += (_, _) => RefreshReminderUi();
        SetReminderClock(DateTime.Now.AddMinutes(1));
        RefreshReminderUi();
        TodoList.ItemsSource = _todo.Items;
        foreach (var item in _todoStore.Load()) _todo.Items.Add(item);
        _todo.Items.CollectionChanged += (_, _) => RefreshTodoUi();
        RefreshTodoUi();
        ImageList.ItemsSource = _images.Items;
        NoticeList.ItemsSource = _notices;
        _images.Items.CollectionChanged += (_, _) => ImageEmpty.Visibility = _images.Items.Count == 0 && _images.Pinned.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _notices.CollectionChanged += (_, _) => NoticeEmpty.Visibility = _notices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _bluetooth.Notice += HandleAudioNotice;
        _screenshots.ImageFound += (png, name, at) => { if (_settings.CaptureClipboard) _images.Add(png, name, at); };
        _devicePoll.Tick += async (_, _) => await ScanDevicesAsync();
        ClipboardList.ItemsSource = _clipboardHistory.Items;
        _clipboardHistory.Items.CollectionChanged += (_, _) => ClipboardEmpty.Visibility = _clipboardHistory.Items.Count == 0 && _clipboardHistory.Pinned.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _pins.Load(_clipboardHistory, _images);
        UpdateClipboardEmpty();
        AutoHideCheck.IsChecked = _settings.AutoHide;
        CaptureClipboardCheck.IsChecked = _settings.CaptureClipboard;
        _autoHideTimer.Tick += (_, _) =>
        {
            _autoHideTimer.Stop();
            if (!_expanded && !_dialogOpen && (_forceHide || !Shell.IsMouseOver)) HideToEdge();
        };
        _hoverRevealTimer.Tick += (_, _) =>
        {
            _hoverRevealTimer.Stop();
            if (Shell.IsMouseOver && !_exiting) RevealFromEdge();
        };
        _seekDebounce.Tick += async (_, _) =>
        {
            if (TimelineSlider.IsMouseCaptureWithin) return;
            _seekDebounce.Stop();
            var position = TimeSpan.FromSeconds(TimelineSlider.Value);
            await _media.SeekAsync(position);
            _mediaPosition = position; _mediaReadAt = DateTimeOffset.UtcNow;
        };
        _waveTick.Tick += (_, _) => UpdateAudioBars();
        FileList.ItemsSource = _files;
        _files.CollectionChanged += (_, _) => FileEmpty.Visibility = _files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var path in _settings.FilePaths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
            _files.Add(new FileEntry(path));

        StayTopCheck.IsChecked = _settings.AlwaysOnTop;
        StartupCheck.IsChecked = StartupService.IsEnabled();
        WaterCheck.IsChecked = _settings.WaterReminder;
        Topmost = _settings.AlwaysOnTop;
        _clock.Tick += Clock_Tick;
        _mediaPoll.Tick += async (_, _) => await RefreshMediaAsync();
        _brightnessDebounce.Tick += BrightnessDebounce_Tick;
        _ready = true;
        RefreshTimerUi();
        SetTab("Music");
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        RefreshCompact();
        ReflowPlacement();
        CreateTray();
        try { _browserAudio = new BrowserAudioBridge(); _browserAudio.Start(); }
        catch (Exception ex) { DiagnosticLog.Write("Browser audio initialization", ex); }
        RefreshBrowserAudio();
        // Refresh the registered executable after moving/updating the app, but never
        // enable login startup on behalf of a user who has it switched off.
        if (StartupService.IsEnabled() && !StartupService.SetEnabled(true))
            StatusText.Text = "Otomatik başlangıç yolu güncellenemedi. Ayarlardan yeniden etkinleştirebilirsin.";
        var greeting = LaunchGreeting.Create(_settings.WelcomeShown,
            Environment.GetCommandLineArgs().Any(arg => arg.Equals("--startup", StringComparison.OrdinalIgnoreCase)), DateTime.Now);
        if (greeting is not null)
            EnqueueActivity("launch-greeting", 30, () => RenderLaunchGreeting(greeting));
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            _clipboard = new ClipboardService(source) { Enabled = _settings.CaptureClipboard };
            _clipboard.TextReceived += text => _clipboardHistory.Add(text);
            _clipboard.ImageReceived += png => _images.Add(png, "Kopyalanan görsel", DateTime.Now);
            if (!_clipboard.Listening) StatusText.Text = "Pano dinleyicisi başlatılamadı.";
        }
        _devicePoll.Start();
        _ = ScanDevicesAsync();
        SystemParameters.StaticPropertyChanged += DisplaySettingsChanged;
        QueueAutoHide();
        _clock.Start();
        _mediaPoll.Start();
        Clock_Tick(this, EventArgs.Empty);

        // Initialize sliders without writing any new system settings.
        _ready = false;
        var volume = AudioService.ReadVolume();
        VolumeSlider.IsEnabled = volume.HasValue;
        if (volume.HasValue) VolumeSlider.Value = volume.Value;
        VolumeValue.Text = volume.HasValue ? $"{volume.Value}%" : "Desteklenmiyor";
        _ready = true;

        var brightness = await Task.Run(BrightnessService.Read);
        _brightnessSupported = brightness.HasValue;
        _ready = false;
        BrightnessSlider.IsEnabled = brightness.HasValue;
        if (brightness.HasValue) BrightnessSlider.Value = brightness.Value;
        BrightnessValue.Text = brightness.HasValue ? $"{brightness.Value}%" : "Desteklenmiyor";
        _ready = true;

        await RefreshMediaAsync();
        InitializeMotion();
        StartUpdates();
        // Startup keeps the compact island visible; the tray remains available.
    }

    private void DisplaySettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "WorkArea" or "PrimaryScreenWidth" or "PrimaryScreenHeight" or "VirtualScreenWidth" or "VirtualScreenHeight" or "VirtualScreenLeft" or "VirtualScreenTop")
            Dispatcher.BeginInvoke(new Action(ReflowPlacement));
    }

    private void ApplyDockPosition(bool animated, double? height = null)
    {
        MovePlacement(animated, _targetWidth, height ?? _targetHeight);
    }

    private void QueueAutoHide(int delay = AutoHideDelayMs, bool force = false)
    {
        _autoHideTimer.Stop();
        if (!_settings.AutoHide || _expanded || _dialogOpen || _dragArmed || _activities.HasWork || VolumeOverlay || _islandEdge == IslandEdge.Floating) return;
        _forceHide = force;
        _autoHideTimer.Interval = TimeSpan.FromMilliseconds(delay);
        _autoHideTimer.Start();
    }

    private void HideToEdge()
    {
        if (!_settings.AutoHide || _expanded || _dialogOpen || _dragArmed || !IsVisible || _activities.HasWork || VolumeOverlay || _islandEdge == IslandEdge.Floating) return;
        _dockHidden = true;
        ApplyDockPosition(true);
        RefreshCompact();
    }

    private void RevealFromEdge()
    {
        _hoverRevealTimer.Stop();
        _autoHideTimer.Stop(); _forceHide = false;
        if (!_dockHidden) return;
        _dockHidden = false;
        ApplyDockPosition(true);
        RefreshCompact();
    }

    private void Shell_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _autoHideTimer.Stop();
        // Ignore brief brushes while aiming for browser tabs. No polling when idle.
        if (_dockHidden) { _hoverRevealTimer.Stop(); _hoverRevealTimer.Start(); }
        else RevealFromEdge();
    }

    private void Shell_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _hoverRevealTimer.Stop();
        // Automatic activities retain their own deadlines via QueueAutoHide's guards.
        QueueAutoHide(1);
    }

    private void Pill_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging && Shell.IsMouseOver && !IsPlacementControl(e.OriginalSource)) ToggleExpanded();
    }

    private void ToggleExpanded()
    {
        if (_updateNotice && !_expanded) SetTab("Settings");
        SetExpanded(!_expanded);
    }

    private void SetExpanded(bool expanded)
    {
        if (_expanded == expanded) return;
        _expanded = expanded;
        SyncActivityPause();
        if (expanded) RevealFromEdge();
        _autoHideTimer.Stop();
        ExpandMark.Text = expanded ? "\uE70E" : "\uE70D";
        ExpandMark.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        if (expanded)
        {
            Expanded.Visibility = Visibility.Visible;
            Expanded.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240))
            { BeginTime = TimeSpan.FromMilliseconds(65) });
            ContentTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(450))
            { EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut } });
            Activate();
        }
        else
        {
            var fade = new DoubleAnimation(Expanded.Opacity, 0, TimeSpan.FromMilliseconds(100));
            fade.Completed += (_, _) => { if (!_expanded) Expanded.Visibility = Visibility.Collapsed; };
            Expanded.BeginAnimation(OpacityProperty, fade);
        }
        BeginAnimation(ShellRadiusProperty, new DoubleAnimation((double)GetValue(ShellRadiusProperty), expanded ? 34 : 22, TimeSpan.FromMilliseconds(400))
        { EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut } });
        RefreshCompact();
        if (!expanded) QueueAutoHide(1);
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        if (_expanded && !_dialogOpen && !_dragArmed) SetExpanded(false);
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _dragArmed) { FinishPlacementDrag(true); e.Handled = true; return; }
        if (e.Key == Key.Escape) { SetExpanded(false); e.Handled = true; }
        if ((e.Key == Key.Enter || e.Key == Key.Space) && !_expanded)
        { SetExpanded(true); e.Handled = true; }
    }

    private double ExpandedHeight => _tab switch
    {
        "Music" => 460,
        "Focus" => _reminderComposerOpen ? 680 : 560,
        "Files" => 432,
        "Clipboard" => 530,
        "Todo" => 520,
        "Notifications" => 480,
        "Controls" => 600,
        _ => 466
    };

    private void AnimateWindowSize(double width, double height)
    {
        if (!_loaded || _dragging) return;
        height += PlacementExtraHeight;
        width = Math.Min(width, Math.Max(160, _placementWork.Width));
        height = Math.Min(height, Math.Max(56, _placementWork.Height));
        if (Math.Abs(_targetWidth - width) < 0.1 && Math.Abs(_targetHeight - height) < 0.1) return;
        bool growing = height > _targetHeight;
        _targetWidth = width; _targetHeight = height;
        double extraWidth = growing ? 4 : -2, extraHeight = growing ? 3 : -1;
        BeginAnimation(WidthProperty, Morph(ActualWidth, width, extraWidth));
        BeginAnimation(HeightProperty, Morph(ActualHeight, height, extraHeight));
        ApplyDockPosition(true, height);
    }

    private static DoubleAnimationUsingKeyFrames Morph(double from, double to, double overshoot)
    {
        var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(520) };
        a.KeyFrames.Add(new LinearDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(to + overshoot, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(360)), new QuinticEase { EasingMode = EasingMode.EaseOut }));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(520)), new CubicEase { EasingMode = EasingMode.EaseOut }));
        return a;
    }

    private void RefreshCompact()
    {
        RefreshHome();
        PlacementTools.Visibility = _expanded ? Visibility.Visible : Visibility.Collapsed;
        PillTitle.FontSize = 11;
        PillTitle.FontWeight = FontWeights.Medium;
        PillSubtitle.Foreground = (System.Windows.Media.Brush)FindResource("Muted");
        bool volume = VolumeOverlay;
        bool notice = !_expanded && _showingNotice && !volume;
        bool focus = _timerRunning && _activitySong is null;
        bool media = (_mediaAvailable || _activitySong is not null) && !focus;
        bool idle = !_expanded && !focus && !media && !notice && !volume;
        IdleSurface.Visibility = idle ? Visibility.Visible : Visibility.Collapsed;
        var local = DateTime.Now;
        var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        IdleDay.Text = local.ToString("dd");
        IdleDate.Text = local.ToString("d MMMM", tr);
        IdleWeekday.Text = tr.TextInfo.ToTitleCase(local.ToString("dddd", tr));
        IdleClock.Text = local.ToString("HH:mm");
        MiniArtwork.Visibility = media && !_expanded ? Visibility.Visible : Visibility.Collapsed;
        PillSubtitle.Visibility = Visibility.Collapsed;
        ClockPill.Visibility = Visibility.Visible;
        BatteryPill.Visibility = _expanded ? Visibility.Visible : Visibility.Collapsed;
        IdleDot.Visibility = Visibility.Collapsed;
        Waveform.Visibility = focus || media ? Visibility.Visible : Visibility.Collapsed;
        PillTitle.Text = _expanded ? _tab switch { "Music" => "Ana sayfa", "Focus" => "Odak", "Files" => "Dosya rafı", "Clipboard" => "Kopyalama panosu", "Controls" => "Kontroller", "Notifications" => "Bildirimler", "Todo" => "Yapılacaklar", _ => "Ayarlar" }
            : focus ? FocusTaskTitle : media ? TrackTitle.Text : "";
        PillTitle.Foreground = _expanded || (!media && !focus) ? new SolidColorBrush(Color.FromRgb(142, 142, 147)) : System.Windows.Media.Brushes.White;
        ClockPill.Text = focus ? TimerValue.Text : _mediaAvailable ? RemainingTrackTime() : DateTime.Now.ToString("HH:mm");
        var brush = new SolidColorBrush(focus ? Color.FromRgb(255, 180, 91) : _mediaColor);
        foreach (var bar in new[] { Wave1, Wave2, Wave3, Wave4 }) bar.Fill = brush;
        _spectrum.Enabled = _mediaAvailable && _mediaPlaying && IsVisible && !_dockHidden && !notice && !volume;
        if (_spectrum.Enabled) _waveTick.Start();
        if (focus && !_spectrum.Enabled)
        {
            _waveTick.Stop();
            int i = 0;
            foreach (var bar in new[] { Wave1, Wave2, Wave3, Wave4 }) bar.Height = 5 + i++ * 3;
        }
        if (!_expanded && _activitySong is not null && !notice)
        {
            PillTitle.Text = _activitySong.Title;
            MiniArtBrush.ImageSource = _activitySong.Artwork;
            MiniArtworkFallback.Visibility = _activitySong.Artwork is null ? Visibility.Visible : Visibility.Collapsed;
        }
        VolumeCompactPanel.Visibility = volume ? Visibility.Visible : Visibility.Collapsed;
        PillTextGroup.Visibility = volume || idle ? Visibility.Collapsed : Visibility.Visible;
        if (idle) ClockPill.Visibility = Visibility.Collapsed;
        AudioBatteryPanel.Visibility = notice && _audioNotice?.Connected == true ? Visibility.Visible : Visibility.Collapsed;
        NoticeIcon.Visibility = notice ? Visibility.Visible : Visibility.Collapsed;
        if (notice)
        {
            MiniArtwork.Visibility = Visibility.Collapsed;
            Waveform.Visibility = Visibility.Collapsed;
            IdleDot.Visibility = Visibility.Collapsed;
            ClockPill.Visibility = Visibility.Collapsed;
            PillTitle.Text = _noticeTitle;
            PillTitle.Foreground = System.Windows.Media.Brushes.White;
            PillSubtitle.Text = _noticeBody;
            PillSubtitle.Visibility = string.IsNullOrEmpty(_noticeBody) ? Visibility.Collapsed : Visibility.Visible;
        }
        if (volume)
        {
            NoticeIcon.Visibility = Visibility.Visible;
            NoticeIcon.Data = Geometry.Parse("M3,9 L7,9 12,5 12,19 7,15 3,15 Z M16,8 Q21,12 16,16 M18,5 Q26,12 18,19");
            NoticeIcon.Stroke = System.Windows.Media.Brushes.White;
            MiniArtwork.Visibility = Waveform.Visibility = IdleDot.Visibility = AudioBatteryPanel.Visibility = Visibility.Collapsed;
            ClockPill.Visibility = Visibility.Visible; ClockPill.Text = $"%{_compactVolume}";
        }
        else if (notice)
        {
            NoticeIcon.Data = Geometry.Parse(_audioNotice is not null ? HeadphoneGeometry : BellGeometry);
            NoticeIcon.Stroke = new SolidColorBrush(_audioNotice?.Battery <= 10 ? Color.FromRgb(255,91,93) : _audioNotice?.Battery <= 20 || _audioNotice is null ? Color.FromRgb(255,180,91) : Color.FromRgb(185,248,121));
        }
        if (notice && _reminderNotice)
        {
            NoticeIcon.Data = Geometry.Parse(AlarmGeometry);
            ClockPill.Text = _reminderClock;
            ClockPill.Visibility = Visibility.Visible;
        }
        bool greeting = notice && _launchGreeting is not null;
        GreetingTag.Visibility = greeting ? Visibility.Visible : Visibility.Collapsed;
        if (greeting)
        {
            NoticeIcon.Data = Geometry.Parse(_launchGreeting!.Symbol switch
            {
                "sunrise" => "M2,19 L22,19 M5,16 A7,7 0 0 1 19,16 M12,2 L12,5 M3,7 L5,9 M21,7 L19,9 M1,14 L4,14 M20,14 L23,14",
                "sun" => "M17,12 A5,5 0 1 1 7,12 A5,5 0 1 1 17,12 M12,1 L12,4 M12,20 L12,23 M1,12 L4,12 M20,12 L23,12 M4,4 L6,6 M18,18 L20,20 M4,20 L6,18 M18,6 L20,4",
                "moon" => "M17,3 A9,9 0 1 0 21,17 A9,9 0 0 1 17,3 M4,2 L4,6 M2,4 L6,4",
                _ => "M12,2 L15,9 22,12 15,15 12,22 9,15 2,12 9,9 Z"
            });
            NoticeIcon.Stroke = new SolidColorBrush(Color.FromRgb(232,184,132));
            PillTitle.FontSize = 12;
            PillTitle.FontWeight = FontWeights.SemiBold;
            PillSubtitle.Foreground = new SolidColorBrush(Color.FromRgb(158,148,136));
        }
        AnimateWindowSize(_expanded ? 440 : volume ? 300 : media || notice ? 350 : 244, _expanded ? ExpandedHeight : 56);
    }

    private void UpdateAudioBars()
    {
        if (_timerRunning && !_spectrum.Enabled) return;
        var values = _spectrum.Read();
        var bars = new[] { Wave1, Wave2, Wave3, Wave4 };
        for (int i = 0; i < 4; i++)
        {
            double target = _spectrum.Enabled ? values[i] : 0;
            _waveLevels[i] += (target - _waveLevels[i]) * (target > _waveLevels[i] ? 0.65 : 0.18);
            bars[i].Height = 3 + 17 * _waveLevels[i];
        }
        if (!_spectrum.Enabled && _waveLevels.All(v => v < 0.005)) _waveTick.Stop();
    }

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is string key) SetTab(key);
    }

    private void SetTab(string key)
    {
        bool changed = _tab != key;
        string[] navigation = ["Music","Focus","Todo","Files","Clipboard","Notifications","Controls","Settings"];
        int direction = Array.IndexOf(navigation,key) >= Array.IndexOf(navigation,_tab) ? 1 : -1;
        _tab = key;
        MusicView.Visibility = key == "Music" ? Visibility.Visible : Visibility.Collapsed;
        FocusView.Visibility = key == "Focus" ? Visibility.Visible : Visibility.Collapsed;
        FilesView.Visibility = key == "Files" ? Visibility.Visible : Visibility.Collapsed;
        ClipboardView.Visibility = key == "Clipboard" ? Visibility.Visible : Visibility.Collapsed;
        TodoView.Visibility = key == "Todo" ? Visibility.Visible : Visibility.Collapsed;
        NotificationsView.Visibility = key == "Notifications" ? Visibility.Visible : Visibility.Collapsed;
        if (key == "Notifications") NoticeBadge.Visibility = Visibility.Collapsed;
        ControlsView.Visibility = key == "Controls" ? Visibility.Visible : Visibility.Collapsed;
        if (key is "Settings" or "Music") RefreshBrowserAudio();
        if (key == "Music") _ = RefreshApplicationAudioAsync();
        if (key == "Controls") { RefreshConnectedAudio(); _ = RefreshOutputAudioAsync(); }
        SettingsView.Visibility = key == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        foreach (var tab in new[] { MusicTab, FocusTab, TodoTab, FilesTab, ClipboardTab, NotificationsTab, ControlsTab, SettingsTab })
            tab.Style = (Style)FindResource((string?)tab.Tag == key ? "DockActive" : "DockTab");
        if (_expanded)
        {
            RefreshCompact();
            if (changed)
            {
                ViewHost.BeginAnimation(OpacityProperty, new DoubleAnimation(.45, 1, TimeSpan.FromMilliseconds(240)));
                ViewTranslate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(direction * 12, 0, TimeSpan.FromMilliseconds(360)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                ViewTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(4, 0, TimeSpan.FromMilliseconds(360))
                { EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut } });
            }
        }
    }

    private async Task RefreshMediaAsync()
    {
        if (_mediaBusy) return;
        _mediaBusy = true;
        try
        {
            var state = await _media.ReadAsync();
            TrackTitle.Text = state.Title;
            TrackArtist.Text = state.Artist;
            PlayGlyph.Data = Geometry.Parse(state.IsPlaying ? "M1,0 L7,0 7,22 1,22 Z M13,0 L19,0 19,22 13,22 Z" : "M2,0 L20,11 2,22 Z");
            PlaybackLabel.Text = !state.Available ? "MEDYA OTURUMU YOK" : state.IsPlaying ? "ÇALIYOR" : "DURAKLATILDI";
            if (_previousPlaying != state.IsPlaying)
            {
                _previousPlaying = state.IsPlaying;
                foreach (var prop in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
                {
                    AlbumScale.BeginAnimation(prop, new DoubleAnimation(state.IsPlaying ? 1 : 0.88, TimeSpan.FromMilliseconds(420)) { EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut } });
                    PlayScale.BeginAnimation(prop, new DoubleAnimation(0.75, 1, TimeSpan.FromMilliseconds(240)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                }
            }
            _mediaPosition = state.Position; _mediaDuration = state.Duration; _mediaReadAt = DateTimeOffset.UtcNow;
            _updatingTimeline = true;
            TimelineSlider.Maximum = Math.Max(1, state.Duration.TotalSeconds);
            TimelineSlider.IsEnabled = state.Available && state.CanSeek && state.Duration > TimeSpan.Zero;
            _updatingTimeline = false;
            PlayButton.IsEnabled = state.Available;
            PreviousButton.IsEnabled = state.Available;
            NextButton.IsEnabled = state.Available;
            _mediaAvailable = state.Available;
            _mediaPlaying = state.IsPlaying;
            ArtworkBrush.ImageSource = state.Artwork;
            MiniArtBrush.ImageSource = state.Artwork;
            ArtworkFallback.Visibility = state.Artwork is null ? Visibility.Visible : Visibility.Collapsed;
            MiniArtworkFallback.Visibility = ArtworkFallback.Visibility;
            var songKey = state.Available ? state.Title + "|" + state.Artist : "";
            if (songKey != _songKey)
            {
                _songKey = songKey;
                _mediaColor = ArtworkColor(state.Artwork);
                SongInfo.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(330)));
                SongTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(9, 0, TimeSpan.FromMilliseconds(420)) { EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut } });
                if (state.Available && !_expanded)
                {
                    EnqueueActivity("song", 20, () => { _activitySong = state; });
                }
            }
            UpdateTimeline();
            RefreshCompact();
        }
        finally { _mediaBusy = false; }
    }

    private async void Play_Click(object sender, RoutedEventArgs e) { await _media.PlayPauseAsync(); await RefreshMediaAsync(); }
    private async void Previous_Click(object sender, RoutedEventArgs e) { await _media.PreviousAsync(); await RefreshMediaAsync(); }
    private async void Next_Click(object sender, RoutedEventArgs e) { await _media.NextAsync(); await RefreshMediaAsync(); }

    private static Color ArtworkColor(ImageSource? artwork)
    {
        try
        {
            if (artwork is not BitmapSource source) return Color.FromRgb(185, 248, 121);
            var scaled = new TransformedBitmap(source, new ScaleTransform(8.0 / source.PixelWidth, 8.0 / source.PixelHeight));
            var image = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
            int stride = image.PixelWidth * 4; var bytes = new byte[stride * image.PixelHeight]; image.CopyPixels(bytes, stride, 0);
            long r = 0, g = 0, b = 0; int count = 0;
            for (int i = 0; i < bytes.Length; i += 4) { b += bytes[i]; g += bytes[i + 1]; r += bytes[i + 2]; count++; }
            return Color.FromRgb((byte)(75 + r / count * 180 / 255), (byte)(75 + g / count * 180 / 255), (byte)(75 + b / count * 180 / 255));
        }
        catch { return Color.FromRgb(185, 248, 121); }
    }

    private static string TrackTime(TimeSpan t) => t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{Math.Max(0, (int)t.TotalMinutes)}:{Math.Max(0, t.Seconds):00}";
    private string RemainingTrackTime()
    {
        if (_mediaDuration <= TimeSpan.Zero) return "–:––";
        var position = _mediaPosition + (_mediaPlaying ? DateTimeOffset.UtcNow - _mediaReadAt : TimeSpan.Zero);
        var remaining = TimeSpan.FromSeconds(Math.Clamp((_mediaDuration - position).TotalSeconds, 0, _mediaDuration.TotalSeconds));
        return "−" + TrackTime(remaining);
    }
    private void UpdateTimeline()
    {
        if (!_ready || TimelineSlider.IsMouseCaptureWithin || _seekDebounce.IsEnabled) return;
        var position = _mediaPosition + (_mediaPlaying ? DateTimeOffset.UtcNow - _mediaReadAt : TimeSpan.Zero);
        position = TimeSpan.FromSeconds(Math.Clamp(position.TotalSeconds, 0, Math.Max(0, _mediaDuration.TotalSeconds)));
        _updatingTimeline = true;
        TimelineSlider.Value = position.TotalSeconds;
        _updatingTimeline = false;
        ElapsedLabel.Text = TrackTime(position);
        DurationLabel.Text = _mediaDuration > TimeSpan.Zero ? "−" + TrackTime(_mediaDuration - position) : "–:––";
    }
    private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready || _updatingTimeline || !TimelineSlider.IsEnabled) return;
        ElapsedLabel.Text = TrackTime(TimeSpan.FromSeconds(TimelineSlider.Value));
        _seekDebounce.Stop(); _seekDebounce.Start();
    }
    private async void CopyClipboard_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ClipboardEntry entry) return;
        bool copied = await ClipboardService.CopyAsync(entry.Text);
        if (copied) await CopyFeedback(sender);
        StatusText.Text = copied ? "Kopyalandı. İstediğin yerde Ctrl+V ile yapıştır." : "Pano meşgul. Tekrar dene.";
    }
    private void ClearClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (ImageHistoryPanel.Visibility == Visibility.Visible) _images.Clear(); else _clipboardHistory.Clear();
        StatusText.Text = "Geçmiş temizlendi. Sistem panosu değişmedi.";
    }
    private void RefreshTodoUi()
    {
        int completed = _todo.Items.Count(x => x.IsCompleted);
        TodoCount.Text = _todo.Items.Count == 0 ? "Bir sonraki adımını yaz." : $"{_todo.Items.Count - completed} açık · {completed} tamamlandı";
        TodoEmpty.Visibility = _todo.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TodoClearDone.IsEnabled = completed > 0;
        TodoClearAll.IsEnabled = _todo.Items.Count > 0;
        TodoUndo.Visibility = _todo.CanUndo ? Visibility.Visible : Visibility.Collapsed;
    }
    private void SaveTodo()
    {
        RefreshTodoUi();
        try { _todoStore.Save(_todo.Items); }
        catch { StatusText.Text = "Liste bu oturumda güncellendi; diske kaydedilemedi."; }
    }
    private void TodoAdd_Click(object sender, RoutedEventArgs e) => AddTodo();
    private void TodoInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { AddTodo(); e.Handled = true; }
    }
    private void AddTodo()
    {
        if (!_todo.Add(TodoInput.Text)) return;
        TodoInput.Clear(); SaveTodo(); TodoInput.Focus();
    }
    private void TodoToggle_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not TodoEntry item) return;
        item.IsCompleted = !item.IsCompleted; SaveTodo(); RefreshCompact();
    }
    private void TodoDelete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not TodoEntry item) return;
        _todo.Remove(item); SaveTodo();
    }
    private void TodoClearDone_Click(object sender, RoutedEventArgs e) { _todo.Clear(true); SaveTodo(); }
    private void TodoClearAll_Click(object sender, RoutedEventArgs e) { _todo.Clear(false); SaveTodo(); }
    private void TodoUndo_Click(object sender, RoutedEventArgs e) { _todo.Undo(); SaveTodo(); }

    private void TextHistory_Click(object sender, RoutedEventArgs e) => SetClipboardMode(false);
    private void ImageHistory_Click(object sender, RoutedEventArgs e) => SetClipboardMode(true);
    private void SetClipboardMode(bool images)
    {
        TextHistoryPanel.Visibility = images ? Visibility.Collapsed : Visibility.Visible;
        ImageHistoryPanel.Visibility = images ? Visibility.Visible : Visibility.Collapsed;
        TextHistoryTab.Style = (Style)FindResource(images ? "Chip" : "AccentButton");
        ImageHistoryTab.Style = (Style)FindResource(images ? "AccentButton" : "Chip");
    }
    private async void CopyImage_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ImageEntry entry) return;
        bool copied = await ClipboardService.CopyImageAsync(entry.Png);
        if (copied) await CopyFeedback(sender);
        StatusText.Text = copied ? "Görsel kopyalandı. Ctrl+V ile yapıştır." : "Görsel kopyalanamadı. Tekrar dene.";
    }
    private void ClearNotices_Click(object sender, RoutedEventArgs e)
    {
        _notices.Clear(); NoticeBadge.Visibility = Visibility.Collapsed;
    }
    private async Task ScanDevicesAsync()
    {
        if (_deviceBusy || _exiting) return;
        _deviceBusy = true;
        try
        {
            await _bluetooth.ScanAsync();
            if (_settings.CaptureClipboard && !_exiting) await _screenshots.ScanAsync();
        }
        catch (Exception ex) { DiagnosticLog.Write("DevicePoll", ex); }
        finally { _deviceBusy = false; if (_expanded && _tab == "Controls") { RefreshConnectedAudio(); _ = RefreshOutputAudioAsync(); } }
    }
    private void CaptureClipboardCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _settings.CaptureClipboard = CaptureClipboardCheck.IsChecked == true;
        if (_clipboard is not null) _clipboard.Enabled = _settings.CaptureClipboard;
        if (!_settings.CaptureClipboard) { _clipboardHistory.Clear(); _images.Clear(); }
        SaveSettings();
    }
    private void AutoHideCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _settings.AutoHide = AutoHideCheck.IsChecked == true;
        if (!_settings.AutoHide) RevealFromEdge(); else QueueAutoHide();
        SaveSettings();
    }
    private void SoundSettings_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true }); }
        catch { StatusText.Text = "Windows ses ayarları açılamadı."; }
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        var volume = (int)Math.Round(VolumeSlider.Value);
        VolumeValue.Text = $"{volume}%";
        if (_ready && SystemParameters.ClientAreaAnimation) VolumeValue.BeginAnimation(OpacityProperty, new DoubleAnimation(.4, 1, TimeSpan.FromMilliseconds(220)));
        _volumeChangedUntil = DateTime.UtcNow.AddSeconds(2);
        if (!AudioService.SetVolume(volume)) StatusText.Text = "Ses seviyesi değiştirilemedi.";
    }

    private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready || !_brightnessSupported) return;
        BrightnessValue.Text = $"{(int)Math.Round(BrightnessSlider.Value)}%";
        _brightnessDebounce.Stop();
        _brightnessDebounce.Start();
    }

    private async void BrightnessDebounce_Tick(object? sender, EventArgs e)
    {
        _brightnessDebounce.Stop();
        var value = (int)Math.Round(BrightnessSlider.Value);
        if (!await Task.Run(() => BrightnessService.Set(value)))
            StatusText.Text = "Monitör parlaklık komutunu kabul etmedi.";
    }

    private void Clock_Tick(object? sender, EventArgs e)
    {
        if (_expanded && (_tab is "Settings" or "Music")) RefreshBrowserAudio();
        if (_expanded && _tab == "Music") _ = RefreshApplicationAudioAsync();
        CheckReminders();
        ClockPill.Text = _timerRunning ? TimerValue.Text : DateTime.Now.ToString("HH:mm");
        try
        {
            var state = Forms.SystemInformation.PowerStatus;
            bool battery = (state.BatteryChargeStatus & Forms.BatteryChargeStatus.NoSystemBattery) == 0;
            BatteryPill.Text = battery && state.BatteryLifePercent >= 0
                ? $"{(state.PowerLineStatus == Forms.PowerLineStatus.Online ? "↯ " : "")}{state.BatteryLifePercent:P0}" : "";
        }
        catch { BatteryPill.Text = ""; }

        if (DateTime.UtcNow > _volumeChangedUntil && _expanded && MusicView.Visibility == Visibility.Visible)
        {
            var vol = AudioService.ReadVolume();
            if (vol.HasValue && Math.Abs(VolumeSlider.Value - vol.Value) >= 1)
            {
                _ready = false;
                VolumeSlider.Value = vol.Value;
                VolumeValue.Text = $"{vol.Value}%";
                _ready = true;
            }
        }
        if (_timerRunning)
        {
            _remaining = _endAt - DateTimeOffset.UtcNow;
            if (_remaining <= TimeSpan.Zero)
            {
                _remaining = TimeSpan.Zero;
                _timerRunning = false;
                TimerStart.Content = "Yeniden başlat";
                TimerLabel.Text = "Seans tamamlandı";
                SystemSounds.Asterisk.Play();
                ShowNotification("Mola zamanı", $"{_duration.TotalMinutes:0} dakikalık seans tamamlandı. Biraz dinlen.");
            }
            RefreshTimerUi();
        }
        UpdateTimeline();
        RefreshCompact();
        if (_settings.WaterReminder && DateTimeOffset.UtcNow - _lastWaterReminder >= TimeSpan.FromMinutes(45))
        {
            _lastWaterReminder = DateTimeOffset.UtcNow;
            SystemSounds.Asterisk.Play();
            ShowNotification("Su molası 💧", "Bir bardak su içme zamanı.");
        }
    }

    private void RefreshTimerUi()
    {
        var remaining = _remaining < TimeSpan.Zero ? TimeSpan.Zero : _remaining;
        TimerValue.Text = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
        TimerProgress.Value = _duration.TotalSeconds <= 0 ? 0 : Math.Clamp(100 * (1 - remaining.TotalSeconds / _duration.TotalSeconds), 0, 100);
        RefreshCompact();
    }

    private void TimerStart_Click(object sender, RoutedEventArgs e)
    {
        if (_timerRunning)
        {
            _remaining = _endAt - DateTimeOffset.UtcNow;
            _timerRunning = false;
            TimerStart.Content = "Devam et";
            TimerLabel.Text = "Duraklatıldı";
        }
        else
        {
            if (_remaining <= TimeSpan.Zero) _remaining = _duration;
            _endAt = DateTimeOffset.UtcNow + _remaining;
            _timerRunning = true;
            TimerStart.Content = "Duraklat";
            TimerLabel.Text = _focusTaskId is null ? "Odaklanma zamanı" : FocusTaskTitle;
        }
        RefreshTimerUi();
    }

    private void TimerReset_Click(object sender, RoutedEventArgs e)
    {
        _timerRunning = false;
        _focusTaskId = null;
        _remaining = _duration;
        TimerStart.Content = "Başlat";
        TimerLabel.Text = "Biraz alan aç. Odaklan.";
        RefreshTimerUi();
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && int.TryParse(btn.Tag?.ToString(), out var minutes))
        {
            ApplyFocusMinutes(minutes);
            TimerReset_Click(sender, e);
        }
    }

    private void Input_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox field) return;
        if (!IsActive) Activate();
        if (!field.IsKeyboardFocusWithin) { field.Focus(); Keyboard.Focus(field); }
        // Do not mark handled: preserve caret placement, selection and native editing.
    }
    private void Input_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox field) field.BringIntoView();
    }

    private void FocusStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || !int.TryParse(button.Tag?.ToString(), out int step)) return;
        int current = int.TryParse(FocusMinutesInput.Text, out int value) ? value : (int)_duration.TotalMinutes;
        FocusMinutesInput.Text = Math.Clamp(current + step, 1, 1440).ToString();
    }
    private void ReminderOpen_Click(object sender, RoutedEventArgs e)
    {
        _reminderComposerOpen = true; SetReminderClock(DateTime.Now.AddMinutes(1));
        ReminderComposer.Visibility = Visibility.Visible; ReminderOpen.Visibility = Visibility.Collapsed;
        ReminderComposer.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        RefreshCompact();
        Dispatcher.BeginInvoke(new Action(() => { ReminderComposer.BringIntoView(); ReminderTextInput.Focus(); }));
    }
    private void ReminderClose_Click(object sender, RoutedEventArgs e) => CloseReminderComposer();
    private void CloseReminderComposer()
    {
        _reminderComposerOpen = false; ReminderComposer.Visibility = Visibility.Collapsed;
        ReminderOpen.Visibility = Visibility.Visible; RefreshCompact();
    }
    private void SetReminderClock(DateTime time)
    {
        _settingReminderClock = true;
        ReminderHoursInput.Text = time.Hour.ToString("00"); ReminderMinutesInput.Text = time.Minute.ToString("00");
        _settingReminderClock = false; UpdateReminderPreview();
    }
    private bool TryReadReminderClock(out string clock)
    {
        clock = "";
        if (!int.TryParse(ReminderHoursInput.Text, out int hour) || hour is < 0 or > 23 || !int.TryParse(ReminderMinutesInput.Text, out int minute) || minute is < 0 or > 59) return false;
        clock = $"{hour:00}:{minute:00}"; return true;
    }
    private void UpdateReminderPreview()
    {
        if (_settingReminderClock || ReminderHoursInput is null || ReminderMinutesInput is null || ReminderDuePreview is null) return;
        if (!TryReadReminderClock(out var clock)) { ReminderDuePreview.Text = "Geçerli bir saat seç."; return; }
        if (ReminderBoard.TryPlan("preview", clock, DateTimeOffset.Now, TimeZoneInfo.Local, out var item, out _))
            ReminderDuePreview.Text = (item!.Due.LocalDateTime.Date == DateTime.Today ? "Bugün" : "Yarın") + " · " + clock;
    }
    private void ReminderClock_TextChanged(object sender, TextChangedEventArgs e) => UpdateReminderPreview();
    private void ReminderStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string tag) return;
        var split = tag.Split(':'); if (split.Length != 2 || !int.TryParse(split[1], out int step)) return;
        StepReminderClock(split[0] == "hours", step);
    }
    private void ReminderClock_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        StepReminderClock(ReferenceEquals(sender, ReminderHoursInput), e.Delta > 0 ? 1 : -1); e.Handled = true;
    }
    private void StepReminderClock(bool hours, int step)
    {
        var field = hours ? ReminderHoursInput : ReminderMinutesInput;
        int max = hours ? 24 : 60; int current = int.TryParse(field.Text, out int parsed) ? parsed : 0;
        field.Text = ((current + step + max) % max).ToString("00");
    }

    private void FocusMinutesApply_Click(object sender, RoutedEventArgs e) => ApplyCustomFocus();
    private void FocusMinutesInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { ApplyCustomFocus(); e.Handled = true; }
    }
    private void ApplyCustomFocus()
    {
        if (!FocusDuration.TryParse(FocusMinutesInput.Text, out var minutes)) { StatusText.Text = "Dakikayı 1–1440 arasında tam sayı olarak yaz."; return; }
        ApplyFocusMinutes(minutes);
        TimerReset_Click(this, new RoutedEventArgs());
    }
    private void ApplyFocusMinutes(int minutes)
    {
        _duration = TimeSpan.FromMinutes(minutes);
        _settings.FocusMinutes = minutes;
        FocusMinutesInput.Text = minutes.ToString();
        SaveSettings(); StatusText.Text = $"{minutes} dakikalık seans seçildi.";
    }
    private void RefreshReminderUi()
    {
        ReminderEmpty.Visibility = _reminders.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ReminderClearDone.IsEnabled = _reminders.Items.Any(x => x.IsTriggered);
    }
    private void SaveReminders()
    {
        RefreshReminderUi();
        try { _reminderStore.Save(_reminders.Items); }
        catch { StatusText.Text = "Hatırlatıcı bu oturumda ayarlandı; diske kaydedilemedi."; }
    }
    private void ReminderAdd_Click(object sender, RoutedEventArgs e) => AddReminder();
    private void ReminderInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { AddReminder(); e.Handled = true; }
    }
    private void AddReminder()
    {
        if (!TryReadReminderClock(out var clock)) { StatusText.Text = "Saat 00–23, dakika 00–59 arasında olmalı."; return; }
        if (!ReminderBoard.TryPlan(ReminderTextInput.Text, clock, DateTimeOffset.Now, TimeZoneInfo.Local, out var item, out var error)) { StatusText.Text = error; return; }
        _reminders.Items.Insert(0, item!); ReminderTextInput.Clear();
        SaveReminders(); CloseReminderComposer(); StatusText.Text = $"Hatırlatıcı: {item!.Due.LocalDateTime:dd MMM HH:mm}";
    }
    private void ReminderDelete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ReminderEntry item) return;
        _reminders.Items.Remove(item); _activities.RemovePending("reminder:" + item.Id); SaveReminders();
    }
    private void ReminderClearDone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _reminders.Items.Where(x => x.IsTriggered).ToArray()) _reminders.Items.Remove(item);
        SaveReminders();
    }
    private void CheckReminders()
    {
        var due = _reminders.TakeDue(DateTimeOffset.Now); if (due.Count == 0) return;
        SaveReminders();
        if (_expanded) SetExpanded(false);
        foreach (var item in due)
        {
            LogNotice(item.Text, $"Hatırlatıcı · {item.Due.LocalDateTime:HH:mm}");
            EnqueueActivity("reminder:" + item.Id, 80, () =>
            {
                ShowNoticeVisual(item.Text, "Hatırlatıcı");
                _reminderNotice = true; _reminderClock = item.Due.LocalDateTime.ToString("HH:mm");
                NoticeIcon.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                var turn = new RotateTransform(); NoticeIcon.RenderTransform = turn;
                var wobble = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(800), RepeatBehavior = new RepeatBehavior(3) };
                foreach (var key in new[] { (0.0,0.0), (.15,-12.0), (.35,12.0), (.55,-8.0), (.75,8.0), (1.0,0.0) })
                    wobble.KeyFrames.Add(new EasingDoubleKeyFrame(key.Item2, KeyTime.FromPercent(key.Item1)));
                turn.BeginAnimation(RotateTransform.AngleProperty, wobble);
                SystemSounds.Exclamation.Play();
            });
        }
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        RevealFromEdge();
        _autoHideTimer.Stop();
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] paths)
            AddFilePaths(paths);
        e.Handled = true;
    }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Title = "NovaLand'a dosya sabitle" };
        _dialogOpen = true;
        try { if (picker.ShowDialog(this) == true) AddFilePaths(picker.FileNames); }
        finally { _dialogOpen = false; }
    }

    private void AddFilePaths(IEnumerable<string> paths)
    {
        int added = 0;
        foreach (var path in paths)
        {
            if ((!File.Exists(path) && !Directory.Exists(path)) ||
                _files.Any(f => string.Equals(f.FullPath, path, StringComparison.OrdinalIgnoreCase))) continue;
            _files.Add(new FileEntry(path));
            added++;
        }
        SaveFiles();
        StatusText.Text = added > 0 ? $"{added} öğe kaydedildi (yalnızca dosya yolu)." : "Yeni öğe eklenmedi.";
        if (added > 0)
        {
            if (!_expanded) ToggleExpanded();
            SetTab("Files");
        }
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not FileEntry item) return;
        if (!File.Exists(item.FullPath) && !Directory.Exists(item.FullPath))
        {
            StatusText.Text = "Dosya artık bu konumda değil.";
            return;
        }
        try { Process.Start(new ProcessStartInfo(item.FullPath) { UseShellExecute = true }); }
        catch { StatusText.Text = "Dosya açılamadı."; }
    }

    private void RemoveFile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is FileEntry item)
        {
            _files.Remove(item);
            SaveFiles();
            StatusText.Text = "Kısayol kaldırıldı. Asıl dosya korunuyor.";
        }
    }

    private void SaveFiles()
    {
        _settings.FilePaths = _files.Select(f => f.FullPath).ToList();
        SaveSettings();
    }

    private void StartupCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        var enabled = StartupCheck.IsChecked == true;
        if (!StartupService.SetEnabled(enabled))
        {
            StatusText.Text = "Başlangıç ayarı kaydedilemedi.";
            _ready = false;
            StartupCheck.IsChecked = StartupService.IsEnabled();
            _ready = true;
            return;
        }
        _settings.StartWithWindows = enabled;
        SaveSettings();
    }

    private void StayTopCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        Topmost = StayTopCheck.IsChecked == true;
        _settings.AlwaysOnTop = Topmost;
        SaveSettings();
    }

    private void WaterCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _settings.WaterReminder = WaterCheck.IsChecked == true;
        _lastWaterReminder = DateTimeOffset.UtcNow;
        SaveSettings();
    }

    private void SaveSettings()
    {
        try { SettingsStore.Save(_settings); }
        catch { StatusText.Text = "Ayar dosyası kaydedilemedi."; }
    }

    private void CreateTray()
    {
        try
        {
            var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/AppIcon.ico"));
            if (resource is not null)
            {
                using var stream = resource.Stream;
                using var loadedIcon = new System.Drawing.Icon(stream);
                _trayBrandIcon = (System.Drawing.Icon)loadedIcon.Clone();
            }
        }
        catch (Exception ex) { DiagnosticLog.Write("BrandIcon", ex); }
        _tray = new Forms.NotifyIcon
        {
            Icon = _trayBrandIcon ?? System.Drawing.SystemIcons.Application,
            Text = "NovaLand",
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };
        _tray.ContextMenuStrip.Items.Add("Aç", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        _tray.ContextMenuStrip.Items.Add("Konumu sıfırla", null, (_, _) => Dispatcher.Invoke(() =>
        {
            PlacementReset_Click(this, new RoutedEventArgs()); ShowFromTray();
        }));
        _tray.ContextMenuStrip.Items.Add("Kapat", null, (_, _) => Dispatcher.Invoke(Quit));
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        ReflowPlacement();
        RevealFromEdge();
        SetExpanded(true);
    }

    private void HandleAudioNotice(AudioDeviceNotice audio)
    {
        string title = audio.LowThreshold > 0 ? "Kulaklık pili düşük" : audio.Connected ? "Kulaklık bağlandı" : "Kulaklık bağlantısı kesildi";
        LogNotice(title, audio.Name + (audio.Battery.HasValue ? $" · %{audio.Battery}" : ""));
        // Surface the compact notification even when a panel was left open,
        // but never interrupt text entry or a native file dialog.
        if (_expanded && !_dialogOpen && Keyboard.FocusedElement is not System.Windows.Controls.TextBox)
            SetExpanded(false);
        int priority = audio.LowThreshold == 10 ? 100 : audio.LowThreshold == 20 ? 90 : 70;
        EnqueueActivity("audio:" + audio.Id, priority, () => RenderAudioNotice(audio), replaceActive: true);
    }
    private void RenderAudioNotice(AudioDeviceNotice audio)
    {
        string state = !audio.Connected ? "Bağlantı kesildi" : audio.LowThreshold == 10 ? "Pil kritik seviyede" : audio.LowThreshold == 20 ? "Pil azalıyor" : audio.Reason == "BatteryAvailable" ? "Pil bilgisi güncellendi" : "Kulaklık bağlandı";
        ShowNoticeVisual(state, audio.Name);
        _audioNotice = audio;
        _noticeTitle = audio.Name;
        _noticeBody = audio.Connected && audio.Battery is null ? state + " · Pil bilgisi yok" : state;
        NoticeIcon.Data = Geometry.Parse(HeadphoneGeometry);
        NoticeIcon.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        var headphoneScale = new ScaleTransform(); NoticeIcon.RenderTransform = headphoneScale;
        foreach (var property in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
            headphoneScale.BeginAnimation(property, new DoubleAnimation(0.65, 1, TimeSpan.FromMilliseconds(380))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.15 } });
        Color color = audio.Battery <= 10 ? Color.FromRgb(255, 91, 93) : audio.Battery <= 20 ? Color.FromRgb(255, 180, 91) : Color.FromRgb(185, 248, 121);
        var brush = new SolidColorBrush(color);
        NoticeIcon.Stroke = audio.Connected ? brush : new SolidColorBrush(Color.FromRgb(142, 142, 147));
        AudioBatteryFill.Background = brush;
        AudioBatteryPercent.Foreground = brush;
        AudioBatteryPercent.Text = audio.Battery.HasValue ? $"%{audio.Battery}" : "—";
        AudioBatteryFill.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(0, audio.Battery.HasValue ? 17 * audio.Battery.Value / 100.0 : 0, TimeSpan.FromMilliseconds(650))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        AudioBatteryPanel.BeginAnimation(OpacityProperty, null);
        if (audio.LowThreshold > 0)
        {
            var blink = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(850), RepeatBehavior = new RepeatBehavior(3) };
            blink.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0)));
            blink.KeyFrames.Add(new EasingDoubleKeyFrame(0.25, KeyTime.FromPercent(0.5)));
            blink.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1)));
            AudioBatteryPanel.BeginAnimation(OpacityProperty, blink);
            SystemSounds.Exclamation.Play();
        }
        RefreshCompact();
    }

    private void LogNotice(string title, string body)
    {
        StatusText.Text = $"{title}: {body}";
        _notices.Insert(0, new NoticeEntry(title, body, DateTime.Now));
        while (_notices.Count > 20) _notices.RemoveAt(_notices.Count - 1);
        if (_tab != "Notifications" || !_expanded) NoticeBadge.Visibility = Visibility.Visible;
    }
    private void ShowNotification(string title, string body)
    {
        LogNotice(title, body);
        EnqueueActivity("notice:" + title, title == "Mola zamanı" ? 60 : 40, () => ShowNoticeVisual(title, body));
    }
    private void RenderLaunchGreeting(LaunchGreeting greeting)
    {
        ShowNoticeVisual(greeting.Title, greeting.Subtitle);
        _launchGreeting = greeting;
        if (!_settings.WelcomeShown)
        {
            _settings.WelcomeShown = true;
            SaveSettings();
        }
        NoticeIcon.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        var scale = new ScaleTransform();
        NoticeIcon.RenderTransform = scale;
        foreach (var property in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
            scale.BeginAnimation(property, new DoubleAnimation(0.4, 1, TimeSpan.FromMilliseconds(650))
                { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 } });
        GreetingOrbit.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(0, 180, TimeSpan.FromSeconds(5)));
        GreetingSpark.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0.45, 1, TimeSpan.FromMilliseconds(1000)) { AutoReverse = true, RepeatBehavior = new RepeatBehavior(4) });
        GreetingHalo.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0.15, 0.6, TimeSpan.FromMilliseconds(1000)) { AutoReverse = true, RepeatBehavior = new RepeatBehavior(4) });
        PillTextGroup.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(450)));
    }

    private void ShowNoticeVisual(string title, string body)
    {
        _showingNotice = true; _audioNotice = null;
        _noticeTitle = title; _noticeBody = body;
        NoticeIcon.Data = Geometry.Parse(BellGeometry);
        NoticeIcon.Stroke = new SolidColorBrush(Color.FromRgb(255,180,91));
        var pulse = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(900) };
        pulse.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0)));
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(0.7, KeyTime.FromPercent(0.45)));
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1)));
        NoticeIcon.BeginAnimation(OpacityProperty, pulse);
    }
    private bool VolumeOverlay => !_expanded && DateTimeOffset.UtcNow < _volumeUntil;
    private void SyncActivityPause() => _activities.Pause(_expanded || _dialogOpen || _dragging || VolumeOverlay, DateTimeOffset.UtcNow);
    private void EnqueueActivity(string key, int priority, Action show, bool replaceActive = false)
    {
        SyncActivityPause(); _activities.Enqueue(key, priority, show, DateTimeOffset.UtcNow, replaceActive);
    }
    private void Pill_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_expanded) return;
        e.Handled = true;
        _wheelRemainder += e.Delta;
        int steps = _wheelRemainder / 120; if (steps == 0) return;
        _wheelRemainder %= 120;
        var current = AudioService.ReadVolume();
        if (!current.HasValue) { StatusText.Text = "Ses çıkışı okunamadı."; return; }
        int volume = Math.Clamp(current.Value + steps * 2, 0, 100);
        if (!AudioService.SetVolume(volume)) { StatusText.Text = "Ses değiştirilemedi."; return; }
        _compactVolume = volume;
        _volumeChangedUntil = DateTime.UtcNow.AddSeconds(2);
        _volumeUntil = DateTimeOffset.UtcNow.AddSeconds(2);
        SyncActivityPause();
        _wasVolume = true; RevealFromEdge(); RefreshCompact();
        VolumeMiniFill.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(128 * volume / 100.0, TimeSpan.FromMilliseconds(100)));
        VolumeValue.Text = $"{volume}%";
        if (_ready && SystemParameters.ClientAreaAnimation) VolumeValue.BeginAnimation(OpacityProperty, new DoubleAnimation(.4, 1, TimeSpan.FromMilliseconds(220)));
        _ready = false; VolumeSlider.Value = volume; _ready = true;
    }
    private void UpdateClipboardEmpty()
    {
        ClipboardEmpty.Visibility = _clipboardHistory.Items.Count + _clipboardHistory.Pinned.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ImageEmpty.Visibility = _images.Items.Count + _images.Pinned.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private async void PinClipboard_Click(object sender, RoutedEventArgs e)
    {
        switch ((sender as FrameworkElement)?.DataContext)
        {
            case ClipboardEntry text: _clipboardHistory.TogglePin(text); break;
            case ImageEntry image: _images.TogglePin(image); break;
            default: return;
        }
        try { await _pins.SaveAsync(_clipboardHistory, _images); StatusText.Text = "Sabitlenenler kaydedildi."; }
        catch { StatusText.Text = "Sabitleme bu oturumda yapıldı; diske kaydedilemedi."; }
        UpdateClipboardEmpty();
    }

    private void Hide_Click(object sender, RoutedEventArgs e)
    {
        _activities.Clear(); _volumeUntil = DateTimeOffset.MinValue;
        SetExpanded(false);
        if (_settings.AutoHide) HideToEdge();
    }
    private void RefreshBrowserAudio()
    {
        if (QuickSiteCard is null) return;
        var snapshots = _browserAudio?.Snapshot() ?? Array.Empty<BrowserAudioSnapshot>();
        foreach (var old in _browserRows.Where(r => !snapshots.Any(s => s.Client == r.Client && s.Tab.Id == r.Id)).ToArray())
            _browserRows.Remove(old);
        foreach (var snapshot in snapshots)
        {
            var row = _browserRows.FirstOrDefault(r => r.Client == snapshot.Client && r.Id == snapshot.Tab.Id);
            if (row is null)
            {
                row = new BrowserAudioRow(snapshot);
                row.Requested += (item, volume) =>
                {
                    if (_browserAudio?.SetVolume(item.Client, item.Id, volume) != true)
                        StatusText.Text = "Sekme bağlantısı kesildi. Video sekmesini yenileyebilirsin.";
                };
                _browserRows.Add(row);
            }
            else row.Update(snapshot.Tab);
        }
        RefreshQuickSite();
        BrowserAudioStatus.Text = _browserAudio?.Listening != true ? "Bağlantı açılamadı" : _browserAudio.Connected ? "Bağlı" : "Eklenti bekleniyor";
        BrowserAudioHelp.Text = _browserAudio?.Connected == true
            ? "Site sesleri müzik sayfasında. Açık medya sekmeni bir kez yenilemeyi dene."
            : "Ses eklentisini yükle ve aşağıdaki kodla eşleştir. Sonra YouTube veya Kick sekmesini yenile.";
    }
    private void BrowserMute_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is IQuickAudioTarget row) row.Volume = 0;
    }
    private void BrowserPair_Click(object sender, RoutedEventArgs e)
    {
        if (_browserAudio?.Listening != true) { StatusText.Text = "Yerel ses bağlantısı açılamadı. Uygulamayı yeniden başlatmayı dene."; return; }
        try
        {
            // Keep the pairing key out of the application's own clipboard history.
            var data = new System.Windows.DataObject();
            data.SetText(_browserAudio.Key);
            data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(BitConverter.GetBytes(1)));
            System.Windows.Clipboard.SetDataObject(data, true);
            StatusText.Text = "Kod kopyalandı. Tarayıcıdaki NovaLand ses eklentisine yapıştır.";
        }
        catch { StatusText.Text = "Kod kopyalanamadı. Tekrar deneyebilirsin."; }
    }
    private void BrowserExtension_Click(object sender, RoutedEventArgs e)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Tarayici_Ses_Eklentisi");
        try { Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); }
        catch { StatusText.Text = "ZIP içindeki Tarayici_Ses_Eklentisi klasörünü kullan."; }
    }
    private void Quit_Click(object sender, RoutedEventArgs e) => Quit();
    private void Quit() { _exiting = true; Close(); }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_exiting)
        {
            e.Cancel = true;
            SetExpanded(false);
            HideToEdge();
            return;
        }
        if (_dragArmed) FinishPlacementDrag(true);
        SystemParameters.StaticPropertyChanged -= DisplaySettingsChanged;
        _autoHideTimer.Stop(); _hoverRevealTimer.Stop(); _seekDebounce.Stop();
        _updateTimer.Stop(); _updates.Dispose();
        _activityTick.Stop();
        _browserAudio?.Dispose();
        _waveTick.Stop();
        _spectrum.Dispose();
        _devicePoll.Stop();
        _clipboard?.Dispose();
        _images.Clear();
        _notices.Clear();
        _clipboardHistory.Clear();
        _clock.Stop();
        _mediaPoll.Stop();
        _brightnessDebounce.Stop();
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        _trayBrandIcon?.Dispose();
        SaveSettings();
    }
}
