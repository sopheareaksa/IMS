namespace IMS.Services;

/// <summary>
/// Category artwork for the plan cards. A photograph is optional: drop a JPG at
/// wwwroot/img/categories/{key}.jpg and the card renders it over the gradient band.
/// </summary>
public static class PlanVisuals
{
    public sealed record Category(string Key, string Icon, string Gradient);

    private static readonly (string[] Keywords, Category Category)[] Rules =
    {
        (new[] { "motor", "vehicle", "auto", "car", "transport" },
            new Category("motor", "car", "from-sky-600 to-blue-700")),
        (new[] { "health", "medical", "hospital", "dental", "wellness" },
            new Category("health", "stethoscope", "from-teal-600 to-emerald-600")),
        (new[] { "life", "term", "family", "death", "funeral" },
            new Category("life", "users", "from-indigo-600 to-violet-700")),
        (new[] { "home", "property", "house", "fire", "building", "plant" },
            new Category("home", "house", "from-amber-500 to-orange-600")),
        (new[] { "travel", "trip", "flight", "aviation", "baggage" },
            new Category("travel", "plane", "from-cyan-600 to-sky-700")),
        (new[] { "business", "commercial", "liability", "cargo", "marine", "engineering", "shop" },
            new Category("business", "briefcase", "from-slate-700 to-gray-900"))
    };

    private static readonly Category Fallback = new("general", "shield-check", "from-blue-600 to-indigo-700");

    public static Category For(string? insuranceTypeName)
    {
        var name = insuranceTypeName ?? string.Empty;
        foreach (var (keywords, category) in Rules)
        {
            if (keywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                return category;
            }
        }

        return Fallback;
    }

    public static string PhotoPath(Category category) => $"/img/categories/{category.Key}.jpg";
}
