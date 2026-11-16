using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace NovaIsland.Services;

internal enum BluetoothDeviceChange { Applied, RestartRequired, Cancelled, Failed }

internal static class BluetoothDeviceSwitch
{
    internal static BluetoothDeviceChange SetEnabled(ulong address, string instanceId, bool enabled)
    {
        if (!BluetoothBatteryReader.IsClassicDevice(instanceId, address))
            return BluetoothDeviceChange.Failed;

        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "pnputil.exe"))
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        start.ArgumentList.Add(enabled ? "/enable-device" : "/disable-device");
        start.ArgumentList.Add(instanceId);

        try
        {
            using var process = Process.Start(start);
            if (process is null) return BluetoothDeviceChange.Failed;
            process.WaitForExit();
            return process.ExitCode switch
            {
                0 => BluetoothDeviceChange.Applied,
                3010 => BluetoothDeviceChange.RestartRequired,
                _ => BluetoothDeviceChange.Failed
            };
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return BluetoothDeviceChange.Cancelled;
        }
        catch (Win32Exception ex)
        {
            DiagnosticLog.Write("BluetoothDeviceSwitch", ex);
            return BluetoothDeviceChange.Failed;
        }
    }
}
