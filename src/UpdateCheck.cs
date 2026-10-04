using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace KbLight;

static class AppVersion
{
    /// <summary>Major.minor.patch from the assembly, i.e. &lt;Version&gt; in KbLight.csproj without the commit suffix.</summary>
    public static Version Current { get; } = Trim(typeof(AppVersion).Assembly.GetName().Version ?? new Version(0, 0, 0));

    public static string Text => $"KbLight {Current}";

    static Version Trim(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}

sealed record ReleaseInfo(Version Version, string Url);

/// <summary>
/// Asks GitHub for the latest release (spec update-check). Downloads nothing: the tray only tells the user
/// and opens the release page. A fork changes <see cref="Repository"/>.
/// </summary>
static class UpdateCheck
{
    const string Repository = "lostintired/sbarda-kblight";
    const string ReleasesPrefix = "https://github.com/" + Repository + "/";

    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("KbLight", AppVersion.Current.ToString()));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return http;
    }

    /// <returns>the latest release if it is newer than this program, otherwise null</returns>
    /// <exception cref="Exception">network errors, timeouts, non-200 answers and unreadable JSON, for the log</exception>
    public static async Task<ReleaseInfo?> FindNewer()
    {
        using var response = await Http.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"GitHub {(int)response.StatusCode} {response.ReasonPhrase}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string? tag = json.RootElement.GetProperty("tag_name").GetString();
        string? url = json.RootElement.GetProperty("html_url").GetString();

        // A tag in another format is not an error, just not a version we can compare.
        if (ParseTag(tag) is not { } version || version <= AppVersion.Current) return null;
        if (url is null || !url.StartsWith(ReleasesPrefix, StringComparison.OrdinalIgnoreCase))
            throw new FormatException("html_url " + url);
        return new ReleaseInfo(version, url);
    }

    static Version? ParseTag(string? tag)
    {
        string text = tag?.Trim() ?? "";
        if (text.StartsWith('v') || text.StartsWith('V')) text = text[1..];
        return text.Split('.').Length == 3 && Version.TryParse(text, out var v) ? v : null;
    }

    public static void Open(ReleaseInfo release) =>
        Process.Start(new ProcessStartInfo(release.Url) { UseShellExecute = true });
}
