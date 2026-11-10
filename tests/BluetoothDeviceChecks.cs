using NovaIsland.Services;

internal static class BluetoothDeviceChecks
{
    internal static void Run(Action<bool, string> assert)
    {
        const ulong address = 0x020A1B2C3D4E;
        const string placeholder = "Bluetooth 02:0a:1b:2c:3d:4e";
        var paired = Guid.NewGuid();
        var unrelated = Guid.NewGuid();
        BatteryNode[] nodes =
        [
            new(@"BTHENUM\DEV_020A1B2C3D4E", paired, null, placeholder, "AirPods Pro"),
            new(@"BTHHFENUM\Headset", paired, 62),
            new(@"BTHENUM\DEV_1020A1B2C3D4E", unrelated, 5, "Other headphones"),
            new(@"BTHENUM\DEV_000000000000", unrelated, 15, "Unidentified device")
        ];

        assert(BluetoothBatteryReader.ResolveName("Personal AirPods", placeholder, nodes, address, Guid.Empty)
            == "Personal AirPods", "Prefer the paired name over a Bluetooth address");
        assert(BluetoothBatteryReader.ResolveName(placeholder, null, nodes, address, Guid.Empty)
            == "AirPods Pro", "Recover the Bluetooth name from the matching physical device");
        assert(BluetoothBatteryReader.ResolveName(placeholder, null, [], address, Guid.Empty)
            == "Bluetooth ses cihazı", "Use a neutral label when the device name is unavailable");
        assert(BluetoothBatteryReader.Match(nodes, address, Guid.Empty) == 62,
            "Read battery from the matched headset child and reject partial address matches");
        assert(BluetoothBatteryReader.Match(nodes, 0, Guid.Empty) is null,
            "Missing identity does not match a placeholder address");
        assert(BluetoothBatteryReader.Match([], address, paired) is null,
            "A missing battery remains unknown");
    }
}
