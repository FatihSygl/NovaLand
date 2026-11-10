using System.Runtime.InteropServices;
using System.Text;
namespace NovaIsland.Services;
public sealed record BatteryNode(string InstanceId, Guid Container, int? Percentage, string? Name = null, string? BusName = null);
public static class BluetoothBatteryReader
{
    // Windows HFP battery property; optional, driver-dependent. Never substitute laptop battery.
    private static readonly PropertyKey BatteryKey = new(new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"), 2);
    private static readonly PropertyKey ContainerKey = new(new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"), 2);
    private static readonly PropertyKey FriendlyNameKey = new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);
    private static readonly PropertyKey BusNameKey = new(new Guid("540B947E-8B40-45BC-A8A2-6A0B894CBDA2"), 4);

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
                bool physicalDevice = IsPhysicalBluetoothDevice(instance);
                nodes.Add(new BatteryNode(instance, container, percentage,
                    physicalDevice ? ReadName(set, ref info, FriendlyNameKey) : null,
                    physicalDevice ? ReadName(set, ref info, BusNameKey) : null));
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return nodes;
    }
    public static int? Match(IReadOnlyList<BatteryNode> nodes, ulong address, Guid container) =>
        MatchingNodes(nodes, address, container).Min(node => node.Percentage);

    internal static string ResolveName(string? pairedName, string? deviceName, IReadOnlyList<BatteryNode> nodes,
        ulong address, Guid container)
    {
        var name = UsableName(pairedName) ?? UsableName(deviceName);
        if (name is not null) return name;

        string hex = address.ToString("X12");
        return MatchingNodes(nodes, address, container)
            .Where(node => IsPhysicalBluetoothDevice(node.InstanceId))
            .OrderByDescending(node => address != 0 && HasAddress(node.InstanceId, hex))
            .Select(node => UsableName(node.Name) ?? UsableName(node.BusName))
            .FirstOrDefault(value => value is not null) ?? "Bluetooth ses cihazı";
    }

    internal static string? UsableName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        name = name.Trim();
        if (name.Equals("Bluetooth", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Bluetooth Device", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Bluetooth Audio Device", StringComparison.OrdinalIgnoreCase))
            return null;

        const string prefix = "Bluetooth ";
        string address = name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? name[prefix.Length..] : name;
        address = address.Replace(":", "").Replace("-", "");
        return address.Length == 12 && address.All(Uri.IsHexDigit) ? null : name;
    }

    private static IEnumerable<BatteryNode> MatchingNodes(IReadOnlyList<BatteryNode> nodes, ulong address, Guid container)
    {
        string hex = address.ToString("X12");
        var matchingContainers = nodes
            .Where(node => address != 0 && node.Container != Guid.Empty && HasAddress(node.InstanceId, hex))
            .Select(node => node.Container).ToHashSet();
        if (container != Guid.Empty) matchingContainers.Add(container);
        return nodes.Where(node => matchingContainers.Contains(node.Container) ||
            (address != 0 && HasAddress(node.InstanceId, hex)));
    }

    private static bool HasAddress(string instance, string hex)
    {
        for (int start = 0; start <= instance.Length - hex.Length;)
        {
            int index = instance.IndexOf(hex, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return false;
            int end = index + hex.Length;
            if ((index == 0 || !Uri.IsHexDigit(instance[index - 1])) &&
                (end == instance.Length || !Uri.IsHexDigit(instance[end]))) return true;
            start = index + 1;
        }
        return false;
    }

    private static bool IsPhysicalBluetoothDevice(string id) =>
        id.StartsWith(@"BTHENUM\DEV_", StringComparison.OrdinalIgnoreCase) ||
        id.StartsWith(@"BTHLE\DEV_", StringComparison.OrdinalIgnoreCase);

    private static string? ReadName(IntPtr set, ref DeviceInfo info, PropertyKey key)
    {
        const uint stringProperty = 0x12;
        var bytes = GetProperty(set, ref info, key, out var type, capacity: 1024);
        return type == stringProperty && bytes is { Length: >= 2 } && bytes.Length % 2 == 0
            ? Encoding.Unicode.GetString(bytes).TrimEnd('\0') : null;
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
    private static byte[]? GetProperty(IntPtr set, ref DeviceInfo info, PropertyKey key, out uint type, int capacity = 32)
    {
        var buffer = new byte[capacity];
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
