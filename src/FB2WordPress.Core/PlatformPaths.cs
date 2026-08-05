namespace FB2WordPress;

/// <summary>Single authority for user-writable application paths on every supported desktop OS.</summary>
public static class PlatformPaths
{
    public static string LocalDataDirectory => Path.Combine(LocalDataRoot(), "FB2WordPress");
    public static string ReportsDirectory => Path.Combine(DocumentsRoot(), "FB2WordPress Reports");

    public static string EnsureLocalDataDirectory()
    {
        Directory.CreateDirectory(LocalDataDirectory);
        return LocalDataDirectory;
    }

    public static string EnsureReportsDirectory()
    {
        Directory.CreateDirectory(ReportsDirectory);
        return ReportsDirectory;
    }

    static string LocalDataRoot()
    {
        var known = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(known)) return known;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS()) return Path.Combine(home, "Library", "Application Support");
        if (OperatingSystem.IsLinux()) return Path.Combine(home, ".local", "share");
        return Path.Combine(home, "AppData", "Local");
    }

    static string DocumentsRoot()
    {
        var known = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(known)) return known;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents");
    }
}
