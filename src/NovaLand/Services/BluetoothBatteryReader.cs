using System.Runtime.InteropServices;
using System.Text;
namespace NovaIsland.Services;
public sealed record BatteryNode(string InstanceId, Guid Container, int? Percentage);
public static class BluetoothBatteryReader
{
    // Windows HFP battery property; optional, driver-dependent. Never substitute laptop battery.
    private static readonly PropertyKey BatteryKey = new(new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"), 2);
    private static readonly PropertyKey ContainerKey = new(new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"), 2);
    public static List<BatteryNode> Read()
    {
        var nodes = new List<BatteryNode>();
        var set = SetupDiGetClassDevsW(IntPtr.Zero, null, IntPtr.Zero, 6);
        if (set == new IntPtr(-1)) return nodes;
        try
        {
            for (uint index = 0; ; index++)
            {
                var info = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>() };
                if (!SetupDiEnumDeviceInfo(set, index, ref info)) break;
                var id = new StringBuilder(1024);
                if (!SetupDiGetDeviceInstanceIdW(set, ref info, id, id.Capacity, out _)) continue;
                string instance = id.ToString();
                // Include Bluetooth children in System/Audio classes, not merely Bluetooth class nodes.
                var batteryBytes = GetProperty(set, ref info, BatteryKey, out var type);
                int? percentage = ParseBattery(type, batteryBytes);
                if (percentage is null && !instance.Contains("BTH", StringComparison.OrdinalIgnoreCase)) continue;
                var containerBytes = GetProperty(set, ref info, ContainerKey, out _);
                var container = containerBytes?.Length == 16 ? new Guid(containerBytes) : Guid.Empty;
                nodes.Add(new BatteryNode(instance, container, percentage));
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return nodes;
    }
    public static int? Match(IReadOnlyList<BatteryNode> nodes, ulong address, Guid container)
    {
        string hex = address.ToString("X12");
        var matchingContainers = nodes.Where(n => n.InstanceId.Contains(hex, StringComparison.OrdinalIgnoreCase) && n.Container != Guid.Empty).Select(n => n.Container).ToHashSet();
        if (container != Guid.Empty) matchingContainers.Add(container);
        var values = nodes.Where(n => n.Percentage.HasValue && ((n.Container != Guid.Empty && matchingContainers.Contains(n.Container)) || n.InstanceId.Contains(hex, StringComparison.OrdinalIgnoreCase))).Select(n => n.Percentage!.Value).ToArray();
        return values.Length > 0 ? values.Min() : null;
    }
    private static int? ParseBattery(uint type, byte[]? bytes)
    {
        if (bytes is null) return null;
        long value = type switch
        {
            3 when bytes.Length >= 1 => bytes[0],
            5 when bytes.Length >= 2 => BitConverter.ToUInt16(bytes),
            6 when bytes.Length >= 4 => BitConverter.ToInt32(bytes),
            7 when bytes.Length >= 4 => BitConverter.ToUInt32(bytes),
            _ => -1
        };
        return value is >= 0 and <= 100 ? (int)value : null;
    }
    private static byte[]? GetProperty(IntPtr set, ref DeviceInfo info, PropertyKey key, out uint type)
    {
        var buffer = new byte[32];
        if (!SetupDiGetDevicePropertyW(set, ref info, ref key, out type, buffer, buffer.Length, out var size, 0) || size > buffer.Length) return null;
        return buffer[..(int)size];
    }
    [StructLayout(LayoutKind.Sequential)] private struct DeviceInfo { public uint Size; public Guid Class; public uint DeviceInstance; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey(Guid format, uint id) { public Guid Format = format; public uint Id = id; }
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr SetupDiGetClassDevsW(IntPtr guid, string? enumerator, IntPtr hwnd, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref DeviceInfo info);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiGetDeviceInstanceIdW(IntPtr set, ref DeviceInfo info, StringBuilder id, int length, out uint required);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiGetDevicePropertyW(IntPtr set, ref DeviceInfo info, ref PropertyKey key, out uint type, byte[] buffer, int length, out uint required, uint flags);
    [DllImport("setupapi.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
}
