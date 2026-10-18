using System.Globalization;
using System.Runtime.InteropServices;

namespace KbLight;

static class LocalClock
{
    // Windows возвращает текущий часовой пояс без кэша TimeZoneInfo процесса.
    public static DateTime Now
    {
        get
        {
            GetLocalTime(out var time);
            return new DateTime(time.Year, time.Month, time.Day, time.Hour,
                time.Minute, time.Second, time.Milliseconds, DateTimeKind.Local);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SystemTime
    {
        public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds;
    }

    [DllImport("kernel32.dll")]
    static extern void GetLocalTime(out SystemTime time);
}

sealed record QuietHoursSettings
{
    public bool Enabled { get; init; }
    public string? Start { get; init; } = "23:00";
    public string? End { get; init; } = "09:00";
}

readonly record struct QuietHoursState(bool Active, bool IsValid, bool SameTime, TimeOnly End)
{
    public static bool TryTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    public static QuietHoursState At(QuietHoursSettings? settings, DateTime localTime)
    {
        settings ??= new();
        if (!TryTime(settings.Start, out var start) || !TryTime(settings.End, out var end))
            return new(false, false, false, default);
        var now = TimeOnly.FromDateTime(localTime);
        bool inside = start < end ? now >= start && now < end : now >= start || now < end;
        return new(settings.Enabled && start != end && inside, true, start == end, end);
    }
}

readonly record struct LightingPolicy(bool KeysOff, bool BoxOff, QuietHoursState Schedule)
{
    public static LightingPolicy At(LightSettings settings, DateTime localTime)
    {
        var schedule = QuietHoursState.At(settings.QuietHours, localTime);
        return new(!settings.Enabled || schedule.Active, !settings.LightBoxEnabled || schedule.Active, schedule);
    }
}
