using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using NovaIsland.Services;
namespace NovaIsland;
public partial class MainWindow
{
    private readonly WeatherService _weatherService = new();
    private readonly DispatcherTimer _weatherRefresh = new() { Interval=TimeSpan.FromMinutes(15) };
    private WeatherReading? _weather;
    private bool _weatherBusy;
    private void WeatherCredit_Navigate(object sender,System.Windows.Navigation.RequestNavigateEventArgs e) { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://open-meteo.com/"){UseShellExecute=true});e.Handled=true; }
    private void StartWeather()
    {
        InitializeMotion();
        WeatherCity.Text=string.IsNullOrEmpty(_settings.WeatherCity)?"Gökyüzüne bir bakış":_settings.WeatherCity;
        WeatherMini.Text=string.IsNullOrEmpty(_settings.WeatherCity)?"Hava durumu · Şehir seç":"Hava durumu · "+_settings.WeatherCity;
        WeatherArt.IsVisibleChanged+=async (_,_)=>{if(WeatherArt.IsVisible)await RefreshWeather();};
        _weatherRefresh.Tick+=async (_,_)=>{if(WeatherArt.IsVisible)await RefreshWeather();};
        _weatherRefresh.Start();
    }
    private void WeatherOpen_Click(object sender,RoutedEventArgs e){SetTab("Controls");SetExpanded(true);}
    private async void WeatherSearch_Click(object sender,RoutedEventArgs e)=>await SearchWeather();
    private async void WeatherQuery_KeyDown(object sender,System.Windows.Input.KeyEventArgs e){if(e.Key==Key.Enter){e.Handled=true;await SearchWeather();}}
    private async Task SearchWeather()
    {
        if(WeatherSearchButton.IsEnabled==false)return;
        string query=WeatherQuery.Text.Trim();if(query.Length<2)return;
        WeatherSearchButton.IsEnabled=false;WeatherStatus.Text="Şehir aranıyor…";
        try{WeatherPlaces.ItemsSource=await _weatherService.Search(query);WeatherPlaces.Visibility=Visibility.Visible;WeatherStatus.Text=WeatherPlaces.Items.Count==0?"Şehir bulunamadı.":"Sonuçlardan şehrini seç.";}
        catch(Exception ex){DiagnosticLog.Write("Weather search",ex);WeatherStatus.Text="Şehir araması yapılamadı. Tekrar deneyebilirsin.";}
        finally{if(!_exiting)WeatherSearchButton.IsEnabled=true;}
    }
    private async void WeatherPlace_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(WeatherPlaces.SelectedItem is not WeatherPlace p||_weatherBusy)return;
        _settings.WeatherCity=p.Name;_settings.WeatherLatitude=p.Latitude;_settings.WeatherLongitude=p.Longitude;SaveSettings();_weather=null;
        WeatherPlaces.Visibility=Visibility.Collapsed;await RefreshWeather();
    }
    private async void WeatherRefresh_Click(object sender,RoutedEventArgs e)=>await RefreshWeather(true);
    private async Task RefreshWeather(bool force=false)
    {
        if(_weatherBusy||_exiting||_settings.WeatherLatitude is not double lat||_settings.WeatherLongitude is not double lon)return;
        if(!force&&_weather is not null&&DateTimeOffset.Now-_weather.Fetched<TimeSpan.FromMinutes(15))return;
        _weatherBusy=true;WeatherPlaces.IsEnabled=false;WeatherStatus.Text="Gökyüzü güncelleniyor…";
        WeatherCity.Text=_settings.WeatherCity;
        try
        {
            var r=await _weatherService.Read(lat,lon);if(_exiting)return;_weather=r;
            WeatherArt.BeginAnimation(OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(.3,1,TimeSpan.FromMilliseconds(700)));
            WeatherArt.Code=r.Code;WeatherArt.Day=r.Day;WeatherArt.InvalidateVisual();
            WeatherTemperature.Text=$"{r.Temperature:0}°";WeatherCondition.Text=r.Condition;
            WeatherDetails.Text=$"Hissedilen {r.Feels:0}°   ·   Rüzgâr {r.Wind:0} km/sa   ·   Nem %{r.Humidity}";
            WeatherMini.Text=$"{r.Temperature:0}° · {r.Condition} · {_settings.WeatherCity.Split(',')[0]}";
            WeatherStatus.Text=$"Son kontrol {r.Fetched:HH:mm} · Seçili şehir için 15 dk önbellek";
            WeatherTemperature.BeginAnimation(OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(0,1,TimeSpan.FromMilliseconds(600)));
        }
        catch(Exception ex){DiagnosticLog.Write("Weather",ex);WeatherStatus.Text=_weather is null?"Hava durumu alınamadı. Bağlantını kontrol et.":$"Çevrimdışı · Son veri {_weather.Fetched:HH:mm}";}
        finally{_weatherBusy=false;if(!_exiting)WeatherPlaces.IsEnabled=true;}
    }
}
