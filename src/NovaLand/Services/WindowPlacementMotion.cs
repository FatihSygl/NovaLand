using System.Windows;
using System.Windows.Media.Animation;
using NovaIsland.Models;
namespace NovaIsland.Services;
public static class WindowPlacementMotion
{
    public static DockBox Freeze(Window window)
    {
        var box = new DockBox(window.Left,window.Top,window.ActualWidth,window.ActualHeight);
        foreach(var property in new[]{Window.LeftProperty,Window.TopProperty,Window.WidthProperty,Window.HeightProperty})
            window.BeginAnimation(property,null);
        window.Left=box.Left;window.Top=box.Top;window.Width=box.Width;window.Height=box.Height;
        return box;
    }
    public static void Move(Window window, DependencyProperty property, double target, int milliseconds)
    {
        double from=(double)window.GetValue(property);
        window.BeginAnimation(property,null);
        window.SetValue(property,target);
        if(milliseconds>0 && SystemParameters.ClientAreaAnimation && double.IsFinite(from))
            window.BeginAnimation(property,new DoubleAnimation(from,target,TimeSpan.FromMilliseconds(milliseconds))
            {EasingFunction=new QuinticEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop});
    }
}
