using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DriveSyncPortable;

internal sealed class SyncRecord
{
    public string RemoteId { get; set; } = "";
    public string RemoteVersion { get; set; } = "";
    public string LocalVersion { get; set; } = "";
}

internal sealed class AppSettings
{
    public string ClientId { get; set; } = "";
    public string ProtectedClientSecret { get; set; } = "";
    public string ProtectedToken { get; set; } = "";
    public string GoogleEmail { get; set; } = "";
    public string LocalFolder { get; set; } = "";
    public string RemoteFolderId { get; set; } = "";
    public string RemoteFolderName { get; set; } = "";
    public string ChangeToken { get; set; } = "";
    public bool MirrorDeletes { get; set; } = false;
    public bool MappingApproved { get; set; } = false;
    public Dictionary<string, SyncRecord> Records { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class OAuthToken
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
}

internal sealed class SettingsStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DriveSyncPortable-v1-CurrentUser");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static string BaseFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DriveSyncPortable");
    public string StorageFolder { get; }
    private string SettingsPath => Path.Combine(StorageFolder, "settings.json");
    public AppSettings Data { get; private set; }

    public SettingsStore(string? storageFolder = null)
    {
        StorageFolder = storageFolder ?? BaseFolder;
        Directory.CreateDirectory(StorageFolder);
        try
        {
            Data = File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions) ?? new AppSettings()
                : new AppSettings();
            Data.Records = new Dictionary<string, SyncRecord>(Data.Records ?? new(), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("Không thể đọc cấu hình. Không được tiếp tục đồng bộ để tránh ghi nhầm dữ liệu.", ex);
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(StorageFolder);
        var temp = SettingsPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Data, JsonOptions), Encoding.UTF8);
        File.Move(temp, SettingsPath, true);
    }

    private static string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        byte[] raw = Encoding.UTF8.GetBytes(plain);
        try
        {
            byte[] encrypted = ProtectedData.Protect(raw, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }
        finally { CryptographicOperations.ZeroMemory(raw); }
    }

    private static string Unprotect(string encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted)) return "";
        byte[] buffer = ProtectedData.Unprotect(Convert.FromBase64String(encrypted), Entropy, DataProtectionScope.CurrentUser);
        try { return Encoding.UTF8.GetString(buffer); }
        finally { CryptographicOperations.ZeroMemory(buffer); }
    }

    public string GetClientSecret() => Unprotect(Data.ProtectedClientSecret);
    public void SetClientSecret(string secret) => Data.ProtectedClientSecret = Protect(secret);
    public OAuthToken? GetToken()
    {
        var plain = Unprotect(Data.ProtectedToken);
        return string.IsNullOrEmpty(plain) ? null : JsonSerializer.Deserialize<OAuthToken>(plain, JsonOptions);
    }
    public void SetToken(OAuthToken token)
    {
        Data.ProtectedToken = Protect(JsonSerializer.Serialize(token, JsonOptions));
        Save();
    }
    public void Logout()
    {
        Data.ProtectedToken = "";
        Data.GoogleEmail = "";
        Data.ChangeToken = "";
        Data.RemoteFolderId = "";
        Data.RemoteFolderName = "";
        Data.Records.Clear();
        Save();
    }
}
