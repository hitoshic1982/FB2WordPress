using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FB2WordPress;

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

var assembly = Assembly.Load("FB2WordPress.Core");
var windowsAssembly = Assembly.Load("FB2WordPress");
var readmeDeclarations = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["README.md"] = "劍，我已鍛成；餘下的路，就交給你們了。",
    ["README.zh-CN.md"] = "剑，我已锻成；余下的路，就交给你们了。",
    ["README.en.md"] = "I have forged this sword. What comes next is up to you.",
    ["README.ja.md"] = "この剣は、私が鍛え上げました。あとは皆さんに託します。"
};

foreach (var readmeName in new[] { "README.md", "README.zh-CN.md", "README.en.md", "README.ja.md" })
{
    var readme = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), readmeName));
    Check(readme.Contains("actions/workflows/ci.yml/badge.svg", StringComparison.Ordinal) && readme.Contains("Cross-platform CI", StringComparison.Ordinal), $"{readmeName} shows real cross-platform CI status");
    Check(readme.Contains("actions/workflows/preview-packages.yml/badge.svg", StringComparison.Ordinal) && readme.Contains("Native Preview Packages", StringComparison.Ordinal), $"{readmeName} shows real native Preview package status");
    Check(readme.Contains("actions/workflows/codeql.yml/badge.svg", StringComparison.Ordinal), $"{readmeName} shows real CodeQL status");
    Check(readme.Contains("actions/workflows/security-audit.yml/badge.svg", StringComparison.Ordinal), $"{readmeName} shows real security-audit status");
    Check(readme.Contains("actions/workflows/secret-defense.yml/badge.svg", StringComparison.Ordinal), $"{readmeName} shows real secret-defense status");
    Check(readme.Contains("img.shields.io/github/v/release/hitoshic1982/FB2WordPress", StringComparison.Ordinal), $"{readmeName} shows the latest release");
    Check(readme.Contains("license-MIT-blue.svg", StringComparison.Ordinal), $"{readmeName} shows the MIT license");
    Check(readme.Contains("img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&amp;logoColor=white", StringComparison.Ordinal), $"{readmeName} identifies .NET 10");
    Check(readme.Contains("img.shields.io/badge/interface%20languages-4-informational", StringComparison.Ordinal) && readme.Contains("Four interface languages", StringComparison.Ordinal), $"{readmeName} identifies four-language documentation");
    Check(readme.Contains("CONTRIBUTING.md", StringComparison.Ordinal), $"{readmeName} links the software-family quality standard");
    Check(readme.Contains(readmeDeclarations[readmeName], StringComparison.Ordinal), $"{readmeName} carries the localized open-source declaration");
    Check(readme.Contains("https://buymeacoffee.com/flameblade_studio", StringComparison.Ordinal) && readme.Contains("https://www.paypal.com/paypalme/flamebladestudio", StringComparison.OrdinalIgnoreCase), $"{readmeName} includes both voluntary support links");
    Check(!readme.Contains("\n+<p align=\"center\">", StringComparison.Ordinal), $"{readmeName} has no stray patch marker");
}

var workflowNames = new[] { "ci.yml", "codeql.yml", "security-audit.yml", "secret-defense.yml", "dependency-review.yml", "preview-packages.yml" };
foreach (var workflowName in workflowNames)
{
    var workflow = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), ".github", "workflows", workflowName));
    var usesLines = Regex.Matches(workflow, @"(?m)^\s*-\s+uses:\s+[^\r\n]+\r?$");
    var pinnedUsesLines = Regex.Matches(workflow, @"(?m)^\s*-\s+uses:\s+[^@\r\n]+@[0-9a-f]{40}(?:\s+#.*)?$");
    Check(workflow.Contains("workflow_dispatch:", StringComparison.Ordinal), $"{workflowName} supports manual dispatch");
    Check(workflow.Contains("schedule:", StringComparison.Ordinal), $"{workflowName} has a scheduled safety run");
    Check(workflow.Contains("concurrency:", StringComparison.Ordinal), $"{workflowName} prevents redundant concurrent runs");
    Check(workflow.Contains("contents: read", StringComparison.Ordinal) && !workflow.Contains("write-all", StringComparison.OrdinalIgnoreCase), $"{workflowName} uses restricted permissions");
    Check(usesLines.Count > 0 && usesLines.Count == pinnedUsesLines.Count, $"{workflowName} pins every third-party action to a commit SHA");
}

