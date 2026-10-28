using System.Collections.Concurrent;
using System.Windows.Media;
namespace NovaIsland.Services;
// All cached objects are frozen, so they can be shared safely across refreshes.
public static class FrozenVisualCache
{
    private const int Capacity = 64;
    private static readonly ConcurrentDictionary<Color, SolidColorBrush> Brushes = new();
    private static readonly ConcurrentDictionary<string, Geometry> Shapes = new(StringComparer.Ordinal);
    public static SolidColorBrush Brush(Color color)
    {
        if (Brushes.TryGetValue(color, out var value)) return value;
        var brush = new SolidColorBrush(color); brush.Freeze();
        if (Brushes.Count >= Capacity) Brushes.Clear();
        return Brushes.GetOrAdd(color, brush);
    }
    public static Geometry Shape(string data)
    {
        if (Shapes.TryGetValue(data, out var value)) return value;
        var shape = Geometry.Parse(data); shape.Freeze();
        if (Shapes.Count >= Capacity) Shapes.Clear();
        return Shapes.GetOrAdd(data, shape);
    }
}
