using System.Management;

namespace NovaIsland.Services;

public static class BrightnessService
{
    // Built-in laptop displays usually expose this WMI provider. External monitors often do not.
    public static int? Read()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active = TRUE");
            using var results = searcher.Get();
            foreach (ManagementObject monitor in results)
            {
                using (monitor) return Convert.ToInt32(monitor["CurrentBrightness"]);
            }
        }
        catch { }
        return null;
    }

    public static bool Set(int brightness)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods WHERE Active = TRUE");
            using var monitors = searcher.Get();
            foreach (ManagementObject monitor in monitors)
            {
                using (monitor)
                {
                    using var args = monitor.GetMethodParameters("WmiSetBrightness");
                    args["Brightness"] = (byte)Math.Clamp(brightness, 0, 100);
                    args["Timeout"] = 0;
                    using var result = monitor.InvokeMethod("WmiSetBrightness", args, null);
                    return result is not null && Convert.ToUInt32(result["ReturnValue"]) == 0;
                }
            }
        }
        catch { }
        return false;
    }
}
