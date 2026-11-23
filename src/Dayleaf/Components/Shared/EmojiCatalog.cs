namespace Dayleaf.Components.Shared;

/// <summary>Curated sticker sets (user content — emoji are allowed here).</summary>
public static class EmojiCatalog
{
    public sealed record Sticker(string Char, string Name);
    public sealed record Category(string Id, string Name, IReadOnlyList<Sticker> Items);

    public static readonly IReadOnlyList<Category> Categories =
    [
        new("nature", "Nature", S("🌱 seedling", "🌳 tree", "🌲 pine", "🍂 leaves", "🍁 maple", "🌿 herb", "🌸 blossom", "🌻 sunflower", "🌷 tulip", "🌹 rose", "🍄 mushroom", "🌵 cactus", "🪴 plant", "🌾 grain", "🍀 clover", "🐝 bee", "🦋 butterfly", "🐞 ladybug", "🐦 bird", "🌊 wave", "⛰️ mountain", "🏔️ peak", "🌋 volcano", "🏕️ camping")),
        new("farm", "Farm", S("🚜 tractor", "🍎 apple", "🍐 pear", "🍒 cherries", "🍑 peach", "🍇 grapes", "🫒 olive", "🍋 lemon", "🥕 carrot", "🌽 corn", "🍅 tomato", "🥔 potato", "🐄 cow", "🐑 sheep", "🐓 rooster", "🐐 goat", "🐴 horse", "🐕 dog", "🐈 cat", "🧺 basket", "🪣 bucket", "💧 water", "🏡 house", "🛖 hut")),
        new("work", "Work", S("⌨️ keyboard", "💻 laptop", "🖥️ desktop", "📱 phone", "🛠️ tools", "⚙️ gear", "🧪 test", "📈 growth", "📊 chart", "📝 memo", "📌 pin", "📎 clip", "✏️ pencil", "🖊️ pen", "📚 books", "💡 idea", "🧠 brain", "🐛 bug", "🚀 rocket", "🏆 trophy", "🎯 target", "☕ coffee", "🗂️ files", "🔑 key")),
        new("celebrate", "Celebrate", S("🎉 party", "🎊 confetti", "🥳 party face", "🎂 cake", "🎁 gift", "🎈 balloon", "🍾 bubbly", "🥂 cheers", "💐 bouquet", "💍 ring", "👶 baby", "🎓 graduation", "🏅 medal", "⭐ star", "🌟 glowing star", "✨ sparkles", "🎆 fireworks", "🎵 music", "🎸 guitar", "📸 camera", "✈️ plane", "🗺️ map", "🧳 luggage", "🏖️ beach")),
        new("weather", "Weather", S("☀️ sun", "🌤️ sun cloud", "⛅ cloudy", "🌧️ rain", "⛈️ storm", "🌩️ lightning", "❄️ snow", "☃️ snowman", "🌈 rainbow", "🌙 moon", "🌕 full moon", "🌫️ fog", "🌪️ tornado", "💨 wind", "🔥 fire", "🌡️ heat")),
        new("feelings", "Feelings", S("❤️ heart", "🧡 orange heart", "💛 yellow heart", "💚 green heart", "💙 blue heart", "💜 purple heart", "🤍 white heart", "😊 smile", "😄 grin", "🥹 touched", "😌 relieved", "🤔 thinking", "😴 sleepy", "😢 sad", "😤 determined", "🤯 mind blown", "🙏 thanks", "👏 clap", "💪 strong", "👋 wave", "🤝 deal", "✌️ peace", "👀 eyes", "💯 hundred")),
        new("food", "Food", S("🍞 bread", "🥐 croissant", "🧀 cheese", "🍳 egg", "🥗 salad", "🍝 pasta", "🍕 pizza", "🍔 burger", "🌮 taco", "🍜 noodles", "🍣 sushi", "🍲 stew", "🫖 teapot", "🍵 tea", "🧃 juice", "🍰 cake slice", "🍪 cookie", "🍫 chocolate", "🍯 honey", "🫙 jar")),
    ];

    public static IEnumerable<Sticker> All => Categories.SelectMany(c => c.Items);

    public static readonly string[] SpaceIcons =
    [
        "📓", "🌳", "⌨️", "🌿", "🎈", "🏡", "✈️", "👶", "💼", "🎓", "🏃", "🍳", "📚", "🎨", "🎵", "🧘", "💪", "🐶", "🌻", "🚜",
        "💡", "🚀", "❤️", "⭐", "🌊", "⛰️", "📸", "🛠️", "🧪", "💰",
    ];

    private static List<Sticker> S(params string[] items) =>
        items.Select(i => { var p = i.IndexOf(' '); return new Sticker(i[..p], i[(p + 1)..]); }).ToList();
}
