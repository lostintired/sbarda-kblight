namespace KbLight;

static class AppFiles
{
    public static string DataDir { get; } = Directory.CreateDirectory(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KbLight")).FullName;

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
    const long MaxBytes = 512 * 1024;
    static readonly object Gate = new();

    public static void Write(string message)
    {
        lock (Gate)
        {
            try
            {
                var file = new FileInfo(AppFiles.Log);
                if (file.Exists && file.Length > MaxBytes)
                    File.Move(AppFiles.Log, AppFiles.Log + ".old", overwrite: true);
                File.AppendAllText(AppFiles.Log, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
