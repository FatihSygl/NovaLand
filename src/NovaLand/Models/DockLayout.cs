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

    public static IslandEdge SnapReleased(DockBox screen, DockBox work, DockBox window, DockPoint cursor, double distance = 40)
    {
        // Explicitly dragging the pointer to a screen edge wins, including taskbar boundaries.
        var pointer = new[] {
            (IslandEdge.Top, Math.Abs(cursor.Y - screen.Top)),
            (IslandEdge.Right, Math.Abs(cursor.X - screen.Right)),
            (IslandEdge.Bottom, Math.Min(Math.Abs(cursor.Y - screen.Bottom), Math.Abs(cursor.Y - work.Bottom))),
            (IslandEdge.Left, Math.Abs(cursor.X - screen.Left))
        };
        var pointed = pointer.OrderBy(p => p.Item2).First();
        if (pointed.Item2 <= distance) return pointed.Item1;
        // Crossing an edge must remain docked; abs(distance) incorrectly rejects overshoot.
        var touching = new[] {
            (IslandEdge.Top, Math.Max(0, window.Top - screen.Top)),
            (IslandEdge.Right, Math.Max(0, screen.Right - window.Right)),
            (IslandEdge.Bottom, Math.Max(0, Math.Min(screen.Bottom,work.Bottom) - window.Bottom)),
            (IslandEdge.Left, Math.Max(0, window.Left - screen.Left))
        };
        var edge = touching.OrderBy(p => p.Item2).ThenBy(p => pointer.First(x=>x.Item1==p.Item1).Item2).First();
        return edge.Item2 <= distance ? edge.Item1 : IslandEdge.Floating;
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
