using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

var failures = new List<string>();
void Check(bool condition, string name) { Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}"); if (!condition) failures.Add(name); }

static List<(int Start, int End)> LocalizedCallRanges(string source)
{
    var ranges = new List<(int, int)>();
    var searchFrom = 0;
    while (true)
    {
        var start = source.IndexOf("L.P(", searchFrom, StringComparison.Ordinal);
        if (start < 0) return ranges;
        var end = MatchingCallEnd(source, start + 3);
        if (end < 0) return ranges;
        ranges.Add((start, end));
        searchFrom = end;
    }
}

static int MatchingCallEnd(string source, int openParenthesis)
{
    var depth = 0;
    var inString = false;
    var verbatimString = false;
    var inCharacter = false;
    var lineComment = false;
    var blockComment = false;
    for (var i = openParenthesis; i < source.Length; i++)
    {
        var current = source[i];
        var next = i + 1 < source.Length ? source[i + 1] : '\0';
        if (lineComment)
        {
            if (current == '\n') lineComment = false;
            continue;
        }
        if (blockComment)
        {
            if (current == '*' && next == '/') { blockComment = false; i++; }
            continue;
        }
        if (inString)
        {
            if (verbatimString && current == '"' && next == '"') { i++; continue; }
            if (!verbatimString && current == '\\') { i++; continue; }
            if (current == '"') { inString = false; verbatimString = false; }
            continue;
        }
        if (inCharacter)
        {
            if (current == '\\') { i++; continue; }
            if (current == '\'') inCharacter = false;
            continue;
        }
        if (current == '/' && next == '/') { lineComment = true; i++; continue; }
        if (current == '/' && next == '*') { blockComment = true; i++; continue; }
        if (current == '"') { inString = true; verbatimString = i > 0 && source[i - 1] == '@'; continue; }
        if (current == '\'') { inCharacter = true; continue; }
        if (current == '(') depth++;
        else if (current == ')' && --depth == 0) return i + 1;
    }
    return -1;
}

var assembly = Assembly.Load("FB2WordPress");

foreach (var readmeName in new[] { "README.md", "README.zh-CN.md", "README.en.md", "README.ja.md" })
{
    var readme = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), readmeName));
    Check(readme.Contains("actions/workflows/ci.yml/badge.svg", StringComparison.Ordinal), $"{readmeName} shows real Windows CI status");
    Check(readme.Contains("img.shields.io/github/v/release/hitoshic1982/FB2WordPress", StringComparison.Ordinal), $"{readmeName} shows the latest release");
    Check(readme.Contains("license-MIT-blue.svg", StringComparison.Ordinal), $"{readmeName} shows the MIT license");
    Check(readme.Contains("https://buymeacoffee.com/flameblade_studio", StringComparison.Ordinal) && readme.Contains("https://www.paypal.com/paypalme/flamebladestudio", StringComparison.OrdinalIgnoreCase), $"{readmeName} includes both voluntary support links");
    Check(!readme.Contains("\n+<p align=\"center\">", StringComparison.Ordinal), $"{readmeName} has no stray patch marker");
}

