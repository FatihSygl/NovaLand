using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using NovaIsland.Services;

namespace NovaIsland;

public partial class MainWindow
{
    private const string MissingMeal = "Bu tarih için menü yok.";
    private sealed record MealDishRow(string Text, bool IsStatus = false);
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly TimeZoneInfo AnkaraTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");

    private DateOnly _mealDay;
    private DateOnly? _selectedMealDay;
    private DateOnly _calendarMonth;
    private CancellationTokenSource? _dormRequest;
    private DateTimeOffset _dormRetryAt;

    private DateOnly CurrentMealDay => _selectedMealDay ?? AnkaraToday();

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
        DateOnly day = CurrentMealDay;
        if (day != _mealDay)
        {
            _mealDay = day;
            _dormRequest?.Cancel();
            _dormRequest = null;
            _dormRetryAt = default;
            MealsTitle.Text = day == AnkaraToday() ? "Bugünün menüsü" : "Günün menüsü";
            MealsDateText.Text = day.ToString("d MMMM yyyy, dddd", TurkishCulture);
            if (MealsCalendar.Visibility == Visibility.Visible) RenderMealCalendar();
            var school = DailyMealMenus.GetSchool(day);
            SchoolLunchDishes.ItemsSource = MealRows(SplitSchoolDishes(school?.Lunch));
            SchoolDinnerDishes.ItemsSource = MealRows(SplitSchoolDishes(school?.Dinner));
        }

        if (_dormRequest is null && DateTimeOffset.UtcNow >= _dormRetryAt)
            _ = RefreshDormMealsAsync(day);
    }

    private async Task RefreshDormMealsAsync(DateOnly day)
    {
        using var request = new CancellationTokenSource();
        _dormRequest = request;
        DormBreakfastDishes.ItemsSource = DormDinnerDishes.ItemsSource = MealRows(null, "Menü alınıyor…");
        try
        {
            var menu = await DailyMealMenus.GetDormAsync(day, request.Token);
            if (_dormRequest != request) return;
            DormBreakfastDishes.ItemsSource = MealRows(menu.Breakfast);
            DormDinnerDishes.ItemsSource = MealRows(menu.Dinner);
            _dormRetryAt = menu.Breakfast is null || menu.Dinner is null
                ? DateTimeOffset.UtcNow.AddMinutes(15)
                : DateTimeOffset.MaxValue;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            if (_dormRequest != request) return;
            DormBreakfastDishes.ItemsSource = DormDinnerDishes.ItemsSource = MealRows(null, "Menüye ulaşılamıyor.");
            _dormRetryAt = DateTimeOffset.UtcNow.AddMinutes(1);
        }
        finally
        {
            if (_dormRequest == request) _dormRequest = null;
        }
    }

    private sealed record MealCalendarCell(DateOnly? Date, string Label, string Description, bool IsToday, bool IsSelected);

    private void MealsDate_Click(object sender, RoutedEventArgs e)
    {
        if (CloseMealCalendar()) return;

        var day = CurrentMealDay;
        _calendarMonth = new DateOnly(day.Year, day.Month, 1);
        RenderMealCalendar();
        MealsMenus.Visibility = Visibility.Collapsed;
        MealsCalendar.Visibility = Visibility.Visible;
        MealsView.ScrollToTop();
    }

    private bool CloseMealCalendar(bool restoreFocus = false)
    {
        if (MealsCalendar.Visibility != Visibility.Visible) return false;
        MealsCalendar.Visibility = Visibility.Collapsed;
        MealsMenus.Visibility = Visibility.Visible;
        if (restoreFocus) MealsDateButton.Focus();
        return true;
    }

    private void MealMonth_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string direction }) return;
        if (direction == "-1" && MealPreviousMonth.IsEnabled)
            _calendarMonth = _calendarMonth.AddMonths(-1);
        else if (direction == "1" && MealNextMonth.IsEnabled)
            _calendarMonth = _calendarMonth.AddMonths(1);
        RenderMealCalendar();
    }

    private void RenderMealCalendar()
    {
        MealCalendarMonth.Text = _calendarMonth.ToString("MMMM yyyy", TurkishCulture);
        MealPreviousMonth.IsEnabled = _calendarMonth > DateOnly.MinValue;
        MealNextMonth.IsEnabled = _calendarMonth.Year < DateOnly.MaxValue.Year ||
            _calendarMonth.Month < DateOnly.MaxValue.Month;

        DateOnly today = AnkaraToday();
        DateOnly selected = _selectedMealDay ?? today;
        int offset = ((int)_calendarMonth.DayOfWeek + 6) % 7;
        int count = DateTime.DaysInMonth(_calendarMonth.Year, _calendarMonth.Month);
        var cells = new List<MealCalendarCell>(offset + count);
        for (int i = 0; i < offset; i++)
            cells.Add(new MealCalendarCell(null, "", "", false, false));
        for (int i = 0; i < count; i++)
        {
            DateOnly day = _calendarMonth.AddDays(i);
            cells.Add(new MealCalendarCell(day, day.Day.ToString(TurkishCulture),
                day.ToString("d MMMM yyyy, dddd", TurkishCulture), day == today, day == selected));
        }
        MealCalendarDays.ItemsSource = cells;
    }

    private void MealDay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MealCalendarCell { Date: DateOnly day } })
            SelectMealDay(day);
    }

    private void MealToday_Click(object sender, RoutedEventArgs e) => SelectMealDay(AnkaraToday());

    private void SelectMealDay(DateOnly day)
    {
        _selectedMealDay = day == AnkaraToday() ? null : day;
        CloseMealCalendar(restoreFocus: true);
        RefreshMeals();
    }

    private static IEnumerable<MealDishRow> MealRows(string[]? dishes, string status = MissingMeal) =>
        dishes is { Length: > 0 }
            ? dishes.Select(dish => new MealDishRow(dish))
            : new[] { new MealDishRow(status, IsStatus: true) };

    private static string[]? SplitSchoolDishes(string? menu) =>
        menu?.Split(" · ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(FormatSchoolDish)
            .ToArray();

    private static string FormatSchoolDish(string dish)
    {
        var casing = TurkishCulture.TextInfo;
        if (dish != casing.ToUpper(dish)) return dish;

        string lower = casing.ToLower(dish);
        return casing.ToUpper(lower[..1]) + lower[1..];
    }

    private void MealSource_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(DailyMealMenus.DormSourceUrl) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception) { StatusText.Text = "Menü kaynağı açılamadı."; }
    }
}
