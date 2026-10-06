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
    // Names by effect code are in Text.EffectName. Which effects a keyboard has and their options come from
    // its model file (KeyboardModels); a model file may name codes missing there.
    public static string KnownName(int id) => Text.EffectName(id);

    public override string ToString() => Name;
}

/// <summary>A light box mode (byte 24) and the options the window enables for it (spec light-box).</summary>
sealed record LightBoxMode(int Id, EffectOptions Options)
{
    public static readonly IReadOnlyList<LightBoxMode> All =
    [
        new(0, EffectOptions.Brightness | EffectOptions.Speed | EffectOptions.Color | EffectOptions.Multicolor), // flowing lines
        new(1, EffectOptions.Brightness | EffectOptions.Speed),                                                  // flashing, always multicolor
        new(2, EffectOptions.Brightness | EffectOptions.Color),                                                  // steady; multicolor is plain red
        new(3, EffectOptions.Brightness | EffectOptions.Speed | EffectOptions.Color | EffectOptions.Multicolor), // breathing
        new(4, 0),                                                                                               // off
    ];

    /// <summary>The mode for a code, out-of-range codes clamped to 0..4.</summary>
    public static LightBoxMode Find(int id) => All[Math.Clamp(id, 0, All.Count - 1)];

    public string Name => Text.LightBoxModeName(Id);

    public override string ToString() => Name;
}

/// <summary>
/// Light box settings. Immutable, so the tray's settings snapshot (a shallow clone) never changes under the write thread;
/// the window replaces the whole object with a <c>with</c> copy.
/// </summary>
sealed record LightBoxSettings
{
    public int Mode { get; init; }
    public int Brightness { get; init; } = 100; // 0..100
    public int Speed { get; init; } = 2;        // 0..4, as on the slider
    public bool Multicolor { get; init; } = true;
    public string Rgb { get; init; } = "#FF0000";

    public Color GetColor() => LightSettings.ParseColor(Rgb);

    /// <summary>Takes the light box from a settings block read from the keyboard (bytes 24..31, byte 27 aside).</summary>
    public static LightBoxSettings FromBlock(ReadOnlySpan<byte> block) => new()
    {
        Mode = Math.Clamp((int)block[24], 0, LightBoxMode.All.Count - 1),
        Brightness = Math.Min((int)block[25], 100),
        Speed = Math.Clamp(4 - block[26], 0, 4),
        Multicolor = block[28] != 0,
        Rgb = $"#{block[29]:X2}{block[30]:X2}{block[31]:X2}",
    };
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

    // Null until taken from a keyboard with a light box or edited in the window; until then bytes 24..31 are left alone.
    public LightBoxSettings? LightBox { get; set; }

    // Last valid settings block read from the keyboard (hex). Used as the base for a write
    // if the keyboard ever answers with a damaged block, so its other settings stay intact.
    public string? LastGoodBlock { get; set; }

    // VID:PID of the keyboard last written to or read from; picks the effect list the window shows.
    public string? Model { get; set; }

    // Whether the tray asks GitHub for a newer release (spec update-check); missing in older files means yes.
    public bool CheckUpdates { get; set; } = true;

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
            if (Exists) Log.Write(Text.SettingsNotRead(e.Message));
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

    public Color GetColor() => ParseColor(Rgb);

    /// <summary>#RRGGBB as a color; an unreadable one is red.</summary>
    public static Color ParseColor(string rgb)
    {
        try { return ColorTranslator.FromHtml(rgb); }
        catch (Exception) { return Color.Red; }
    }

    public void SetColor(Color c) => Rgb = FormatColor(c);

    public static string FormatColor(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>
    /// Takes the lighting from a settings block read from the keyboard (bytes 8..16),
    /// and the light box (bytes 24..31) if the model has one.
    /// </summary>
    public void SetFromBlock(ReadOnlySpan<byte> block, KeyboardModel? model)
    {
        if (model?.LightBox == true) LightBox = LightBoxSettings.FromBlock(block);
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

    /// <param name="model">the keyboard the lighting went to; by default the one in <see cref="Model"/></param>
    public string Describe(KeyboardModel? model = null)
    {
        string name = (model ?? KeyboardModels.Find(Model))?.FindEffect(Effect)?.Name ?? KbLight.Effect.KnownName(Effect);
        return Text.Describe(name, Brightness);
    }
}
