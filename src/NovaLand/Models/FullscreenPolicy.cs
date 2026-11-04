namespace NovaIsland.Models;
public static class FullscreenPolicy
{
    public static bool Covers(DockBox window, DockBox monitor) => monitor.Width > 0 && monitor.Height > 0
        && window.Width > 0 && window.Height > 0
        && window.Left <= monitor.Left + 2 && window.Top <= monitor.Top + 2
        && window.Right >= monitor.Right - 2 && window.Bottom >= monitor.Bottom - 2;
}
