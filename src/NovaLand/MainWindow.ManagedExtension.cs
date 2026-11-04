using System.IO;
using NovaIsland.Services;
namespace NovaIsland;
public partial class MainWindow
{
    private ManagedExtension? _managedExtension;
    private async Task PrepareBrowserExtensionAsync()
    {
        if (_browserAudio is null) return;
        try
        {
            var key = _browserAudio.Key;
            _managedExtension = await Task.Run(() => ManagedBrowserExtension.Prepare(
                Path.Combine(AppContext.BaseDirectory,"Tarayici_Ses_Eklentisi"), ManagedBrowserExtension.DefaultFolder,key));
            _browserAudio.ExtensionVersion = _managedExtension.Version;
            _browserAudio.ExtensionInstallId = _managedExtension.InstallId;
        }
        catch(Exception ex) { DiagnosticLog.Write("Managed extension setup",ex); }
    }
}
