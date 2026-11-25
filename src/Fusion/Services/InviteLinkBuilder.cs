using System.Text;

namespace Fusion.Services;

public static class InviteLinkBuilder
{
    /// <summary>
    /// Administrator permission bit + bot + applications.commands scopes.
    /// </summary>
    public static string Build(ulong clientId) =>
        $"https://discord.com/api/oauth2/authorize?client_id={clientId}&permissions=8&scope=bot%20applications.commands";

    public static bool TryExtractClientIdFromToken(string token, out ulong clientId)
    {
        clientId = 0;
        if (string.IsNullOrWhiteSpace(token)) return false;

        var parts = token.Trim().Split('.');
        if (parts.Length < 1) return false;

        try
        {
            var padded = parts[0].Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }

            var bytes = Convert.FromBase64String(padded);
            var idText = Encoding.UTF8.GetString(bytes);
            return ulong.TryParse(idText, out clientId);
        }
        catch
        {
            return false;
        }
    }
}
