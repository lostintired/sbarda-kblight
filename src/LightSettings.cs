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

    /// <param name="model">the keyboard the lighting went to; by default the one in <see cref="Model"/></param>
    public string Describe(KeyboardModel? model = null)
    {
        string name = (model ?? KeyboardModels.Find(Model))?.FindEffect(Effect)?.Name ?? KbLight.Effect.KnownName(Effect);
        return Text.Describe(name, Brightness);
    }
}
