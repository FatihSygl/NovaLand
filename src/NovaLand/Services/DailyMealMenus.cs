using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace NovaIsland.Services;

internal static class DailyMealMenus
{
    public const string DormSourceUrl = "https://kepce.org/ankara";

    private static readonly Lazy<Dictionary<DateOnly, SchoolMenu>> SchoolMenus = new(LoadSchoolMenus);
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(8),
        MaxResponseContentBufferSize = 256 * 1024
    };
    private static readonly JsonSerializerOptions ApiJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    internal sealed record SchoolMenu(string? Lunch, string? Dinner);
    internal sealed record DormMenu(string[]? Breakfast, string[]? Dinner);
    private sealed record ApiMeal(string CitySlug, DateOnly ServeDate, string MealType, string Status, ApiDish[]? Items);
    private sealed record ApiDish(int OrderIndex, string? Name, bool IsAlternative);

    public static SchoolMenu? GetSchool(DateOnly day) => SchoolMenus.Value.GetValueOrDefault(day);

    public static async Task<DormMenu> GetDormAsync(DateOnly day, CancellationToken cancellationToken)
    {
        string date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var json = await Client.GetStringAsync($"https://kepce.org/api/v1/menus?city=ankara&date={date}", cancellationToken)
            .ConfigureAwait(false);
        var meals = JsonSerializer.Deserialize<ApiMeal[]>(json, ApiJson) ?? [];
        var matching = meals.Where(meal => meal.CitySlug == "ankara" && meal.ServeDate == day &&
            meal.Status == "approved");
        return new DormMenu(FormatMeal(matching.FirstOrDefault(meal => meal.MealType == "breakfast")),
            FormatMeal(matching.FirstOrDefault(meal => meal.MealType == "dinner")));
    }

    private static Dictionary<DateOnly, SchoolMenu> LoadSchoolMenus()
    {
        using var resource = typeof(DailyMealMenus).Assembly.GetManifestResourceStream("NovaIsland.SchoolMeals2026-11.json");
        return resource is null ? [] : JsonSerializer.Deserialize<Dictionary<DateOnly, SchoolMenu>>(resource) ?? [];
    }

    private static string[]? FormatMeal(ApiMeal? meal)
    {
        if (meal?.Items is not { Length: > 0 } items) return null;
        var groups = items.Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.OrderIndex)
            .OrderBy(group => group.Key)
            .Select(group => string.Join(" / ", group.OrderBy(item => item.IsAlternative).Select(item => item.Name)))
            .ToArray();
        return groups.Length == 0 ? null : groups;
    }
}
