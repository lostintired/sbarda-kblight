using System.ComponentModel;
using System.Runtime.InteropServices;

namespace KbLight;

static class Program
{
    // KbLight.exe             tray icon + settings window (a running copy just shows its window)
    // KbLight.exe --tray      tray icon only; used by autostart
    // KbLight.exe --apply     write the saved lighting once and exit
    // KbLight.exe --check     write it only if the keyboard has something else, then exit
    //                         (both exit with 3 and leave the keyboard alone if there is no settings.json yet,
    //                         and with 4 if the only keyboards found have no model file)
    // KbLight.exe --autostart on|off
    [STAThread]
    static int Main(string[] args)
    {
        string mode = args.FirstOrDefault()?.ToLowerInvariant() ?? "";
        switch (mode)
        {
            case "--apply": return ApplyOnce(force: true);
            case "--check": return ApplyOnce(force: false);
            case "--autostart": return SetAutostart(args.ElementAtOrDefault(1));
        }

        using var instance = new Mutex(true, @"Local\KbLight.Instance", out bool first);
        if (!first)
        {
            if (mode != "--tray" && EventWaitHandle.TryOpenExisting(TrayApp.ShowSignalName, out var signal))
            {
                AllowSetForegroundWindow(-1); // ASFW_ANY: let the running copy bring its window to front
                signal.Set();
                signal.Dispose();
            }
            return 0;
        }

        ApplicationConfiguration.Initialize();
        Application.SetColorMode(SystemColorMode.System);
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        using var app = new TrayApp(showSettings: mode != "--tray");
        Application.Run(app);
        app.WaitIdle(TimeSpan.FromSeconds(5));
        return 0;
    }

    static int ApplyOnce(bool force)
    {
        string mode = force ? "--apply" : "--check";
        if (!LightSettings.Exists)
        {
            Log.Write($"{mode}: NoSettings — нет settings.json, подсветку ещё не выбирали");
            return 3;
        }
        var settings = LightSettings.Load();
        var result = Keyboard.Apply(LightState.From(settings), force, settings.GetLastGoodBlock());
        Log.Write($"{mode}: {result.Status} — {result.Message}");
        return result.Status switch
        {
            ApplyStatus.Written or ApplyStatus.AlreadySet => 0,
            ApplyStatus.NotFound => 1,
            ApplyStatus.Unsupported => 4,
            _ => 2,
        };
    }

    static int SetAutostart(string? value)
    {
        try
        {
            if (value == "on") Autostart.Enable(Environment.ProcessPath!);
            else if (value == "off") Autostart.Disable();
            else return 2;
            Log.Write($"--autostart {value}");
            return 0;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or Win32Exception)
        {
            Log.Write($"--autostart {value}: {e.Message}");
            return 1;
        }
    }

    [DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int processId);
}
