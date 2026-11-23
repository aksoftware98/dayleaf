using Dayleaf.Localization;
using Dayleaf.Services;

namespace Dayleaf.Components.Shared;

/// <summary>Curated sticker sets (user content — emoji are allowed here). Each sticker has an English and an Arabic name.</summary>
public static class EmojiCatalog
{
    public sealed record Sticker(string Char, string Name, string ArabicName)
    {
        public string LocalName => Lang.Current == Lang.Ar ? ArabicName : Name;

        /// <summary>Matches either name; Arabic is compared after normalization (hamza, taa marbuta, tashkeel…).</summary>
        public bool Matches(string? query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            var q = query.Trim();
            return Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                   || ArabicText.Normalize(ArabicName).Contains(ArabicText.Normalize(q), StringComparison.Ordinal);
        }
    }

    public sealed record Category(string Id, string Name, IReadOnlyList<Sticker> Items);

    public static readonly IReadOnlyList<Category> Categories =
    [
        new("nature", "Nature", S(
            "🌱 seedling|شتلة", "🌳 tree|شجرة", "🌲 pine|صنوبر", "🍂 leaves|أوراق الخريف", "🍁 maple|قيقب", "🌿 herb|عشب", "🌸 blossom|زهر", "🌻 sunflower|دوار الشمس",
            "🌷 tulip|توليب", "🌹 rose|وردة", "🍄 mushroom|فطر", "🌵 cactus|صبار", "🪴 plant|نبتة", "🌾 grain|سنابل", "🍀 clover|برسيم", "🐝 bee|نحلة",
            "🦋 butterfly|فراشة", "🐞 ladybug|دعسوقة", "🐦 bird|عصفور", "🌊 wave|موجة", "⛰️ mountain|جبل", "🏔️ peak|قمة", "🌋 volcano|بركان", "🏕️ camping|تخييم")),
        new("farm", "Farm", S(
            "🚜 tractor|جرار", "🍎 apple|تفاح", "🍐 pear|إجاص", "🍒 cherries|كرز", "🍑 peach|خوخ", "🍇 grapes|عنب", "🫒 olive|زيتون", "🍋 lemon|ليمون",
            "🥕 carrot|جزر", "🌽 corn|ذرة", "🍅 tomato|طماطم", "🥔 potato|بطاطا", "🐄 cow|بقرة", "🐑 sheep|خروف", "🐓 rooster|ديك", "🐐 goat|ماعز",
            "🐴 horse|حصان", "🐕 dog|كلب", "🐈 cat|قطة", "🧺 basket|سلة", "🪣 bucket|دلو", "💧 water|ماء", "🏡 house|بيت", "🛖 hut|كوخ")),
        new("work", "Work", S(
            "⌨️ keyboard|لوحة مفاتيح", "💻 laptop|حاسوب محمول", "🖥️ desktop|حاسوب", "📱 phone|هاتف", "🛠️ tools|أدوات", "⚙️ gear|ترس", "🧪 test|تجربة", "📈 growth|نمو",
            "📊 chart|رسم بياني", "📝 memo|مذكرة", "📌 pin|دبوس", "📎 clip|مشبك", "✏️ pencil|قلم رصاص", "🖊️ pen|قلم", "📚 books|كتب", "💡 idea|فكرة",
            "🧠 brain|دماغ", "🐛 bug|خلل", "🚀 rocket|صاروخ", "🏆 trophy|كأس", "🎯 target|هدف", "☕ coffee|قهوة", "🗂️ files|ملفات", "🔑 key|مفتاح")),
        new("celebrate", "Celebrate", S(
            "🎉 party|احتفال", "🎊 confetti|قصاصات ملونة", "🥳 party face|فرحة", "🎂 cake|كعكة", "🎁 gift|هدية", "🎈 balloon|بالون", "🍾 bubbly|احتفال", "🥂 cheers|نخب",
            "💐 bouquet|باقة ورد", "💍 ring|خاتم", "👶 baby|طفل", "🎓 graduation|تخرج", "🏅 medal|ميدالية", "⭐ star|نجمة", "🌟 glowing star|نجمة لامعة", "✨ sparkles|بريق",
            "🎆 fireworks|ألعاب نارية", "🎵 music|موسيقى", "🎸 guitar|غيتار", "📸 camera|كاميرا", "✈️ plane|طائرة", "🗺️ map|خريطة", "🧳 luggage|حقيبة سفر", "🏖️ beach|شاطئ")),
        new("weather", "Weather", S(
            "☀️ sun|شمس", "🌤️ sun cloud|شمس وغيوم", "⛅ cloudy|غائم", "🌧️ rain|مطر", "⛈️ storm|عاصفة", "🌩️ lightning|برق", "❄️ snow|ثلج", "☃️ snowman|رجل الثلج",
            "🌈 rainbow|قوس قزح", "🌙 moon|هلال", "🌕 full moon|بدر", "🌫️ fog|ضباب", "🌪️ tornado|إعصار", "💨 wind|رياح", "🔥 fire|نار", "🌡️ heat|حرارة")),
        new("feelings", "Feelings", S(
            "❤️ heart|قلب", "🧡 orange heart|قلب برتقالي", "💛 yellow heart|قلب أصفر", "💚 green heart|قلب أخضر", "💙 blue heart|قلب أزرق", "💜 purple heart|قلب بنفسجي", "🤍 white heart|قلب أبيض", "😊 smile|ابتسامة",
            "😄 grin|ضحكة", "🥹 touched|تأثر", "😌 relieved|ارتياح", "🤔 thinking|تفكير", "😴 sleepy|نعسان", "😢 sad|حزين", "😤 determined|عزيمة", "🤯 mind blown|ذهول",
            "🙏 thanks|شكراً", "👏 clap|تصفيق", "💪 strong|قوة", "👋 wave|تحية", "🤝 deal|اتفاق", "✌️ peace|سلام", "👀 eyes|عيون", "💯 hundred|مئة بالمئة")),
        new("food", "Food", S(
            "🍞 bread|خبز", "🥐 croissant|كرواسان", "🧀 cheese|جبنة", "🍳 egg|بيض", "🥗 salad|سلطة", "🍝 pasta|معكرونة", "🍕 pizza|بيتزا", "🍔 burger|برغر",
            "🌮 taco|تاكو", "🍜 noodles|شعيرية", "🍣 sushi|سوشي", "🍲 stew|طبخة", "🫖 teapot|إبريق شاي", "🍵 tea|شاي", "🧃 juice|عصير", "🍰 cake slice|قطعة كعك",
            "🍪 cookie|بسكويت", "🍫 chocolate|شوكولا", "🍯 honey|عسل", "🫙 jar|مرطبان")),
        new("arabic", "Arabic", S(
            "🌙 crescent|هلال رمضان", "🕌 mosque|مسجد", "🏮 lantern|فانوس", "🕋 kaaba|الكعبة", "📿 beads|مسبحة", "🌴 palm|نخلة", "🐪 camel|جمل", "🏜️ desert|صحراء",
            "☕ arabic coffee|قهوة عربية", "🫖 tea|شاي", "🍬 sweets|حلوى", "🤲 dua|دعاء")),
    ];

    public static IEnumerable<Sticker> All => Categories.SelectMany(c => c.Items);

    public static readonly string[] SpaceIcons =
    [
        "📓", "🌳", "⌨️", "🌿", "🎈", "🏡", "✈️", "👶", "💼", "🎓", "🏃", "🍳", "📚", "🎨", "🎵", "🧘", "💪", "🐶", "🌻", "🚜",
        "💡", "🚀", "❤️", "⭐", "🌊", "⛰️", "📸", "🛠️", "🧪", "💰", "🕌", "🌙", "🌴", "🏮",
    ];

    private static List<Sticker> S(params string[] items) => items.Select(i =>
    {
        var space = i.IndexOf(' ');
        var bar = i.IndexOf('|');
        return new Sticker(i[..space], i[(space + 1)..bar], i[(bar + 1)..]);
    }).ToList();
}