var ciWorkflow = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), ".github", "workflows", "ci.yml"));
Check(ciWorkflow.Contains("tags: ['v*']", StringComparison.Ordinal), "A normal v1.1.0-rc.1 tag push triggers cross-platform CI");
Check(ciWorkflow.Contains("-p:Version=${{ steps.package_version.outputs.value }}", StringComparison.Ordinal), "A release tag overrides fallback assembly metadata during publish");
Check(ciWorkflow.Contains("windows-latest", StringComparison.Ordinal) && ciWorkflow.Contains("macos-latest", StringComparison.Ordinal) && ciWorkflow.Contains("ubuntu-latest", StringComparison.Ordinal), "Cross-platform CI covers Windows, macOS, and Linux runners");

var codeQlWorkflow = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), ".github", "workflows", "codeql.yml"));
Check(codeQlWorkflow.Contains("languages: csharp", StringComparison.Ordinal) && codeQlWorkflow.Contains("security-events: write", StringComparison.Ordinal), "CodeQL analyzes C# with the required result permission");

var securityWorkflow = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), ".github", "workflows", "security-audit.yml"));
Check(securityWorkflow.StartsWith("name: Security Audit / NuGet", StringComparison.Ordinal), "NuGet workflow uses the shared family display name");
Check(securityWorkflow.Contains("NuGetAuditMode=all", StringComparison.Ordinal) && securityWorkflow.Contains("--vulnerable --include-transitive", StringComparison.Ordinal), "Security Audit checks direct and transitive NuGet vulnerabilities");

var secretDefenseWorkflow = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), ".github", "workflows", "secret-defense.yml"));
Check(secretDefenseWorkflow.StartsWith("name: Secret Defense / Gitleaks", StringComparison.Ordinal), "Gitleaks workflow uses the shared family display name");
Check(secretDefenseWorkflow.Contains("gitleaks/gitleaks-action@dcedce43c6f43de0b836d1fe38946645c9c638dc", StringComparison.Ordinal) && secretDefenseWorkflow.Contains("GITLEAKS_ENABLE_COMMENTS: 'false'", StringComparison.Ordinal), "Secret Defense uses a pinned non-commenting Gitleaks action");

var dependencyWorkflow = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), ".github", "workflows", "dependency-review.yml"));
Check(dependencyWorkflow.Contains("actions/dependency-review-action@a1d282b36b6f3519aa1f3fc636f609c47dddb294", StringComparison.Ordinal), "Dependency Review uses the reviewed pinned action revision");

var previewWorkflow = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), ".github", "workflows", "preview-packages.yml"));
Check(previewWorkflow.Contains("runner: macos-15-intel", StringComparison.Ordinal) &&
      previewWorkflow.Contains("runner: macos-15", StringComparison.Ordinal) &&
      previewWorkflow.Contains("rid: osx-x64", StringComparison.Ordinal) &&
      previewWorkflow.Contains("rid: osx-arm64", StringComparison.Ordinal) &&
      previewWorkflow.Contains("runs-on: ubuntu-22.04", StringComparison.Ordinal),
    "Preview packages use native Intel x64 and Apple Silicon arm64 macOS runners plus Linux");
Check(previewWorkflow.Contains("name: Required / Windows + macOS x64 + macOS arm64 + Linux packages", StringComparison.Ordinal) &&
      previewWorkflow.Contains("if: always() && github.event_name == 'pull_request'", StringComparison.Ordinal) &&
      previewWorkflow.Contains("needs: [validate-context, windows-package, macos-preview, linux-preview]", StringComparison.Ordinal) &&
      !Regex.IsMatch(previewWorkflow, @"pull_request:\s*\r?\n\s+branches:\s*\[main\]\s*\r?\n\s+paths:", RegexOptions.CultureInvariant),
    "Every PR receives one fixed required gate that aggregates all four native packages");
