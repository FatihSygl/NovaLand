using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using NovaIsland.Models;
namespace NovaIsland.Services;
public sealed class BluetoothAudioService
{
    private sealed record KnownAudio(string Id,string Name,ulong Address,Guid Container);
    private readonly Dictionary<string,KnownAudio> _known = new();
    private readonly BluetoothAudioTracker _tracker = new();
    private List<BatteryNode> _batteryNodes = new();
    private Task<List<BatteryNode>>? _batteryRead;
    private DateTimeOffset _lastBatteryRead;
    public BluetoothAudioSnapshot[] Connected { get; private set; } = [];
    public event Action<AudioDeviceNotice>? Notice;
    public BluetoothAudioService()=>_tracker.Notice+=notice=>Notice?.Invoke(notice);
    public async Task ScanAsync()
    {
        DeviceInformationCollection devices;
        try
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(4));
            devices=await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected),new[]{"System.Devices.ContainerId","System.Devices.Aep.ContainerId"}).AsTask(timeout.Token);
        }
        catch(Exception ex){DiagnosticLog.Write("BluetoothEnumeration",ex);return;}
        var audio=new List<KnownAudio>();
        foreach(var info in devices)
        {
            if(_known.TryGetValue(info.Id,out var cached)){audio.Add(cached);continue;}
            try
            {
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using var device=await BluetoothDevice.FromIdAsync(info.Id).AsTask(timeout.Token);
                if(device?.ClassOfDevice.MajorClass!=BluetoothMajorClass.AudioVideo)continue;
                Guid container=Guid.Empty;
                foreach(var key in new[]{"System.Devices.ContainerId","System.Devices.Aep.ContainerId"})
                    if(info.Properties.TryGetValue(key,out var value)&&Guid.TryParse(value?.ToString(),out var g)){container=g;break;}
                var item=new KnownAudio(info.Id,string.IsNullOrWhiteSpace(device.Name)?info.Name:device.Name,device.BluetoothAddress,container);
                _known[info.Id]=item;audio.Add(item);
            }
            catch(Exception ex){DiagnosticLog.Write("BluetoothDevice",ex);}
        }
        // Battery reads cannot hold connection/disconnection notices hostage.
        if(audio.Count>0)
        {
            if(_batteryRead is null && (audio.Any(x=>!_tracker.IsConnected(x.Id))||DateTimeOffset.UtcNow-_lastBatteryRead>TimeSpan.FromSeconds(20)))
                _batteryRead=Task.Run(BluetoothBatteryReader.Read);
            if(_batteryRead is not null)
            {
                try
                {
                    _batteryNodes=await _batteryRead.WaitAsync(TimeSpan.FromMilliseconds(450));
                    _lastBatteryRead=DateTimeOffset.UtcNow;_batteryRead=null;
                }
                catch(TimeoutException) { /* Reuse cached/unknown battery; keep checking connections. */ }
                catch(Exception ex){DiagnosticLog.Write("BluetoothBattery",ex);_batteryRead=null;_lastBatteryRead=DateTimeOffset.UtcNow;}
            }
        }
        Connected = audio.Select(x=>new BluetoothAudioSnapshot(x.Id,x.Name,BluetoothBatteryReader.Match(_batteryNodes,x.Address,x.Container))).ToArray();
        _tracker.Update(Connected);
    }
}
