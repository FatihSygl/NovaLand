namespace NovaIsland.Models;
public sealed record AudioDeviceNotice(string Id, string Name, bool Connected, int? Battery, int LowThreshold, string Reason);
public sealed record BluetoothAudioSnapshot(string Id, string Name, int? Battery);
public sealed class BluetoothAudioTracker
{
    private Dictionary<string,BluetoothAudioSnapshot> _connected = new();
    private readonly Dictionary<string,BatteryAlertState> _alerts = new();
    public bool IsConnected(string id)=>_connected.ContainsKey(id);
    public event Action<AudioDeviceNotice>? Notice;
    public void Update(IEnumerable<BluetoothAudioSnapshot> snapshot)
    {
        var current=snapshot.GroupBy(x=>x.Id).ToDictionary(x=>x.Key,x=>x.Last());
        foreach(var item in current.Values)
        {
            if(!_alerts.TryGetValue(item.Id,out var alert))_alerts[item.Id]=alert=new();
            int low=alert.Update(item.Battery);
            if(!_connected.TryGetValue(item.Id,out var old))Notice?.Invoke(new(item.Id,item.Name,true,item.Battery,low,"Connected"));
            else if(low>0)Notice?.Invoke(new(item.Id,item.Name,true,item.Battery,low,"LowBattery"));
            else if(old.Battery is null && item.Battery.HasValue)Notice?.Invoke(new(item.Id,item.Name,true,item.Battery,0,"BatteryAvailable"));
        }
        foreach(var old in _connected.Values.Where(x=>!current.ContainsKey(x.Id)))
        { Notice?.Invoke(new(old.Id,old.Name,false,null,0,"Disconnected"));_alerts.Remove(old.Id); }
        _connected=current;
    }
}
