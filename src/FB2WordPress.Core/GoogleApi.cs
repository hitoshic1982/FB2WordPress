using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace FB2WordPress;

public sealed class GoogleQuotaException(string message) : Exception(message);

// WordPress REST API plus the existing Google OAuth flow used only for YouTube.
public sealed class GoogleApi : IDisposable
{
    const int ScopeVersion = 1;
    const string Scopes = "https://www.googleapis.com/auth/youtube.upload https://www.googleapis.com/auth/youtube.readonly";
    readonly HttpClient http;
    readonly bool ownsHttpClient;
    readonly AppSettings settings;
    readonly Action<string> log;
    readonly Func<AppSettings, CancellationToken, Task> saveSettingsAsync;
    string accessToken = "";
    DateTime tokenExpires;

    public GoogleApi(
        AppSettings settings,
        Action<string> log,
        Func<AppSettings, CancellationToken, Task> saveSettingsAsync,
        HttpClient? httpClient = null)
    {
        this.settings = settings;
        this.log = log;
        this.saveSettingsAsync = saveSettingsAsync;
        http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromHours(4) };
        ownsHttpClient = httpClient is null;
    }

    string Api(string path) => settings.SiteUrl.TrimEnd('/') + "/wp-json/wp/v2/" + path.TrimStart('/');
    AuthenticationHeaderValue WordPressAuth() => new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(settings.WordPressUser + ":" + settings.WordPressAppPassword)));

    async Task<HttpResponseMessage> SendWordPressAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, Api(path)) { Content = content };
        request.Headers.Authorization = WordPressAuth(); request.Headers.UserAgent.ParseAdd("FB2WordPress/1.0");
        return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    public async Task EnsureAuthorizedAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.SiteUrl) || string.IsNullOrWhiteSpace(settings.WordPressUser) || string.IsNullOrWhiteSpace(settings.WordPressAppPassword))
            throw new InvalidOperationException(L.P("尚未設定 WordPress 網站與應用程式密碼。", "尚未设置 WordPress 网站与应用程序密码。", "The WordPress site and application password are not configured.", "WordPress サイトとアプリケーションパスワードが設定されていません。"));
        using var response = await SendWordPressAsync(HttpMethod.Get, "users/me?context=edit", null, ct);
        var body = await response.Content.ReadAsStringAsync(ct); EnsureWordPress(response, body, L.P("連線 WordPress", "连接 WordPress", "Connect to WordPress", "WordPress に接続"));
        settings.BlogName = JsonNode.Parse(body)?["name"]?.GetValue<string>() ?? settings.WordPressUser;
        await saveSettingsAsync(settings, ct);
    }

    public async Task<List<BlogInfo>> GetBlogsAsync(CancellationToken ct)
    {
        await EnsureAuthorizedAsync(ct); return [new("wordpress", settings.BlogName.Length > 0 ? settings.BlogName : settings.SiteUrl)];
    }

    public async Task<string> UploadImageAsync(string path, CancellationToken ct, string? displayName = null)
    {
        await using var file = File.OpenRead(path); using var content = new StreamContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue(Mime(path));
        var originalName = displayName ?? Path.GetFileName(path);
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "\"" + SafeFileName(originalName) + "\"", FileNameStar = originalName };
        using var response = await SendWordPressAsync(HttpMethod.Post, "media", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct); EnsureWordPress(response, body, L.P("上傳圖片", "上传图片", "Upload image", "画像をアップロード"));
        return JsonNode.Parse(body)?["source_url"]?.GetValue<string>() ?? throw new InvalidOperationException(L.P("WordPress 未回傳圖片網址。", "WordPress 未返回图片地址。", "WordPress did not return an image URL.", "WordPress から画像URLが返されませんでした。"));
    }

    public async Task<List<WordPressMediaInfo>> GetMediaImagesAsync(CancellationToken ct)
    {
        var output = new List<WordPressMediaInfo>();
        for (var page = 1; ; page++)
        {
            using var response = await SendWordPressAsync(HttpMethod.Get, $"media?media_type=image&context=edit&per_page=100&page={page}", null, ct);
            if (response.StatusCode == HttpStatusCode.BadRequest && page > 1) break;
            var body = await response.Content.ReadAsStringAsync(ct); EnsureWordPress(response, body, L.P("讀取媒體庫", "读取媒体库", "Read Media Library", "メディアライブラリを読み込み"));
            var items = JsonNode.Parse(body)?.AsArray() ?? []; if (items.Count == 0) break;
            foreach (var item in items)
            {
                var url = item?["source_url"]?.GetValue<string>() ?? "";
                output.Add(new(item?["id"]?.ToString() ?? "", Path.GetFileName(Uri.UnescapeDataString(url)), 0, url));
            }
            if (items.Count < 100) break;
        }
        return output;
    }

    public async Task<string> CreatePostAsync(string blogId, FacebookPost post, string html, bool draft, CancellationToken ct)
    {
        var tagIds = new JsonArray();
        foreach (var label in post.Labels.Distinct(StringComparer.OrdinalIgnoreCase).Take(100)) tagIds.Add(await EnsureTagAsync(label, ct));
        var payload = new JsonObject
        {
            ["title"] = post.Title, ["content"] = html, ["status"] = draft ? "draft" : "publish",
            ["date"] = post.Published.ToString("yyyy-MM-ddTHH:mm:ss"), ["date_gmt"] = post.Published.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss"), ["tags"] = tagIds
        };
        using var content = Json(payload); using var response = await SendWordPressAsync(HttpMethod.Post, "posts", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct); EnsureWordPress(response, body, L.P("建立 WordPress 文章", "创建 WordPress 文章", "Create WordPress post", "WordPress 記事を作成"));
        return JsonNode.Parse(body)?["id"]?.ToString() ?? "";
    }

    async Task<int> EnsureTagAsync(string name, CancellationToken ct)
    {
        using var search = await SendWordPressAsync(HttpMethod.Get, "tags?per_page=100&search=" + Uri.EscapeDataString(name), null, ct);
        var searchBody = await search.Content.ReadAsStringAsync(ct); EnsureWordPress(search, searchBody, L.P("查詢標籤", "查询标签", "Find tag", "タグを検索"));
        foreach (var item in JsonNode.Parse(searchBody)?.AsArray() ?? [])
            if (string.Equals(WebUtility.HtmlDecode(item?["name"]?.GetValue<string>() ?? ""), name, StringComparison.OrdinalIgnoreCase)) return item!["id"]!.GetValue<int>();
        using var content = Json(new JsonObject { ["name"] = name }); using var created = await SendWordPressAsync(HttpMethod.Post, "tags", content, ct);
        var body = await created.Content.ReadAsStringAsync(ct);
        if (!created.IsSuccessStatusCode && JsonNode.Parse(body)?["data"]?["term_id"] is JsonNode existing) return existing.GetValue<int>();
        EnsureWordPress(created, body, L.P("建立標籤", "创建标签", "Create tag", "タグを作成")); return JsonNode.Parse(body)!["id"]!.GetValue<int>();
    }

    public async Task<List<WordPressPostInfo>> GetAllPostsAsync(string blogId, CancellationToken ct)
    {
        var output = new List<WordPressPostInfo>();
        for (var page = 1; ; page++)
        {
            using var response = await SendWordPressAsync(HttpMethod.Get, $"posts?context=edit&status=publish,draft,pending,private,future&per_page=100&page={page}", null, ct);
            if (response.StatusCode == HttpStatusCode.BadRequest && page > 1) break;
            var body = await response.Content.ReadAsStringAsync(ct); EnsureWordPress(response, body, L.P("讀取 WordPress 文章", "读取 WordPress 文章", "Read WordPress posts", "WordPress 記事を読み込み"));
            var items = JsonNode.Parse(body)?.AsArray() ?? []; if (items.Count == 0) break;
            foreach (var item in items)
            {
                var html = item?["content"]?["raw"]?.GetValue<string>() ?? item?["content"]?["rendered"]?.GetValue<string>() ?? "";
                DateTimeOffset.TryParse(item?["date_gmt"]?.GetValue<string>() + "Z", out var published);
                output.Add(new(item?["id"]?.ToString() ?? "", WebUtility.HtmlDecode(item?["title"]?["raw"]?.GetValue<string>() ?? ""), published, ExtractMigrationKey(html), html));
            }
            if (items.Count < 100) break;
        }
        return output;
    }

    public async Task UpdatePostContentAsync(string postId, string html, CancellationToken ct)
    {
        using var content = Json(new JsonObject { ["content"] = html }); using var response = await SendWordPressAsync(HttpMethod.Post, "posts/" + postId, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct); EnsureWordPress(response, body, L.P("更新文章圖片", "更新文章图片", "Update post images", "記事画像を更新"));
    }

    public async Task DeleteMediaAsync(string mediaId, CancellationToken ct)
    {
        using var response = await SendWordPressAsync(HttpMethod.Delete, $"media/{Uri.EscapeDataString(mediaId)}?force=true", null, ct);
        var body = await response.Content.ReadAsStringAsync(ct); EnsureWordPress(response, body, L.P("移除已替換的舊圖片", "移除已替换的旧图片", "Remove replaced image", "置換済みの旧画像を削除"));
    }

    static string ExtractMigrationKey(string html)
    {
        const string prefix = "<!-- FB2WORDPRESS:"; var start = html.IndexOf(prefix, StringComparison.Ordinal); if (start < 0) return "";
        start += prefix.Length; var end = html.IndexOf(" -->", start, StringComparison.Ordinal); return end > start ? WebUtility.HtmlDecode(html[start..end]) : "";
    }

    async Task EnsureYouTubeAuthorizedAsync(CancellationToken ct)
    {
        if (tokenExpires > DateTime.MinValue.AddMinutes(5) && DateTime.UtcNow < tokenExpires.AddMinutes(-2)) return;
        if (!string.IsNullOrWhiteSpace(settings.RefreshToken) && settings.AuthorizedScopeVersion >= ScopeVersion)
        { try { await RefreshAsync(ct); return; } catch { log(L.P("Google 授權已失效，將重新登入一次。", "Google 授权已失效，将重新登录。", "Google authorization expired; signing in again.", "Google の認証が失効したため、再度ログインします。")); } }
        await InteractiveAuthorizeAsync(ct);
    }

    async Task InteractiveAuthorizeAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.ClientId)) throw new InvalidOperationException(L.P("文章包含影片，請先在設定中填入 Google OAuth Desktop Client ID。", "文章包含视频，请先在设置中填写 Google OAuth Desktop Client ID。", "This post contains video. Enter a Google OAuth Desktop Client ID in Settings first.", "記事に動画が含まれています。設定で Google OAuth Desktop Client ID を入力してください。"));
        var verifier = Base64(RandomNumberGenerator.GetBytes(48)); var challenge = Base64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))); var state = Base64(RandomNumberGenerator.GetBytes(24));
        var port = FreePort(); var redirect = $"http://127.0.0.1:{port}/"; using var listener = new HttpListener(); listener.Prefixes.Add(redirect); listener.Start();
        var url = "https://accounts.google.com/o/oauth2/v2/auth?" + $"client_id={Uri.EscapeDataString(settings.ClientId)}&redirect_uri={Uri.EscapeDataString(redirect)}&response_type=code&scope={Uri.EscapeDataString(Scopes)}&access_type=offline&prompt=consent&code_challenge={challenge}&code_challenge_method=S256&state={state}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); log(L.P("請在瀏覽器完成一次 Google／YouTube 授權。", "请在浏览器完成 Google／YouTube 授权。", "Complete Google/YouTube authorization in your browser.", "ブラウザーで Google／YouTube の認証を完了してください。"));
        var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromMinutes(10), ct); var code = context.Request.QueryString["code"]; var returnedState = context.Request.QueryString["state"];
        var reply = Encoding.UTF8.GetBytes($"<html><meta charset='utf-8'><body style='font-family:sans-serif;padding:40px'><h2>{WebUtility.HtmlEncode(L.P("授權完成", "授权完成", "Authorization complete", "認証完了"))}</h2><p>{WebUtility.HtmlEncode(L.P("可以關閉此頁並回到 FB2WordPress。", "可以关闭此页面并返回 FB2WordPress。", "You may close this page and return to FB2WordPress.", "このページを閉じて FB2WordPress に戻れます。"))}</p></body></html>");
        context.Response.ContentType = "text/html; charset=utf-8"; context.Response.ContentLength64 = reply.Length; await context.Response.OutputStream.WriteAsync(reply, ct); context.Response.Close();
        if (string.IsNullOrEmpty(code) || returnedState != state) throw new InvalidOperationException(L.P("Google 授權驗證失敗。", "Google 授权验证失败。", "Google authorization validation failed.", "Google 認証の検証に失敗しました。"));
        var form = new Dictionary<string, string> { ["client_id"] = settings.ClientId, ["code"] = code, ["code_verifier"] = verifier, ["redirect_uri"] = redirect, ["grant_type"] = "authorization_code" };
        if (settings.ClientSecret.Length > 0) form["client_secret"] = settings.ClientSecret; var json = await PostTokenAsync(form, ct); ApplyToken(json);
        settings.RefreshToken = json["refresh_token"]?.GetValue<string>() ?? ""; settings.AuthorizedScopeVersion = ScopeVersion; await saveSettingsAsync(settings, ct);
    }

    async Task RefreshAsync(CancellationToken ct)
    {
        var form = new Dictionary<string, string> { ["client_id"] = settings.ClientId, ["refresh_token"] = settings.RefreshToken, ["grant_type"] = "refresh_token" };
        if (settings.ClientSecret.Length > 0) form["client_secret"] = settings.ClientSecret; ApplyToken(await PostTokenAsync(form, ct));
    }
    async Task<JsonNode> PostTokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(form); using var response = await http.PostAsync("https://oauth2.googleapis.com/token", content, ct); var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(L.P("Google 授權失敗：{0}", "Google 授权失败：{0}", "Google authorization failed: {0}", "Google 認証に失敗しました：{0}", FriendlyError(body))); return JsonNode.Parse(body)!;
    }
    void ApplyToken(JsonNode json) { accessToken = json["access_token"]?.GetValue<string>() ?? throw new InvalidOperationException(L.P("Google 未回傳存取權杖。", "Google 未返回访问令牌。", "Google did not return an access token.", "Google からアクセストークンが返されませんでした。")); tokenExpires = DateTime.UtcNow.AddSeconds(json["expires_in"]?.GetValue<int>() ?? 3500); }
    async Task<HttpResponseMessage> SendGoogleAsync(HttpMethod method, string url, HttpContent? content, CancellationToken ct)
    {
        await EnsureYouTubeAuthorizedAsync(ct); using var request = new HttpRequestMessage(method, url) { Content = content }; request.Headers.Authorization = new("Bearer", accessToken); return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    public async Task<string> UploadVideoAsync(string path, string title, string description, string privacy, CancellationToken ct)
    {
        var metadata = new JsonObject { ["snippet"] = new JsonObject { ["title"] = title, ["description"] = description }, ["status"] = new JsonObject { ["privacyStatus"] = privacy, ["selfDeclaredMadeForKids"] = false } };
        using var initContent = Json(metadata); using var init = await SendGoogleAsync(HttpMethod.Post, "https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable&part=snippet,status", initContent, ct);
        var initBody = await init.Content.ReadAsStringAsync(ct); EnsureGoogle(init, initBody, L.P("啟動 YouTube 上傳", "启动 YouTube 上传", "Start YouTube upload", "YouTube アップロードを開始")); var uploadUrl = init.Headers.Location ?? throw new InvalidOperationException(L.P("YouTube 未回傳上傳網址。", "YouTube 未返回上传地址。", "YouTube did not return an upload URL.", "YouTube からアップロードURLが返されませんでした。"));
        await using var file = File.OpenRead(path); using var video = new StreamContent(file); video.Headers.ContentType = new("application/octet-stream");
        using var response = await SendGoogleAsync(HttpMethod.Put, uploadUrl.ToString(), video, ct); var body = await response.Content.ReadAsStringAsync(ct); EnsureGoogle(response, body, L.P("上傳 YouTube 影片", "上传 YouTube 视频", "Upload YouTube video", "YouTube 動画をアップロード"));
        return JsonNode.Parse(body)?["id"]?.GetValue<string>() ?? throw new InvalidOperationException(L.P("YouTube 未回傳影片 ID。", "YouTube 未返回视频 ID。", "YouTube did not return a video ID.", "YouTube から動画IDが返されませんでした。"));
    }

    public async Task<List<YouTubeVideoInfo>> GetUploadedVideosAsync(CancellationToken ct)
    {
        using var channel = await SendGoogleAsync(HttpMethod.Get, "https://www.googleapis.com/youtube/v3/channels?part=contentDetails&mine=true", null, ct); var cb = await channel.Content.ReadAsStringAsync(ct); EnsureGoogle(channel, cb, L.P("讀取 YouTube 頻道", "读取 YouTube 频道", "Read YouTube channel", "YouTube チャンネルを読み込み"));
        var playlist = JsonNode.Parse(cb)?["items"]?[0]?["contentDetails"]?["relatedPlaylists"]?["uploads"]?.GetValue<string>(); if (string.IsNullOrEmpty(playlist)) return [];
        var ids = new List<string>(); string token = "";
        do { var url = $"https://www.googleapis.com/youtube/v3/playlistItems?part=snippet&maxResults=50&playlistId={Uri.EscapeDataString(playlist)}" + (token.Length > 0 ? "&pageToken=" + Uri.EscapeDataString(token) : ""); using var r = await SendGoogleAsync(HttpMethod.Get, url, null, ct); var b = await r.Content.ReadAsStringAsync(ct); EnsureGoogle(r, b, L.P("讀取 YouTube 影片", "读取 YouTube 视频", "Read YouTube videos", "YouTube 動画を読み込み")); var root = JsonNode.Parse(b); token = root?["nextPageToken"]?.GetValue<string>() ?? ""; foreach (var x in root?["items"]?.AsArray() ?? []) { var id = x?["snippet"]?["resourceId"]?["videoId"]?.GetValue<string>() ?? ""; if (id.Length > 0) ids.Add(id); } } while (token.Length > 0);
        var output = new List<YouTubeVideoInfo>();
        foreach (var batch in ids.Chunk(50)) { using var r = await SendGoogleAsync(HttpMethod.Get, "https://www.googleapis.com/youtube/v3/videos?part=snippet,fileDetails&id=" + string.Join(',', batch), null, ct); var b = await r.Content.ReadAsStringAsync(ct); EnsureGoogle(r, b, L.P("讀取 YouTube 影片資料", "读取 YouTube 视频数据", "Read YouTube video details", "YouTube 動画情報を読み込み")); foreach (var x in JsonNode.Parse(b)?["items"]?.AsArray() ?? []) { _ = long.TryParse(x?["fileDetails"]?["fileSize"]?.ToString().Trim('"'), out var size); output.Add(new(x?["id"]?.GetValue<string>() ?? "", x?["snippet"]?["title"]?.GetValue<string>() ?? "", x?["snippet"]?["description"]?.GetValue<string>() ?? "", x?["fileDetails"]?["fileName"]?.GetValue<string>() ?? "", size)); } }
        return output;
    }

    static StringContent Json(JsonNode value) => new(value.ToJsonString(), Encoding.UTF8, "application/json");
    static void EnsureWordPress(HttpResponseMessage response, string body, string action) { if (!response.IsSuccessStatusCode) throw new InvalidOperationException(L.P("{0}失敗：{1}", "{0}失败：{1}", "{0} failed: {1}", "{0}に失敗しました：{1}", action, FriendlyError(body))); }
    static void EnsureGoogle(HttpResponseMessage response, string body, string action) { if (!response.IsSuccessStatusCode) { var msg = FriendlyError(body); if (response.StatusCode == HttpStatusCode.TooManyRequests || msg.Contains("quota", StringComparison.OrdinalIgnoreCase)) throw new GoogleQuotaException(L.P("{0}遇到 Google 配額限制：{1}", "{0}遇到 Google 配额限制：{1}", "{0} hit a Google quota limit: {1}", "{0}で Google の割り当て制限に達しました：{1}", action, msg)); throw new InvalidOperationException(L.P("{0}失敗：{1}", "{0}失败：{1}", "{0} failed: {1}", "{0}に失敗しました：{1}", action, msg)); } }
    static string FriendlyError(string body) { try { return JsonNode.Parse(body)?["message"]?.GetValue<string>() ?? JsonNode.Parse(body)?["error"]?["message"]?.GetValue<string>() ?? body; } catch { return body.Length > 500 ? body[..500] : body; } }
    static string Mime(string path) => Path.GetExtension(path).ToLowerInvariant() switch { ".png" => "image/png", ".gif" => "image/gif", ".webp" => "image/webp", ".bmp" => "image/bmp", _ => "image/jpeg" };
    static string SafeFileName(string value)
    {
        var ext = Path.GetExtension(value); var stem = Path.GetFileNameWithoutExtension(value);
        var ascii = new string(stem.Select(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' ? c : '-').ToArray()).Trim('-');
        return (ascii.Length > 0 ? ascii : "image-" + Guid.NewGuid().ToString("N")[..8]) + (ext.Length is > 1 and < 8 ? ext.ToLowerInvariant() : ".jpg");
    }
    static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    static int FreePort() { using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port; }
    public void Dispose()
    {
        if (ownsHttpClient) http.Dispose();
    }
}