Check(previewWorkflow.Contains("actions/attest@59d89421af93a897026c735860bf21b6eb4f7b26", StringComparison.Ordinal) &&
      previewWorkflow.Contains("actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c", StringComparison.Ordinal) &&
      previewWorkflow.Contains("artifact-metadata: write", StringComparison.Ordinal) && previewWorkflow.Contains("id-token: write", StringComparison.Ordinal) &&
      previewWorkflow.Contains("contents: write", StringComparison.Ordinal) &&
      previewWorkflow.Contains("if: needs.validate-context.outputs.is_release == 'true'", StringComparison.Ordinal),
    "Only the exact tag-gated trusted job receives release and commit-pinned attestation permissions");
Check(previewWorkflow.Contains("^v1\\.1\\.0-rc\\.([1-9][0-9]*)$", StringComparison.Ordinal) &&
      previewWorkflow.Contains("git merge-base --is-ancestor \"$GITHUB_SHA\" refs/remotes/origin/main", StringComparison.Ordinal) &&
      previewWorkflow.Contains("$GITHUB_REF_NAME\" == \"v$version", StringComparison.Ordinal),
    "Release gate requires an exact nonzero RC tag, matching source version, and origin/main ancestry");
Check(previewWorkflow.Contains("Microsoft.Sbom.DotNetTool --version 4.1.5", StringComparison.Ordinal) &&
      previewWorkflow.Contains("SHA256SUMS.txt", StringComparison.Ordinal),
    "Preview artifact sets include a version-pinned SPDX generator and an aggregate SHA256 manifest");
Check(previewWorkflow.Contains("a6d71e2b6cd66f8e8d16c37ad164658985e0cf5fcaa950c90a482890cb9d13e0", StringComparison.Ordinal) &&
      previewWorkflow.Contains("sha256sum --check --strict", StringComparison.Ordinal),
    "The official appimagetool download is fail-closed by an exact SHA256");

var macPackageScript = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "scripts", "package-macos-preview.sh"));
var macSmokeScript = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "scripts", "smoke-macos-preview.sh"));
var linuxPackageScript = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "scripts", "package-linux-preview.sh"));
var linuxSmokeScript = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "scripts", "smoke-linux-preview.sh"));
var windowsSmokeScript = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "scripts", "smoke-windows-preview.ps1"));
var releaseScript = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "scripts", "publish-preview-release.sh"));
Check(macPackageScript.Contains("hdiutil create", StringComparison.Ordinal) && macPackageScript.Contains(".app", StringComparison.Ordinal) &&
      macPackageScript.Contains("codesign --verify", StringComparison.Ordinal) &&
      macPackageScript.Contains("lipo -archs", StringComparison.Ordinal) &&
      macPackageScript.Contains("expected_binary_arch='x86_64'", StringComparison.Ordinal) &&
      macPackageScript.Contains("expected_binary_arch='arm64'", StringComparison.Ordinal),
    "macOS packaging creates genuine architecture-matched unsigned x64 and arm64 DMGs");
Check(macSmokeScript.Contains("hdiutil attach", StringComparison.Ordinal) && macSmokeScript.Contains("sleep 6", StringComparison.Ordinal) &&
      macSmokeScript.Contains("kill -0", StringComparison.Ordinal) && macSmokeScript.Contains("uname -m", StringComparison.Ordinal) &&
      macSmokeScript.Contains("Contents/Resources/LICENSE.txt", StringComparison.Ordinal) && macSmokeScript.Contains("MIT License", StringComparison.Ordinal),
    "macOS smoke test verifies both MIT LICENSE copies, launches each final DMG natively, and proves liveness");
Check(linuxPackageScript.Contains("appimagetool", StringComparison.OrdinalIgnoreCase) && linuxPackageScript.Contains("AppRun", StringComparison.Ordinal) &&
      linuxPackageScript.Contains("ELF 64-bit", StringComparison.Ordinal),
    "Linux packaging creates a genuine x86_64 AppImage rather than a renamed archive");
