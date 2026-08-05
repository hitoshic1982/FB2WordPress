using System.Text;

namespace FB2WordPress;

public static class ReportStore
{
    public static string Write(string prefix, string contents, DateTimeOffset? timestamp = null)
    {
        var safePrefix = new string(prefix.Where(character => !Path.GetInvalidFileNameChars().Contains(character)).ToArray());
        if (string.IsNullOrWhiteSpace(safePrefix)) safePrefix = "report";
        var path = Path.Combine(PlatformPaths.EnsureReportsDirectory(), $"{safePrefix}-{(timestamp ?? DateTimeOffset.Now):yyyyMMdd-HHmmss}.txt");
        File.WriteAllText(path, contents, new UTF8Encoding(false));
        return path;
    }
}
