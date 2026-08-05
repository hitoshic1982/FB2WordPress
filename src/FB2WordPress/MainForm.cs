using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FB2WordPress;

internal sealed class MainForm : Form
{
    readonly TextBox zipPath = new() { ReadOnly = true, PlaceholderText = "尚未選擇 Facebook ZIP", Dock = DockStyle.Fill };
    readonly Button choose = new() { Text = "1  選擇 Facebook ZIP", Height = 45, Dock = DockStyle.Fill };
    readonly Button start = new() { Text = "2  開始搬家", Height = 52, Dock = DockStyle.Fill, Enabled = false };
    readonly Button settingsButton = new() { Text = "設定", AutoSize = true };
    readonly Button stop = new() { Text = "暫停搬家（可續跑）", Height = 52, Dock = DockStyle.Fill, Enabled = false, BackColor = Color.IndianRed, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
    readonly Button stopCompose = new() { Text = "暫停發布", Height = 52, Dock = DockStyle.Fill, Enabled = false, BackColor = Color.IndianRed, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
    readonly ProgressBar progress = new() { Dock = DockStyle.Fill };
    readonly Label status = new() { Text = "準備就緒", AutoSize = true };
    readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    readonly TextBox composeTitle = new() { PlaceholderText = "文章標題（可留空，自動取內容第一行）", Dock = DockStyle.Fill };
    readonly TextBox composeBody = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, PlaceholderText = "在這裡輸入文章內容；#Hashtag 會自動成為 WordPress Labels", Dock = DockStyle.Fill };
    readonly ListBox composeMedia = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true };
    readonly Button addMedia = new() { Text = "選擇圖片或影片…", AutoSize = true };
    readonly Button removeMedia = new() { Text = "移除選取項目", AutoSize = true };
    readonly Button publishArticle = new() { Text = "發布文章到 WordPress", Height = 52, Dock = DockStyle.Fill };
    readonly CheckBox composeDraft = new() { Text = "先存成草稿", AutoSize = true };
    readonly Label composeStatus = new() { Text = "準備就緒。圖片會上傳 WordPress 媒體庫；影片會上傳 YouTube。", ForeColor = Color.DimGray, AutoSize = true };
    readonly Button optimizeLibrary = new() { Text = "壓縮並替換既有文章圖片", Height = 52, Dock = DockStyle.Top };
    readonly Label optimizeNote = new() { Text = "程式會建立較小的新圖片、更新文章中的圖片網址，再刪除已成功替換的舊檔。已經夠小的圖片不會變動。", AutoSize = false, Dock = DockStyle.Top, Height = 70 };
    readonly Button normalizeWhitespace = new() { Text = "整理全部文章空白行", Height = 52, Dock = DockStyle.Top };
    readonly Button stopWhitespace = new() { Text = "安全停止", Height = 46, Dock = DockStyle.Top, Enabled = false, BackColor = Color.IndianRed, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
    readonly ProgressBar whitespaceProgress = new() { Dock = DockStyle.Top, Height = 28 };
    readonly Label whitespaceStatus = new() { Text = "將連續兩行以上的空白縮成一行；圖片、影片、日期、標籤與網址都不會更動。", AutoSize = false, Dock = DockStyle.Top, Height = 70 };
    readonly Dictionary<string, HostedMedia> composeMediaCache = new(StringComparer.OrdinalIgnoreCase);
    string composePostKey = "";
    AppSettings settings = SettingsStore.Load();
    CancellationTokenSource? cts;

    public MainForm()
    {
        Text = "FB2WordPress"; Width = 820; Height = 630; MinimumSize = new(700, 540); StartPosition = FormStartPosition.CenterScreen; Font = new("Microsoft JhengHei UI", 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(16), ColumnCount = 1, RowCount = 8 };
        layout.RowStyles.Add(new(SizeType.Absolute, 58)); layout.RowStyles.Add(new(SizeType.Absolute, 55)); layout.RowStyles.Add(new(SizeType.Absolute, 44)); layout.RowStyles.Add(new(SizeType.Absolute, 64)); layout.RowStyles.Add(new(SizeType.Absolute, 35)); layout.RowStyles.Add(new(SizeType.Absolute, 34)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.Absolute, 42));
        layout.Controls.Add(new Label { Text = "FB2WordPress", Font = new("Microsoft JhengHei UI", 22, FontStyle.Bold), AutoSize = true });
        layout.Controls.Add(choose); layout.Controls.Add(zipPath);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 }; actions.ColumnStyles.Add(new(SizeType.Percent, 65)); actions.ColumnStyles.Add(new(SizeType.Percent, 35)); actions.Controls.Add(start, 0, 0); actions.Controls.Add(stop, 1, 0); layout.Controls.Add(actions);
        layout.Controls.Add(progress); layout.Controls.Add(status); layout.Controls.Add(log);
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; bottom.Controls.Add(settingsButton); layout.Controls.Add(bottom);
        var migrationTab = new TabPage("Facebook 搬家") { Padding = new(4) }; migrationTab.Controls.Add(layout);
        var composeTab = new TabPage("發布新文章") { Padding = new(4) }; composeTab.Controls.Add(BuildComposer());
        var optimizePanel = new Panel { Dock = DockStyle.Fill, Padding = new(24) }; optimizePanel.Controls.Add(optimizeLibrary); optimizePanel.Controls.Add(optimizeNote);
        var optimizeTab = new TabPage("壓縮既有圖片") { Padding = new(4) }; optimizeTab.Controls.Add(optimizePanel);
        var whitespacePanel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(24), ColumnCount = 1, RowCount = 5 };
        whitespacePanel.RowStyles.Add(new(SizeType.Absolute, 80)); whitespacePanel.RowStyles.Add(new(SizeType.Absolute, 60)); whitespacePanel.RowStyles.Add(new(SizeType.Absolute, 40)); whitespacePanel.RowStyles.Add(new(SizeType.Absolute, 54)); whitespacePanel.RowStyles.Add(new(SizeType.Percent, 100));
        whitespacePanel.Controls.Add(whitespaceStatus); whitespacePanel.Controls.Add(normalizeWhitespace); whitespacePanel.Controls.Add(whitespaceProgress); whitespacePanel.Controls.Add(stopWhitespace);
        var whitespaceTab = new TabPage("整理文章空白行") { Padding = new(4) }; whitespaceTab.Controls.Add(whitespacePanel);
        var tabs = new TabControl { Dock = DockStyle.Fill, Font = Font }; tabs.TabPages.Add(migrationTab); tabs.TabPages.Add(composeTab); tabs.TabPages.Add(optimizeTab); tabs.TabPages.Add(whitespaceTab); Controls.Add(tabs);
        choose.Click += ChooseZip; start.Click += StartMigration; settingsButton.Click += Configure; stop.Click += RequestPause; stopCompose.Click += RequestPause; FormClosing += HandleFormClosing;
        addMedia.Click += AddComposerMedia; removeMedia.Click += (_, _) => { while (composeMedia.SelectedIndices.Count > 0) composeMedia.Items.RemoveAt(composeMedia.SelectedIndices[0]); composePostKey = ""; };
        publishArticle.Click += PublishArticle;
        composeTitle.TextChanged += (_, _) => { if (cts is null) composePostKey = ""; };
        composeBody.TextChanged += (_, _) => { if (cts is null) composePostKey = ""; };
        optimizeLibrary.Click += OptimizeWordPressLibrary;
        normalizeWhitespace.Click += NormalizeWordPressWhitespace;
        stopWhitespace.Click += RequestPause;
        Shown += async (_, _) => { if (string.IsNullOrWhiteSpace(settings.SiteUrl)) await ConfigureFirstRunAsync(); else Say($"已設定：{settings.SiteUrl}"); };
    }

    Control BuildComposer()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(16), ColumnCount = 1, RowCount = 8 };
        panel.RowStyles.Add(new(SizeType.Absolute, 42)); panel.RowStyles.Add(new(SizeType.Absolute, 45)); panel.RowStyles.Add(new(SizeType.Percent, 55)); panel.RowStyles.Add(new(SizeType.Absolute, 34)); panel.RowStyles.Add(new(SizeType.Percent, 45)); panel.RowStyles.Add(new(SizeType.Absolute, 42)); panel.RowStyles.Add(new(SizeType.Absolute, 60)); panel.RowStyles.Add(new(SizeType.Absolute, 32));
        panel.Controls.Add(new Label { Text = "撰寫 WordPress 新文章", Font = new("Microsoft JhengHei UI", 18, FontStyle.Bold), AutoSize = true });
        panel.Controls.Add(composeTitle); panel.Controls.Add(composeBody);
        panel.Controls.Add(new Label { Text = "圖片與影片（可多選）", AutoSize = true }); panel.Controls.Add(composeMedia);
        var mediaButtons = new FlowLayoutPanel { Dock = DockStyle.Fill }; mediaButtons.Controls.Add(addMedia); mediaButtons.Controls.Add(removeMedia); mediaButtons.Controls.Add(composeDraft); panel.Controls.Add(mediaButtons);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 }; actions.ColumnStyles.Add(new(SizeType.Percent, 65)); actions.ColumnStyles.Add(new(SizeType.Percent, 35)); actions.Controls.Add(publishArticle, 0, 0); actions.Controls.Add(stopCompose, 1, 0); panel.Controls.Add(actions);
        panel.Controls.Add(composeStatus);
        return panel;
    }

    void AddComposerMedia(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Multiselect = true, Title = "選擇圖片或影片", Filter = "圖片或影片|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.bmp;*.mp4;*.mov;*.m4v;*.avi;*.mkv;*.webm|所有檔案|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        foreach (var path in dialog.FileNames) if (!composeMedia.Items.Contains(path)) composeMedia.Items.Add(path);
        composePostKey = "";
    }

    async void PublishArticle(object? sender, EventArgs e)
    {
        if (cts is not null) return;
        var body = NormalizePlainTextBlankLines(composeBody.Text.Trim());
        var paths = composeMedia.Items.Cast<string>().ToList();
        if (body.Length == 0 && paths.Count == 0) { MessageBox.Show("請輸入文章內容，或至少選擇一個圖片／影片。", "FB2WordPress"); return; }
        if (string.IsNullOrWhiteSpace(settings.SiteUrl) && !await ConfigureFirstRunAsync()) return;

        var title = composeTitle.Text.Trim();
        if (title.Length == 0)
        {
            title = body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? $"新文章 {DateTime.Now:yyyy-MM-dd HH:mm}";
            if (title.Length > 90) title = title[..90] + "…";
        }
        var isRetry = composePostKey.Length > 0;
        if (!isRetry) composePostKey = "manual-" + Guid.NewGuid().ToString("N");
        var post = new FacebookPost(composePostKey, title, body, DateTimeOffset.Now, FacebookParser.ExtractLabels(body), []);
        cts = new(); ToggleBusy(true);
        try
        {
            using var api = new GoogleApi(settings, Say); await api.EnsureAuthorizedAsync(cts.Token);
            if (isRetry)
            {
                Say("正在確認上次發布是否其實已經成功…");
                var existing = await api.GetAllPostsAsync(settings.BlogId, cts.Token);
                if (existing.Any(p => p.MigrationKey == post.Key))
                {
                    composeTitle.Clear(); composeBody.Clear(); composeMedia.Items.Clear(); composeMediaCache.Clear(); composePostKey = "";
                    Say("上次發布其實已成功，已避免建立重複文章。");
                    MessageBox.Show("文章已經存在於 WordPress，程式沒有重複發布。", "FB2WordPress", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            var html = new StringBuilder($"<!-- FB2WORDPRESS:{post.Key} -->");
            if (body.Length > 0) html.Append("<div style=\"white-space:pre-wrap\">").Append(WebUtility.HtmlEncode(body)).Append("</div>");
            List<YouTubeVideoInfo> videos = []; var claimed = new HashSet<string>(StringComparer.Ordinal);
            if (paths.Any(IsVideoPath)) { Say("正在檢查 YouTube 影片，避免重複…"); videos = await api.GetUploadedVideosAsync(cts.Token); }
            foreach (var path in paths)
            {
                cts.Token.ThrowIfCancellationRequested();
                if (!File.Exists(path)) throw new FileNotFoundException("找不到選取的媒體檔案。", path);
                var video = IsVideoPath(path); var item = new MediaItem(Path.GetFileName(path), video);
                var cacheKey = ComposerCacheKey(path);
                if (!composeMediaCache.TryGetValue(cacheKey, out var hosted))
                {
                    if (video)
                    {
                        var found = FindExistingVideo(videos, claimed, post, item, path);
                        if (found is not null) { Say($"沿用既有 YouTube 影片：{Path.GetFileName(path)}"); hosted = new() { Kind = "youtube", Value = found.Id }; claimed.Add(found.Id); }
                        else { Say($"正在上傳影片：{Path.GetFileName(path)}"); var description = YouTubeDescription(post, item); hosted = new() { Kind = "youtube", Value = await api.UploadVideoAsync(path, title, description, settings.VideoPrivacy, cts.Token) }; claimed.Add(hosted.Value); videos.Add(new(hosted.Value, title, description, Path.GetFileName(path), new FileInfo(path).Length)); }
                    }
                    else { Say($"正在智慧壓縮並上傳圖片：{Path.GetFileName(path)}"); hosted = await UploadOptimizedImageAsync(api, path, cts.Token); }
                    composeMediaCache[cacheKey] = hosted;
                }
                else Say($"沿用本次先前已上傳的媒體：{Path.GetFileName(path)}");

                if (video) html.Append($"<div style=\"margin:16px 0\"><iframe width=\"560\" height=\"315\" src=\"https://www.youtube.com/embed/{WebUtility.HtmlEncode(hosted.Value)}\" title=\"YouTube video\" frameborder=\"0\" allowfullscreen></iframe></div>");
                else html.Append($"<p><img src=\"{WebUtility.HtmlEncode(hosted.Value)}\" alt=\"文章圖片\" style=\"max-width:100%;height:auto\"></p>");
            }
            Say(composeDraft.Checked ? "正在儲存 WordPress 草稿…" : "正在發布 WordPress 文章…");
            await api.CreatePostAsync(settings.BlogId, post, html.ToString(), composeDraft.Checked, cts.Token);
            composeTitle.Clear(); composeBody.Clear(); composeMedia.Items.Clear(); composeMediaCache.Clear(); composePostKey = "";
            Say(composeDraft.Checked ? "新文章已存成 WordPress 草稿。" : "新文章已成功發布到 WordPress。");
            MessageBox.Show(composeDraft.Checked ? "文章已存成 WordPress 草稿。" : "文章已成功發布到 WordPress。", "FB2WordPress", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) { Say("發布已暫停；本次已上傳的媒體會暫時保留，按發布可再次嘗試。 "); }
        catch (GoogleQuotaException ex) { Say(ex.Message); MessageBox.Show(ex.Message, "Google 配額限制", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        catch (Exception ex) { Say("發布失敗：" + ex.Message); MessageBox.Show(ex.Message, "發布失敗", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { cts.Dispose(); cts = null; ToggleBusy(false); }
    }

    static bool IsVideoPath(string path) => Path.GetExtension(path).ToLowerInvariant() is ".mp4" or ".mov" or ".m4v" or ".avi" or ".mkv" or ".webm";
    static string ComposerCacheKey(string path) { var file = new FileInfo(path); return $"{Path.GetFullPath(path)}|{file.Length}|{file.LastWriteTimeUtc.Ticks}"; }

    async Task<HostedMedia> UploadOptimizedImageAsync(GoogleApi api, string originalPath, CancellationToken ct)
    {
        using var optimized = await Task.Run(() => ImageOptimizer.Prepare(originalPath), ct);
        if (optimized.IsTemporary) Say($"圖片已縮小：{FormatBytes(optimized.OriginalBytes)} → {FormatBytes(optimized.UploadBytes)}");
        var url = await api.UploadImageAsync(optimized.Path, ct, Path.GetFileName(originalPath));
        return new() { Kind = "image", Value = url, Optimized = true };
    }

    void ChooseZip(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Filter = "Facebook ZIP 檔案 (*.zip)|*.zip", Title = "選擇 Facebook 匯出的 ZIP" };
        if (dialog.ShowDialog(this) == DialogResult.OK) { zipPath.Text = dialog.FileName; start.Enabled = true; Say("ZIP 已選好，按「開始搬家」。"); }
    }

    async void Configure(object? sender, EventArgs e) => await ConfigureFirstRunAsync();

    void RequestPause(object? sender, EventArgs e)
    {
        if (cts is null) return;
        stop.Enabled = false;
        status.Text = "正在安全暫停，請稍候…";
        log.AppendText($"[{DateTime.Now:HH:mm:ss}] 已要求暫停；正在保存目前進度…\r\n");
        cts.Cancel();
    }

    void HandleFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason is CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing)
        {
            cts?.Cancel();
            return;
        }
        if (cts is not null)
        {
            e.Cancel = true;
            RequestPause(sender, EventArgs.Empty);
            MessageBox.Show("正在安全暫停並保存進度。畫面恢復可操作後，再關閉程式。", "FB2WordPress", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (composeTitle.TextLength > 0 || composeBody.TextLength > 0 || composeMedia.Items.Count > 0)
        {
            var answer = MessageBox.Show("「發布新文章」中還有尚未發布的內容。確定要關閉並捨棄嗎？", "尚未發布", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) e.Cancel = true;
        }
    }

    async Task<bool> ConfigureFirstRunAsync()
    {
        using var dialog = new SetupDialog(settings); if (dialog.ShowDialog(this) != DialogResult.OK) return false;
        settings = dialog.Settings;
        try
        {
            ToggleBusy(true); using var api = new GoogleApi(settings, Say); await api.EnsureAuthorizedAsync(CancellationToken.None);
            var blogs = await api.GetBlogsAsync(CancellationToken.None);
            if (blogs.Count == 0) throw new InvalidOperationException("無法讀取 WordPress 網站，請檢查網址與應用程式密碼。");
            BlogInfo selected = blogs[0];
            if (blogs.Count > 1)
            {
                using var picker = new Form { Text = "選擇 WordPress 網誌", Width = 430, Height = 150, StartPosition = FormStartPosition.CenterParent, Font = Font, FormBorderStyle = FormBorderStyle.FixedDialog };
                var combo = new ComboBox { DataSource = blogs, DropDownStyle = ComboBoxStyle.DropDownList, Width = 370, Location = new(20, 18) }; var ok = new Button { Text = "確定", DialogResult = DialogResult.OK, Location = new(315, 57) }; picker.Controls.Add(combo); picker.Controls.Add(ok); picker.AcceptButton = ok;
                if (picker.ShowDialog(this) != DialogResult.OK) return false; selected = (BlogInfo)combo.SelectedItem!;
            }
            settings.BlogId = selected.Id; settings.BlogName = selected.Name; SettingsStore.Save(settings); Say($"設定完成：{selected.Name}。以後不需再登入。"); return true;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "設定失敗", MessageBoxButtons.OK, MessageBoxIcon.Error); Say("設定失敗：" + ex.Message); return false; }
        finally { ToggleBusy(false); }
    }

    async void StartMigration(object? sender, EventArgs e)
    {
        if (!File.Exists(zipPath.Text)) { MessageBox.Show("請先選擇 Facebook ZIP。"); return; }
        if (string.IsNullOrWhiteSpace(settings.SiteUrl) && !await ConfigureFirstRunAsync()) return;
        cts = new(); ToggleBusy(true); var report = new MigrationReport(); var temp = Path.Combine(Path.GetTempPath(), "FB2WordPress", Guid.NewGuid().ToString("N"));
        try
        {
            Say("正在清理上次可能留下的暫存資料…"); await Task.Run(CleanupStaleTemps, cts.Token);
            Directory.CreateDirectory(temp); Say("正在解開 Facebook ZIP…"); await Task.Run(() => SafeExtract(zipPath.Text, temp, cts.Token), cts.Token);
            Say("正在尋找貼文、圖片與影片…"); var posts = await Task.Run(() => FacebookParser.Read(temp, Say, cts.Token), cts.Token); report.Total = posts.Count;
            if (posts.Count == 0) throw new InvalidOperationException("找不到 Facebook 貼文。請從 Facebook 下載「JSON」格式，而不是 HTML 格式。");
            var legacyStateFile = SettingsStore.StateFile(zipPath.Text);
            var legacyCompleted = File.Exists(legacyStateFile) ? File.ReadAllLines(legacyStateFile).ToHashSet() : [];
            var migration = SettingsStore.LoadMigration(zipPath.Text);
            using var api = new GoogleApi(settings, Say); await api.EnsureAuthorizedAsync(cts.Token);

            Say("正在核對 WordPress，找出已刪除或已存在的文章…");
            var bloggerPosts = await api.GetAllPostsAsync(settings.BlogId, cts.Token);
            var blogByKey = bloggerPosts.Where(p => p.MigrationKey.Length > 0).GroupBy(p => p.MigrationKey).ToDictionary(g => g.Key, g => g.First());
            var blogBySecond = bloggerPosts.GroupBy(p => p.Published.ToUnixTimeSeconds()).ToDictionary(g => g.Key, g => g.First());
            foreach (var post in posts)
            {
                if (!migration.Posts.TryGetValue(post.Key, out var saved)) migration.Posts[post.Key] = saved = new();
                if (blogByKey.TryGetValue(post.Key, out var existing) || blogBySecond.TryGetValue(post.Published.ToUnixTimeSeconds(), out existing))
                {
                    saved.Complete = true; saved.WordPressPostId = existing.Id;
                }
                else if (saved.Complete || legacyCompleted.Contains(post.Key))
                {
                    // It was previously completed but no longer exists on WordPress:
                    // rebuild it while retaining any cached uploaded media.
                    saved.Complete = false; saved.WordPressPostId = "";
                }
            }
            SettingsStore.SaveMigration(zipPath.Text, migration);

            List<YouTubeVideoInfo> youtubeVideos = [];
            var claimedYouTubeIds = new HashSet<string>(StringComparer.Ordinal);
            if (posts.Any(p => p.Media.Any(m => m.IsVideo)))
            {
                Say("正在核對 YouTube，避免影片重複上傳…");
                youtubeVideos = await api.GetUploadedVideosAsync(cts.Token);
            }
            for (var index = 0; index < posts.Count; index++)
            {
                cts.Token.ThrowIfCancellationRequested(); var post = posts[index];
                var postState = migration.Posts[post.Key];
                if (postState.Complete) { report.Skipped++; UpdateProgress(index + 1, posts.Count, $"略過 WordPress 中仍存在的文章 {index + 1}/{posts.Count}"); continue; }
                try
                {
                    Say($"正在搬第 {index + 1}/{posts.Count} 篇：{post.Title}");
                    var html = new StringBuilder($"<!-- FB2WORDPRESS:{WebUtility.HtmlEncode(post.Key)} -->");
                    if (!string.IsNullOrWhiteSpace(post.Text)) html.Append("<div style=\"white-space:pre-wrap\">").Append(WebUtility.HtmlEncode(NormalizePlainTextBlankLines(post.Text))).Append("</div>");
                    foreach (var media in post.Media)
                    {
                        var path = ResolveMedia(temp, media.RelativePath); if (path is null) { Say($"找不到媒體：{media.RelativePath}"); continue; }
                        if (postState.Media.TryGetValue(media.RelativePath, out var hosted) && hosted.Value.Length > 0)
                        {
                            Say(media.IsVideo ? "沿用先前已上傳的 YouTube 影片。" : "沿用先前已上傳的圖片。");
                        }
                        else if (media.IsVideo)
                        {
                            var description = YouTubeDescription(post, media);
                            var found = FindExistingVideo(youtubeVideos, claimedYouTubeIds, post, media, path);
                            if (found is not null) { Say("找到先前已上傳的 YouTube 影片，直接沿用。"); hosted = new() { Kind = "youtube", Value = found.Id }; claimedYouTubeIds.Add(found.Id); }
                            else { Say("上傳影片到 YouTube…"); hosted = new() { Kind = "youtube", Value = await api.UploadVideoAsync(path, post.Title, description, settings.VideoPrivacy, cts.Token) }; youtubeVideos.Add(new(hosted.Value, post.Title, description, Path.GetFileName(path), new FileInfo(path).Length)); claimedYouTubeIds.Add(hosted.Value); report.Videos++; }
                            postState.Media[media.RelativePath] = hosted; SettingsStore.SaveMigration(zipPath.Text, migration);
                        }
                        else
                        {
                            Say("智慧壓縮並上傳圖片…"); hosted = await UploadOptimizedImageAsync(api, path, cts.Token); report.Images++;
                            postState.Media[media.RelativePath] = hosted; SettingsStore.SaveMigration(zipPath.Text, migration);
                        }

                        if (media.IsVideo) html.Append($"<div style=\"margin:16px 0\"><iframe width=\"560\" height=\"315\" src=\"https://www.youtube.com/embed/{WebUtility.HtmlEncode(hosted.Value)}\" title=\"YouTube video\" frameborder=\"0\" allowfullscreen></iframe></div>");
                        else html.Append($"<p><img src=\"{WebUtility.HtmlEncode(hosted.Value)}\" alt=\"Facebook 圖片\" style=\"max-width:100%;height:auto\"></p>");
                    }
                    postState.WordPressPostId = await api.CreatePostAsync(settings.BlogId, post, html.ToString(), settings.CreateAsDraft, cts.Token);
                    postState.Complete = true; SettingsStore.SaveMigration(zipPath.Text, migration);
                    if (!legacyCompleted.Contains(post.Key)) { await File.AppendAllLinesAsync(legacyStateFile, [post.Key], cts.Token); legacyCompleted.Add(post.Key); }
                    report.Imported++;
                    // Avoid WordPress's per-user write burst limit during large migrations.
                    await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
                }
                catch (GoogleQuotaException) { throw; }
                catch (Exception ex) when (ex is not OperationCanceledException) { report.Failed++; report.Errors.Add($"{post.Published:yyyy-MM-dd} {post.Title}：{ex.Message}"); Say("此篇失敗，繼續下一篇：" + ex.Message); }
                UpdateProgress(index + 1, posts.Count, $"已完成 {index + 1}/{posts.Count}");
            }
            ShowReport(report, false);
        }
        catch (OperationCanceledException) { Say("已停止；下次選同一個 ZIP 會從未完成處繼續。"); ShowReport(report, true); }
        catch (GoogleQuotaException ex) { Say(ex.Message); ShowReport(report, true); MessageBox.Show(ex.Message, "Google 配額暫停", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        catch (Exception ex) { Say("搬家失敗：" + ex.Message); MessageBox.Show(ex.Message, "FB2WordPress", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { try { Directory.Delete(temp, true); } catch { } cts.Dispose(); cts = null; ToggleBusy(false); }
    }

    async void NormalizeWordPressWhitespace(object? sender, EventArgs e)
    {
        if (cts is not null) return;
        if (string.IsNullOrWhiteSpace(settings.SiteUrl) && !await ConfigureFirstRunAsync()) return;
        if (MessageBox.Show(
            "程式會檢查所有 WordPress 文章，只把 FB2WordPress 文字區塊中過多的連續空白行縮成一行。\n\n圖片、影片、日期、標籤、文章網址與一般 WordPress 內容都不會更動。過程可以安全停止並重新執行。要開始嗎？",
            "整理文章空白行", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        cts = new(); ToggleBusy(true); whitespaceProgress.Value = 0;
        var changed = 0; var skipped = 0; var failed = 0; var total = 0;
        string backupPath = "";
        try
        {
            using var api = new GoogleApi(settings, Say); await api.EnsureAuthorizedAsync(cts.Token);
            whitespaceStatus.Text = "正在分頁讀取 WordPress 文章…";
            var posts = await api.GetAllPostsAsync(settings.BlogId, cts.Token); total = posts.Count;

            var reportFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FB2WordPress Reports");
            Directory.CreateDirectory(reportFolder);
            backupPath = Path.Combine(reportFolder, $"空白行整理備份-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl");
            await using var backup = new StreamWriter(backupPath, false, new UTF8Encoding(false));

            for (var i = 0; i < posts.Count; i++)
            {
                cts.Token.ThrowIfCancellationRequested();
                var post = posts[i];
                var revised = NormalizeFacebookHtmlBlankLines(post.Content);
                if (string.Equals(revised, post.Content, StringComparison.Ordinal))
                {
                    skipped++;
                }
                else
                {
                    try
                    {
                        await backup.WriteLineAsync(JsonSerializer.Serialize(new { post.Id, post.Title, post.Published, Content = post.Content }));
                        await backup.FlushAsync(cts.Token);
                        await UpdatePostWithRetryAsync(api, post.Id, revised, cts.Token);
                        changed++;
                        await Task.Delay(TimeSpan.FromMilliseconds(750), cts.Token);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { failed++; Say($"文章「{post.Title}」整理失敗，已保留原文：{ex.Message}"); }
                }

                var done = i + 1;
                whitespaceProgress.Value = posts.Count == 0 ? 0 : Math.Clamp(done * 100 / posts.Count, 0, 100);
                whitespaceStatus.Text = $"正在處理 {done}/{posts.Count} 篇；已整理 {changed}、不需處理 {skipped}、失敗 {failed}";
                Say($"整理空白行 {done}/{posts.Count}");
            }

            whitespaceStatus.Text = $"整理完成：已整理 {changed} 篇，不需處理 {skipped} 篇，失敗 {failed} 篇。";
            MessageBox.Show($"文章空白行整理完成。\n\n檢查：{total}\n已整理：{changed}\n不需處理：{skipped}\n失敗：{failed}\n\n更新前內容已備份到「文件\\FB2WordPress Reports」。",
                "整理文章空白行", MessageBoxButtons.OK, failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            whitespaceStatus.Text = $"已安全停止。已整理 {changed} 篇；下次重新按開始即可續跑。";
            MessageBox.Show($"已安全停止。\n\n已整理：{changed}\n不需處理：{skipped}\n失敗：{failed}\n\n重新執行時，完成的文章會自動略過。",
                "整理文章空白行", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            whitespaceStatus.Text = "整理失敗：" + ex.Message;
            MessageBox.Show(ex.Message, "整理文章空白行", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { cts.Dispose(); cts = null; ToggleBusy(false); }
    }

    static async Task UpdatePostWithRetryAsync(GoogleApi api, string postId, string html, CancellationToken ct)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            try { await api.UpdatePostContentAsync(postId, html, ct); return; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                last = ex;
                if (attempt < 4) await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
            }
        }
        throw last ?? new InvalidOperationException("WordPress 更新失敗。");
    }

    static string NormalizePlainTextBlankLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        normalized = Regex.Replace(normalized, @"\n[ \t]*(?:\n[ \t]*){2,}", "\n\n", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        return normalized;
    }

    static string NormalizeFacebookHtmlBlankLines(string html)
    {
        if (string.IsNullOrEmpty(html) || html.IndexOf("white-space", StringComparison.OrdinalIgnoreCase) < 0) return html;
        const string pattern = @"(<div\b(?=[^>]*\bstyle\s*=\s*[""'][^""']*white-space\s*:\s*pre-wrap\b[^""']*[""'])[^>]*>)(.*?)(</div\s*>)";
        return Regex.Replace(html, pattern,
            match => match.Groups[1].Value + NormalizePlainTextBlankLines(match.Groups[2].Value) + match.Groups[3].Value,
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(3));
    }

    async void OptimizeWordPressLibrary(object? sender, EventArgs e)
    {
        if (cts is not null) return;
        if (string.IsNullOrWhiteSpace(settings.SiteUrl) && !await ConfigureFirstRunAsync()) return;
        if (MessageBox.Show("即將檢查所有 WordPress 文章圖片。只有壓縮後確實較小的圖片才會替換，過程可安全暫停。要開始嗎？", "壓縮既有圖片", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        cts = new(); ToggleBusy(true); var temp = Path.Combine(Path.GetTempPath(), "FB2WordPress", "opt-" + Guid.NewGuid().ToString("N"));
        var changed = 0; var skipped = 0; var failed = 0;
        try
        {
            Directory.CreateDirectory(temp); using var api = new GoogleApi(settings, Say); await api.EnsureAuthorizedAsync(cts.Token);
            var posts = await api.GetAllPostsAsync(settings.BlogId, cts.Token); var media = await api.GetMediaImagesAsync(cts.Token);
            using var downloader = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            for (var i = 0; i < media.Count; i++)
            {
                cts.Token.ThrowIfCancellationRequested(); var item = media[i];
                var affected = posts.Where(p => p.Content.Contains(item.SourceUrl, StringComparison.Ordinal)).ToList();
                if (affected.Count == 0 || !Uri.TryCreate(item.SourceUrl, UriKind.Absolute, out _)) { skipped++; continue; }
                try
                {
                    Say($"檢查既有圖片 {i + 1}/{media.Count}：{item.Name}");
                    var bytes = await downloader.GetByteArrayAsync(item.SourceUrl, cts.Token);
                    if (bytes.Length > 80 * 1024 * 1024) { Say("圖片超過 80 MB，為保護記憶體而略過。"); skipped++; continue; }
                    var ext = Path.GetExtension(item.Name); if (ext.Length is < 2 or > 6) ext = ".jpg";
                    var local = Path.Combine(temp, Guid.NewGuid().ToString("N") + ext); await File.WriteAllBytesAsync(local, bytes, cts.Token);
                    using var optimized = await Task.Run(() => ImageOptimizer.Prepare(local), cts.Token);
                    if (!optimized.IsTemporary || optimized.UploadBytes >= optimized.OriginalBytes) { skipped++; continue; }
                    var newUrl = await api.UploadImageAsync(optimized.Path, cts.Token, item.Name);
                    foreach (var post in affected)
                    {
                        var revised = post.Content.Replace(item.SourceUrl, newUrl, StringComparison.Ordinal);
                        await api.UpdatePostContentAsync(post.Id, revised, cts.Token);
                        var index = posts.FindIndex(x => x.Id == post.Id); if (index >= 0) posts[index] = post with { Content = revised };
                    }
                    await api.DeleteMediaAsync(item.Id, cts.Token); changed++;
                    Say($"已替換：{item.Name}（{FormatBytes(optimized.OriginalBytes)} → {FormatBytes(optimized.UploadBytes)}）");
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { failed++; Say($"圖片略過：{item.Name}；{ex.Message}"); }
                UpdateProgress(i + 1, media.Count, $"已檢查 {i + 1}/{media.Count}");
            }
            MessageBox.Show($"圖片處理完成。\n\n已壓縮替換：{changed}\n不需處理：{skipped}\n失敗但未破壞原圖：{failed}", "FB2WordPress", MessageBoxButtons.OK, failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) { Say("已安全暫停圖片處理。"); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "圖片處理失敗", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { try { Directory.Delete(temp, true); } catch { } cts.Dispose(); cts = null; ToggleBusy(false); }
    }

    void ShowReport(MigrationReport r, bool stopped)
    {
        var text = $"FB2WordPress 搬家報告\r\n時間：{r.Started:g} - {DateTime.Now:g}\r\n狀態：{(stopped ? "已停止" : "完成")}\r\n總文章：{r.Total}\r\n成功：{r.Imported}\r\n已完成略過：{r.Skipped}\r\n失敗：{r.Failed}\r\n圖片：{r.Images}\r\n影片：{r.Videos}\r\n" + (r.Errors.Count > 0 ? "\r\n失敗明細：\r\n" + string.Join("\r\n", r.Errors) : "");
        var reportNote = "報告未能寫入，但搬家進度已安全保存。";
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FB2WordPress Reports");
            Directory.CreateDirectory(folder); var path = Path.Combine(folder, $"搬家報告-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(path, text, Encoding.UTF8); reportNote = "報告已保存到「文件\\FB2WordPress Reports」。";
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        Say(stopped ? $"搬家已停止。{reportNote}" : $"搬家完成：成功 {r.Imported}，略過 {r.Skipped}，失敗 {r.Failed}。{reportNote}");
        MessageBox.Show($"{(stopped ? "已停止" : "搬家完成")}\n\n成功：{r.Imported}\n已完成略過：{r.Skipped}\n失敗：{r.Failed}\n圖片：{r.Images}\n影片：{r.Videos}\n\n{reportNote}", "FB2WordPress 搬家報告", MessageBoxButtons.OK, r.Failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    static string? ResolveMedia(string root, string relative)
    {
        var normalized = relative.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); var candidate = Path.GetFullPath(Path.Combine(root, normalized)); var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        if (candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate)) return candidate;
        var name = Path.GetFileName(relative); return string.IsNullOrEmpty(name) ? null : Directory.EnumerateFiles(root, name, SearchOption.AllDirectories).FirstOrDefault();
    }

    static string YouTubeDescription(FacebookPost post, MediaItem media)
    {
        var marker = $"\n\n[FB2WordPress:{post.Key}:{media.RelativePath.Replace('\\', '/')} ]";
        var maxText = Math.Max(0, 5000 - marker.Length);
        var text = post.Text.Length <= maxText ? post.Text : post.Text[..maxText];
        if (text.Length > 0 && char.IsHighSurrogate(text[^1])) text = text[..^1];
        return text + marker;
    }

    static YouTubeVideoInfo? FindExistingVideo(List<YouTubeVideoInfo> videos, HashSet<string> claimedIds, FacebookPost post, MediaItem media, string localPath)
    {
        // Strong fingerprint for videos previously uploaded manually: YouTube
        // exposes the owner's original upload file name and exact byte size.
        var localName = Path.GetFileName(localPath).Normalize(NormalizationForm.FormC);
        var localSize = new FileInfo(localPath).Length;
        var fingerprint = videos.FirstOrDefault(v =>
            !claimedIds.Contains(v.Id) && v.OriginalFileSize == localSize &&
            v.OriginalFileName.Length > 0 &&
            string.Equals(v.OriginalFileName.Normalize(NormalizationForm.FormC), localName, StringComparison.OrdinalIgnoreCase));
        if (fingerprint is not null) return fingerprint;

        var markedDescription = YouTubeDescription(post, media);
        var exact = videos.FirstOrDefault(v => !claimedIds.Contains(v.Id) && v.Title == post.Title && v.Description == markedDescription);
        if (exact is not null) return exact;
        // Version 2 used the post text only. Claim each matching upload once so a
        // multi-video Facebook post cannot reuse one YouTube ID for every video.
        exact = videos.FirstOrDefault(v => !claimedIds.Contains(v.Id) && v.Title == post.Title && v.Description == post.Text);
        if (exact is not null) return exact;
        // Version 1 uploaded text before repairing Facebook's legacy UTF-8-as-Latin1 encoding.
        try
        {
            var oldTitle = Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(post.Title));
            var oldText = Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(post.Text));
            return videos.FirstOrDefault(v => !claimedIds.Contains(v.Id) && v.Title == oldTitle && v.Description == oldText);
        }
        catch { return null; }
    }

    static void SafeExtract(string archive, string target, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(target) + Path.DirectorySeparatorChar;
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count > 250_000) throw new InvalidDataException("ZIP 內檔案數異常過多，為保護電腦已停止解壓縮。");

        long totalBytes = 0;
        foreach (var entry in zip.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.FullName.Length > 1024) throw new InvalidDataException("ZIP 包含異常過長的檔名。");
            try { totalBytes = checked(totalBytes + entry.Length); }
            catch (OverflowException) { throw new InvalidDataException("ZIP 宣告的容量異常。 "); }
        }

        var archiveBytes = Math.Max(1, new FileInfo(archive).Length);
        if (totalBytes > 10L * 1024 * 1024 * 1024 && totalBytes / archiveBytes > 500)
            throw new InvalidDataException("ZIP 壓縮比例異常，可能是會塞滿硬碟的惡意壓縮檔。");
        var driveRoot = Path.GetPathRoot(root) ?? root;
        var available = new DriveInfo(driveRoot).AvailableFreeSpace;
        var reserve = 2L * 1024 * 1024 * 1024;
        if (totalBytes > Math.Max(0, available - reserve))
            throw new IOException($"系統碟空間不足。解壓縮約需 {FormatBytes(totalBytes)}，目前可安全使用約 {FormatBytes(Math.Max(0, available - reserve))}。");

        foreach (var entry in zip.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(Path.Combine(target, entry.FullName));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("ZIP 包含不安全的路徑。");
            if (string.IsNullOrEmpty(entry.Name)) Directory.CreateDirectory(path);
            else { Directory.CreateDirectory(Path.GetDirectoryName(path)!); entry.ExtractToFile(path, true); }
        }
    }

    static void CleanupStaleTemps()
    {
        var root = Path.Combine(Path.GetTempPath(), "FB2WordPress");
        if (!Directory.Exists(root)) return;
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            try { Directory.Delete(directory, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    static string FormatBytes(long bytes) => bytes >= 1024L * 1024 * 1024
        ? $"{bytes / (1024d * 1024 * 1024):0.0} GB"
        : $"{bytes / (1024d * 1024):0.0} MB";

    void ToggleBusy(bool busy)
    {
        choose.Enabled = !busy; start.Enabled = !busy && File.Exists(zipPath.Text); settingsButton.Enabled = !busy; optimizeLibrary.Enabled = !busy; normalizeWhitespace.Enabled = !busy;
        composeTitle.Enabled = !busy; composeBody.Enabled = !busy; composeMedia.Enabled = !busy; addMedia.Enabled = !busy; removeMedia.Enabled = !busy; composeDraft.Enabled = !busy; publishArticle.Enabled = !busy;
        stop.Enabled = busy; stopCompose.Enabled = busy; stopWhitespace.Enabled = busy; UseWaitCursor = busy;
    }
    void UpdateProgress(int current, int total, string message) { progress.Value = total == 0 ? 0 : Math.Clamp(current * 100 / total, 0, 100); Say(message); }
    void Say(string message) { if (InvokeRequired) { BeginInvoke(() => Say(message)); return; } status.Text = message; composeStatus.Text = message; log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n"); }
}