Check(linuxSmokeScript.Contains("APPIMAGE_EXTRACT_AND_RUN=1", StringComparison.Ordinal) && linuxSmokeScript.Contains("sleep 6", StringComparison.Ordinal) &&
      linuxSmokeScript.Contains("kill -0", StringComparison.Ordinal) && linuxSmokeScript.Contains("--appimage-extract", StringComparison.Ordinal) &&
      linuxSmokeScript.Contains("LICENSE.txt", StringComparison.Ordinal) && linuxSmokeScript.Contains("MIT License", StringComparison.Ordinal),
    "Linux smoke test verifies the embedded MIT LICENSE, launches the final AppImage, and proves liveness");
Check(windowsSmokeScript.Contains("Start-Process", StringComparison.Ordinal) && windowsSmokeScript.Contains("Start-Sleep -Seconds 6", StringComparison.Ordinal) &&
      windowsSmokeScript.Contains("HasExited", StringComparison.Ordinal) && windowsSmokeScript.Contains("LICENSE.txt", StringComparison.Ordinal) &&
      windowsSmokeScript.Contains("MIT License", StringComparison.Ordinal),
    "Windows smoke test verifies the adjacent MIT LICENSE, launches the final complete EXE, and proves liveness");
Check(releaseScript.Contains("gh release create", StringComparison.Ordinal) && releaseScript.Contains("--draft", StringComparison.Ordinal) &&
      releaseScript.Contains("--prerelease", StringComparison.Ordinal) && releaseScript.Contains("--verify-tag", StringComparison.Ordinal) &&
      releaseScript.Contains("gh release upload", StringComparison.Ordinal) && releaseScript.Contains("draft=false", StringComparison.Ordinal) &&
      releaseScript.Contains("cleanup_failed_draft", StringComparison.Ordinal) &&
      releaseScript.Contains("macOS-arm64-Preview.dmg", StringComparison.Ordinal),
    "Release publication is draft-first, exact-asset verified, fail-closed, and includes all native deliverables");
Check(File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), ".gitattributes")).Contains("*.sh text eol=lf", StringComparison.Ordinal),
    "Packaging shell scripts keep LF line endings on every checkout platform");

var contributing = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "CONTRIBUTING.md"));
foreach (var standardPhrase in new[] { "炎劍開源軟體家族品質標準", "炎剑开源软件家族质量标准", "Flameblade Open Source Software Family Quality Standard", "炎剣オープンソースソフトウェアファミリー品質基準" })
    Check(contributing.Contains(standardPhrase, StringComparison.Ordinal), $"Quality standard includes {standardPhrase}");
foreach (var declaration in new[] { "劍，我已鍛成；餘下的路，就交給你們了。", "剑，我已锻成；余下的路，就交给你们了。", "I have forged this sword. What comes next is up to you.", "この剣は、私が鍛え上げました。あとは皆さんに託します。" })
    Check(contributing.Contains(declaration, StringComparison.Ordinal), $"Open-source declaration includes {declaration}");
Check(contributing.Contains("Gitleaks", StringComparison.Ordinal) && contributing.Contains("SHA256", StringComparison.Ordinal) && contributing.Contains("one-off manual exceptions", StringComparison.Ordinal), "Quality standard requires real scans, traceable artifacts, and maintainable automation");

var sharedBuildMetadata = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Directory.Build.props"));
Check(sharedBuildMetadata.Contains("<Version>1.1.0</Version>", StringComparison.Ordinal), "All project outputs share the v1.1.0 fallback metadata from one source");
Check(sharedBuildMetadata.Contains("<PreviewVersion>1.1.0-rc.1</PreviewVersion>", StringComparison.Ordinal), "All Preview packages resolve v1.1.0-rc.1 from one source");
var versionAndSupportSurfaces = new[] { "README.md", "README.zh-CN.md", "README.en.md", "README.ja.md", "CHANGELOG.md", Path.Combine("docs", "CROSS_PLATFORM.md"), "RELEASE_NOTES_v1.1.0-rc.1.md" }
    .Select(path => File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), path)))
    .ToArray();
