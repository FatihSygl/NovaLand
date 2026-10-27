namespace NovaIsland.Models;

public enum IslandEdge { Top, Right, Bottom, Left, Floating }
public readonly record struct DockBox(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}
public readonly record struct DockPoint(double X, double Y);

public static class DockLayout
{
    public static double Anchor(double value, double fallback = 0.5) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : fallback;
    private static double Clamp(double value, double start, double end) => Math.Clamp(value, start, Math.Max(start, end));

    // Anchors describe the centre of the header horizontally and its top vertically.
    // They never change when panels expand, so collapsing cannot cause position drift.
    public static DockPoint Place(DockBox screen, DockBox work, IslandEdge edge, double x, double y, double width, double height, bool hidden)
    {
        double left = Clamp(screen.Left + Anchor(x) * screen.Width - width / 2, work.Left, work.Right - width);
        double top = Clamp(screen.Top + Anchor(y, 0.15) * screen.Height, work.Top, work.Bottom - height);
        if (edge == IslandEdge.Top) top = screen.Top;
        if (edge == IslandEdge.Bottom) top = screen.Bottom - height;
        if (edge == IslandEdge.Left) left = screen.Left;
        if (edge == IslandEdge.Right) left = screen.Right - width;
        if (hidden)
        {
            if (edge == IslandEdge.Top) top = screen.Top - height + 16;
            if (edge == IslandEdge.Bottom) top = screen.Bottom - 16;
            if (edge == IslandEdge.Left) left = screen.Left - width + 16;
            if (edge == IslandEdge.Right) left = screen.Right - 16;
        }
        return new(left, top);
    }

    public static IslandEdge Snap(DockBox screen, DockBox window, double distance = 28)
    {
        var candidates = new[] { (IslandEdge.Top, Math.Abs(window.Top - screen.Top)),
            (IslandEdge.Right, Math.Abs(window.Right - screen.Right)),
            (IslandEdge.Bottom, Math.Abs(window.Bottom - screen.Bottom)),
            (IslandEdge.Left, Math.Abs(window.Left - screen.Left)) };
        var nearest = candidates.OrderBy(c => c.Item2).First();
        return nearest.Item2 <= distance ? nearest.Item1 : IslandEdge.Floating;
    }
}
