using System.Text.Json;

namespace KbLight;

[Flags]
enum EffectOptions
{
    Brightness = 0x01,
    Speed = 0x02,
    DirectionHorizontal = 0x04,
    DirectionVertical = 0x08,
    Color = 0x10,
    Multicolor = 0x20,
    DirectionRadial = 0x40,
    DirectionRotation = 0x80,
    AnyDirection = DirectionHorizontal | DirectionVertical | DirectionRadial | DirectionRotation,
}

sealed record Effect(int Id, string Name, EffectOptions Options)
{
    const EffectOptions Animated = EffectOptions.Brightness | EffectOptions.Speed;
    const EffectOptions Colored = EffectOptions.Color | EffectOptions.Multicolor;

    // Ids and option masks mirror sbarda's own table (t_light_data.mode / config_func) for this keyboard.
    // Music rhythm (128) and per-key custom light (0) need the vendor app running, so they are left out.
    public static IReadOnlyList<Effect> All { get; } =
    [
        new(1, "Спектр", Animated),
        new(2, "Ступени", EffectOptions.Brightness | Colored),
        new(3, "Статичный цвет", EffectOptions.Brightness | Colored),
        new(4, "Дыхание", Animated | Colored),
        new(5, "Цветение", Animated),
        new(6, "Волна", Animated | Colored | EffectOptions.DirectionHorizontal),
        new(7, "Волна вверх-вниз", Animated | Colored | EffectOptions.DirectionVertical),
        new(8, "Фонтан", Animated | Colored | EffectOptions.DirectionRadial),
        new(9, "Млечный путь", Animated | Colored),
        new(10, "Вращение", Animated | Colored | EffectOptions.DirectionRotation),
        new(11, "Прилив", Animated | Colored),
        new(12, "Морская волна", Animated | Colored),
        new(13, "Рябь от нажатий", Animated | Colored),
        new(14, "Рябь на подсветке", Animated | Colored),
        new(15, "Одна клавиша", Animated | Colored),
        new(16, "Сетка", Animated | Colored),
        new(17, "Пианино", Animated | Colored),
        new(18, "Перелив", Animated | Colored),
        new(19, "Дождь", Animated | Colored),
        new(22, "Волновая полоса", Animated | Colored),
    ];

    public static Effect? Find(int id) => All.FirstOrDefault(e => e.Id == id);

    public override string ToString() => Name;
}

sealed class LightSettings
{
    public int Effect { get; set; } = 13;
    public int Brightness { get; set; } = 100;  // 0..100
    public int Speed { get; set; }              // 0..4, as on sbarda's slider
    public bool ReverseDirection { get; set; }
    public bool Multicolor { get; set; } = true;
    public int ColorIndex { get; set; }
    public string Rgb { get; set; } = "#FF0000";

    // Last valid settings block read from the keyboard (hex). Used as the base for a write
    // if the keyboard ever answers with a damaged block, so its other settings stay intact.
    public string? LastGoodBlock { get; set; }

    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static bool Exists => File.Exists(AppFiles.Settings);

    public static LightSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<LightSettings>(File.ReadAllText(AppFiles.Settings), JsonOptions) ?? new();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            if (Exists) Log.Write($"settings.json не прочитан ({e.Message}), беру значения по умолчанию");
            return new();
        }
    }

    public void Save()
    {
        string temp = AppFiles.Settings + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonOptions));
        File.Move(temp, AppFiles.Settings, overwrite: true);
    }

    public LightSettings Clone() => (LightSettings)MemberwiseClone();

    public Color GetColor()
    {
        try { return ColorTranslator.FromHtml(Rgb); }
        catch (Exception) { return Color.Red; }
    }

    public void SetColor(Color c) => Rgb = $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>Takes the lighting from a settings block read from the keyboard (bytes 8..16).</summary>
    public void SetFromBlock(ReadOnlySpan<byte> block)
    {
        Effect = block[8];
        Brightness = Math.Min((int)block[9], 100);
        Speed = Math.Clamp(4 - block[10], 0, 4);
        ReverseDirection = block[11] != 0;
        Multicolor = block[12] != 0;
        ColorIndex = block[13];
        Rgb = $"#{block[14]:X2}{block[15]:X2}{block[16]:X2}";
    }

    public byte[]? GetLastGoodBlock()
    {
        try { return LastGoodBlock is null ? null : Convert.FromHexString(LastGoodBlock); }
        catch (FormatException) { return null; }
    }

    public string Describe()
    {
        string name = KbLight.Effect.Find(Effect)?.Name ?? $"эффект {Effect}";
        return $"{name}, яркость {Brightness}%";
    }
}
