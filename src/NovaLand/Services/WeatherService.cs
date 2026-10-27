using System.Globalization;
using System.Net.Http;
using System.Text.Json;
namespace NovaIsland.Services;
public sealed record WeatherPlace(string Name, double Latitude, double Longitude);
public sealed record WeatherReading(double Temperature, double Feels, int Code, bool Day, double Wind, int Humidity, DateTimeOffset Fetched)
{
    public string Condition => Code switch { 0 => Day ? "Açık" : "Açık gece", 1 or 2 => "Parçalı bulutlu", 3 => "Bulutlu",45 or 48 => "Sisli", >=71 and <=77 or 85 or 86 => "Kar yağışlı", >=95 => "Gök gürültülü", >=51 and <=67 or >=80 and <=82 => "Yağmurlu", _ => "Değişken" };
}
public sealed class WeatherService : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 512000 };
    public async Task<WeatherPlace[]> Search(string query)
    {
        using var json = JsonDocument.Parse(await _http.GetStringAsync("https://geocoding-api.open-meteo.com/v1/search?name=" + Uri.EscapeDataString(query) + "&count=8&language=tr&format=json"));
        if (!json.RootElement.TryGetProperty("results", out var results)) return [];
        return results.EnumerateArray().Select(p => new WeatherPlace(p.GetProperty("name").GetString() + ", " + (p.TryGetProperty("admin1", out var a) ? a.GetString() + ", " : "") + (p.TryGetProperty("country", out var c) ? c.GetString() : ""),p.GetProperty("latitude").GetDouble(),p.GetProperty("longitude").GetDouble())).ToArray();
    }
    public async Task<WeatherReading> Read(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || Math.Abs(latitude)>90 || Math.Abs(longitude)>180) throw new ArgumentException();
        var url = FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,apparent_temperature,weather_code,is_day,wind_speed_10m,relative_humidity_2m");
        using var json = JsonDocument.Parse(await _http.GetStringAsync(url));
        var p=json.RootElement.GetProperty("current");
        var t=p.GetProperty("temperature_2m").GetDouble();var f=p.GetProperty("apparent_temperature").GetDouble();var w=p.GetProperty("wind_speed_10m").GetDouble();
        if(!double.IsFinite(t)||!double.IsFinite(f)||!double.IsFinite(w))throw new JsonException();
        return new(t,f,p.GetProperty("weather_code").GetInt32(),p.GetProperty("is_day").GetInt32()==1,w,p.GetProperty("relative_humidity_2m").GetInt32(),DateTimeOffset.Now);
    }
    public void Dispose()=>_http.Dispose();
}
