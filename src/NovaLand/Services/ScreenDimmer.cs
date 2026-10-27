using Microsoft.Win32;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
namespace NovaIsland.Services;
// A compositor overlay: does not change monitor brightness, gamma or saved settings.
public sealed class ScreenDimmer : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Action _restoreIslandOrder;
    private readonly List<DimSurface> _surfaces = new();
    private bool _disposed;
    private double _amount;
    public ScreenDimmer(Dispatcher dispatcher, Action restoreIslandOrder)
    {
        _dispatcher = dispatcher; _restoreIslandOrder = restoreIslandOrder;
        SystemEvents.DisplaySettingsChanged += DisplaysChanged;
    }
    public void SetLevel(double percent)
    {
        if (_disposed) return;
        _amount = double.IsFinite(percent) ? Math.Clamp(percent, 0, 70) / 100 : 0;
        if (_amount == 0) { Clear(); return; }
        if (_surfaces.Count == 0) Rebuild();
        else foreach (var surface in _surfaces) surface.Opacity = _amount;
    }
    private void DisplaysChanged(object? sender, EventArgs e)
    {
        _dispatcher.BeginInvoke(new Action(() => { if (!_disposed && _amount > 0) Rebuild(); }));
    }
    private void Rebuild()
    {
        Clear();
        try
        {
            foreach (var screen in Forms.Screen.AllScreens)
            {
                var surface = new DimSurface { Bounds = screen.Bounds, Opacity = _amount };
                _surfaces.Add(surface); surface.Show();
                // Reapply after handle creation to include the full monitor, including its taskbar.
                surface.Bounds = screen.Bounds;
            }
            _restoreIslandOrder();
        }
        catch { Clear(); _amount = 0; throw; }
    }
    private void Clear()
    {
        foreach (var surface in _surfaces) { surface.Close(); surface.Dispose(); }
        _surfaces.Clear();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; SystemEvents.DisplaySettingsChanged -= DisplaysChanged; Clear();
    }
    private sealed class DimSurface : Forms.Form
    {
        public DimSurface()
        {
            FormBorderStyle = Forms.FormBorderStyle.None;
            StartPosition = Forms.FormStartPosition.Manual;
            BackColor = System.Drawing.Color.Black;
            ShowInTaskbar = false; TopMost = true;
            AutoScaleMode = Forms.AutoScaleMode.None;
        }
        protected override bool ShowWithoutActivation => true;
        protected override Forms.CreateParams CreateParams
        {
            get
            {
                var p = base.CreateParams;
                // Layered + mouse pass-through + no Alt-Tab + never activate.
                p.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x08000000;
                return p;
            }
        }
    }
}
