using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
namespace NovaIsland.Visuals;
// Vector scene: no videos, bitmaps, external assets or continuous hidden rendering.
public sealed class WeatherScene : FrameworkElement
{
    private readonly DispatcherTimer _timer = new() { Interval=TimeSpan.FromMilliseconds(42) };
    private readonly Stopwatch _time = new();
    public int Code { get;set; }
    public bool Day { get;set; } = true;
    public WeatherScene()
    {
        _timer.Tick+=(_,_)=>InvalidateVisual();
        IsVisibleChanged+=(_,_)=>Sync(); Loaded+=(_,_)=>Sync(); Unloaded+=(_,_)=>{_timer.Stop();_time.Stop();};
    }
    private void Sync(){if(IsVisible && SystemParameters.ClientAreaAnimation){_time.Start();_timer.Start();}else{_timer.Stop();_time.Stop();}InvalidateVisual();}
    private static SolidColorBrush B(byte r,byte g,byte b,byte a=255)=>new(Color.FromArgb(a,r,g,b));
    protected override void OnRender(DrawingContext d)
    {
        base.OnRender(d);double w=ActualWidth,h=ActualHeight;if(w<=0||h<=0)return;double t=_time.Elapsed.TotalSeconds;
        bool snow=Code is >=71 and <=77 or 85 or 86, rain=Code is >=51 and <=67 or >=80 and <=82 or >=95, cloud=Code!=0;
        d.PushClip(new RectangleGeometry(new Rect(0,0,w,h),19,19));
        d.DrawRectangle(new LinearGradientBrush(Day ? (rain?Color.FromRgb(27,43,66):Color.FromRgb(29,80,119)):Color.FromRgb(10,15,38),Day?Color.FromRgb(12,24,39):Color.FromRgb(29,28,57),90),null,new Rect(0,0,w,h));
        if(!Day)for(int i=0;i<26;i++){double x=(i*79.3)%w,y=(i*43.7)%(h*.7);d.DrawEllipse(B(220,230,255,(byte)(70+70*(1+Math.Sin(t*.7+i))/2)),null,new Point(x,y),i%4==0?1.3:.7,i%4==0?1.3:.7);}
        double cx=w*.77,cy=h*.35+Math.Sin(t*.55)*3;
        var halo=new RadialGradientBrush();halo.GradientStops.Add(new GradientStop(Day?Color.FromArgb(70,255,205,120):Color.FromArgb(55,185,207,255),0));halo.GradientStops.Add(new GradientStop(Colors.Transparent,1));
        d.DrawEllipse(halo,null,new Point(cx,cy),86,86);
        if(Day){for(int i=0;i<12;i++){double a=i*Math.PI/6+t*.06;d.DrawLine(new Pen(B(255,214,140,75),2),new Point(cx+Math.Cos(a)*35,cy+Math.Sin(a)*35),new Point(cx+Math.Cos(a)*44,cy+Math.Sin(a)*44));}d.DrawEllipse(new RadialGradientBrush(Color.FromRgb(255,238,174),Color.FromRgb(255,172,78)),null,new Point(cx,cy),27,27);}
        else{d.DrawEllipse(B(222,233,255),null,new Point(cx,cy),25,25);d.DrawEllipse(B(22,29,54),null,new Point(cx+12,cy-8),23,23);}
        if(cloud){Cloud(d,cx-28+Math.Sin(t*.22)*12,cy+26,1.1,Day);Cloud(d,w*.26+Math.Sin(t*.17+2)*18,h*.32,.7,Day);}
        if(rain||snow)for(int i=0;i<(snow?35:45);i++){double x=((i*71.7+t*(snow?5:-34))% (w+20)+w+20)%(w+20)-10;double y=(i*33.2+t*(snow?16:150))%(h+20)-10;if(snow)d.DrawEllipse(B(230,244,255,150),null,new Point(x+Math.Sin(t+i)*4,y),1.8,1.8);else d.DrawLine(new Pen(B(153,200,239,95),1),new Point(x,y),new Point(x-3,y+11));}
        if(Code>=95){double flash=Math.Pow(Math.Max(0,Math.Sin(t*.7)),45);d.DrawRectangle(B(210,222,255,(byte)(flash*45)),null,new Rect(0,0,w,h));}
        if(Code is 45 or 48)for(int i=0;i<5;i++)d.DrawRoundedRectangle(B(205,218,231,(byte)(22+i*4)),null,new Rect(-20+Math.Sin(t*.15+i)*20,45+i*26,w+40,14),7,7);
        d.DrawRectangle(new LinearGradientBrush(Colors.Transparent,Color.FromArgb(180,5,10,18),90),null,new Rect(0,h*.4,w,h*.6));
        d.Pop();
    }
    private static void Cloud(DrawingContext d,double x,double y,double s,bool day)
    {
        var b=new LinearGradientBrush(day?Color.FromRgb(210,221,232):Color.FromRgb(97,111,142),day?Color.FromRgb(120,145,169):Color.FromRgb(46,58,83),90);
        d.DrawRoundedRectangle(b,null,new Rect(x-55*s,y,110*s,29*s),14*s,14*s);
        d.DrawEllipse(b,null,new Point(x-23*s,y),24*s,24*s);d.DrawEllipse(b,null,new Point(x+10*s,y-9*s),31*s,31*s);d.DrawEllipse(b,null,new Point(x+36*s,y+3*s),20*s,20*s);
    }
}
