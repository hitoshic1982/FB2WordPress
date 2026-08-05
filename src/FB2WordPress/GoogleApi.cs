using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace FB2WordPress;

internal sealed class GoogleQuotaException(string message) : Exception(message);

// WordPress REST API plus the existing Google OAuth flow used only for YouTube.
internal sealed class GoogleApi : IDisposable
{
    const int ScopeVersion = 1;
    const string Scopes = "https://www.googleapis.com/auth/youtube.upload https://www.googleapis.com/auth/youtube.readonly";
    readonly HttpClient http = new() { Timeout = TimeSpan.FromHours(4) };
    readonly AppSettings settings;
    readonly Action<string> log;
    string accessToken = "";
    DateTime tokenExpires;

    public GoogleApi(AppSettings settings, Action<string> log) { this.settings = settings; this.log = log; }

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
            throw new InvalidOperationException("尚未設定 WordPress 網站與應用程式密碼。");
        using var response = await SendWordPressAsync(HttpMethod.Get, "users/me?context=edit", null, ct);
        var body = await response.Content.ReadAsStringAsync(ct); EnsureWordPress(response, body, "連線 WordPress");
        settings.BlogName = JsonNode.Parse(body)?["name"]?.GetValue<string>() ?? settings.WordPressUser;
        SettingsStore.Save(settings);
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
        var body = await response.Content.ReadAsStringAsync(ct); EnsureWordPress(response, body, "上傳圖片");
        return JsonNode.Parse(body)?["source_url"]?.GetValue<string>() ?? throw new InvalidOperationException("WordPress 未回傳圖片網址。");
    }

    public async Task<List<WordPressMediaInfo>> GetMediaImagesAsync(CancellationToken ct)
    {
        var output = new List<WordPressMediaInfo>();
        for (var page = 1; ; page++)
        {
            using var response = await SendWordPressAsync(HttpMethod.Get, $"media?media_type=image&context=edit&per_page=100&page={page}", null, ct);
            if (response.StatusCode == HttpStatusCode.BadRequest && page > 1) break;
            var body = await response.Content.ReadAsStringAsync(ct); EnsureWordPress(response, body, "讀取媒體庫");
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
        var body = await response.Content.ReadAsStringAsync(ct); EnsureWordPress(response, body, "建立 WordPress 文章");
        return JsonNode.Parse(body)?["id"]?.ToString() ?? "";
    }

    async Task<int> EnsureTagAsync(string name, CancellationToken ct)
    {
        using var search = await SendWordPressAsync(HttpMethod.Get, "tags?per_page=100&search=" + Uri.EscapeDataString(name), null, ct);
        var searchBody = await search.Content.ReadAsStringAsync(ct); EnsureWordPress(search, searchBody, "查詢標籤");
        foreach (var item in JsonNode.Parse(searchBody)?.AsArray() ?? [])
            if (string.Equals(WebUtility.HtmlDecode(item?["name"]?.GetValue<string>() ?? ""), name, StringComparison.OrdinalIgnoreCase)) return item!["id"]!.GetValue<int>();
        using var content = Json(new JsonObject { ["name"] = name }); using var created = await SendWordPressAsync(HttpMethod.Post, "tags", content, ct);
        var body = await created.Content.ReadAsStringAsync(ct);
        if (!created.IsSuccessStatusCode && JsonNode.Parse(body)?["data"]?["term_id"] is JsonNode existing) return existing.GetValue<int>();
        EnsureWordPress(created, body, "建立標籤"); return JsonNode.Parse(body)!["id"]!.GetValue<int>();
    }

    public async Task<List<WordPressPostInfo>> GetAllPostsAsync(string blogId, CancellationToken ct)
    {
        var output = new List<WordPressPostInfo>();
        for (var page = 1; ; page++)
        {
            using var response = await SendWordPressAsync(HttpMethod.Get, $"posts?context=edit&status=publish,draft,pending,private,future&per_page=100&page={page}", null, ct);
            if (response.StatusCode == HttpStatusCode.BadRequest && page > 1) break;
            var body = await response.Content.ReadAsStringAsync(ct); EnsureWordPress(response, body, "讀取 WordPress 文章");
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
        var body = await response.Content.ReadAsStringAsync(ct); EnsureWordPress(response, body, "更新文章圖片");
    }

    public async Task DeleteMediaAsync(string mediaId, CancellationToken ct)
    {
        using var response = await SendWordPressAsync(HttpMethod.Delete, $"media/{Uri.EscapeDataString(mediaId)}?force=true", null, ct);
        var body = await response.Content.ReadAsStringAsync(ct); EnsureWordPress(response, body, "移除已替換的舊圖片");
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
        { try { await RefreshAsync(ct); return; } catch { log("Google 授權已失效，將重新登入一次。"); } }
        await InteractiveAuthorizeAsync(ct);
    }

    async Task InteractiveAuthorizeAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.ClientId)) throw new InvalidOperationException("文章包含影片，請先在設定中填入 Google OAuth Desktop Client ID。");
        var verifier = Base64(RandomNumberGenerator.GetBytes(48)); var challenge = Base64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))); var state = Base64(RandomNumberGenerator.GetBytes(24));
        var port = FreePort(); var redirect = $"http://127.0.0.1:{port}/"; using var listener = new HttpListener(); listener.Prefixes.Add(redirect); listener.Start();
        var url = "https://accounts.google.com/o/oauth2/v2/auth?" + $"client_id={Uri.EscapeDataString(settings.ClientId)}&redirect_uri={Uri.EscapeDataString(redirect)}&response_type=code&scope={Uri.EscapeDataString(Scopes)}&access_type=offline&prompt=consent&code_challenge={challenge}&code_challenge_method=S256&state={state}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); log("請在瀏覽器完成一次 Google／YouTube 授權。");
        var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromMinutes(10), ct); var code = context.Request.QueryString["code"]; var returnedState = context.Request.QueryString["state"];
        var reply = Encoding.UTF8.GetBytes("<html><meta charset='utf-8'><body style='font-family:sans-serif;padding:40px'><h2>授權完成</h2><p>可以關閉此頁並回到 FB2WordPress。</p></body></html>");
        context.Response.ContentType = "text/html; charset=utf-8"; context.Response.ContentLength64 = reply.Length; await context.Response.OutputStream.WriteAsync(reply, ct); context.Response.Close();
        if (string.IsNullOrEmpty(code) || returnedState != state) throw new InvalidOperationException("Google 授權驗證失敗。");
        var form = new Dictionary<string, string> { ["client_id"] = settings.ClientId, ["code"] = code, ["code_verifier"] = verifier, ["redirect_uri"] = redirect, ["grant_type"] = "authorization_code" };
        if (settings.ClientSecret.Length > 0) form["client_secret"] = settings.ClientSecret; var json = await PostTokenAsync(form, ct); ApplyToken(json);
        settings.RefreshToken = json["refresh_token"]?.GetValue<string>() ?? ""; settings.AuthorizedScopeVersion = ScopeVersion; SettingsStore.Save(settings);
    }

    async Task RefreshAsync(CancellationToken ct)
    {
        var form = new Dictionary<string, string> { ["client_id"] = settings.ClientId, ["refresh_token"] = settings.RefreshToken, ["grant_type"] = "refresh_token" };
        if (settings.ClientSecret.Length > 0) form["client_secret"] = settings.ClientSecret; ApplyToken(await PostTokenAsync(form, ct));
    }
    async Task<JsonNode> PostTokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(form); using var response = await http.PostAsync("https://oauth2.googleapis.com/token", content, ct); var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Google 授權失敗：" + FriendlyError(body)); return JsonNode.Parse(body)!;
    }
    void ApplyToken(JsonNode json) { accessToken = json["access_token"]?.GetValue<string>() ?? throw new InvalidOperationException("Google 未回傳存取權杖。"); tokenExpires = DateTime.UtcNow.AddSeconds(json["expires_in"]?.GetValue<int>() ?? 3500); }
    async Task<HttpResponseMessage> SendGoogleAsync(HttpMethod method, string url, HttpContent? content, CancellationToken ct)
    {
        await EnsureYouTubeAuthorizedAsync(ct); using var request = new HttpRequestMessage(method, url) { Content = content }; request.Headers.Authorization = new("Bearer", accessToken); return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    public async Task<string> UploadVideoAsync(string path, string title, string description, string privacy, CancellationToken ct)
    {
        var metadata = new JsonObject { ["snippet"] = new JsonObject { ["title"] = title, ["description"] = description }, ["status"] = new JsonObject { ["privacyStatus"] = privacy, ["selfDeclaredMadeForKids"] = false } };
        using var initContent = Json(metadata); using var init = await SendGoogleAsync(HttpMethod.Post, "https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable&part=snippet,status", initContent, ct);
        var initBody = await init.Content.ReadAsStringAsync(ct); EnsureGoogle(init, initBody, "啟動 YouTube 上傳"); var uploadUrl = init.Headers.Location ?? throw new InvalidOperationException("YouTube 未回傳上傳網址。");
        await using var file = File.OpenRead(path); using var video = new StreamContent(file); video.Headers.ContentType = new("application/octet-stream");
        using var response = await SendGoogleAsync(HttpMethod.Put, uploadUrl.ToString(), video, ct); var body = await response.Content.ReadAsStringAsync(ct); EnsureGoogle(response, body, "上傳 YouTube 影片");
        return JsonNode.Parse(body)?["id"]?.GetValue<string>() ?? throw new InvalidOperationException("YouTube 未回傳影片 ID。");
    }

    public async Task<List<YouTubeVideoInfo>> GetUploadedVideosAsync(CancellationToken ct)
    {
        using var channel = await SendGoogleAsync(HttpMethod.Get, "https://www.googleapis.com/youtube/v3/channels?part=contentDetails&mine=true", null, ct); var cb = await channel.Content.ReadAsStringAsync(ct); EnsureGoogle(channel, cb, "讀取 YouTube 頻道");
        var playlist = JsonNode.Parse(cb)?["items"]?[0]?["contentDetails"]?["relatedPlaylists"]?["uploads"]?.GetValue<string>(); if (string.IsNullOrEmpty(playlist)) return [];
        var ids = new List<string>(); string token = "";
        do { var url = $"https://www.googleapis.com/youtube/v3/playlistItems?part=snippet&maxResults=50&playlistId={Uri.EscapeDataString(playlist)}" + (token.Length > 0 ? "&pageToken=" + Uri.EscapeDataString(token) : ""); using var r = await SendGoogleAsync(HttpMethod.Get, url, null, ct); var b = await r.Content.ReadAsStringAsync(ct); EnsureGoogle(r, b, "讀取 YouTube 影片"); var root = JsonNode.Parse(b); token = root?["nextPageToken"]?.GetValue<string>() ?? ""; foreach (var x in root?["items"]?.AsArray() ?? []) { var id = x?["snippet"]?["resourceId"]?["videoId"]?.GetValue<string>() ?? ""; if (id.Length > 0) ids.Add(id); } } while (token.Length > 0);
        var output = new List<YouTubeVideoInfo>();
        foreach (var batch in ids.Chunk(50)) { using var r = await SendGoogleAsync(HttpMethod.Get, "https://www.googleapis.com/youtube/v3/videos?part=snippet,fileDetails&id=" + string.Join(',', batch), null, ct); var b = await r.Content.ReadAsStringAsync(ct); EnsureGoogle(r, b, "讀取 YouTube 影片資料"); foreach (var x in JsonNode.Parse(b)?["items"]?.AsArray() ?? []) { _ = long.TryParse(x?["fileDetails"]?["fileSize"]?.ToString().Trim('"'), out var size); output.Add(new(x?["id"]?.GetValue<string>() ?? "", x?["snippet"]?["title"]?.GetValue<string>() ?? "", x?["snippet"]?["description"]?.GetValue<string>() ?? "", x?["fileDetails"]?["fileName"]?.GetValue<string>() ?? "", size)); } }
        return output;
    }

    static StringContent Json(JsonNode value) => new(value.ToJsonString(), Encoding.UTF8, "application/json");
    static void EnsureWordPress(HttpResponseMessage response, string body, string action) { if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"{action}失敗：{FriendlyError(body)}"); }
    static void EnsureGoogle(HttpResponseMessage response, string body, string action) { if (!response.IsSuccessStatusCode) { var msg = FriendlyError(body); if (response.StatusCode == HttpStatusCode.TooManyRequests || msg.Contains("quota", StringComparison.OrdinalIgnoreCase)) throw new GoogleQuotaException($"{action}遇到 Google 配額限制：{msg}"); throw new InvalidOperationException($"{action}失敗：{msg}"); } }
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
    public void Dispose() => http.Dispose();
}
