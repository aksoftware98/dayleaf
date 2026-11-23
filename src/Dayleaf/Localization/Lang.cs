using System.Globalization;

namespace Dayleaf.Localization;

/// <summary>The languages Dayleaf speaks. "en" and "ar"; Arabic is right-to-left.</summary>
public static class Lang
{
    public const string En = "en";
    public const string Ar = "ar";
    public static readonly string[] All = [En, Ar];

    // en-US matches the formats the app always used; "ar" is Gregorian with Western digits.
    private static readonly CultureInfo EnCulture = CultureInfo.ReadOnly(new CultureInfo("en-US"));
    private static readonly CultureInfo ArCulture = CultureInfo.ReadOnly(new CultureInfo("ar"));

    public static string Normalize(string? code) => code?.StartsWith(Ar, StringComparison.OrdinalIgnoreCase) == true ? Ar : En;

    public static CultureInfo Culture(string? code) => Normalize(code) == Ar ? ArCulture : EnCulture;

    public static bool IsRtl(string? code) => Normalize(code) == Ar;

    public static string Dir(string? code) => IsRtl(code) ? "rtl" : "ltr";

    /// <summary>The language of the current request / circuit (from the culture cookie or the browser).</summary>
    public static string Current => Normalize(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public static string NativeName(string code) => Normalize(code) == Ar ? "العربية" : "English";
}

public enum DateStyle
{
    /// <summary>Nov 16, 2026 · 16 نوفمبر 2026</summary>
    Short,
    /// <summary>Monday, November 16, 2026 · الاثنين، 16 نوفمبر 2026</summary>
    Long,
    /// <summary>Mon, Nov 16, 2026 · الاثنين، 16 نوفمبر 2026</summary>
    WeekdayShort,
    /// <summary>Nov 16 · 16 نوفمبر</summary>
    MonthDay,
    /// <summary>November 16 · 16 نوفمبر</summary>
    MonthDayLong,
    /// <summary>November 16, 2026 · 16 نوفمبر 2026</summary>
    LongNoWeekday,
    /// <summary>Nov 2026 · نوفمبر 2026</summary>
    MonthYear,
    /// <summary>Nov · نوفمبر (Arabic has no month abbreviations)</summary>
    MonthAbbr,
    /// <summary>November · نوفمبر</summary>
    Month,
    /// <summary>Monday · الاثنين</summary>
    Weekday,
    /// <summary>Mon · Nov 16, 2026 · الاثنين · 16 نوفمبر 2026 (sheet footer)</summary>
    Footer,
}

public static class Dates
{
    public static string Format(DateOnly d, DateStyle style, string? lang = null) =>
        Format(d.ToDateTime(TimeOnly.MinValue), style, lang);

    public static string Format(DateTime d, DateStyle style, string? lang = null)
    {
        var code = lang is null ? Lang.Current : Lang.Normalize(lang);
        var c = Lang.Culture(code);
        var pattern = code == Lang.Ar
            ? style switch
            {
                DateStyle.Short or DateStyle.LongNoWeekday => "d MMMM yyyy",
                DateStyle.Long or DateStyle.WeekdayShort => "dddd، d MMMM yyyy",
                DateStyle.MonthDay or DateStyle.MonthDayLong => "d MMMM",
                DateStyle.MonthYear => "MMMM yyyy",
                DateStyle.MonthAbbr or DateStyle.Month => "MMMM",
                DateStyle.Weekday => "dddd",
                DateStyle.Footer => "dddd · d MMMM yyyy",
                _ => "d MMMM yyyy",
            }
            : style switch
            {
                DateStyle.Short => "MMM d, yyyy",
                DateStyle.Long => "dddd, MMMM d, yyyy",
                DateStyle.WeekdayShort => "ddd, MMM d, yyyy",
                DateStyle.MonthDay => "MMM d",
                DateStyle.MonthDayLong => "MMMM d",
                DateStyle.LongNoWeekday => "MMMM d, yyyy",
                DateStyle.MonthYear => "MMM yyyy",
                DateStyle.MonthAbbr => "MMM",
                DateStyle.Month => "MMMM",
                DateStyle.Weekday => "dddd",
                DateStyle.Footer => "ddd · MMM d, yyyy",
                _ => "MMM d, yyyy",
            };
        return d.ToString(pattern, c);
    }

    public static string Time(DateTimeOffset t) => t.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string MonthName(int month, bool abbreviated = false, string? lang = null) =>
        Format(new DateTime(2000, month, 1), abbreviated ? DateStyle.MonthAbbr : DateStyle.Month, lang);
}
