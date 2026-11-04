using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NovaIsland.Models;
using NovaIsland.Services;
namespace NovaIsland;
public partial class MainWindow
{
    private bool _showUpdateArrival, _updateArrivalVisible;
    private void PrepareUpdateArrival()
    {
        _showUpdateArrival = UpdateArrival.ShouldNotify(_settings.LastOpenedVersion,UpdateService.CurrentVersion.ToString(),
            _settings.WelcomeShown,Environment.GetCommandLineArgs().Contains("--update-parent"));
    }
    private void RecordUpdateArrival()
    {
        _settings.LastOpenedVersion = UpdateService.CurrentVersion.ToString(); SaveSettings();
        if (!_showUpdateArrival) return;
        _showUpdateArrival = false;
        EnqueueActivity("update-success",40,() =>
        {
            ShowNoticeVisual("NovaLand güncellendi", UpdateService.CurrentVersion + " · Her şey hazır.");
            _updateArrivalVisible = true;
            NoticeIcon.BeginAnimation(OpacityProperty,null); NoticeIcon.Opacity = 1;
            UpdateSuccessTick.BeginAnimation(System.Windows.Shapes.Shape.StrokeDashOffsetProperty,null);
            UpdateSuccessTick.StrokeDashOffset = 0;
            UpdateSuccessScale.BeginAnimation(ScaleTransform.ScaleXProperty,null);
            UpdateSuccessScale.BeginAnimation(ScaleTransform.ScaleYProperty,null);
            UpdateSuccessScale.ScaleX = UpdateSuccessScale.ScaleY = 1;
            if (!SystemParameters.ClientAreaAnimation) return;
            UpdateSuccessTick.BeginAnimation(System.Windows.Shapes.Shape.StrokeDashOffsetProperty,
                new DoubleAnimation(24,0,TimeSpan.FromMilliseconds(450)));
            foreach(var property in new[]{ScaleTransform.ScaleXProperty,ScaleTransform.ScaleYProperty})
                UpdateSuccessScale.BeginAnimation(property,new DoubleAnimation(.55,1,TimeSpan.FromMilliseconds(550))
                {EasingFunction=new BackEase{EasingMode=EasingMode.EaseOut,Amplitude=.25}});
            UpdateSuccessTag.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(250)));
        });
    }
}
