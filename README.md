# FB2WordPress

[繁體中文](README.md) · [简体中文](README.zh-CN.md) · [English](README.en.md) · [日本語](README.ja.md)

把 Facebook 官方下載資料中的貼文、圖片與影片，整理並移轉到自架 WordPress 網站的 Windows 桌面工具。

> 社群平台適合接觸讀者，自己的網站才是內容長期落腳的家。FB2WordPress 協助創作者把流量背後真正重要的文章與媒體，轉化為自己掌控的數位資產。

## 網站才是品牌總部

2026 年 7 月，炎劍文化工作室的 Facebook 粉絲專頁突然遭到停權。與其繼續把時間耗在沒有明確回應的申訴流程，作者選擇在 Bluehost 架起 WordPress 主站，將文章、圖片、搜尋能見度與品牌入口重新掌握在自己手上。FB2WordPress 是這場三日重建行動中，負責把舊內容送回品牌總部的核心工具。

它讀取你本人透過 Facebook「下載你的資訊」取得的 ZIP，並使用 WordPress 官方 REST API 將內容送到你自己的網站。它不會登入、爬取或繞過 Facebook，也無法恢復遭停權的帳號或粉絲專頁。

## 從社群備份走向可經營的網站

- 解析 Facebook 官方匯出的貼文 JSON，包括新版 UTF-8 與部分舊版亂碼格式。
- 保留文章時間、文字、Emoji、Hashtag，並將 Hashtag 建立為 WordPress 標籤。
- 將圖片安全最佳化後上傳 WordPress 媒體庫；影片可選擇上傳 YouTube 並嵌入文章。
- 可先建立為草稿，讓你逐篇檢查後再公開。
- 以隱藏識別碼避免同一篇文章重複匯入。
- 記錄每篇移轉進度；中斷後可安全接續，損壞的進度檔可由備份復原。
- 只整理本工具匯入文章的多餘空白，不任意改動網站其他文章。
- 防止惡意 ZIP 路徑穿越，不會修改原始 Facebook ZIP 或原圖。

## 開始前，先保護你的網站

1. Windows 10/11 x64。
2. 自己管理且已啟用 HTTPS 與 WordPress REST API 的 WordPress 網站。
3. 在 WordPress 個人資料頁建立「應用程式密碼」，不要把主登入密碼交給程式。
4. 從 Facebook 下載自己的資料，建議選 JSON 格式並包含貼文、相片與影片。
5. 只有需要將影片上傳 YouTube 時，才需另建 Google Desktop OAuth Client 並啟用 YouTube Data API v3。
6. 從 [GitHub Releases](https://github.com/hitoshic1982/FB2WordPress/releases/latest) 下載最新版 `FB2WordPress.exe`，並核對 SHA256。

## 第一次搬家建議這樣做

1. 啟動程式，輸入你的 WordPress 網址、使用者名稱與專用的應用程式密碼。
2. 選擇 Facebook ZIP。
3. 選擇公開／草稿模式；首次操作建議選草稿。
4. 開始移轉，完成後查看報告並抽查文章、標籤、圖片、影片與日期。

請先備份 WordPress，並用測試站或草稿模式驗證。主機限制、REST API 防火牆、Facebook 匯出格式差異及大量媒體上傳，都可能需要分批處理。

## 你的網站帳密如何被處理

- WordPress 應用程式密碼、OAuth 憑證與更新權杖只存放在目前 Windows 使用者的 LocalAppData，並以 Windows DPAPI 加密。
- 專案不附帶作者的網站帳密、Client ID、Client Secret、Token、Facebook 匯出資料或文章資料庫。
- 軟體直接與你的 WordPress 和選用的 Google API 溝通；炎劍文化工作室不架設中繼伺服器收取你的內容。
- 建議為本工具建立可撤銷的專用 WordPress 應用程式密碼，完成後可立即撤銷。

詳見 [PRIVACY.md](PRIVACY.md) 與 [SECURITY.md](SECURITY.md)。

## 開發者入口

```powershell
dotnet build src/FB2WordPress/FB2WordPress.csproj -c Release
dotnet run --project tests/AuditHarness/WordPressAuditHarness.csproj -c Release
dotnet publish src/FB2WordPress/FB2WordPress.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts
```

需要 .NET 10 SDK。正式 Release 提供自含式單一 EXE，使用者不必另裝 .NET Runtime。

## 開源與責任界線

本專案採 [MIT License](LICENSE)。歡迎檢查原始碼、回報問題及提交 PR。

這是獨立開源工具，與 Meta、Facebook、Automattic、WordPress Foundation、Google 或 YouTube 無隸屬或背書關係。請只移轉你有權處理的內容，並遵守各平台條款、著作權與個資法規。

作者：CHOU MING HUA／炎劍文化工作室 · [官方網站](https://www.flamebladestudio.com.tw/)
