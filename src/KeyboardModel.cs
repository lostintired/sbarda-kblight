using System.Globalization;
using System.Text.Json;

namespace KbLight;

/// <summary>A keyboard KbLight may write to: USB ids, the settings interface, its lighting effects and
/// whether it has a light box (settings block bytes 24..31, spec light-box).</summary>
sealed record KeyboardModel(string Name, ushort Vid, ushort Pid, byte Interface, IReadOnlyList<Effect> Effects, bool LightBox,
    string? File)
{
    public string Id => FormatId(Vid, Pid);

    public static string FormatId(ushort vid, ushort pid) => $"{vid:X4}:{pid:X4}";

    public Effect? FindEffect(int id) => Effects.FirstOrDefault(e => e.Id == id);
}

/// <summary>
/// Built-in models (src/models/*.json, embedded) plus the user's %LOCALAPPDATA%\KbLight\models\*.json.
/// A user file replaces the built-in model with the same VID:PID. Files are re-read when they change,
/// so a new model works without restarting; each file content is logged at most once.
/// </summary>
static class KeyboardModels
{
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    static readonly object Gate = new();
    static readonly Dictionary<string, (DateTime Time, long Length, KeyboardModel? Model)> Cache = new(StringComparer.OrdinalIgnoreCase);
    static readonly HashSet<string> Logged = [];
    static IReadOnlyList<KeyboardModel>? _builtin;

    static readonly Dictionary<string, EffectOptions> OptionNames = new()
    {
        ["brightness"] = EffectOptions.Brightness,
        ["speed"] = EffectOptions.Speed,
        ["color"] = EffectOptions.Color,
        ["multicolor"] = EffectOptions.Multicolor,
        ["direction-horizontal"] = EffectOptions.DirectionHorizontal,
        ["direction-vertical"] = EffectOptions.DirectionVertical,
        ["direction-radial"] = EffectOptions.DirectionRadial,
        ["direction-rotation"] = EffectOptions.DirectionRotation,
    };

    public static string Folder => Path.Combine(AppFiles.DataDir, "models");

    public static IReadOnlyList<KeyboardModel> Current()
    {
        lock (Gate)
        {
            var models = new List<KeyboardModel>(Builtin());
            var taken = new HashSet<string>();
            foreach (var model in UserModels())
            {
                if (!taken.Add(model.Id))
                {
                    LogOnce($"dup {model.Id} {model.File}",
                        Text.ModelDuplicate(model.Id, models.First(m => m.Id == model.Id && m.File is not null).File));
                    continue;
                }
                int builtin = models.FindIndex(m => m.Id == model.Id);
                if (builtin >= 0)
                {
                    LogOnce($"replace {model.Id} {model.File} {Cache[Path.Combine(Folder, model.File!)].Time.Ticks}",
                        Text.ModelReplaces(model.Id, model.File));
                    models[builtin] = model;
                }
                else
                {
                    models.Add(model);
                }
            }
            return models;
        }
    }

    public static KeyboardModel? Find(string? id) =>
        id is null ? null : Current().FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The model whose effect list the window shows when no keyboard has been seen yet.</summary>
    public static KeyboardModel? Default => Current().FirstOrDefault();

    static IReadOnlyList<KeyboardModel> Builtin()
    {
        if (_builtin is not null) return _builtin;
        var list = new List<KeyboardModel>();
        var assembly = typeof(KeyboardModels).Assembly;
        foreach (string name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("KbLight.models.")).Order())
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            list.Add(Parse(reader.ReadToEnd(), file: null)); // built-in files are checked at build review; a bad one should fail loudly
        }
        return _builtin = list;
    }

    static IEnumerable<KeyboardModel> UserModels()
    {
        string[] files;
        try { files = Directory.Exists(Folder) ? Directory.GetFiles(Folder, "*.json") : []; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { files = []; }

        foreach (string path in files.Order(StringComparer.OrdinalIgnoreCase))
        {
            var info = new FileInfo(path);
            if (!Cache.TryGetValue(path, out var cached) || cached.Time != info.LastWriteTimeUtc || cached.Length != info.Length)
            {
                KeyboardModel? model = null;
                try { model = Parse(File.ReadAllText(path), info.Name); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or FormatException)
                {
                    string reason = e is JsonException ? Text.NotJson : e.Message;
                    LogOnce($"bad {path} {info.LastWriteTimeUtc.Ticks} {info.Length}", Text.ModelNotRead(info.Name, reason));
                }
                cached = (info.LastWriteTimeUtc, info.Length, model);
                Cache[path] = cached;
            }
            if (cached.Model is not null) yield return cached.Model;
        }
    }

    static void LogOnce(string key, string message)
    {
        if (Logged.Add(key)) Log.Write(message);
    }

    sealed class ModelFile
    {
        public string? Name { get; set; }
        public string? Vid { get; set; }
        public string? Pid { get; set; }
        public int? Interface { get; set; }
        public List<EffectFile>? Effects { get; set; }
        public bool LightBox { get; set; }
    }

    sealed class EffectFile
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public List<string>? Options { get; set; }
    }

    /// <exception cref="FormatException">with the reason, in the program language, for the log</exception>
    static KeyboardModel Parse(string json, string? file)
    {
        var data = JsonSerializer.Deserialize<ModelFile>(json, JsonOptions) ?? throw new FormatException(Text.FileEmpty);
        if (string.IsNullOrWhiteSpace(data.Name)) throw new FormatException(Text.NoName);
        ushort vid = ParseId(data.Vid, "vid"), pid = ParseId(data.Pid, "pid");
        if (data.Interface is not (>= 0 and <= 255)) throw new FormatException(Text.BadInterface);
        if (data.Effects is not { Count: > 0 }) throw new FormatException(Text.NoEffects);

        var effects = new List<Effect>();
        foreach (var e in data.Effects)
        {
            if (e.Id is not (>= 0 and <= 255)) throw new FormatException(Text.BadEffectId);
            if (effects.Any(x => x.Id == e.Id)) throw new FormatException(Text.EffectRepeated(e.Id.Value));
            EffectOptions options = 0;
            foreach (string option in e.Options ?? [])
                options |= OptionNames.TryGetValue(option.ToLowerInvariant(), out var flag)
                    ? flag : throw new FormatException(Text.UnknownOption(option));
            string name = string.IsNullOrWhiteSpace(e.Name) ? Effect.KnownName(e.Id.Value) : e.Name.Trim();
            effects.Add(new Effect(e.Id.Value, name, options));
        }
        return new KeyboardModel(data.Name.Trim(), vid, pid, (byte)data.Interface.Value, effects, data.LightBox, file);
    }

    static ushort ParseId(string? text, string field) =>
        text is { Length: 4 } && ushort.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort value)
            ? value : throw new FormatException(Text.BadId(field));
}
