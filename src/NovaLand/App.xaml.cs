using System.Threading;
using System.Windows;

namespace NovaIsland;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (Services.UpdateService.RedirectOrWait(e.Args)) { Shutdown(); return; }
        _singleInstance = new Mutex(true, @"Local\NovaIsland.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }
        DispatcherUnhandledException += (_, args) => Services.DiagnosticLog.Write("UIUnhandled", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception error) Services.DiagnosticLog.Write("Unhandled", error);
        };
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _singleInstance?.ReleaseMutex(); }
        catch (ApplicationException) { }
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
