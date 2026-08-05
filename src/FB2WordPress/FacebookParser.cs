using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FB2WordPress;

internal static partial class FacebookParser
{
    [GeneratedRegex(@"(?<![\p{L}\p{N}_])#([\p{L}\p{N}_-]+)")]
    private static partial Regex HashtagRegex();

    public static List<string> ExtractLabels(string text) => HashtagRegex().Matches(text)
        .Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();

    public static List<FacebookPost> Read(string root, Action<string> log, CancellationToken cancellationToken = default)
    {
        var output = new List<FacebookPost>();
        var files = Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
            .Where(p => p.Contains("post", StringComparison.OrdinalIgnoreCase) ||
                        p.Contains("timeline", StringComparison.OrdinalIgnoreCase) ||
                        p.Contains("your_activity", StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan);
                using var doc = JsonDocument.Parse(input);
                // Facebook exports can contain very deeply nested attachment trees.
                // Use an explicit stack so malformed/large exports cannot crash the
                // process with a native stack overflow.
                var nodes = new Stack<JsonElement>();
                nodes.Push(doc.RootElement);
                while (nodes.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var node = nodes.Pop();
                    if (node.ValueKind == JsonValueKind.Object)
                    {
                        TryAdd(node, output);
                        foreach (var property in node.EnumerateObject())
                            if (property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                                nodes.Push(property.Value);
                    }
                    else if (node.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in node.EnumerateArray())
                            if (item.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                                nodes.Push(item);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { log(L.P("略過 {0}：{1}", "跳过 {0}：{1}", "Skipped {0}: {1}", "{0} をスキップ：{1}", Path.GetFileName(file), ex.Message)); }
        }

        return output.GroupBy(x => x.Key).Select(x => x.First()).OrderBy(x => x.Published).ToList();
    }

    static void TryAdd(JsonElement item, List<FacebookPost> output)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("timestamp", out var stamp) || !stamp.TryGetInt64(out var unix)) return;
        var textParts = new List<string>();
        if (item.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            foreach (var d in data.EnumerateArray())
                if (d.ValueKind == JsonValueKind.Object && d.TryGetProperty("post", out var post) && post.ValueKind == JsonValueKind.String)
                    textParts.Add(FixEncoding(post.GetString() ?? ""));

        var media = new List<MediaItem>();
        if (item.TryGetProperty("attachments", out var attachments)) CollectMedia(attachments, media);
        var text = string.Join(Environment.NewLine, textParts).Trim();
        if (text.Length == 0 && media.Count == 0) return;

        var date = DateTimeOffset.FromUnixTimeSeconds(unix);
        var labels = ExtractLabels(text);
        var firstLine = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        var title = string.IsNullOrEmpty(firstLine) ? L.P("Facebook 貼文 {0}", "Facebook 帖子 {0}", "Facebook post {0}", "Facebook 投稿 {0}", date.ToString("yyyy-MM-dd HH:mm")) :
            firstLine.Length <= 90 ? firstLine : firstLine[..90] + "…";
        var key = $"{unix}:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16]}";
        output.Add(new(key, title, text, date, labels, media.DistinctBy(m => m.RelativePath).ToList()));
    }

    static void CollectMedia(JsonElement node, List<MediaItem> media)
    {
        var nodes = new Stack<JsonElement>();
        nodes.Push(node);
        while (nodes.Count > 0)
        {
            var current = nodes.Pop();
            if (current.ValueKind == JsonValueKind.Object)
            {
                if (current.TryGetProperty("uri", out var uri) && uri.ValueKind == JsonValueKind.String)
                {
                    var path = FixEncoding(uri.GetString() ?? "").Replace('/', Path.DirectorySeparatorChar);
                    var ext = Path.GetExtension(path).ToLowerInvariant();
                    var video = ext is ".mp4" or ".mov" or ".m4v" or ".avi" or ".mkv" or ".webm";
                    if (video || ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp") media.Add(new(path, video));
                }
                foreach (var property in current.EnumerateObject())
                    if (property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                        nodes.Push(property.Value);
            }
            else if (current.ValueKind == JsonValueKind.Array)
                foreach (var item in current.EnumerateArray())
                    if (item.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                        nodes.Push(item);
        }
    }

    static string FixEncoding(string value)
    {
        // Older Facebook exports encode UTF-8 bytes as Latin-1 JSON strings.
        try
        {
            if (value.Any(c => c > 255)) return value;
            var decoded = new UTF8Encoding(false, true).GetString(Encoding.Latin1.GetBytes(value));
            var decodedUseful = decoded.Count(c => c >= 0x2E80 || char.IsSurrogate(c));
            var originalUseful = value.Count(c => c >= 0x2E80 || char.IsSurrogate(c));
            var suspicious = value.Count(c => c is >= '\u0080' and <= '\u00FF');
            return decodedUseful > originalUseful || suspicious >= 2 ? decoded : value;
        }
        catch { return value; }
    }
}
