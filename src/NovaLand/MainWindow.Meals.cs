using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using NovaIsland.Services;

namespace NovaIsland;

public partial class MainWindow
{
    private const string MissingMeal = "Bugün için menü kaydı yok.";
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly TimeZoneInfo AnkaraTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");

    private DateOnly _mealDay;
    private bool _dormLoading;
    private DateTimeOffset _dormRetryAt;

    private static DateOnly AnkaraToday() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, AnkaraTimeZone).DateTime);

    private void MealsCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _settings.MealsEnabled = MealsCheck.IsChecked == true;
        MealsTab.Visibility = _settings.MealsEnabled ? Visibility.Visible : Visibility.Collapsed;
        SaveSettings();
    }

    private void RefreshMeals()
    {
        DateOnly day = AnkaraToday();
        if (day != _mealDay)
        {
            _mealDay = day;
            _dormLoading = false;
            _dormRetryAt = default;
            MealsDateText.Text = day.ToString("d MMMM yyyy, dddd", TurkishCulture);
            var school = DailyMealMenus.GetSchool(day);
            SchoolLunchText.Text = school?.Lunch ?? MissingMeal;
            SchoolDinnerText.Text = school?.Dinner ?? MissingMeal;
        }

        if (!_dormLoading && DateTimeOffset.UtcNow >= _dormRetryAt)
            _ = RefreshDormMealsAsync(day);
    }

    private async Task RefreshDormMealsAsync(DateOnly day)
    {
        _dormLoading = true;
        DormBreakfastText.Text = DormDinnerText.Text = "Menü alınıyor…";
        try
        {
            var menu = await DailyMealMenus.GetDormAsync(day);
            if (_mealDay != day) return;
            DormBreakfastText.Text = menu.Breakfast ?? MissingMeal;
            DormDinnerText.Text = menu.Dinner ?? MissingMeal;
            _dormRetryAt = menu.Breakfast is null || menu.Dinner is null
                ? DateTimeOffset.UtcNow.AddMinutes(15)
                : DateTimeOffset.MaxValue;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            if (_mealDay != day) return;
            DormBreakfastText.Text = DormDinnerText.Text = "Menüye şu an ulaşılamıyor.";
            _dormRetryAt = DateTimeOffset.UtcNow.AddMinutes(1);
        }
        finally
        {
            if (_mealDay == day) _dormLoading = false;
        }
    }

    private void MealSource_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(DailyMealMenus.DormSourceUrl) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception) { StatusText.Text = "Menü kaynağı açılamadı."; }
    }
}
