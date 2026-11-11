using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NovaIsland.Services;

internal static class FileShelfAnimationChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var frozenScale = new ScaleTransform(1, 1);
        frozenScale.Freeze();
        var grip = new Border { RenderTransform = frozenScale };
        var insertionLine = new Border { RenderTransform = frozenScale };

        var gripScale = RenderTransformAnimation.ForElement<ScaleTransform>(grip);
        gripScale.ScaleX = 0.88;
        gripScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.88, TimeSpan.FromMilliseconds(90)));

        var lineScale = RenderTransformAnimation.ForElement<ScaleTransform>(insertionLine);
        lineScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.78, 1, TimeSpan.FromMilliseconds(145)));
        check(!gripScale.IsFrozen && !lineScale.IsFrozen && !ReferenceEquals(gripScale, lineScale),
            "File grip and insertion line can animate independently of the shared template transform");
        check(frozenScale.ScaleX == 1 && ReferenceEquals(gripScale, RenderTransformAnimation.ForElement<ScaleTransform>(grip)),
            "Animation preserves the shared transform and reuses the grip's local transform");

        var frozenOffset = new TranslateTransform(0, 7);
        frozenOffset.Freeze();
        var row = new ContentPresenter { RenderTransform = frozenOffset };
        var rowOffset = RenderTransformAnimation.ForElement<TranslateTransform>(row);
        check(rowOffset.Y == 7 && !rowOffset.IsFrozen, "File row receives a mutable copy of its template offset");
        rowOffset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(185)));
    }
}
