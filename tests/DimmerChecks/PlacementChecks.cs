using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NovaIsland.Services;
using Brushes = System.Windows.Media.Brushes;
internal static class PlacementChecks
{
    public static void Run()
    {
        int checks=0;
        void Check(bool result,string label){if(!result)throw new Exception(label);checks++;}
        var window=new Window {Width=300,Height=90,Left=100,Top=100,WindowStyle=WindowStyle.None,
            AllowsTransparency=true,Background=Brushes.Black,ShowInTaskbar=false,ShowActivated=false};
        UtilityWindowStyle.Attach(window);
        try
        {
            window.Show(); Pump(80);
            window.BeginAnimation(Window.LeftProperty,new DoubleAnimation(100,600,TimeSpan.FromMilliseconds(600)));
            window.BeginAnimation(Window.WidthProperty,new DoubleAnimation(300,440,TimeSpan.FromMilliseconds(600)));
            Pump(150);
            double left=window.Left,width=window.ActualWidth;
            var frozen=WindowPlacementMotion.Freeze(window);
            Check(Math.Abs(window.Left-left)<1 && Math.Abs(frozen.Left-left)<1,"drag starts at current animated position without jumping");
            Check(Math.Abs(window.Width-width)<1,"drag preserves visible animated width");
            window.Left=230;
            Check(Math.Abs(window.Left-230)<.1,"previous animation no longer overrides drag movement");
            WindowPlacementMotion.Move(window,Window.LeftProperty,480,180);
            Pump(260);window.BeginAnimation(Window.LeftProperty,null);
            Check(Math.Abs(window.Left-480)<.1,"dock position remains committed after animation removed");
            WindowPlacementMotion.Move(window,Window.LeftProperty,700,300);Pump(70);
            WindowPlacementMotion.Move(window,Window.LeftProperty,350,180);Pump(260);
            window.BeginAnimation(Window.LeftProperty,null);
            Check(Math.Abs(window.Left-350)<.1,"interrupted dock movement ends at latest committed target");
            Console.WriteLine($"{checks} native placement checks passed");
        }
        finally {window.Close();}
    }
    private static void Pump(int ms)
    {
        var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};
        timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
    }
}
