using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace FB2WordPress.Desktop;

internal sealed class MainWindow : Window
{
    static readonly IBrush Navy = Brush.Parse("#18334A");
    static readonly IBrush Muted = Brush.Parse("#5D6A75");
    static readonly IBrush Surface = Brush.Parse("#F5F8FB");
    bool changingLanguage;

    public MainWindow()
    {
        Width = 980;
        Height = 700;
        MinWidth = 720;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = BuildContent();
    }

    Control BuildContent()
    {
        Title = L.P("FB2WordPress 跨平台桌面基礎", "FB2WordPress 跨平台桌面基础", "FB2WordPress cross-platform desktop foundation", "FB2WordPress クロスプラットフォーム基盤");
        var language = new ComboBox
        {
            ItemsSource = L.Supported,
            SelectedItem = L.Supported.First(item => item.Code == L.Language),
            MinWidth = 190,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        language.SelectionChanged += (_, _) =>
        {
            if (changingLanguage || language.SelectedItem is not LanguageOption selected || selected.Code == L.Language) return;
            changingLanguage = true;
            L.Configure(selected.Code);
            Content = BuildContent();
            changingLanguage = false;
        };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 24 };
        header.Children.Add(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "FB2WordPress", FontSize = 32, FontWeight = FontWeight.Bold, Foreground = Navy },
                new TextBlock
                {
                    Text = L.P("安全遷移預覽｜尚非完整發行版", "安全迁移预览｜尚非完整发行版", "Safe migration preview | not a complete release", "安全な移行プレビュー｜完全版ではありません"),
                    FontSize = 14,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brush.Parse("#A34662")
                }
            }
        });
        Grid.SetColumn(language, 1);
        header.Children.Add(language);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("2*,3*"),
            ColumnSpacing = 24,
            RowDefinitions = new RowDefinitions("Auto")
        };
        body.Children.Add(Card(
            L.P("先把共用核心打穩", "先夯实共享核心", "A dependable shared core first", "まず共有コアを堅牢に"),
            L.P(
                "Facebook 匯出解析、WordPress REST 與媒體流程、搬家進度及四語目錄已能獨立於 Windows 介面建置與測試。這個視窗是 macOS 與 Linux 桌面版的最小入口，不會假裝尚未移植的功能已經完成。",
                "Facebook 导出解析、WordPress REST 与媒体流程、迁移进度及四语目录现已可独立于 Windows 界面构建和测试。此窗口是 macOS 与 Linux 桌面版的最小入口，不会将尚未移植的功能冒充为已完成。",
                "Facebook export parsing, WordPress REST and media flows, migration state, and the four-language catalog now build and test independently of the Windows UI. This window is the minimum macOS/Linux entry point and does not present unfinished ports as complete.",
                "Facebook エクスポート解析、WordPress REST・メディア処理、移行状態、4言語カタログは Windows UI から独立してビルド・テストできます。この画面は macOS／Linux 版の最小入口であり、未移植の機能を完成済みとして表示しません。")));
        var status = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                Card(
                    L.P("已完成：可跨平台核心", "已完成：跨平台核心", "Ready: cross-platform core", "完了：クロスプラットフォーム・コア"),
                    L.P("Facebook ZIP 解析、WordPress 文章與媒體 API、YouTube OAuth、搬家狀態與報告路徑已抽離成共用元件。", "Facebook ZIP 解析、WordPress 文章与媒体 API、YouTube OAuth、迁移状态和报告路径已拆分为共享组件。", "Facebook ZIP parsing, WordPress post/media APIs, YouTube OAuth, migration state, and report paths are separated into shared components.", "Facebook ZIP 解析、WordPress 投稿・メディア API、YouTube OAuth、移行状態、レポート保存先を共有コンポーネントへ分離しました。")),
                Card(
                    L.P("Windows 正式版保持原樣", "Windows 正式版保持不变", "The Windows release stays intact", "Windows 正式版は従来どおり"),
                    L.P("現有 WinForms 操作、DPAPI 加密、圖片壓縮與單一 EXE 發行流程仍由 Windows 專案負責，沒有刪除或降低任何功能。", "现有 WinForms 操作、DPAPI 加密、图片压缩及单一 EXE 发布流程仍由 Windows 项目负责，未删除或削弱任何功能。", "The existing WinForms workflow, DPAPI encryption, image optimization, and single-EXE release remain in the Windows project without removing or weakening features.", "既存の WinForms 操作、DPAPI 暗号化、画像最適化、単一 EXE 配布は Windows プロジェクトで維持し、機能を削除・弱体化していません。")),
                Card(
                    L.P("下一階段：作業系統專屬整合", "下一阶段：操作系统专属集成", "Next: OS-specific integration", "次の段階：OS 固有の統合"),
                    L.P("macOS Keychain、Linux Secret Service、跨平台圖片處理、檔案挑選與完整搬家畫面仍需實作及真實裝置驗證；未完成前不會發布成正式相容版。", "macOS Keychain、Linux Secret Service、跨平台图片处理、文件选择与完整迁移界面仍需实现并在真实设备上验证；完成前不会作为正式兼容版发布。", "macOS Keychain, Linux Secret Service, cross-platform image processing, file pickers, and the complete migration UI still require implementation and real-device validation. They will not be released as fully compatible beforehand.", "macOS Keychain、Linux Secret Service、クロスプラットフォーム画像処理、ファイル選択、完全な移行 UI は今後の実装と実機検証が必要です。完了前に正式対応版として公開しません。"))
            }
        };
        Grid.SetColumn(status, 1);
        body.Children.Add(status);

        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(36),
                Spacing = 26,
                MaxWidth = 1120,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Children =
                {
                    header,
                    new TextBlock { Text = L.P("目前執行平台：{0}", "当前运行平台：{0}", "Current runtime platform: {0}", "現在の実行環境：{0}", PlatformName()), Foreground = Muted, FontSize = 14 },
                    body,
                    new TextBlock { Text = L.P("預定的本機資料位置：{0}", "预定的本地数据位置：{0}", "Planned local data location: {0}", "予定しているローカルデータ保存先：{0}", PlatformPaths.LocalDataDirectory), Foreground = Muted, TextWrapping = TextWrapping.Wrap }
                }
            }
        };
    }

    static Border Card(string title, string description) => new()
    {
        Background = Surface,
        BorderBrush = Brush.Parse("#D7E1E8"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(18),
        Padding = new Thickness(24),
        Child = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeight.SemiBold, Foreground = Navy, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = description, FontSize = 15, Foreground = Muted, TextWrapping = TextWrapping.Wrap, LineHeight = 24 }
            }
        }
    };

    static string PlatformName() =>
        OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : OperatingSystem.IsLinux() ? "Linux" : Environment.OSVersion.Platform.ToString();
}
