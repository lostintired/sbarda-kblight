namespace KbLight;

static class AppFiles
{
    public static string DataDir { get; } = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "KbLightChecks", Guid.NewGuid().ToString("N"))).FullName;
    public static string Settings => Path.Combine(DataDir, "settings.json");
    public static string Log => Path.Combine(DataDir, "kblight.log");
    public static Icon LoadIcon(int size)
    {
        using var stream = typeof(AppFiles).Assembly.GetManifestResourceStream("KbLight.app.ico")!;
        return new Icon(stream, size, size);
    }
}

static class Log
{
    public static void Write(string message) => File.AppendAllText(AppFiles.Log, message + Environment.NewLine);
}
