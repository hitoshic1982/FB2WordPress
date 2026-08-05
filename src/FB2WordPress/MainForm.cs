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
    readonly TextBox zipPath = new() { ReadOnly = true, PlaceholderText = L.T("zip_empty"), Dock = DockStyle.Fill };
    readonly Button choose = new() { Text = L.T("choose_zip"), Height = 45, Dock = DockStyle.Fill };
    readonly Button start = new() { Text = L.T("start_move"), Height = 52, Dock = DockStyle.Fill, Enabled = false };
    readonly Button settingsButton = new() { Text = L.T("settings"), AutoSize = true };
    readonly Button stop = new() { Text = L.T("pause_move"), Height = 52, Dock = DockStyle.Fill, Enabled = false, BackColor = Color.IndianRed, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
    readonly Button stopCompose = new() { Text = L.T("pause_publish"), Height = 52, Dock = DockStyle.Fill, Enabled = false, BackColor = Color.IndianRed, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
    readonly ProgressBar progress = new() { Dock = DockStyle.Fill };
    readonly Label status = new() { Text = L.T("ready"), AutoSize = true };
    readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    readonly TextBox composeTitle = new() { PlaceholderText = L.T("compose_title_placeholder"), Dock = DockStyle.Fill };
    readonly TextBox composeBody = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, PlaceholderText = L.T("compose_body_placeholder"), Dock = DockStyle.Fill };
    readonly ListBox composeMedia = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true };
    readonly Button addMedia = new() { Text = L.T("choose_media"), AutoSize = true };
    readonly Button removeMedia = new() { Text = L.T("remove_media"), AutoSize = true };
    readonly Button publishArticle = new() { Text = L.T("publish_wordpress"), Height = 52, Dock = DockStyle.Fill };
    readonly CheckBox composeDraft = new() { Text = L.T("save_draft"), AutoSize = true };
    readonly Label composeStatus = new() { Text = L.T("composer_ready"), ForeColor = Color.DimGray, AutoSize = true };
    readonly Button optimizeLibrary = new() { Text = L.T("optimize_images"), Height = 52, Dock = DockStyle.Top };
    readonly Label optimizeNote = new() { Text = L.T("optimize_note"), AutoSize = false, Dock = DockStyle.Top, Height = 90 };
    readonly Button normalizeWhitespace = new() { Text = L.T("normalize_whitespace"), Height = 52, Dock = DockStyle.Top };
    readonly Button stopWhitespace = new() { Text = L.T("safe_stop"), Height = 46, Dock = DockStyle.Top, Enabled = false, BackColor = Color.IndianRed, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
    readonly ProgressBar whitespaceProgress = new() { Dock = DockStyle.Top, Height = 28 };
    readonly Label whitespaceStatus = new() { Text = L.T("whitespace_note"), AutoSize = false, Dock = DockStyle.Top, Height = 80 };
    readonly Dictionary<string, HostedMedia> composeMediaCache = new(StringComparer.OrdinalIgnoreCase);
    string composePostKey = "";
    readonly AppSettings settings;
    CancellationTokenSource? cts;

    public MainForm(AppSettings settings)
    {
        this.settings = settings;
        Text = "FB2WordPress"; Width = 900; Height = 650; MinimumSize = new(760, 560); StartPosition = FormStartPosition.CenterScreen; Font = new(PlatformPresentation.FontName, 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(16), ColumnCount = 1, RowCount = 8 };
        layout.RowStyles.Add(new(SizeType.Absolute, 58)); layout.RowStyles.Add(new(SizeType.Absolute, 55)); layout.RowStyles.Add(new(SizeType.Absolute, 44)); layout.RowStyles.Add(new(SizeType.Absolute, 64)); layout.RowStyles.Add(new(SizeType.Absolute, 35)); layout.RowStyles.Add(new(SizeType.Absolute, 34)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.Absolute, 42));
        layout.Controls.Add(new Label { Text = "FB2WordPress", Font = new("Microsoft JhengHei UI", 22, FontStyle.Bold), AutoSize = true });
        layout.Controls.Add(choose); layout.Controls.Add(zipPath);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 }; actions.ColumnStyles.Add(new(SizeType.Percent, 65)); actions.ColumnStyles.Add(new(SizeType.Percent, 35)); actions.Controls.Add(start, 0, 0); actions.Controls.Add(stop, 1, 0); layout.Controls.Add(actions);
        layout.Controls.Add(progress); layout.Controls.Add(status); layout.Controls.Add(log);
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; bottom.Controls.Add(settingsButton); layout.Controls.Add(bottom);
        var migrationTab = new TabPage(L.T("tab_move")) { Padding = new(4) }; migrationTab.Controls.Add(layout);
        var composeTab = new TabPage(L.T("tab_compose")) { Padding = new(4) }; composeTab.Controls.Add(BuildComposer());
        var optimizePanel = new Panel { Dock = DockStyle.Fill, Padding = new(24) }; optimizePanel.Controls.Add(optimizeLibrary); optimizePanel.Controls.Add(optimizeNote);
        var optimizeTab = new TabPage(L.T("tab_optimize")) { Padding = new(4) }; optimizeTab.Controls.Add(optimizePanel);
        var whitespacePanel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(24), ColumnCount = 1, RowCount = 5 };
        whitespacePanel.RowStyles.Add(new(SizeType.Absolute, 80)); whitespacePanel.RowStyles.Add(new(SizeType.Absolute, 60)); whitespacePanel.RowStyles.Add(new(SizeType.Absolute, 40)); whitespacePanel.RowStyles.Add(new(SizeType.Absolute, 54)); whitespacePanel.RowStyles.Add(new(SizeType.Percent, 100));
        whitespacePanel.Controls.Add(whitespaceStatus); whitespacePanel.Controls.Add(normalizeWhitespace); whitespacePanel.Controls.Add(whitespaceProgress); whitespacePanel.Controls.Add(stopWhitespace);
        var whitespaceTab = new TabPage(L.T("tab_whitespace")) { Padding = new(4) }; whitespaceTab.Controls.Add(whitespacePanel);
        var tabs = new TabControl { Dock = DockStyle.Fill, Font = Font }; tabs.TabPages.Add(migrationTab); tabs.TabPages.Add(composeTab); tabs.TabPages.Add(optimizeTab); tabs.TabPages.Add(whitespaceTab); Controls.Add(tabs);
        choose.Click += ChooseZip; start.Click += StartMigration; settingsButton.Click += Configure; stop.Click += RequestPause; stopCompose.Click += RequestPause; FormClosing += HandleFormClosing;
        addMedia.Click += AddComposerMedia; removeMedia.Click += (_, _) => { while (composeMedia.SelectedIndices.Count > 0) composeMedia.Items.RemoveAt(composeMedia.SelectedIndices[0]); composePostKey = ""; };
        publishArticle.Click += PublishArticle;
        composeTitle.TextChanged += (_, _) => { if (cts is null) composePostKey = ""; };
        composeBody.TextChanged += (_, _) => { if (cts is null) composePostKey = ""; };
        optimizeLibrary.Click += OptimizeWordPressLibrary;
        normalizeWhitespace.Click += NormalizeWordPressWhitespace;
        stopWhitespace.Click += RequestPause;
        Shown += async (_, _) => { if (string.IsNullOrWhiteSpace(settings.SiteUrl)) await ConfigureFirstRunAsync(); else Say(L.T("configured_site", settings.SiteUrl)); };
    }

    Control BuildComposer()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(16), ColumnCount = 1, RowCount = 8 };
        panel.RowStyles.Add(new(SizeType.Absolute, 42)); panel.RowStyles.Add(new(SizeType.Absolute, 45)); panel.RowStyles.Add(new(SizeType.Percent, 55)); panel.RowStyles.Add(new(SizeType.Absolute, 34)); panel.RowStyles.Add(new(SizeType.Percent, 45)); panel.RowStyles.Add(new(SizeType.Absolute, 42)); panel.RowStyles.Add(new(SizeType.Absolute, 60)); panel.RowStyles.Add(new(SizeType.Absolute, 32));
        panel.Controls.Add(new Label { Text = L.T("compose_heading"), Font = new(PlatformPresentation.FontName, 18, FontStyle.Bold), AutoSize = true });
        panel.Controls.Add(composeTitle); panel.Controls.Add(composeBody);
        panel.Controls.Add(new Label { Text = L.T("media_heading"), AutoSize = true }); panel.Controls.Add(composeMedia);
        var mediaButtons = new FlowLayoutPanel { Dock = DockStyle.Fill }; mediaButtons.Controls.Add(addMedia); mediaButtons.Controls.Add(removeMedia); mediaButtons.Controls.Add(composeDraft); panel.Controls.Add(mediaButtons);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 }; actions.ColumnStyles.Add(new(SizeType.Percent, 65)); actions.ColumnStyles.Add(new(SizeType.Percent, 35)); actions.Controls.Add(publishArticle, 0, 0); actions.Controls.Add(stopCompose, 1, 0); panel.Controls.Add(actions);
        panel.Controls.Add(composeStatus);
        return panel;
    }

    void AddComposerMedia(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Multiselect = true, Title = L.T("media_dialog_title"), Filter = L.T("media_filter") };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        foreach (var path in dialog.FileNames) if (!composeMedia.Items.Contains(path)) composeMedia.Items.Add(path);
        composePostKey = "";
    }

    async void PublishArticle(object? sender, EventArgs e)
    {
        if (cts is not null) return;
        var body = NormalizePlainTextBlankLines(composeBody.Text.Trim());
        var paths = composeMedia.Items.Cast<string>().ToList();
        if (body.Length == 0 && paths.Count == 0) { MessageBox.Show(L.T("content_required"), "FB2WordPress"); return; }
        if (string.IsNullOrWhiteSpace(settings.SiteUrl) && !await ConfigureFirstRunAsync()) return;

        var title = composeTitle.Text.Trim();
        if (title.Length == 0)
        {
            title = body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? L.T("new_post_title", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            if (title.Length > 90) title = title[..90] + "…";
        }
        var isRetry = composePostKey.Length > 0;
        if (!isRetry) composePostKey = "manual-" + Guid.NewGuid().ToString("N");
        var post = new FacebookPost(composePostKey, title, body, DateTimeOffset.Now, FacebookParser.ExtractLabels(body), []);
        cts = new(); ToggleBusy(true);
        try
        {
            using var api = new GoogleApi(settings, Say, SettingsStore.SaveAsync); await api.EnsureAuthorizedAsync(cts.Token);
            if (isRetry)
            {
                Say(L.T("checking_previous_publish"));
                var existing = await api.GetAllPostsAsync(settings.BlogId, cts.Token);
                if (existing.Any(p => p.MigrationKey == post.Key))
                {
                    composeTitle.Clear(); composeBody.Clear(); composeMedia.Items.Clear(); composeMediaCache.Clear(); composePostKey = "";
                    Say(L.T("duplicate_avoided"));
                    MessageBox.Show(L.T("post_already_exists"), "FB2WordPress", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            var html = new StringBuilder($"<!-- FB2WORDPRESS:{post.Key} -->");
            if (body.Length > 0) html.Append("<div style=\"white-space:pre-wrap\">").Append(WebUtility.HtmlEncode(body)).Append("</div>");
            List<YouTubeVideoInfo> videos = []; var claimed = new HashSet<string>(StringComparer.Ordinal);
            if (paths.Any(IsVideoPath)) { Say(L.T("checking_youtube")); videos = await api.GetUploadedVideosAsync(cts.Token); }
            foreach (var path in paths)
            {
                cts.Token.ThrowIfCancellationRequested();
                if (!File.Exists(path)) throw new FileNotFoundException(L.T("media_not_found"), path);
                var video = IsVideoPath(path); var item = new MediaItem(Path.GetFileName(path), video);
                var cacheKey = ComposerCacheKey(path);
                if (!composeMediaCache.TryGetValue(cacheKey, out var hosted))
                {
                    if (video)
                    {
                        var found = FindExistingVideo(videos, claimed, post, item, path);
                        if (found is not null) { Say(L.T("reuse_youtube", Path.GetFileName(path))); hosted = new() { Kind = "youtube", Value = found.Id }; claimed.Add(found.Id); }
                        else { Say(L.T("uploading_video", Path.GetFileName(path))); var description = YouTubeDescription(post, item); hosted = new() { Kind = "youtube", Value = await api.UploadVideoAsync(path, title, description, settings.VideoPrivacy, cts.Token) }; claimed.Add(hosted.Value); videos.Add(new(hosted.Value, title, description, Path.GetFileName(path), new FileInfo(path).Length)); }
                    }
                    else { Say(L.T("uploading_image", Path.GetFileName(path))); hosted = await UploadOptimizedImageAsync(api, path, cts.Token); }
                    composeMediaCache[cacheKey] = hosted;
                }
                else Say(L.T("reuse_media", Path.GetFileName(path)));

                if (video) html.Append($"<div style=\"margin:16px 0\"><iframe width=\"560\" height=\"315\" src=\"https://www.youtube.com/embed/{WebUtility.HtmlEncode(hosted.Value)}\" title=\"YouTube video\" frameborder=\"0\" allowfullscreen></iframe></div>");
                else html.Append($"<p><img src=\"{WebUtility.HtmlEncode(hosted.Value)}\" alt=\"{WebUtility.HtmlEncode(L.T("image_alt"))}\" style=\"max-width:100%;height:auto\"></p>");
            }
            Say(composeDraft.Checked ? L.T("saving_draft") : L.T("publishing_post"));
            await api.CreatePostAsync(settings.BlogId, post, html.ToString(), composeDraft.Checked, cts.Token);
            composeTitle.Clear(); composeBody.Clear(); composeMedia.Items.Clear(); composeMediaCache.Clear(); composePostKey = "";
            Say(composeDraft.Checked ? L.T("draft_saved") : L.T("post_published"));
            MessageBox.Show(composeDraft.Checked ? L.T("draft_saved") : L.T("post_published"), "FB2WordPress", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) { Say(L.T("publish_paused")); }
        catch (GoogleQuotaException ex) { Say(ex.Message); MessageBox.Show(ex.Message, L.T("quota_limit"), MessageBoxButtons.OK, MessageBoxIcon.Information); }
        catch (Exception ex) { Say(L.T("publish_failed_detail", ex.Message)); MessageBox.Show(ex.Message, L.T("publish_failed"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { cts.Dispose(); cts = null; ToggleBusy(false); }
    }

    static bool IsVideoPath(string path) => Path.GetExtension(path).ToLowerInvariant() is ".mp4" or ".mov" or ".m4v" or ".avi" or ".mkv" or ".webm";
    static string ComposerCacheKey(string path) { var file = new FileInfo(path); return $"{Path.GetFullPath(path)}|{file.Length}|{file.LastWriteTimeUtc.Ticks}"; }

    async Task<HostedMedia> UploadOptimizedImageAsync(GoogleApi api, string originalPath, CancellationToken ct)
    {
        using var optimized = await Task.Run(() => ImageOptimizer.Prepare(originalPath), ct);
        if (optimized.IsTemporary) Say(L.T("image_reduced", FormatBytes(optimized.OriginalBytes), FormatBytes(optimized.UploadBytes)));
        var url = await api.UploadImageAsync(optimized.Path, ct, Path.GetFileName(originalPath));
        return new() { Kind = "image", Value = url, Optimized = true };
    }

    void ChooseZip(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Filter = L.T("zip_filter"), Title = L.T("choose_export_zip") };
        if (dialog.ShowDialog(this) == DialogResult.OK) { zipPath.Text = dialog.FileName; start.Enabled = true; Say(L.T("zip_selected")); }
    }

    async void Configure(object? sender, EventArgs e) => await ConfigureFirstRunAsync();

    void RequestPause(object? sender, EventArgs e)
    {
        if (cts is null) return;
        stop.Enabled = false;
        status.Text = L.T("pausing_safely");
        log.AppendText($"[{DateTime.Now:HH:mm:ss}] {L.T("pause_requested")}\r\n");
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
            MessageBox.Show(L.T("wait_before_close"), "FB2WordPress", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (composeTitle.TextLength > 0 || composeBody.TextLength > 0 || composeMedia.Items.Count > 0)
        {
            var answer = MessageBox.Show(L.T("discard_unpublished"), L.T("unpublished"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) e.Cancel = true;
        }
    }

    async Task<bool> ConfigureFirstRunAsync()
    {
        var previousLanguage = settings.InterfaceLanguage;
        using var dialog = new SetupDialog(settings); if (dialog.ShowDialog(this) != DialogResult.OK) return false;
        if (!string.Equals(previousLanguage, settings.InterfaceLanguage, StringComparison.Ordinal))
        {
            L.Configure(settings.InterfaceLanguage);
            MessageBox.Show(L.T("language_changed"), "FB2WordPress", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Application.Restart();
            return false;
        }
        try
        {
            ToggleBusy(true); using var api = new GoogleApi(settings, Say, SettingsStore.SaveAsync); await api.EnsureAuthorizedAsync(CancellationToken.None);
            var blogs = await api.GetBlogsAsync(CancellationToken.None);
            if (blogs.Count == 0) throw new InvalidOperationException(L.P("無法讀取 WordPress 網站，請檢查網址與應用程式密碼。", "无法读取 WordPress 网站，请检查地址与应用程序密码。", "Unable to read the WordPress site. Check the URL and application password.", "WordPress サイトを読み込めません。URLとアプリケーションパスワードを確認してください。"));
            BlogInfo selected = blogs[0];
            if (blogs.Count > 1)
            {
                using var picker = new Form { Text = L.P("選擇 WordPress 網誌", "选择 WordPress 网站", "Choose a WordPress site", "WordPress サイトを選択"), Width = 430, Height = 150, StartPosition = FormStartPosition.CenterParent, Font = Font, FormBorderStyle = FormBorderStyle.FixedDialog };
                var combo = new ComboBox { DataSource = blogs, DropDownStyle = ComboBoxStyle.DropDownList, Width = 370, Location = new(20, 18) }; var ok = new Button { Text = L.P("確定", "确定", "OK", "決定"), DialogResult = DialogResult.OK, Location = new(315, 57) }; picker.Controls.Add(combo); picker.Controls.Add(ok); picker.AcceptButton = ok;
                if (picker.ShowDialog(this) != DialogResult.OK) return false; selected = (BlogInfo)combo.SelectedItem!;
            }
            settings.BlogId = selected.Id; settings.BlogName = selected.Name; SettingsStore.Save(settings); Say(L.P("設定完成：{0}。以後不需再登入。", "设置完成：{0}。以后无需再次登录。", "Setup complete: {0}. You will not need to sign in again.", "設定完了：{0}。次回から再ログインは不要です。", selected.Name)); return true;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, L.P("設定失敗", "设置失败", "Setup failed", "設定に失敗"), MessageBoxButtons.OK, MessageBoxIcon.Error); Say(L.P("設定失敗：{0}", "设置失败：{0}", "Setup failed: {0}", "設定に失敗しました：{0}", ex.Message)); return false; }
        finally { ToggleBusy(false); }
    }

    async void StartMigration(object? sender, EventArgs e)
    {
        if (!File.Exists(zipPath.Text)) { MessageBox.Show(L.P("請先選擇 Facebook ZIP。", "请先选择 Facebook ZIP。", "Choose a Facebook ZIP first.", "先に Facebook ZIP を選択してください。")); return; }
        if (string.IsNullOrWhiteSpace(settings.SiteUrl) && !await ConfigureFirstRunAsync()) return;
        cts = new(); ToggleBusy(true); var report = new MigrationReport(); var temp = Path.Combine(Path.GetTempPath(), "FB2WordPress", Guid.NewGuid().ToString("N"));
        try
        {
            Say(L.P("正在清理上次可能留下的暫存資料…", "正在清理上次可能留下的临时数据…", "Cleaning temporary data left by a previous run…", "前回の一時データを整理しています…")); await Task.Run(CleanupStaleTemps, cts.Token);
            Directory.CreateDirectory(temp); Say(L.P("正在解開 Facebook ZIP…", "正在解压 Facebook ZIP…", "Extracting the Facebook ZIP…", "Facebook ZIP を展開しています…")); await Task.Run(() => SafeExtract(zipPath.Text, temp, cts.Token), cts.Token);
            Say(L.P("正在尋找貼文、圖片與影片…", "正在查找帖子、图片与视频…", "Finding posts, images, and videos…", "投稿、画像、動画を検索しています…")); var posts = await Task.Run(() => FacebookParser.Read(temp, Say, cts.Token), cts.Token); report.Total = posts.Count;
            if (posts.Count == 0) throw new InvalidOperationException(L.P("找不到 Facebook 貼文。請從 Facebook 下載「JSON」格式，而不是 HTML 格式。", "找不到 Facebook 帖子。请从 Facebook 下载“JSON”格式，而不是 HTML 格式。", "No Facebook posts were found. Download your Facebook data in JSON format, not HTML.", "Facebook の投稿が見つかりません。HTML ではなく JSON 形式でダウンロードしてください。"));
            var legacyStateFile = SettingsStore.StateFile(zipPath.Text);
            var legacyCompleted = File.Exists(legacyStateFile) ? File.ReadAllLines(legacyStateFile).ToHashSet() : [];
            var migration = SettingsStore.LoadMigration(zipPath.Text);
            using var api = new GoogleApi(settings, Say, SettingsStore.SaveAsync); await api.EnsureAuthorizedAsync(cts.Token);

            Say(L.P("正在核對 WordPress，找出已刪除或已存在的文章…", "正在核对 WordPress，查找已删除或已存在的文章…", "Checking WordPress for deleted or existing posts…", "削除済みまたは既存の記事を WordPress で確認しています…"));
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
                Say(L.P("正在核對 YouTube，避免影片重複上傳…", "正在核对 YouTube，避免视频重复上传…", "Checking YouTube to avoid duplicate video uploads…", "動画の重複アップロードを防ぐため YouTube を確認しています…"));
                youtubeVideos = await api.GetUploadedVideosAsync(cts.Token);
            }
            for (var index = 0; index < posts.Count; index++)
            {
                cts.Token.ThrowIfCancellationRequested(); var post = posts[index];
                var postState = migration.Posts[post.Key];
                if (postState.Complete) { report.Skipped++; UpdateProgress(index + 1, posts.Count, L.P("略過 WordPress 中仍存在的文章 {0}/{1}", "跳过 WordPress 中仍存在的文章 {0}/{1}", "Skipping post that still exists in WordPress {0}/{1}", "WordPress に存在する記事をスキップ {0}/{1}", index + 1, posts.Count)); continue; }
                try
                {
                    Say(L.P("正在搬第 {0}/{1} 篇：{2}", "正在迁移第 {0}/{1} 篇：{2}", "Migrating post {0}/{1}: {2}", "記事を移行中 {0}/{1}：{2}", index + 1, posts.Count, post.Title));
                    var html = new StringBuilder($"<!-- FB2WORDPRESS:{WebUtility.HtmlEncode(post.Key)} -->");
                    if (!string.IsNullOrWhiteSpace(post.Text)) html.Append("<div style=\"white-space:pre-wrap\">").Append(WebUtility.HtmlEncode(NormalizePlainTextBlankLines(post.Text))).Append("</div>");
                    foreach (var media in post.Media)
                    {
                        var path = ResolveMedia(temp, media.RelativePath); if (path is null) { Say(L.P("找不到媒體：{0}", "找不到媒体：{0}", "Media not found: {0}", "メディアが見つかりません：{0}", media.RelativePath)); continue; }
                        if (postState.Media.TryGetValue(media.RelativePath, out var hosted) && hosted.Value.Length > 0)
                        {
                            Say(media.IsVideo ? L.P("沿用先前已上傳的 YouTube 影片。", "沿用先前已上传的 YouTube 视频。", "Reusing a previously uploaded YouTube video.", "以前アップロードした YouTube 動画を再利用します。") : L.P("沿用先前已上傳的圖片。", "沿用先前已上传的图片。", "Reusing a previously uploaded image.", "以前アップロードした画像を再利用します。"));
                        }
                        else if (media.IsVideo)
                        {
                            var description = YouTubeDescription(post, media);
                            var found = FindExistingVideo(youtubeVideos, claimedYouTubeIds, post, media, path);
                            if (found is not null) { Say(L.P("找到先前已上傳的 YouTube 影片，直接沿用。", "找到先前已上传的 YouTube 视频，直接沿用。", "Found a previously uploaded YouTube video; reusing it.", "以前アップロードした YouTube 動画を再利用します。")); hosted = new() { Kind = "youtube", Value = found.Id }; claimedYouTubeIds.Add(found.Id); }
                            else { Say(L.P("上傳影片到 YouTube…", "上传视频到 YouTube…", "Uploading video to YouTube…", "動画を YouTube にアップロード中…")); hosted = new() { Kind = "youtube", Value = await api.UploadVideoAsync(path, post.Title, description, settings.VideoPrivacy, cts.Token) }; youtubeVideos.Add(new(hosted.Value, post.Title, description, Path.GetFileName(path), new FileInfo(path).Length)); claimedYouTubeIds.Add(hosted.Value); report.Videos++; }
                            postState.Media[media.RelativePath] = hosted; SettingsStore.SaveMigration(zipPath.Text, migration);
                        }
                        else
                        {
                            Say(L.P("智慧壓縮並上傳圖片…", "智能压缩并上传图片…", "Optimizing and uploading image…", "画像を最適化してアップロード中…")); hosted = await UploadOptimizedImageAsync(api, path, cts.Token); report.Images++;
                            postState.Media[media.RelativePath] = hosted; SettingsStore.SaveMigration(zipPath.Text, migration);
                        }

                        if (media.IsVideo) html.Append($"<div style=\"margin:16px 0\"><iframe width=\"560\" height=\"315\" src=\"https://www.youtube.com/embed/{WebUtility.HtmlEncode(hosted.Value)}\" title=\"YouTube video\" frameborder=\"0\" allowfullscreen></iframe></div>");
                        else html.Append($"<p><img src=\"{WebUtility.HtmlEncode(hosted.Value)}\" alt=\"{WebUtility.HtmlEncode(L.P("Facebook 圖片", "Facebook 图片", "Facebook image", "Facebook 画像"))}\" style=\"max-width:100%;height:auto\"></p>");
                    }
                    postState.WordPressPostId = await api.CreatePostAsync(settings.BlogId, post, html.ToString(), settings.CreateAsDraft, cts.Token);
                    postState.Complete = true; SettingsStore.SaveMigration(zipPath.Text, migration);
                    if (!legacyCompleted.Contains(post.Key)) { await File.AppendAllLinesAsync(legacyStateFile, [post.Key], cts.Token); legacyCompleted.Add(post.Key); }
                    report.Imported++;
                    // Avoid WordPress's per-user write burst limit during large migrations.
                    await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
                }
                catch (GoogleQuotaException) { throw; }
                catch (Exception ex) when (ex is not OperationCanceledException) { report.Failed++; report.Errors.Add($"{post.Published:yyyy-MM-dd} {post.Title}：{ex.Message}"); Say(L.P("此篇失敗，繼續下一篇：{0}", "此篇失败，继续下一篇：{0}", "This post failed; continuing with the next: {0}", "この記事は失敗しました。次へ進みます：{0}", ex.Message)); }
                UpdateProgress(index + 1, posts.Count, L.P("已完成 {0}/{1}", "已完成 {0}/{1}", "Completed {0}/{1}", "完了 {0}/{1}", index + 1, posts.Count));
            }
            ShowReport(report, false);
        }
        catch (OperationCanceledException) { Say(L.P("已停止；下次選同一個 ZIP 會從未完成處繼續。", "已停止；下次选择同一 ZIP 会从未完成处继续。", "Stopped. Choose the same ZIP next time to resume.", "停止しました。次回同じ ZIP を選ぶと未完了部分から再開します。")); ShowReport(report, true); }
        catch (GoogleQuotaException ex) { Say(ex.Message); ShowReport(report, true); MessageBox.Show(ex.Message, L.P("Google 配額暫停", "Google 配额暂停", "Paused for Google quota", "Google の割り当てにより一時停止"), MessageBoxButtons.OK, MessageBoxIcon.Information); }
        catch (Exception ex) { Say(L.P("搬家失敗：{0}", "迁移失败：{0}", "Migration failed: {0}", "移行に失敗しました：{0}", ex.Message)); MessageBox.Show(ex.Message, "FB2WordPress", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { try { Directory.Delete(temp, true); } catch { } cts.Dispose(); cts = null; ToggleBusy(false); }
    }

    async void NormalizeWordPressWhitespace(object? sender, EventArgs e)
    {
        if (cts is not null) return;
        if (string.IsNullOrWhiteSpace(settings.SiteUrl) && !await ConfigureFirstRunAsync()) return;
        if (MessageBox.Show(
            L.P("程式會檢查所有 WordPress 文章，只把 FB2WordPress 文字區塊中過多的連續空白行縮成一行。\n\n圖片、影片、日期、標籤、文章網址與一般 WordPress 內容都不會更動。過程可以安全停止並重新執行。要開始嗎？",
                "程序会检查所有 WordPress 文章，只将 FB2WordPress 文本区块中过多的连续空行缩减为一行。\n\n图片、视频、日期、标签、文章链接及一般 WordPress 内容均不会更改。过程可安全停止并重新执行。是否开始？",
                "The app will check every WordPress post and reduce excessive consecutive blank lines only inside FB2WordPress text blocks.\n\nImages, videos, dates, tags, post URLs, and ordinary WordPress content will not be changed. The process can be stopped safely and run again. Start now?",
                "すべての WordPress 記事を確認し、FB2WordPress のテキスト部分にある過剰な連続空行だけを1行に整えます。\n\n画像、動画、日付、タグ、記事 URL、通常の WordPress コンテンツは変更しません。処理は安全に停止して再実行できます。開始しますか？"),
            L.P("整理文章空白行", "整理文章空行", "Clean up post blank lines", "記事の空行を整理"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        cts = new(); ToggleBusy(true); whitespaceProgress.Value = 0;
        var changed = 0; var skipped = 0; var failed = 0; var total = 0;
        string backupPath = "";
        try
        {
            using var api = new GoogleApi(settings, Say, SettingsStore.SaveAsync); await api.EnsureAuthorizedAsync(cts.Token);
            whitespaceStatus.Text = L.P("正在分頁讀取 WordPress 文章…", "正在分页读取 WordPress 文章…", "Reading WordPress posts page by page…", "WordPress 記事をページごとに読み込んでいます…");
            var posts = await api.GetAllPostsAsync(settings.BlogId, cts.Token); total = posts.Count;

            var reportFolder = PlatformPaths.EnsureReportsDirectory();
            backupPath = Path.Combine(reportFolder, L.P("空白行整理備份-{0:yyyyMMdd-HHmmss}.jsonl", "空行整理备份-{0:yyyyMMdd-HHmmss}.jsonl", "blank-line-cleanup-backup-{0:yyyyMMdd-HHmmss}.jsonl", "空行整理バックアップ-{0:yyyyMMdd-HHmmss}.jsonl", DateTime.Now));
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
                    catch (Exception ex) { failed++; Say(L.P("文章「{0}」整理失敗，已保留原文：{1}", "文章“{0}”整理失败，已保留原文：{1}", "Could not clean up “{0}”; the original content was preserved: {1}", "記事「{0}」の整理に失敗しました。元の内容は保持されています：{1}", post.Title, ex.Message)); }
                }

                var done = i + 1;
                whitespaceProgress.Value = posts.Count == 0 ? 0 : Math.Clamp(done * 100 / posts.Count, 0, 100);
                whitespaceStatus.Text = L.P("正在處理 {0}/{1} 篇；已整理 {2}、不需處理 {3}、失敗 {4}", "正在处理 {0}/{1} 篇；已整理 {2}、无需处理 {3}、失败 {4}", "Processing {0}/{1}; cleaned {2}, unchanged {3}, failed {4}", "処理中 {0}/{1}；整理済み {2}、変更不要 {3}、失敗 {4}", done, posts.Count, changed, skipped, failed);
                Say(L.P("整理空白行 {0}/{1}", "整理空行 {0}/{1}", "Cleaning blank lines {0}/{1}", "空行を整理中 {0}/{1}", done, posts.Count));
            }

            whitespaceStatus.Text = L.P("整理完成：已整理 {0} 篇，不需處理 {1} 篇，失敗 {2} 篇。", "整理完成：已整理 {0} 篇，无需处理 {1} 篇，失败 {2} 篇。", "Cleanup complete: {0} cleaned, {1} unchanged, {2} failed.", "整理完了：整理済み {0} 件、変更不要 {1} 件、失敗 {2} 件。", changed, skipped, failed);
            MessageBox.Show(L.P("文章空白行整理完成。\n\n檢查：{0}\n已整理：{1}\n不需處理：{2}\n失敗：{3}\n\n更新前內容已備份到「文件\\FB2WordPress Reports」。",
                    "文章空行整理完成。\n\n检查：{0}\n已整理：{1}\n无需处理：{2}\n失败：{3}\n\n更新前内容已备份到“文档\\FB2WordPress Reports”。",
                    "Post blank-line cleanup is complete.\n\nChecked: {0}\nCleaned: {1}\nUnchanged: {2}\nFailed: {3}\n\nThe pre-update content was backed up in Documents\\FB2WordPress Reports.",
                    "記事の空行整理が完了しました。\n\n確認：{0}\n整理済み：{1}\n変更不要：{2}\n失敗：{3}\n\n更新前の内容は「ドキュメント\\FB2WordPress Reports」にバックアップされています。",
                    total, changed, skipped, failed),
                L.P("整理文章空白行", "整理文章空行", "Clean up post blank lines", "記事の空行を整理"), MessageBoxButtons.OK, failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            whitespaceStatus.Text = L.P("已安全停止。已整理 {0} 篇；下次重新按開始即可續跑。", "已安全停止。已整理 {0} 篇；下次重新点击开始即可继续。", "Stopped safely after cleaning {0} posts. Choose Start next time to resume.", "安全に停止しました。{0} 件を整理済みです。次回「開始」を押すと再開できます。", changed);
            MessageBox.Show(L.P("已安全停止。\n\n已整理：{0}\n不需處理：{1}\n失敗：{2}\n\n重新執行時，完成的文章會自動略過。",
                    "已安全停止。\n\n已整理：{0}\n无需处理：{1}\n失败：{2}\n\n重新执行时，已完成的文章会自动跳过。",
                    "Stopped safely.\n\nCleaned: {0}\nUnchanged: {1}\nFailed: {2}\n\nCompleted posts will be skipped automatically when the process runs again.",
                    "安全に停止しました。\n\n整理済み：{0}\n変更不要：{1}\n失敗：{2}\n\n再実行すると、完了済みの記事は自動的にスキップされます。",
                    changed, skipped, failed),
                L.P("整理文章空白行", "整理文章空行", "Clean up post blank lines", "記事の空行を整理"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            whitespaceStatus.Text = L.P("整理失敗：{0}", "整理失败：{0}", "Cleanup failed: {0}", "整理に失敗しました：{0}", ex.Message);
            MessageBox.Show(ex.Message, L.P("整理文章空白行", "整理文章空行", "Clean up post blank lines", "記事の空行を整理"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        throw last ?? new InvalidOperationException(L.P("WordPress 更新失敗。", "WordPress 更新失败。", "The WordPress update failed.", "WordPress の更新に失敗しました。"));
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
        if (MessageBox.Show(L.P("即將檢查所有 WordPress 文章圖片。只有壓縮後確實較小的圖片才會替換，過程可安全暫停。要開始嗎？", "即将检查所有 WordPress 文章图片。只有压缩后确实更小的图片才会替换，过程可安全暂停。是否开始？", "All images used by WordPress posts will be checked. An image will be replaced only when the optimized copy is genuinely smaller, and the process can be paused safely. Start now?", "WordPress 記事内のすべての画像を確認します。最適化後に実際に小さくなる画像だけを置き換え、処理は安全に一時停止できます。開始しますか？"), L.P("壓縮既有圖片", "压缩现有图片", "Optimize existing images", "既存の画像を最適化"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        cts = new(); ToggleBusy(true); var temp = Path.Combine(Path.GetTempPath(), "FB2WordPress", "opt-" + Guid.NewGuid().ToString("N"));
        var changed = 0; var skipped = 0; var failed = 0;
        try
        {
            Directory.CreateDirectory(temp); using var api = new GoogleApi(settings, Say, SettingsStore.SaveAsync); await api.EnsureAuthorizedAsync(cts.Token);
            var posts = await api.GetAllPostsAsync(settings.BlogId, cts.Token); var media = await api.GetMediaImagesAsync(cts.Token);
            using var downloader = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            for (var i = 0; i < media.Count; i++)
            {
                cts.Token.ThrowIfCancellationRequested(); var item = media[i];
                var affected = posts.Where(p => p.Content.Contains(item.SourceUrl, StringComparison.Ordinal)).ToList();
                if (affected.Count == 0 || !Uri.TryCreate(item.SourceUrl, UriKind.Absolute, out _)) { skipped++; continue; }
                try
                {
                    Say(L.P("檢查既有圖片 {0}/{1}：{2}", "检查现有图片 {0}/{1}：{2}", "Checking existing image {0}/{1}: {2}", "既存の画像を確認 {0}/{1}：{2}", i + 1, media.Count, item.Name));
                    var bytes = await downloader.GetByteArrayAsync(item.SourceUrl, cts.Token);
                    if (bytes.Length > 80 * 1024 * 1024) { Say(L.P("圖片超過 80 MB，為保護記憶體而略過。", "图片超过 80 MB，为保护内存而跳过。", "The image exceeds 80 MB and was skipped to protect memory.", "画像が 80 MB を超えているため、メモリを保護するためスキップしました。")); skipped++; continue; }
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
                    Say(L.P("已替換：{0}（{1} → {2}）", "已替换：{0}（{1} → {2}）", "Replaced: {0} ({1} → {2})", "置換済み：{0}（{1} → {2}）", item.Name, FormatBytes(optimized.OriginalBytes), FormatBytes(optimized.UploadBytes)));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { failed++; Say(L.P("圖片略過：{0}；{1}", "图片已跳过：{0}；{1}", "Image skipped: {0}; {1}", "画像をスキップ：{0}；{1}", item.Name, ex.Message)); }
                UpdateProgress(i + 1, media.Count, L.P("已檢查 {0}/{1}", "已检查 {0}/{1}", "Checked {0}/{1}", "確認済み {0}/{1}", i + 1, media.Count));
            }
            MessageBox.Show(L.P("圖片處理完成。\n\n已壓縮替換：{0}\n不需處理：{1}\n失敗但未破壞原圖：{2}", "图片处理完成。\n\n已压缩替换：{0}\n无需处理：{1}\n失败但未破坏原图：{2}", "Image processing is complete.\n\nOptimized and replaced: {0}\nUnchanged: {1}\nFailed without altering the original: {2}", "画像処理が完了しました。\n\n最適化して置換：{0}\n変更不要：{1}\n元画像を変更せず失敗：{2}", changed, skipped, failed), "FB2WordPress", MessageBoxButtons.OK, failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        catch (OperationCanceledException) { Say(L.P("已安全暫停圖片處理。", "已安全暂停图片处理。", "Image processing was paused safely.", "画像処理を安全に一時停止しました。")); }
        catch (Exception ex) { MessageBox.Show(ex.Message, L.P("圖片處理失敗", "图片处理失败", "Image processing failed", "画像処理に失敗"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { try { Directory.Delete(temp, true); } catch { } cts.Dispose(); cts = null; ToggleBusy(false); }
    }

    void ShowReport(MigrationReport r, bool stopped)
    {
        var state = stopped ? L.P("已停止", "已停止", "Stopped", "停止") : L.P("完成", "完成", "Completed", "完了");
        var text = L.P("FB2WordPress 搬家報告\r\n時間：{0:g} - {1:g}\r\n狀態：{2}\r\n總文章：{3}\r\n成功：{4}\r\n已完成略過：{5}\r\n失敗：{6}\r\n圖片：{7}\r\n影片：{8}\r\n", "FB2WordPress 迁移报告\r\n时间：{0:g} - {1:g}\r\n状态：{2}\r\n文章总数：{3}\r\n成功：{4}\r\n已完成并跳过：{5}\r\n失败：{6}\r\n图片：{7}\r\n视频：{8}\r\n", "FB2WordPress migration report\r\nTime: {0:g} - {1:g}\r\nStatus: {2}\r\nTotal posts: {3}\r\nSucceeded: {4}\r\nCompleted and skipped: {5}\r\nFailed: {6}\r\nImages: {7}\r\nVideos: {8}\r\n", "FB2WordPress 移行レポート\r\n時間：{0:g} - {1:g}\r\n状態：{2}\r\n記事総数：{3}\r\n成功：{4}\r\n完了済みのためスキップ：{5}\r\n失敗：{6}\r\n画像：{7}\r\n動画：{8}\r\n", r.Started, DateTime.Now, state, r.Total, r.Imported, r.Skipped, r.Failed, r.Images, r.Videos) + (r.Errors.Count > 0 ? L.P("\r\n失敗明細：\r\n", "\r\n失败明细：\r\n", "\r\nFailure details:\r\n", "\r\n失敗の詳細：\r\n") + string.Join("\r\n", r.Errors) : "");
        var reportNote = L.P("報告未能寫入，但搬家進度已安全保存。", "报告无法写入，但迁移进度已安全保存。", "The report could not be written, but migration progress was saved safely.", "レポートを書き込めませんでしたが、移行の進捗は安全に保存されています。");
        try
        {
            var folder = PlatformPaths.EnsureReportsDirectory();
            Directory.CreateDirectory(folder); var path = Path.Combine(folder, L.P("搬家報告-{0:yyyyMMdd-HHmmss}.txt", "迁移报告-{0:yyyyMMdd-HHmmss}.txt", "migration-report-{0:yyyyMMdd-HHmmss}.txt", "移行レポート-{0:yyyyMMdd-HHmmss}.txt", DateTime.Now));
            File.WriteAllText(path, text, Encoding.UTF8); reportNote = L.P("報告已保存到「文件\\FB2WordPress Reports」。", "报告已保存到“文档\\FB2WordPress Reports”。", "The report was saved in Documents\\FB2WordPress Reports.", "レポートを「ドキュメント\\FB2WordPress Reports」に保存しました。");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        Say(stopped ? L.P("搬家已停止。{0}", "迁移已停止。{0}", "Migration stopped. {0}", "移行を停止しました。{0}", reportNote) : L.P("搬家完成：成功 {0}，略過 {1}，失敗 {2}。{3}", "迁移完成：成功 {0}，跳过 {1}，失败 {2}。{3}", "Migration complete: {0} succeeded, {1} skipped, {2} failed. {3}", "移行完了：成功 {0}、スキップ {1}、失敗 {2}。{3}", r.Imported, r.Skipped, r.Failed, reportNote));
        MessageBox.Show(L.P("{0}\n\n成功：{1}\n已完成略過：{2}\n失敗：{3}\n圖片：{4}\n影片：{5}\n\n{6}", "{0}\n\n成功：{1}\n已完成并跳过：{2}\n失败：{3}\n图片：{4}\n视频：{5}\n\n{6}", "{0}\n\nSucceeded: {1}\nCompleted and skipped: {2}\nFailed: {3}\nImages: {4}\nVideos: {5}\n\n{6}", "{0}\n\n成功：{1}\n完了済みのためスキップ：{2}\n失敗：{3}\n画像：{4}\n動画：{5}\n\n{6}", stopped ? L.P("已停止", "已停止", "Stopped", "停止") : L.P("搬家完成", "迁移完成", "Migration complete", "移行完了"), r.Imported, r.Skipped, r.Failed, r.Images, r.Videos, reportNote), L.P("FB2WordPress 搬家報告", "FB2WordPress 迁移报告", "FB2WordPress migration report", "FB2WordPress 移行レポート"), MessageBoxButtons.OK, r.Failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
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
        if (zip.Entries.Count > 250_000) throw new InvalidDataException(L.P("ZIP 內檔案數異常過多，為保護電腦已停止解壓縮。", "ZIP 内文件数量异常过多，为保护电脑已停止解压。", "The ZIP contains an unusually large number of files. Extraction stopped to protect the computer.", "ZIP 内のファイル数が異常に多いため、パソコンを保護するため展開を停止しました。"));

        long totalBytes = 0;
        foreach (var entry in zip.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.FullName.Length > 1024) throw new InvalidDataException(L.P("ZIP 包含異常過長的檔名。", "ZIP 包含异常过长的文件名。", "The ZIP contains an unusually long file name.", "ZIP に異常に長いファイル名が含まれています。"));
            try { totalBytes = checked(totalBytes + entry.Length); }
            catch (OverflowException) { throw new InvalidDataException(L.P("ZIP 宣告的容量異常。", "ZIP 声明的容量异常。", "The ZIP declares an invalid size.", "ZIP に記録された容量が不正です。")); }
        }

        var archiveBytes = Math.Max(1, new FileInfo(archive).Length);
        if (totalBytes > 10L * 1024 * 1024 * 1024 && totalBytes / archiveBytes > 500)
            throw new InvalidDataException(L.P("ZIP 壓縮比例異常，可能是會塞滿硬碟的惡意壓縮檔。", "ZIP 压缩比异常，可能是会占满硬盘的恶意压缩文件。", "The ZIP has an abnormal compression ratio and may be a malicious archive that could fill the disk.", "ZIP の圧縮率が異常です。ディスクを埋め尽くす悪意ある圧縮ファイルの可能性があります。"));
        var driveRoot = Path.GetPathRoot(root) ?? root;
        var available = new DriveInfo(driveRoot).AvailableFreeSpace;
        var reserve = 2L * 1024 * 1024 * 1024;
        if (totalBytes > Math.Max(0, available - reserve))
            throw new IOException(L.P("系統碟空間不足。解壓縮約需 {0}，目前可安全使用約 {1}。", "系统盘空间不足。解压约需 {0}，当前可安全使用约 {1}。", "The system drive does not have enough space. Extraction needs about {0}; approximately {1} is safely available.", "システムドライブの空き容量が不足しています。展開には約 {0} 必要で、安全に使用できる容量は約 {1} です。", FormatBytes(totalBytes), FormatBytes(Math.Max(0, available - reserve))));

        foreach (var entry in zip.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(Path.Combine(target, entry.FullName));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(L.P("ZIP 包含不安全的路徑。", "ZIP 包含不安全的路径。", "The ZIP contains an unsafe path.", "ZIP に安全でないパスが含まれています。"));
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
