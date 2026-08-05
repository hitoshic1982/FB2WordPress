using System.Security.Cryptography;
using System.Text.Json;

namespace FB2WordPress;

internal static class SettingsStore
{
    static readonly string Folder = PlatformPaths.LocalDataDirectory;
    static readonly string FileName = Path.Combine(Folder, "settings.dat");
    static readonly MigrationStateStore MigrationStore = new(Folder);

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

    public static Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Save(settings);
        return Task.CompletedTask;
    }

    public static string StateFile(string zipPath) => MigrationStore.LegacyStateFile(zipPath);

    public static string DetailedStateFile(string zipPath) => MigrationStore.DetailedStateFile(zipPath);

    public static MigrationState LoadMigration(string zipPath) => MigrationStore.Load(zipPath);

    public static void SaveMigration(string zipPath, MigrationState state) => MigrationStore.Save(zipPath, state);
}
