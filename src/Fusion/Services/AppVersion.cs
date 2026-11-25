namespace Fusion.Services;

public static class AppVersion
{
    public const string Current = "1.1.0";
    public const string GitHubOwner = "Lysacae";
    public const string GitHubRepo = "FusionBot";
    public const string GitHubProfileUrl = "https://github.com/Lysacae";
    public const string DiscordUsername = "lysacae";

    public static int Compare(string a, string b)
    {
        var pa = Parse(a);
        var pb = Parse(b);
        for (var i = 0; i < 3; i++)
        {
            var d = pa[i] - pb[i];
            if (d != 0) return d;
        }
        return 0;
    }

    public static bool IsNewer(string latest, string current) =>
        Compare(latest, current) > 0;

    private static int[] Parse(string version)
    {
        var clean = version.Trim().TrimStart('v', 'V');
        var parts = clean.Split('.');
        var result = new int[3];
        for (var i = 0; i < 3; i++)
            result[i] = i < parts.Length && int.TryParse(parts[i], out var n) ? n : 0;
        return result;
    }
}
