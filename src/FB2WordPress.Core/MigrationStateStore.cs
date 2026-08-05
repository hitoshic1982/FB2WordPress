using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FB2WordPress;

/// <summary>Cross-platform, recoverable migration progress persistence.</summary>
public sealed class MigrationStateStore
{
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    readonly string folder;

    public MigrationStateStore(string? folder = null) => this.folder = folder ?? PlatformPaths.LocalDataDirectory;

    public string LegacyStateFile(string sourcePath)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(sourcePath))));
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, $"completed-{key}.txt");
    }

    public string DetailedStateFile(string sourcePath) => Path.ChangeExtension(LegacyStateFile(sourcePath), ".json");

    public MigrationState Load(string sourcePath)
    {
        try
        {
            var path = DetailedStateFile(sourcePath);
            if (!File.Exists(path)) return new();
            try { return JsonSerializer.Deserialize<MigrationState>(File.ReadAllText(path)) ?? new(); }
            catch (JsonException) when (File.Exists(path + ".bak"))
            {
                return JsonSerializer.Deserialize<MigrationState>(File.ReadAllText(path + ".bak")) ?? new();
            }
        }
        catch { return new(); }
    }

    public void Save(string sourcePath, MigrationState state)
    {
        Directory.CreateDirectory(folder);
        var path = DetailedStateFile(sourcePath);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, JsonOptions));
        if (!File.Exists(path))
        {
            File.Move(temp, path);
            return;
        }

        try { File.Replace(temp, path, path + ".bak", true); }
        catch (PlatformNotSupportedException) { PortableReplace(temp, path); }
        catch (IOException) { PortableReplace(temp, path); }
    }

    static void PortableReplace(string temp, string path)
    {
        File.Copy(path, path + ".bak", true);
        File.Move(temp, path, true);
    }
}
