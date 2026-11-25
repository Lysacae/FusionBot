using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fusion.Services;

public sealed class StoredSecrets
{
    public string DiscordToken { get; set; } = string.Empty;
    public string GeminiApiKey { get; set; } = string.Empty;
}

public sealed class SecretStore
{
    private readonly string _path;

    public SecretStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Fusion");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "secrets.dat");
    }

    public StoredSecrets Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new StoredSecrets();

            var protectedBytes = File.ReadAllBytes(_path);
            var plain = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(plain);
            return JsonSerializer.Deserialize<StoredSecrets>(json) ?? new StoredSecrets();
        }
        catch
        {
            return new StoredSecrets();
        }
    }

    public void Save(StoredSecrets secrets)
    {
        var json = JsonSerializer.Serialize(new StoredSecrets
        {
            DiscordToken = secrets.DiscordToken?.Trim() ?? string.Empty,
            GeminiApiKey = secrets.GeminiApiKey?.Trim() ?? string.Empty
        });
        var plain = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, protectedBytes);
    }
}
