using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
namespace NovaIsland.Visuals;
// Render only while visible; the weather symbol is independent of the text layout.
public sealed class WeatherScene : FrameworkElement
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(42) };
    private readonly Stopwatch _time = new();
    private static readonly Geometry CloudShape = Geometry.Parse("M -48,20 C -71,20 -74,-14 -51,-20 C -50,-45 -18,-54 -2,-32 C 21,-46 46,-28 43,-9 C 69,-5 66,26 43,26 L -48,26 Z");
    public int Code { get; set; }
    public bool Day { get; set; } = true;
    public WeatherScene()
    {
        CloudShape.Freeze();
        _timer.Tick += (_, _) => InvalidateVisual();
        IsVisibleChanged += (_, _) => Sync(); Loaded += (_, _) => Sync();
        Unloaded += (_, _) => { _timer.Stop(); _time.Stop(); };
    }
    private void Sync()
    {
        if (IsVisible && SystemParameters.ClientAreaAnimation) { _time.Start(); _timer.Start(); }
        else { _timer.Stop(); _time.Stop(); }
        InvalidateVisual();
    }
    private static SolidColorBrush B(byte r, byte g, byte b, byte a = 255) => new(Color.FromArgb(a, r, g, b));
    protected override void OnRender(DrawingContext d)
    {
        base.OnRender(d);
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        double t = _time.Elapsed.TotalSeconds;
        bool snow = Code is >= 71 and <= 77 or 85 or 86;
        bool rain = Code is >= 51 and <= 67 or >= 80 and <= 82 or >= 95;
        d.PushClip(new RectangleGeometry(new Rect(0, 0, w, h), 22, 22));
        d.DrawRectangle(new LinearGradientBrush(Color.FromRgb(37, 38, 44), Color.FromRgb(24, 25, 30), 90), null, new Rect(0, 0, w, h));
        double cx = w * .77, cy = h * .38 + Math.Sin(t * .65) * 2;
        var glow = new RadialGradientBrush(Day ? Color.FromArgb(25, 255, 200, 107) : Color.FromArgb(24, 147, 179, 255), Colors.Transparent);
        d.DrawEllipse(glow, null, new Point(cx, cy), 110, 100);
        // A small sculpted symbol, not a full-panel animation behind the reading.
        if (Day)
        {
            double sx = cx + (Code == 0 ? 0 : 18), sy = cy - (Code == 0 ? 0 : 22);
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4 + t * .035;
                d.DrawLine(new Pen(B(255, 202, 102, 115), 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round },
                    new Point(sx + Math.Cos(a) * 39, sy + Math.Sin(a) * 39), new Point(sx + Math.Cos(a) * 44, sy + Math.Sin(a) * 44));
            }
            var sun = new RadialGradientBrush { GradientOrigin = new Point(.3, .2), Center = new Point(.4, .4), RadiusX = .7, RadiusY = .7 };
            sun.GradientStops.Add(new GradientStop(Color.FromRgb(255, 244, 181), 0));
            sun.GradientStops.Add(new GradientStop(Color.FromRgb(255, 193, 67), .65));
            sun.GradientStops.Add(new GradientStop(Color.FromRgb(220, 125, 32), 1));
            d.DrawEllipse(sun, new Pen(B(255, 224, 149, 100), 1), new Point(sx, sy), 30, 30);
        }
        else
        {
            var moon = new CombinedGeometry(GeometryCombineMode.Exclude,
                new EllipseGeometry(new Point(cx, cy - 12), 30, 30), new EllipseGeometry(new Point(cx + 16, cy - 23), 28, 28));
            d.DrawGeometry(new LinearGradientBrush(Color.FromRgb(238, 243, 255), Color.FromRgb(118, 147, 204), 55), null, moon);
            for (int i = 0; i < 5; i++)
                d.DrawEllipse(B(215, 227, 255, (byte)(85 + 40 * Math.Sin(t * .8 + i))), null, new Point(cx - 60 + i * 25, 20 + i % 2 * 14), 1, 1);
        }
        if (Code != 0)
        {
            d.PushTransform(new TranslateTransform(cx - 7 + Math.Sin(t * .35) * 3, cy + 16));
            d.PushTransform(new ScaleTransform(.82, .82));
            d.PushTransform(new TranslateTransform(0, 5));
            d.DrawGeometry(B(0, 0, 0, 50), null, CloudShape);
            d.Pop();
            var cloud = new LinearGradientBrush { StartPoint = new Point(.25, 0), EndPoint = new Point(.65, 1) };
            cloud.GradientStops.Add(new GradientStop(Color.FromRgb(215, 234, 248), 0));
            cloud.GradientStops.Add(new GradientStop(Color.FromRgb(143, 181, 207), .42));
            cloud.GradientStops.Add(new GradientStop(Color.FromRgb(72, 112, 147), 1));
            d.DrawGeometry(cloud, new Pen(B(210, 235, 250, 70), 1), CloudShape);
            d.Pop(); d.Pop();
        }
        if (rain || snow)
            for (int i = 0; i < 9; i++)
            {
                double x = cx - 43 + i * 10, y = cy + 42 + (i * 17 + t * (snow ? 14 : 45)) % 31;
                byte alpha = (byte)(150 * (1 - (y - cy - 42) / 40));
                if (snow) d.DrawEllipse(B(216, 235, 255, alpha), null, new Point(x + Math.Sin(t + i) * 2, y), 1.6, 1.6);
                else d.DrawLine(new Pen(B(129, 188, 237, alpha), 1.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, new Point(x, y), new Point(x - 2, y + 5));
            }
        if (Code >= 95)
        {
            d.PushTransform(new TranslateTransform(cx - 7, cy + 34));
            d.DrawGeometry(B(255, 211, 104, (byte)(155 + 65 * Math.Sin(t * 2))), null, Geometry.Parse("M0,0 L-8,14 0,14 -4,25 12,9 4,9 9,0 Z"));
            d.Pop();
        }
        if (Code is 45 or 48)
            for (int i = 0; i < 3; i++) d.DrawRoundedRectangle(B(189, 206, 223, 65), null, new Rect(cx - 48 + Math.Sin(t * .4 + i) * 4, cy + 42 + i * 7, 88, 2), 1, 1);
        d.Pop();
    }
}
