using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace Fusion.Services;

public sealed class LatestUpdate
{
    public string Version { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public string Filename { get; init; } = string.Empty;
}

public sealed class UpdateService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FusionBot/" + AppVersion.Current);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public async Task<LatestUpdate?> GetLatestAsync(CancellationToken ct = default)
    {
        var url = $"https://api.github.com/repos/{AppVersion.GitHubOwner}/{AppVersion.GitHubRepo}/releases/latest";
        using var resp = await Http.GetAsync(url, ct).ConfigureAwait(false);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = doc.RootElement;

        var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
        var version = tag.Trim().TrimStart('v', 'V');
        var notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? string.Empty : string.Empty;

        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        JsonElement? chosen = null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? string.Empty;
            if (name.Equals("Fusion.exe", StringComparison.OrdinalIgnoreCase))
            {
                chosen = asset;
                break;
            }
            if (chosen is null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                chosen = asset;
        }

        if (chosen is null || string.IsNullOrWhiteSpace(version))
            return null;

        var downloadUrl = chosen.Value.GetProperty("browser_download_url").GetString() ?? string.Empty;
        var filename = chosen.Value.GetProperty("name").GetString() ?? "Fusion.exe";
        if (string.IsNullOrWhiteSpace(downloadUrl))
            return null;

        return new LatestUpdate
        {
            Version = version,
            Notes = notes,
            DownloadUrl = downloadUrl,
            Filename = filename
        };
    }

    public async Task<LatestUpdate?> CheckForAvailableUpdateAsync(CancellationToken ct = default)
    {
        var latest = await GetLatestAsync(ct).ConfigureAwait(false);
        if (latest is null) return null;
        return AppVersion.IsNewer(latest.Version, AppVersion.Current) ? latest : null;
    }

    public async Task DownloadAndInstallAsync(LatestUpdate update, CancellationToken ct = default)
    {
        var bytes = await Http.GetByteArrayAsync(update.DownloadUrl, ct).ConfigureAwait(false);
        if (bytes.Length < 1024)
            throw new InvalidOperationException("Fichier de mise a jour incomplet.");

        var temp = Path.Combine(Path.GetTempPath(), "Fusion-update.exe");
        await File.WriteAllBytesAsync(temp, bytes, ct).ConfigureAwait(false);

        var current = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Impossible de localiser Fusion.exe.");

        var pid = Environment.ProcessId;
        var scriptPath = Path.Combine(Path.GetTempPath(), $"fusion-update-{pid}.ps1");
        var script =
            $$"""
            $targetPid = {{pid}}
            while (Get-Process -Id $targetPid -ErrorAction SilentlyContinue) {
              Start-Sleep -Milliseconds 300
            }
            Start-Sleep -Seconds 1
            Copy-Item -LiteralPath '{{Escape(temp)}}' -Destination '{{Escape(current)}}' -Force
            Start-Process -FilePath '{{Escape(current)}}'
            Remove-Item -LiteralPath '{{Escape(scriptPath)}}' -Force -ErrorAction SilentlyContinue
            """;
        await File.WriteAllTextAsync(scriptPath, script, ct).ConfigureAwait(false);

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private static string Escape(string path) => path.Replace("'", "''");
}
