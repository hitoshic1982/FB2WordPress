using System.Globalization;

namespace FB2WordPress;

public sealed record LanguageOption(string Code, string Name)
{
    public override string ToString() => Name;
}

public static class L
{
    public static readonly LanguageOption[] Supported =
    [
        new("zh-TW", "繁體中文"), new("zh-CN", "简体中文"), new("en", "English"), new("ja", "日本語")
    ];

    static readonly IReadOnlyDictionary<string, string[]> Texts = Build();
    public static string Language { get; private set; } = Detect();
    static int Index => Array.FindIndex(Supported, item => item.Code == Language) is var index && index >= 0 ? index : 2;

    public static void Configure(string? language)
    {
        Language = Supported.Any(item => item.Code == language) ? language! : Detect();
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(Language);
    }

    public static string T(string key, params object[] args)
    {
        var value = Texts.TryGetValue(key, out var choices) ? choices[Index] : key;
        return args.Length == 0 ? value : string.Format(CultureInfo.CurrentCulture, value, args);
    }

    public static string P(string zhTw, string zhCn, string english, string japanese, params object[] args)
    {
        var value = new[] { zhTw, zhCn, english, japanese }[Index];
        return args.Length == 0 ? value : string.Format(CultureInfo.CurrentCulture, value, args);
    }

    public static IReadOnlyList<string> SupportedCodes => Supported.Select(item => item.Code).ToArray();
    public static IReadOnlyCollection<string> Keys => Texts.Keys.ToArray();

