namespace NovaIsland.Models;

public sealed record LaunchGreeting(string Title, string Subtitle, string Symbol)
{
    // Local Windows time: first-use welcome takes precedence over the login greeting.
    public static LaunchGreeting? Create(bool welcomeShown, bool startup, DateTime localTime)
    {
        if (!welcomeShown) return new("Hoş geldin", "NovaLand yanında.", "spark");
        if (!startup) return null;
        return localTime.Hour switch
        {
            >= 5 and < 12 => new("Günaydın", "Yeni güne güzel bir başlangıç.", "sunrise"),
            >= 12 and < 18 => new("İyi günler", "Günün güzel geçsin.", "sun"),
            >= 18 and < 22 => new("İyi akşamlar", "Günün yorgunluğu geride kalsın.", "moon"),
            _ => new("İyi geceler", "Kendine de biraz zaman ayır.", "moon")
        };
    }
}
