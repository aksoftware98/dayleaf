using System.Globalization;

namespace Dayleaf.Localization;

public enum Noun { Page, Photo, Sticker, Space, Sheet, Result, Item, Year, Day, Element }

/// <summary>
/// UI text. Keys are the English text itself, so components stay readable: <c>@L["Spaces"]</c>,
/// <c>@L["Moved to {0}", date]</c>. Arabic comes from <see cref="Arabic.Strings"/>; a missing key falls
/// back to English (a test makes sure none are missing).
/// Injected into every component through _Imports.razor; services use the static <see cref="T"/>.
/// </summary>
public sealed class Loc
{
    public string this[string key, params object?[] args] => T(key, args);

    public string Count(int n, Noun noun) => Plural(n, noun);

    public string Date(DateOnly d, DateStyle style) => Dates.Format(d, style);
    public string Date(DateTime d, DateStyle style) => Dates.Format(d, style);

    public static string T(string key, params object?[] args) => TFor(Lang.Current, key, args);

    public static string TFor(string lang, string key, params object?[] args)
    {
        // "context|Text" keys tell apart words that are spelled the same in English ("font|Print" vs the Print button);
        // English shows the part after the bar.
        var text = Lang.Normalize(lang) == Lang.Ar && Arabic.Strings.TryGetValue(key, out var ar) ? ar
            : key.IndexOf('|') is var bar and > 0 ? key[(bar + 1)..] : key;
        return args.Length == 0 ? text : string.Format(Lang.Culture(lang), text, args);
    }

    /// <summary>"3 pages" / "3 صفحات", with Arabic's six plural forms (CLDR rules).</summary>
    public static string Plural(int n, Noun noun, string? lang = null)
    {
        var code = lang is null ? Lang.Current : Lang.Normalize(lang);
        if (code == Lang.Ar)
        {
            var (one, two, few, many) = Arabic.Nouns[noun];
            var mod = n % 100;
            return n switch
            {
                0 => $"0 {many}",
                1 => one,
                2 => two,
                _ when mod is >= 3 and <= 10 => $"{n} {few}",
                _ when mod is >= 11 and <= 99 => $"{n} {many}",
                _ => $"{n} {many}",
            };
        }
        var (singular, plural) = English[noun];
        return string.Create(CultureInfo.InvariantCulture, $"{n:N0} {(n == 1 ? singular : plural)}");
    }

    private static readonly Dictionary<Noun, (string, string)> English = new()
    {
        [Noun.Page] = ("page", "pages"),
        [Noun.Photo] = ("photo", "photos"),
        [Noun.Sticker] = ("sticker", "stickers"),
        [Noun.Space] = ("space", "spaces"),
        [Noun.Sheet] = ("sheet", "sheets"),
        [Noun.Result] = ("result", "results"),
        [Noun.Item] = ("item", "items"),
        [Noun.Year] = ("year", "years"),
        [Noun.Day] = ("day", "days"),
        [Noun.Element] = ("element", "elements"),
    };
}
