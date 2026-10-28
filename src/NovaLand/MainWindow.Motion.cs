using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MouseEventHandler = System.Windows.Input.MouseEventHandler;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace NovaIsland;
public partial class MainWindow
{
    private static bool _motionRegistered;
    private static void InitializeMotion()
    {
        if(_motionRegistered)return;_motionRegistered=true;
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.Button),Mouse.MouseEnterEvent,new MouseEventHandler((s,e)=>ButtonMotion(s,1.012,220)));
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.Button),Mouse.MouseLeaveEvent,new MouseEventHandler((s,e)=>ButtonMotion(s,1,260)));
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.Button),UIElement.PreviewMouseLeftButtonDownEvent,new MouseButtonEventHandler((s,e)=>ButtonMotion(s,.975,100)));
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.Button),UIElement.PreviewMouseLeftButtonUpEvent,new MouseButtonEventHandler((s,e)=>ButtonMotion(s,1.012,280)));
    }
    private static void ButtonMotion(object sender,double target,int ms)
    {
        if(!SystemParameters.ClientAreaAnimation||sender is not System.Windows.Controls.Button button||!button.IsEnabled)return;
        if (button.Tag is string tab && DockOrder.Contains(tab)) return;
        if(button.RenderTransform is not ScaleTransform)
        {
            if(!button.RenderTransform.Value.IsIdentity)return;
            button.RenderTransformOrigin=new System.Windows.Point(.5,.5);button.RenderTransform=new ScaleTransform(1,1);
        }
        var scale=(ScaleTransform)button.RenderTransform;
        var ease=new CubicEase{EasingMode=EasingMode.EaseOut};
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(target,TimeSpan.FromMilliseconds(ms)){EasingFunction=ease});
        scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(target,TimeSpan.FromMilliseconds(ms)){EasingFunction=ease});
    }
}