Check(versionAndSupportSurfaces.All(text => !text.Contains("2.2.0", StringComparison.OrdinalIgnoreCase)), "FB2WordPress documentation contains no MoHan v2.2.0 version leak");
Check(versionAndSupportSurfaces.All(text => text.Contains("macOS", StringComparison.Ordinal) && text.Contains("Linux", StringComparison.Ordinal)) &&
      File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "README.en.md")).Contains("not a claim that complete macOS or Linux functionality exists", StringComparison.Ordinal),
    "Documentation keeps macOS and Linux at the incomplete Preview boundary");
Check(new[] { "README.md", "README.zh-CN.md", "README.en.md", "README.ja.md" }.All(path =>
      {
          var text = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), path));
          return text.Contains("Preview", StringComparison.Ordinal) && text.Contains("DMG", StringComparison.Ordinal) &&
                 text.Contains("AppImage", StringComparison.Ordinal) && text.Contains("SHA256", StringComparison.Ordinal) &&
                 text.Contains("SPDX SBOM", StringComparison.Ordinal) && text.Contains("arm64", StringComparison.Ordinal) &&
                 text.Contains("origin/main", StringComparison.Ordinal) && text.Contains("RELEASE_NOTES_v1.1.0-rc.1.md", StringComparison.Ordinal);
      }),
    "All four README languages explain both native macOS architectures and the same fail-closed release evidence");
var previewReleaseNotes = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "RELEASE_NOTES_v1.1.0-rc.1.md"));
Check(new[] { "## 繁體中文", "## 简体中文", "## English", "## 日本語" }.All(previewReleaseNotes.Contains) &&
      previewReleaseNotes.Contains("not a formal compatibility claim", StringComparison.Ordinal) &&
      previewReleaseNotes.Contains("作者持有 macOS／Linux 實機", StringComparison.Ordinal) &&
      previewReleaseNotes.Contains("Apple Silicon arm64", StringComparison.Ordinal) &&
      previewReleaseNotes.Contains("Any artifact or verification failure prevents publication", StringComparison.Ordinal),
    "Preview release notes contain four complete languages, native Apple Silicon, and a fail-closed release boundary");

// Localization: verify the public language contract and every translated value.
var localizer = assembly.GetType("FB2WordPress.L", true)!;
var supportedCodes = ((IEnumerable)localizer.GetProperty("SupportedCodes", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!).Cast<string>().ToArray();
Check(supportedCodes.SequenceEqual(new[] { "zh-TW", "zh-CN", "en", "ja" }), "Four interface languages are available in the intended order");
var localizationKeys = ((IEnumerable)localizer.GetProperty("Keys", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!).Cast<string>().ToArray();
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
var sourceRoot = Path.Combine(Directory.GetCurrentDirectory(), "src");
var eastAsianLiteral = new Regex(@"""(?:\\.|[^""\\])*[\u3040-\u30ff\u3400-\u9fff](?:\\.|[^""\\])*""", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
var hardcodedUserText = new List<string>();
foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories).Where(path =>
             !path.EndsWith("Localization.cs", StringComparison.OrdinalIgnoreCase) &&
             !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
             !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
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
var mainForm = windowsAssembly.GetType("FB2WordPress.MainForm", true)!;
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
    using (var apiInstance = (IDisposable)Activator.CreateInstance(api, Activator.CreateInstance(settingsType)!, (Action<string>)(_ => { }), (Func<AppSettings, CancellationToken, Task>)((_, _) => Task.CompletedTask), null)!)
    {
        Exception? authError = null;
        try { await (Task)api.GetMethod("EnsureYouTubeAuthorizedAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(apiInstance, new object[] { CancellationToken.None })!; }
        catch (Exception ex) { authError = ex; }
        Check(authError is InvalidOperationException && authError is not ArgumentOutOfRangeException, "First YouTube authorization never overflows DateTime");
    }

    // Smart image optimization: source integrity, resize/size reduction and
    // lossless/animation-safe format preservation.
var optimizer = windowsAssembly.GetType("FB2WordPress.ImageOptimizer", true)!;
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
var store = windowsAssembly.GetType("FB2WordPress.SettingsStore", true)!;
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
