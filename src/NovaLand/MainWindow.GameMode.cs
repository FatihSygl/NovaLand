using System.Windows;
using NovaIsland.Services;
namespace NovaIsland;
public partial class MainWindow
{
    private FullscreenService? _fullscreen;
    private bool _immersiveSuppressed, _manualGameMode, _visibleBeforeGame;
    private readonly List<Window> _gameHiddenWindows = [];
    private void InitializeGameMode()
    {
        _fullscreen = new FullscreenService();
        _fullscreen.Changed += () => Dispatcher.BeginInvoke(new Action(RefreshGameMode));
        RefreshGameMode();
    }
    private void RefreshGameMode()
    {
        if (_exiting || !_loaded || _dragArmed || _dragging || IsMouseCaptureWithin) return;
        bool suppress = _manualGameMode || (_settings.HideInFullscreen && FullscreenService.ShouldHide());
        if (_immersiveSuppressed == suppress) return;
        if (suppress)
        {
            _visibleBeforeGame = IsVisible;
            _immersiveSuppressed = true;
            if (_expanded) SetExpanded(false);
            if (_dragArmed) FinishPlacementDrag(true);
            SyncActivityPause();
            _activities.ClearTransient(DateTimeOffset.UtcNow);
            _activityTick.Stop();
            _hoverRevealTimer.Stop(); _autoHideTimer.Stop();
            _spectrum.Enabled = false; _waveTick.Stop();
            _gameHiddenWindows.Clear();
            foreach (Window window in OwnedWindows)
                if (window.IsVisible) { _gameHiddenWindows.Add(window); window.Hide(); }
            Hide();
        }
        else
        {
            _immersiveSuppressed = false;
            SyncActivityPause();
            if (_visibleBeforeGame)
            {
                bool activate = ShowActivated; ShowActivated = false;
                try { Show(); } finally { ShowActivated = activate; }
                _dockHidden = false; ResetEdgeVisibility(); RefreshCompact(); ApplyDockPosition(false); QueueAutoHide();
            }
            foreach (var window in _gameHiddenWindows)
                if (window.IsLoaded)
                {
                    bool activate = window.ShowActivated; window.ShowActivated = false;
                    try { window.Show(); } finally { window.ShowActivated = activate; }
                }
            _gameHiddenWindows.Clear();
        }
    }
    private void FullscreenCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _settings.HideInFullscreen = FullscreenCheck.IsChecked == true;
        SaveSettings(); RefreshGameMode();
    }
}
