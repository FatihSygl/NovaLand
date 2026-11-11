using System.Windows;
using System.Windows.Media;

namespace NovaIsland.Services;

internal static class RenderTransformAnimation
{
    internal static T ForElement<T>(UIElement element) where T : Transform, new()
    {
        // Templates can share frozen transforms. Animation needs a mutable transform owned by this element.
        if (element.ReadLocalValue(UIElement.RenderTransformProperty) is T { IsFrozen: false } local)
            return local;

        var transform = element.RenderTransform is T current ? (T)current.CloneCurrentValue() : new T();
        element.RenderTransform = transform;
        return transform;
    }
}