    static string Detect()
    {
        var name = CultureInfo.CurrentUICulture.Name;
        if (name.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return "ja";
        if (name.Equals("zh-CN", StringComparison.OrdinalIgnoreCase) || name.Equals("zh-SG", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
        if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh-TW";
        return "en";
    }

    static IReadOnlyDictionary<string, string[]> Build() => new Dictionary<string, string[]>
    {
        ["setup_title"] = ["FB2WordPress 連線設定", "FB2WordPress 连接设置", "FB2WordPress Connection Setup", "FB2WordPress 接続設定"],
        ["language"] = ["介面語言：", "界面语言：", "Interface language:", "表示言語："],
        ["language_restart"] = ["儲存後會以所選語言重新啟動。", "保存后将以所选语言重新启动。", "The app will restart in the selected language after saving.", "保存後、選択した言語で再起動します。"],
        ["site_placeholder"] = ["https://你的網域", "https://你的域名", "https://your-domain.example", "https://あなたのドメイン"],
        ["user_placeholder"] = ["WordPress 使用者名稱", "WordPress 用户名", "WordPress username", "WordPress ユーザー名"],
        ["password_placeholder"] = ["WordPress 應用程式密碼", "WordPress 应用程序密码", "WordPress application password", "WordPress アプリケーションパスワード"],
        ["google_client_placeholder"] = ["Google OAuth Desktop Client ID（上傳影片用）", "Google OAuth Desktop Client ID（用于上传视频）", "Google OAuth Desktop Client ID (for video uploads)", "Google OAuth Desktop Client ID（動画アップロード用）"],
        ["google_secret_placeholder"] = ["Google Client Secret（如有）", "Google Client Secret（如有）", "Google Client Secret (if provided)", "Google Client Secret（ある場合）"],
        ["draft_default"] = ["文章先存為草稿", "文章先保存为草稿", "Save posts as drafts first", "記事を先に下書きとして保存"],
        ["privacy_private"] = ["不公開", "不公开", "Private", "非公開"],
        ["privacy_unlisted"] = ["知道連結者可看", "知道链接者可见", "Unlisted", "限定公開"],
        ["privacy_public"] = ["公開", "公开", "Public", "公開"],
        ["setup_note"] = ["第一次設定：在 WordPress 後台 → 使用者 → 個人資料 → 應用程式密碼，建立名稱 FB2WordPress。\r\n\r\n請貼入應用程式密碼，不要填 WordPress 登入密碼。", "初次设置：在 WordPress 后台 → 用户 → 个人资料 → 应用程序密码，创建名为 FB2WordPress 的密码。\r\n\r\n请粘贴应用程序密码，不要填写 WordPress 登录密码。", "First-time setup: in WordPress Admin, open Users → Profile → Application Passwords and create one named FB2WordPress.\r\n\r\nPaste the application password, not your WordPress sign-in password.", "初回設定：WordPress 管理画面の「ユーザー」→「プロフィール」→「アプリケーションパスワード」で FB2WordPress という名前のパスワードを作成します。\r\n\r\nWordPress のログインパスワードではなく、アプリケーションパスワードを貼り付けてください。"],
        ["save_test"] = ["測試連線並儲存", "测试连接并保存", "Test connection and save", "接続をテストして保存"],
        ["cancel"] = ["取消", "取消", "Cancel", "キャンセル"],
        ["connection_required"] = ["請填入 HTTPS 網站網址、WordPress 使用者名稱及應用程式密碼。", "请填写 HTTPS 网站地址、WordPress 用户名及应用程序密码。", "Enter an HTTPS site address, WordPress username, and application password.", "HTTPS のサイトURL、WordPress ユーザー名、アプリケーションパスワードを入力してください。"],
        ["zip_empty"] = ["尚未選擇 Facebook ZIP", "尚未选择 Facebook ZIP", "No Facebook ZIP selected", "Facebook ZIP が選択されていません"],
        ["choose_zip"] = ["1  選擇 Facebook ZIP", "1  选择 Facebook ZIP", "1  Choose Facebook ZIP", "1  Facebook ZIP を選択"],
        ["start_move"] = ["2  開始搬家", "2  开始迁移", "2  Start migration", "2  移行を開始"],
        ["settings"] = ["設定", "设置", "Settings", "設定"],
        ["pause_move"] = ["暫停搬家（可續跑）", "暂停迁移（可继续）", "Pause migration (resumable)", "移行を一時停止（再開可能）"],
        ["pause_publish"] = ["暫停發布", "暂停发布", "Pause publishing", "公開を一時停止"],
        ["ready"] = ["準備就緒", "准备就绪", "Ready", "準備完了"],
        ["tab_move"] = ["Facebook 搬家", "Facebook 迁移", "Facebook Migration", "Facebook 移行"],
        ["tab_compose"] = ["發布新文章", "发布新文章", "New Post", "新規記事"],
        ["tab_optimize"] = ["壓縮既有圖片", "压缩现有图片", "Optimize Existing Images", "既存画像を最適化"],
        ["tab_whitespace"] = ["整理文章空白行", "整理文章空白行", "Clean Up Blank Lines", "記事の空行を整理"],
        ["compose_heading"] = ["撰寫 WordPress 新文章", "撰写 WordPress 新文章", "Write a new WordPress post", "WordPress の新規記事を作成"],
        ["media_heading"] = ["圖片與影片（可多選）", "图片与视频（可多选）", "Images and videos (multiple allowed)", "画像と動画（複数選択可）"],
        ["choose_media"] = ["選擇圖片或影片…", "选择图片或视频…", "Choose images or videos…", "画像または動画を選択…"],
        ["remove_media"] = ["移除選取項目", "移除所选项目", "Remove selected", "選択項目を削除"],
        ["publish_wordpress"] = ["發布文章到 WordPress", "发布文章到 WordPress", "Publish to WordPress", "WordPress に公開"],
        ["save_draft"] = ["先存成草稿", "先保存为草稿", "Save as draft first", "先に下書きとして保存"],
        ["compose_title_placeholder"] = ["文章標題（可留空，自動取內容第一行）", "文章标题（可留空，自动采用内容第一行）", "Post title (optional; uses the first content line)", "記事タイトル（空欄時は本文の先頭行を使用）"],
        ["compose_body_placeholder"] = ["在這裡輸入文章內容；#Hashtag 會自動成為 WordPress 標籤", "在此输入文章内容；#Hashtag 会自动成为 WordPress 标签", "Write your post here; #hashtags become WordPress tags", "記事本文を入力；#Hashtag は WordPress のタグになります"],
        ["composer_ready"] = ["準備就緒。圖片會上傳 WordPress 媒體庫；影片會上傳 YouTube。", "准备就绪。图片会上传到 WordPress 媒体库；视频会上传到 YouTube。", "Ready. Images go to the WordPress Media Library; videos go to YouTube.", "準備完了。画像は WordPress メディアライブラリ、動画は YouTube にアップロードされます。"],
        ["optimize_images"] = ["壓縮並替換既有文章圖片", "压缩并替换现有文章图片", "Optimize and replace existing post images", "既存記事の画像を最適化して置換"],
        ["optimize_note"] = ["程式會建立較小的新圖片、更新文章中的圖片網址，再刪除已成功替換的舊檔。已經夠小的圖片不會變動。", "程序会创建更小的新图片、更新文章中的图片地址，再删除已成功替换的旧文件。已经足够小的图片不会变动。", "The app creates a smaller replacement, updates post image URLs, then deletes the old file only after a successful replacement. Images already small enough are unchanged.", "より小さい画像を作成し、記事内のURLを更新してから、置換に成功した旧ファイルのみ削除します。十分に小さい画像は変更しません。"],
        ["normalize_whitespace"] = ["整理全部文章空白行", "整理全部文章空白行", "Clean up blank lines in all posts", "全記事の空行を整理"],
        ["safe_stop"] = ["安全停止", "安全停止", "Stop safely", "安全に停止"],
        ["whitespace_note"] = ["將連續兩行以上的空白縮成一行；圖片、影片、日期、標籤與網址都不會更動。", "将连续两行以上的空白缩成一行；图片、视频、日期、标签与网址均不会更改。", "Collapses multiple blank lines to one without changing images, videos, dates, tags, or URLs.", "連続する複数の空行を1行にまとめます。画像、動画、日付、タグ、URLは変更しません。"],
        ["media_dialog_title"] = ["選擇圖片或影片", "选择图片或视频", "Choose images or videos", "画像または動画を選択"],
        ["media_filter"] = ["圖片或影片|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.bmp;*.mp4;*.mov;*.m4v;*.avi;*.mkv;*.webm|所有檔案|*.*", "图片或视频|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.bmp;*.mp4;*.mov;*.m4v;*.avi;*.mkv;*.webm|所有文件|*.*", "Images or videos|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.bmp;*.mp4;*.mov;*.m4v;*.avi;*.mkv;*.webm|All files|*.*", "画像または動画|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.bmp;*.mp4;*.mov;*.m4v;*.avi;*.mkv;*.webm|すべてのファイル|*.*"],
        ["configured_site"] = ["已設定：{0}", "已设置：{0}", "Configured: {0}", "設定済み：{0}"],
        ["content_required"] = ["請輸入文章內容，或至少選擇一個圖片／影片。", "请输入文章内容，或至少选择一张图片／一个视频。", "Write some content or choose at least one image or video.", "本文を入力するか、画像または動画を1件以上選択してください。"],
        ["new_post_title"] = ["新文章 {0}", "新文章 {0}", "New post {0}", "新規記事 {0}"],
        ["checking_previous_publish"] = ["正在確認上次發布是否其實已經成功…", "正在确认上次发布是否已成功…", "Checking whether the previous publish actually succeeded…", "前回の公開が成功していたか確認しています…"],
        ["duplicate_avoided"] = ["上次發布其實已成功，已避免建立重複文章。", "上次发布实际已成功，已避免创建重复文章。", "The previous publish succeeded; a duplicate post was avoided.", "前回の公開は成功していました。重複記事の作成を防ぎました。"],
        ["post_already_exists"] = ["文章已經存在於 WordPress，程式沒有重複發布。", "文章已存在于 WordPress，程序未重复发布。", "The post already exists in WordPress, so it was not published again.", "記事はすでに WordPress に存在するため、重複公開しませんでした。"],
        ["checking_youtube"] = ["正在檢查 YouTube 影片，避免重複…", "正在检查 YouTube 视频，避免重复…", "Checking YouTube to avoid duplicate uploads…", "重複アップロードを防ぐため YouTube を確認しています…"],
        ["media_not_found"] = ["找不到選取的媒體檔案。", "找不到所选媒体文件。", "The selected media file was not found.", "選択したメディアファイルが見つかりません。"],
        ["reuse_youtube"] = ["沿用既有 YouTube 影片：{0}", "沿用现有 YouTube 视频：{0}", "Reusing an existing YouTube video: {0}", "既存の YouTube 動画を再利用：{0}"],
        ["uploading_video"] = ["正在上傳影片：{0}", "正在上传视频：{0}", "Uploading video: {0}", "動画をアップロード中：{0}"],
        ["uploading_image"] = ["正在智慧壓縮並上傳圖片：{0}", "正在智能压缩并上传图片：{0}", "Optimizing and uploading image: {0}", "画像を最適化してアップロード中：{0}"],
        ["reuse_media"] = ["沿用本次先前已上傳的媒體：{0}", "沿用本次先前已上传的媒体：{0}", "Reusing media uploaded earlier in this run: {0}", "今回すでにアップロードしたメディアを再利用：{0}"],
        ["image_alt"] = ["文章圖片", "文章图片", "Post image", "記事画像"],
        ["saving_draft"] = ["正在儲存 WordPress 草稿…", "正在保存 WordPress 草稿…", "Saving WordPress draft…", "WordPress の下書きを保存中…"],
        ["publishing_post"] = ["正在發布 WordPress 文章…", "正在发布 WordPress 文章…", "Publishing to WordPress…", "WordPress に公開中…"],
        ["draft_saved"] = ["文章已存成 WordPress 草稿。", "文章已保存为 WordPress 草稿。", "The post was saved as a WordPress draft.", "記事を WordPress の下書きとして保存しました。"],
        ["post_published"] = ["文章已成功發布到 WordPress。", "文章已成功发布到 WordPress。", "The post was published to WordPress.", "記事を WordPress に公開しました。"],
        ["publish_paused"] = ["發布已暫停；本次已上傳的媒體會暫時保留，按發布可再次嘗試。", "发布已暂停；本次已上传的媒体会暂时保留，可再次点击发布重试。", "Publishing paused. Uploaded media is retained temporarily; choose Publish to try again.", "公開を一時停止しました。アップロード済みメディアは一時保持され、再度公開を選ぶと再試行できます。"],
        ["quota_limit"] = ["Google 配額限制", "Google 配额限制", "Google quota limit", "Google の割り当て制限"],
        ["publish_failed"] = ["發布失敗", "发布失败", "Publishing failed", "公開に失敗"],
        ["publish_failed_detail"] = ["發布失敗：{0}", "发布失败：{0}", "Publishing failed: {0}", "公開に失敗しました：{0}"],
        ["image_reduced"] = ["圖片已縮小：{0} → {1}", "图片已缩小：{0} → {1}", "Image reduced: {0} → {1}", "画像を縮小：{0} → {1}"],
        ["zip_filter"] = ["Facebook ZIP 檔案 (*.zip)|*.zip", "Facebook ZIP 文件 (*.zip)|*.zip", "Facebook ZIP files (*.zip)|*.zip", "Facebook ZIP ファイル (*.zip)|*.zip"],
        ["choose_export_zip"] = ["選擇 Facebook 匯出的 ZIP", "选择 Facebook 导出的 ZIP", "Choose the ZIP exported by Facebook", "Facebook から書き出した ZIP を選択"],
        ["zip_selected"] = ["ZIP 已選好，按「開始搬家」。", "ZIP 已选择，请点击“开始迁移”。", "ZIP selected. Choose Start migration.", "ZIP を選択しました。「移行を開始」を押してください。"],
        ["pausing_safely"] = ["正在安全暫停，請稍候…", "正在安全暂停，请稍候…", "Pausing safely. Please wait…", "安全に一時停止しています。しばらくお待ちください…"],
        ["pause_requested"] = ["已要求暫停；正在保存目前進度…", "已请求暂停；正在保存当前进度…", "Pause requested; saving current progress…", "一時停止を受け付けました。現在の進捗を保存しています…"],
        ["wait_before_close"] = ["正在安全暫停並保存進度。畫面恢復可操作後，再關閉程式。", "正在安全暂停并保存进度。界面恢复可操作后再关闭程序。", "The app is pausing safely and saving progress. Close it only after the window becomes responsive again.", "安全に一時停止して進捗を保存しています。画面が操作可能になってから終了してください。"],
        ["discard_unpublished"] = ["「發布新文章」中還有尚未發布的內容。確定要關閉並捨棄嗎？", "“发布新文章”中仍有未发布内容。确定关闭并舍弃吗？", "The New Post tab contains unpublished content. Close and discard it?", "「新規記事」に未公開の内容があります。破棄して終了しますか？"],
        ["unpublished"] = ["尚未發布", "尚未发布", "Unpublished content", "未公開の内容"],
        ["unexpected_error"] = ["程式遇到非預期問題，已保存錯誤報告：\n{0}\n\n{1}", "程序遇到意外问题，已保存错误报告：\n{0}\n\n{1}", "The app encountered an unexpected problem. An error report was saved at:\n{0}\n\n{1}", "予期しない問題が発生しました。エラー記録を保存しました：\n{0}\n\n{1}"],
        ["error_file"] = ["錯誤-{0}.txt", "错误-{0}.txt", "error-{0}.txt", "エラー-{0}.txt"],
        ["error_log_unavailable"] = ["無法寫入錯誤報告", "无法写入错误报告", "Unable to write the error report", "エラー記録を書き込めません"],
        ["language_changed"] = ["介面語言已更新，程式將重新啟動以完整套用。", "界面语言已更新，程序将重新启动以完整应用。", "The interface language was updated. The app will restart to apply it completely.", "表示言語を更新しました。完全に適用するため再起動します。"],
        ["single_instance"] = ["FB2WordPress 已經在執行，請回到原本的視窗。", "FB2WordPress 已在运行，请返回原窗口。", "FB2WordPress is already running. Return to the existing window.", "FB2WordPress はすでに実行中です。開いているウィンドウに戻ってください。"]
    };
}
