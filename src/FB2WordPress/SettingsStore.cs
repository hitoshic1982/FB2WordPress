using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FB2WordPress;

internal static class SettingsStore
{
    static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FB2WordPress");
    static readonly string FileName = Path.Combine(Folder, "settings.dat");
    static readonly JsonSerializerOptions StateJsonOptions = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FileName)) return ImportYouTubeSettings();
            var protectedBytes = File.ReadAllBytes(FileName);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<AppSettings>(bytes) ?? new();
        }
        catch { return new(); }
    }

    static AppSettings ImportYouTubeSettings()
    {
        try
        {
            var oldFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FB2Blogger", "settings.dat");
            if (!File.Exists(oldFile)) return new();
            var bytes = ProtectedData.Unprotect(File.ReadAllBytes(oldFile), null, DataProtectionScope.CurrentUser);
            var old = JsonSerializer.Deserialize<AppSettings>(bytes) ?? new();
            // Only reusable Google/YouTube credentials are imported. Blogger destination and history remain isolated.
            return new AppSettings { ClientId = old.ClientId, ClientSecret = old.ClientSecret, RefreshToken = old.RefreshToken, AuthorizedScopeVersion = old.AuthorizedScopeVersion, VideoPrivacy = old.VideoPrivacy };
        }
        catch { return new(); }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Folder);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(settings);
        var temp = FileName + ".tmp";
        File.WriteAllBytes(temp, ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
        File.Move(temp, FileName, true);
    }

    public static string StateFile(string zipPath)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(zipPath))));
        Directory.CreateDirectory(Folder);
        return Path.Combine(Folder, $"completed-{key}.txt");
    }

    public static string DetailedStateFile(string zipPath) => Path.ChangeExtension(StateFile(zipPath), ".json");

    public static MigrationState LoadMigration(string zipPath)
    {
        try
        {
            var path = DetailedStateFile(zipPath);
            if (!File.Exists(path)) return new();
            try { return JsonSerializer.Deserialize<MigrationState>(File.ReadAllText(path)) ?? new(); }
            catch (JsonException) when (File.Exists(path + ".bak"))
            {
                return JsonSerializer.Deserialize<MigrationState>(File.ReadAllText(path + ".bak")) ?? new();
            }
        }
        catch { return new(); }
    }

    public static void SaveMigration(string zipPath, MigrationState state)
    {
        Directory.CreateDirectory(Folder);
        var path = DetailedStateFile(zipPath);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, StateJsonOptions));
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak", true);
        else File.Move(temp, path);
    }
}