// Localization: verify the public language contract and every translated value.
var localizer = assembly.GetType("FB2WordPress.L", true)!;
var supportedCodes = ((IEnumerable)localizer.GetProperty("SupportedCodes", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Cast<string>().ToArray();
Check(supportedCodes.SequenceEqual(new[] { "zh-TW", "zh-CN", "en", "ja" }), "Four interface languages are available in the intended order");
var localizationKeys = ((IEnumerable)localizer.GetProperty("Keys", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Cast<string>().ToArray();
Check(localizationKeys.Length >= 45, "Localization covers setup and WordPress-specific tools");
var configureLanguage = localizer.GetMethod("Configure", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
var translate = localizer.GetMethod("T", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
var phrase = localizer.GetMethod("P", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
var sampleTranslations = new[] { "traditional", "simplified", "english", "japanese" };
foreach (var code in supportedCodes)
{
    configureLanguage.Invoke(null, new object?[] { code });
    Check(localizationKeys.All(key => !string.Equals((string)translate.Invoke(null, new object[] { key, Array.Empty<object>() })!, key, StringComparison.Ordinal)), $"Every localization key resolves in {code}");
    var expected = sampleTranslations[Array.IndexOf(supportedCodes, code)];
    Check((string)phrase.Invoke(null, new object[] { sampleTranslations[0], sampleTranslations[1], sampleTranslations[2], sampleTranslations[3], Array.Empty<object>() })! == expected, $"Inline four-language phrase selects {code}");
}
configureLanguage.Invoke(null, new object?[] { "unsupported" });

// All East Asian production string literals must either be in the central
// catalog or inside an explicit four-language L.P(...) call.
var sourceRoot = Path.Combine(Directory.GetCurrentDirectory(), "src", "FB2WordPress");
var eastAsianLiteral = new Regex(@"""(?:\\.|[^""\\])*[\u3040-\u30ff\u3400-\u9fff](?:\\.|[^""\\])*""", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
var hardcodedUserText = new List<string>();
foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories).Where(path => !path.EndsWith("Localization.cs", StringComparison.OrdinalIgnoreCase)))
{
    var source = File.ReadAllText(file);
    var ranges = LocalizedCallRanges(source);
    foreach (Match match in eastAsianLiteral.Matches(source))
    {
        if (ranges.Any(range => match.Index >= range.Start && match.Index < range.End)) continue;
        var line = source.AsSpan(0, match.Index).Count('\n') + 1;
        hardcodedUserText.Add($"{Path.GetRelativePath(Directory.GetCurrentDirectory(), file)}:{line}");
    }
}
if (hardcodedUserText.Count > 0) Console.Error.WriteLine("Unlocalized user-facing text: " + string.Join(", ", hardcodedUserText));
Check(hardcodedUserText.Count == 0, "No production user-facing East Asian string bypasses four-language localization");

var root = Path.Combine(Path.GetTempPath(), "FB2WordPress-Audit-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    // Parser: modern UTF-8, legacy UTF-8-as-Latin1, emoji, labels, media, ordering.
    var export = Path.Combine(root, "export"); Directory.CreateDirectory(export);
    var legacyText = Encoding.Latin1.GetString(Encoding.UTF8.GetBytes("舊版中文 😀 #舊標籤"));
    var posts = new object[] {
        new { timestamp = 1700000002L, data = new[] { new { post = legacyText } } },
        new { timestamp = 1700000001L, data = new[] { new { post = "繁體中文 😀 #標籤" } }, attachments = new[] { new { data = new[] { new { media = new { uri = "media/photo.jpg" } } } } } }
    };
    File.WriteAllText(Path.Combine(export, "your_posts_1.json"), JsonSerializer.Serialize(posts), new UTF8Encoding(false));
    var parser = assembly.GetType("FB2WordPress.FacebookParser", true)!;
    var read = parser.GetMethod("Read", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
    var parsed = ((IEnumerable)read.Invoke(null, new object?[] { export, (Action<string>)(_ => { }), CancellationToken.None })!).Cast<object>().ToList();
    Check(parsed.Count == 2, "Facebook JSON parses two posts");
    var textProperty = parsed[0].GetType().GetProperty("Text")!;
    var labelsProperty = parsed[0].GetType().GetProperty("Labels")!;
    var mediaProperty = parsed[0].GetType().GetProperty("Media")!;
    Check((string)textProperty.GetValue(parsed[0])! == "繁體中文 😀 #標籤", "Modern Chinese and emoji preserved");
    Check(((IEnumerable)labelsProperty.GetValue(parsed[0])!).Cast<object>().Any(x => x.ToString() == "標籤"), "Hashtag becomes label");
    Check(((IEnumerable)mediaProperty.GetValue(parsed[0])!).Cast<object>().Count() == 1, "Image attachment detected");
    Check((string)textProperty.GetValue(parsed[1])! == "舊版中文 😀 #舊標籤", "Legacy mojibake repaired");

    // Safe ZIP extraction and zip-slip rejection.
    var mainForm = assembly.GetType("FB2WordPress.MainForm", true)!;
    var extract = mainForm.GetMethod("SafeExtract", BindingFlags.Static | BindingFlags.NonPublic)!;
    var normalZip = Path.Combine(root, "normal.zip");
    using (var z = ZipFile.Open(normalZip, ZipArchiveMode.Create)) { var e = z.CreateEntry("folder/ok.txt"); using var w = new StreamWriter(e.Open()); w.Write("ok"); }
    var normalOut = Path.Combine(root, "normal-out"); extract.Invoke(null, new object[] { normalZip, normalOut, CancellationToken.None });
    Check(File.ReadAllText(Path.Combine(normalOut, "folder", "ok.txt")) == "ok", "Normal ZIP extracts safely");
    var evilZip = Path.Combine(root, "evil.zip");
    using (var z = ZipFile.Open(evilZip, ZipArchiveMode.Create)) { var e = z.CreateEntry("../escape.txt"); using var w = new StreamWriter(e.Open()); w.Write("bad"); }
    var blocked = false;
    try { extract.Invoke(null, new object[] { evilZip, Path.Combine(root, "evil-out"), CancellationToken.None }); }
    catch (TargetInvocationException e) when (e.InnerException is InvalidDataException) { blocked = true; }
    Check(blocked && !File.Exists(Path.Combine(root, "escape.txt")), "ZIP path traversal blocked");
    var cancelled = false; using (var source = new CancellationTokenSource()) { source.Cancel(); try { extract.Invoke(null, new object[] { normalZip, Path.Combine(root, "cancel-out"), source.Token }); } catch (TargetInvocationException e) when (e.InnerException is OperationCanceledException) { cancelled = true; } }
    Check(cancelled, "ZIP extraction honors pause/cancellation");

    // New-article safety helpers: media type, cache invalidation, private paths,
    // YouTube description limit, and WordPress hidden identity marker.
    var isVideo = mainForm.GetMethod("IsVideoPath", BindingFlags.Static | BindingFlags.NonPublic)!;
    Check((bool)isVideo.Invoke(null, new object[] { "clip.MP4" })! && !(bool)isVideo.Invoke(null, new object[] { "photo.jpg" })!, "New article distinguishes video from image");
    var cacheFile = Path.Combine(root, "cache.jpg"); File.WriteAllBytes(cacheFile, [1, 2, 3]);
    var cacheMethod = mainForm.GetMethod("ComposerCacheKey", BindingFlags.Static | BindingFlags.NonPublic)!;
    var cache1 = (string)cacheMethod.Invoke(null, new object[] { cacheFile })!; File.AppendAllText(cacheFile, "changed");
    var cache2 = (string)cacheMethod.Invoke(null, new object[] { cacheFile })!;
    Check(cache1 != cache2, "Changed media invalidates upload cache");
    var postType = assembly.GetType("FB2WordPress.FacebookPost", true)!; var mediaType = assembly.GetType("FB2WordPress.MediaItem", true)!;
    var media = Activator.CreateInstance(mediaType, "private-video.mp4", true)!;
    var labelList = Activator.CreateInstance(typeof(List<>).MakeGenericType(typeof(string)))!;
    var mediaListType = typeof(List<>).MakeGenericType(mediaType); var emptyMediaList = Activator.CreateInstance(mediaListType)!;
    var post = Activator.CreateInstance(postType, "manual-test", "Title", new string('文', 6000), DateTimeOffset.Now, labelList, emptyMediaList)!;
    var description = (string)mainForm.GetMethod("YouTubeDescription", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new[] { post, media })!;
    Check(description.Length <= 5000 && !description.Contains(root, StringComparison.OrdinalIgnoreCase), "YouTube description is bounded and does not expose local path");
    var api = assembly.GetType("FB2WordPress.GoogleApi", true)!;
    var marker = (string)api.GetMethod("ExtractMigrationKey", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { "x<!-- FB2WORDPRESS:manual-test -->y" })!;
    Check(marker == "manual-test", "WordPress hidden identity marker round-trips");
    var normalizeText = mainForm.GetMethod("NormalizePlainTextBlankLines", BindingFlags.Static | BindingFlags.NonPublic)!;
    var normalizeHtml = mainForm.GetMethod("NormalizeFacebookHtmlBlankLines", BindingFlags.Static | BindingFlags.NonPublic)!;
    var spacedText = "第一段\n\n\n\n第二段\n第三段";
    var compactText = (string)normalizeText.Invoke(null, new object[] { spacedText })!;
    Check(compactText == "第一段\n\n第二段\n第三段", "Excess blank lines collapse to exactly one blank line");
    var originalHtml = "<!-- FB2WORDPRESS:test --><div style=\"white-space:pre-wrap\">第一段\n\n\n\n第二段</div><p><img src=\"photo.jpg\"></p><iframe src=\"https://www.youtube.com/embed/test\"></iframe>";
    var compactHtml = (string)normalizeHtml.Invoke(null, new object[] { originalHtml })!;
    Check(compactHtml.Contains("第一段\n\n第二段", StringComparison.Ordinal), "Imported Facebook text block is normalized");
    Check(compactHtml.Contains("<img src=\"photo.jpg\">", StringComparison.Ordinal) && compactHtml.Contains("youtube.com/embed/test", StringComparison.Ordinal), "Images and videos remain unchanged during whitespace cleanup");
    var wordpressRewrittenHtml = "<div class=\"imported\" style=\"overflow-wrap: break-word; white-space: pre-wrap;\">甲\r\n\r\n\r\n\r\n乙</div>";
    var compactRewrittenHtml = (string)normalizeHtml.Invoke(null, new object[] { wordpressRewrittenHtml })!;
    Check(compactRewrittenHtml.Contains("甲\n\n乙", StringComparison.Ordinal), "WordPress-rewritten pre-wrap style with spaces and semicolon is detected");
    Check((string)normalizeHtml.Invoke(null, new object[] { compactHtml })! == compactHtml, "Whitespace cleanup is idempotent and safe to resume");
    Check((string)normalizeHtml.Invoke(null, new object[] { "<p>一般 WordPress 內容\n\n\n保持不變</p>" })! == "<p>一般 WordPress 內容\n\n\n保持不變</p>", "Non-FB2WordPress content is not changed");
    var settingsType = assembly.GetType("FB2WordPress.AppSettings", true)!;
    using (var apiInstance = (IDisposable)Activator.CreateInstance(api, Activator.CreateInstance(settingsType)!, (Action<string>)(_ => { }))!)
    {
        Exception? authError = null;
        try { await (Task)api.GetMethod("EnsureYouTubeAuthorizedAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(apiInstance, new object[] { CancellationToken.None })!; }
        catch (Exception ex) { authError = ex; }
        Check(authError is InvalidOperationException && authError is not ArgumentOutOfRangeException, "First YouTube authorization never overflows DateTime");
    }

    // Smart image optimization: source integrity, resize/size reduction and
    // lossless/animation-safe format preservation.
    var optimizer = assembly.GetType("FB2WordPress.ImageOptimizer", true)!;
    var prepare = optimizer.GetMethod("Prepare", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
    var largeJpeg = Path.Combine(root, "large.jpg");
    using (var bitmap = new Bitmap(4000, 3000))
    {
        using var g = Graphics.FromImage(bitmap); g.Clear(Color.CornflowerBlue); var random = new Random(42);
        for (var i = 0; i < 8000; i++) using (var brush = new SolidBrush(Color.FromArgb(random.Next(256), random.Next(256), random.Next(256)))) g.FillEllipse(brush, random.Next(4000), random.Next(3000), random.Next(10, 180), random.Next(10, 180));
        bitmap.Save(largeJpeg, ImageFormat.Jpeg);
    }
    var originalHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(largeJpeg)));
    using (var optimized = (IDisposable)prepare.Invoke(null, new object[] { largeJpeg })!)
    {
        var optimizedType = optimized.GetType(); var optimizedPath = (string)optimizedType.GetProperty("Path")!.GetValue(optimized)!;
        using var image = Image.FromFile(optimizedPath);
        Check(Math.Max(image.Width, image.Height) <= 2560, "Large JPEG is resized for web reading");
        Check(new FileInfo(optimizedPath).Length < new FileInfo(largeJpeg).Length, "Large JPEG uses less WordPress storage");
    }
    Check(File.Exists(largeJpeg) && Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(largeJpeg))) == originalHash, "Original image is never modified");
    var png = Path.Combine(root, "text.png"); using (var bitmap = new Bitmap(800, 400)) bitmap.Save(png, ImageFormat.Png);
    using (var preserved = (IDisposable)prepare.Invoke(null, new object[] { png })!) Check((string)preserved.GetType().GetProperty("Path")!.GetValue(preserved)! == png, "PNG is preserved without lossy recompression");
    // Migration state atomic save/load and backup recovery.
    var store = assembly.GetType("FB2WordPress.SettingsStore", true)!;
    var stateType = assembly.GetType("FB2WordPress.MigrationState", true)!;
    var state = Activator.CreateInstance(stateType)!;
    var fakeZip = Path.Combine(root, "identity.zip"); File.WriteAllText(fakeZip, "x");
    store.GetMethod("SaveMigration", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(null, new[] { fakeZip, state });
    var loaded = store.GetMethod("LoadMigration", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(null, new[] { fakeZip });
    Check(loaded is not null, "Migration state round-trips");
    var statePath = (string)store.GetMethod("DetailedStateFile", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(null, new[] { fakeZip })!;
    store.GetMethod("SaveMigration", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(null, new[] { fakeZip, state });
    File.WriteAllText(statePath, "corrupt");
    var recovered = store.GetMethod("LoadMigration", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(null, new[] { fakeZip });
    Check(recovered is not null, "Corrupt migration state recovers from backup");
    foreach (var suffix in new[] { "", ".bak", ".tmp" }) try { File.Delete(statePath + suffix); } catch { }
}
finally { try { Directory.Delete(root, true); } catch { } }

if (failures.Count > 0) { Console.Error.WriteLine("FAILED: " + string.Join(", ", failures)); return 1; }
Console.WriteLine("ALL AUDIT TESTS PASSED");
return 0;
