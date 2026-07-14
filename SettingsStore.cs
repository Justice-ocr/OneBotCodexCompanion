using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OneBotCodexCompanion;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;

    public SettingsStore()
    {
        _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OneBot Codex Companion",
            "settings.json");
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(_filePath)) return new AppSettings();
        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions) ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings, string token)
    {
        settings.EncryptedToken = Encrypt(token);
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }

    public static string ReadToken(AppSettings settings) => Decrypt(settings.EncryptedToken);

    private static string Encrypt(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    private static string Decrypt(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException)
        {
            return "";
        }
    }
}
