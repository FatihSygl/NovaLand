namespace NovaIsland.Models;
public sealed class BatteryAlertState
{
    private int _announced;
    public int Update(int? percentage)
    {
        if (percentage is null || percentage < 0 || percentage > 100) return 0;
        if (percentage > 25) { _announced = 0; return 0; }
        int tier = percentage <= 10 ? 2 : percentage <= 20 ? 1 : 0;
        if (tier <= _announced) return 0;
        _announced = tier;
        return tier == 2 ? 10 : 20;
    }
}
