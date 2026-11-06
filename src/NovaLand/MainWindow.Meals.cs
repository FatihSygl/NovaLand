using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using NovaIsland.Services;

namespace NovaIsland;

public partial class MainWindow
{
    private const string MissingMeal = "Bugün kayıt yok.";
    private sealed record MealDishRow(string Text, bool IsStatus = false);
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
            MealsDateText.Text = day.ToString("d MMMM", TurkishCulture);
            MealsWeekdayText.Text = day.ToString("dddd", TurkishCulture);
            var school = DailyMealMenus.GetSchool(day);
            SchoolLunchDishes.ItemsSource = MealRows(SplitSchoolDishes(school?.Lunch));
            SchoolDinnerDishes.ItemsSource = MealRows(SplitSchoolDishes(school?.Dinner));
        }

        if (!_dormLoading && DateTimeOffset.UtcNow >= _dormRetryAt)
            _ = RefreshDormMealsAsync(day);
    }

    private async Task RefreshDormMealsAsync(DateOnly day)
    {
        _dormLoading = true;
        DormBreakfastDishes.ItemsSource = DormDinnerDishes.ItemsSource = MealRows(null, "Menü alınıyor…");
        try
        {
            var menu = await DailyMealMenus.GetDormAsync(day);
            if (_mealDay != day) return;
            DormBreakfastDishes.ItemsSource = MealRows(menu.Breakfast);
            DormDinnerDishes.ItemsSource = MealRows(menu.Dinner);
            _dormRetryAt = menu.Breakfast is null || menu.Dinner is null
                ? DateTimeOffset.UtcNow.AddMinutes(15)
                : DateTimeOffset.MaxValue;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            if (_mealDay != day) return;
            DormBreakfastDishes.ItemsSource = DormDinnerDishes.ItemsSource = MealRows(null, "Menüye ulaşılamıyor.");
            _dormRetryAt = DateTimeOffset.UtcNow.AddMinutes(1);
        }
        finally
        {
            if (_mealDay == day) _dormLoading = false;
        }
    }

    private static IEnumerable<MealDishRow> MealRows(string[]? dishes, string status = MissingMeal) =>
        dishes is { Length: > 0 }
            ? dishes.Select(dish => new MealDishRow(dish))
            : new[] { new MealDishRow(status, IsStatus: true) };

    private static string[]? SplitSchoolDishes(string? menu) =>
        menu?.Split(" · ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void MealSource_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(DailyMealMenus.DormSourceUrl) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception) { StatusText.Text = "Menü kaynağı açılamadı."; }
    }
}
