namespace Dayleaf.Localization;

/// <summary>Arabic text for every UI key (the English text). Generated list, hand-translated.</summary>
public static partial class Arabic
{
    /// <summary>Arabic plural forms: (one, two, few: 3–10, many: 11+ and other).</summary>
    public static readonly IReadOnlyDictionary<Noun, (string One, string Two, string Few, string Many)> Nouns =
        new Dictionary<Noun, (string, string, string, string)>
        {
            [Noun.Page] = ("صفحة واحدة", "صفحتان", "صفحات", "صفحة"),
            [Noun.Photo] = ("صورة واحدة", "صورتان", "صور", "صورة"),
            [Noun.Sticker] = ("ملصق واحد", "ملصقان", "ملصقات", "ملصقاً"),
            [Noun.Space] = ("مساحة واحدة", "مساحتان", "مساحات", "مساحة"),
            [Noun.Sheet] = ("ورقة واحدة", "ورقتان", "أوراق", "ورقة"),
            [Noun.Result] = ("نتيجة واحدة", "نتيجتان", "نتائج", "نتيجة"),
            [Noun.Item] = ("عنصر واحد", "عنصران", "عناصر", "عنصراً"),
            [Noun.Year] = ("سنة واحدة", "سنتان", "سنوات", "سنة"),
            [Noun.Day] = ("يوم واحد", "يومان", "أيام", "يوماً"),
            [Noun.Element] = ("عنصر واحد", "عنصران", "عناصر", "عنصراً"),
        };

    // Built lazily: static initializers in different partial-class files run in no guaranteed order.
    private static readonly Lazy<IReadOnlyDictionary<string, string>> LazyStrings = new(() =>
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (en, ar) in Entries ?? throw new InvalidOperationException("Arabic entries not initialized."))
            if (!d.TryAdd(en, ar)) throw new InvalidOperationException($"Duplicate Arabic key: {en}");
        return d;
    });

    public static IReadOnlyDictionary<string, string> Strings => LazyStrings.Value;
}
