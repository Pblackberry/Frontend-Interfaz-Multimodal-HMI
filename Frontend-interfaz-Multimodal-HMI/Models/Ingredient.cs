using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Frontend_interfaz_Multimodal_HMI.Models;

public sealed record Ingredient(string Name, string? Percent)
{
    public string Display => Percent is null ? Name : $"{Percent}% {Name}";
}

public static class IngredientParser
{
    private static readonly Regex _itemPattern =
        new(@"^(?<name>.+?)\s+(?<pct>\d+(?:[.,]\d+)?)\s*%$", RegexOptions.Compiled);

    public static IReadOnlyList<Ingredient> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) { return Array.Empty<Ingredient>(); }

        var result = new List<Ingredient>();
        var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var m = _itemPattern.Match(part);
            result.Add(m.Success
                ? new Ingredient(Capitalize(m.Groups["name"].Value.Trim()), m.Groups["pct"].Value)
                : new Ingredient(Capitalize(part), null));
        }
        return result;
    }

    private static string Capitalize(string s)
        => s.Length == 0 ? s : char.ToUpper(s[0], CultureInfo.CurrentCulture) + s[1..];
}
