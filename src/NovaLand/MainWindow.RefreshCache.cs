namespace NovaIsland;
public partial class MainWindow
{
    private static readonly System.Globalization.CultureInfo UiCulture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
    private DateTime _idleCalendarDate = DateTime.MinValue;
    private long _clockMinute = -1;
    private string _clockText = "";
    private string ClockText(DateTime local)
    {
        long minute = local.Ticks / TimeSpan.TicksPerMinute;
        if (minute != _clockMinute)
        {
            _clockMinute = minute;
            _clockText = local.ToString("HH:mm");
        }
        return _clockText;
    }
}
