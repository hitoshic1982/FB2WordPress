# FB2WordPress v1.1.0-rc.1 Preview release notes

> Windows remains the complete product. The macOS and Linux downloads are clearly bounded Preview migration entry points.

## 繁體中文｜第一次把可啟動的入口交到三個平台手上

這次候選版本保留 Windows 完整搬家版，並首次提供真正的 macOS x64 未簽章 DMG 與 Linux x86_64 AppImage。兩個 Preview 都能開啟四語 Avalonia 介面，但完整 Facebook ZIP 搬家、圖片處理、系統憑證保管庫及 WordPress 發布流程仍只在 Windows 完整版可用。

DMG 與 AppImage 均在相同作業系統的 GitHub 原生 runner 製作，CI 會從最終成品啟動程式並確認至少存活六秒。下載內容另附 SHA256 與 SPDX SBOM；非 PR 建置在 GitHub 支援時也會建立來源及 SBOM 證明。這是雲端建置與啟動證據，不是作者持有 macOS／Linux 實機的驗證，也不是正式相容承諾。

macOS 未購買 Apple 開發者簽章；首次使用請按住 Control 點選 `.app` 後選擇「打開」，或到「隱私權與安全性」允許開啟。DMG 目前是 Intel x64 版；Apple Silicon 可能需要 Rosetta 2，尚未驗證。Linux 下載後請先執行 `chmod +x FB2WordPress-*-Preview.AppImage`。只有在理解上述限制並願意回報環境與結果時，才建議試用 Preview。

## 简体中文｜首次将可启动入口带到三个平台

本候选版本保留 Windows 完整迁移版，并首次提供真正的 macOS x64 未签名 DMG 与 Linux x86_64 AppImage。两个 Preview 都能打开四语 Avalonia 界面，但完整 Facebook ZIP 迁移、图片处理、系统凭据存储和 WordPress 发布流程仍只在 Windows 完整版中可用。

DMG 与 AppImage 均在对应操作系统的 GitHub 原生 runner 中制作，CI 会从最终成品启动程序并确认至少存活六秒。下载内容还附带 SHA256 与 SPDX SBOM；非 PR 构建在 GitHub 支持时也会生成来源和 SBOM 证明。这是云端构建及启动证据，不是作者在 macOS／Linux 实机上的验证，也不是正式兼容承诺。

macOS 版本没有 Apple 开发者签名；首次使用请按住 Control 点击 `.app` 并选择“打开”，或在“隐私与安全性”中允许打开。DMG 目前为 Intel x64 版；Apple Silicon 可能需要 Rosetta 2，尚未验证。Linux 下载后请先运行 `chmod +x FB2WordPress-*-Preview.AppImage`。只有在理解上述限制并愿意反馈环境和结果时，才建议试用 Preview。

## English | A launchable entry point reaches all three platforms

This release candidate preserves the complete Windows migration product and adds a genuine unsigned macOS x64 DMG plus a Linux x86_64 AppImage. Both Previews open the four-language Avalonia interface, but the complete Facebook ZIP migration, image processing, operating-system credential vault, and WordPress publishing workflow remain available only in the full Windows application.

Each package is produced on a matching native GitHub runner. CI launches the app from the final DMG or AppImage and requires it to remain alive for at least six seconds. Downloads also include SHA256 and SPDX SBOM evidence; non-PR builds receive provenance and SBOM attestations where GitHub supports them. This proves cloud build and startup only. It is not author-owned real-device validation and not a formal compatibility claim.

The macOS app has no Apple Developer signature. On first use, Control-click the `.app` and choose **Open**, or allow it under **Privacy & Security**. The current DMG is Intel x64; Apple Silicon may require Rosetta 2 and remains unvalidated. On Linux, run `chmod +x FB2WordPress-*-Preview.AppImage` after download. Use these Previews only if you understand the limitations and are willing to report your environment and results.

## 日本語｜3つのプラットフォームへ起動可能な入口を届ける

このリリース候補は Windows 完全移行版を維持し、真正な未署名 macOS x64 DMG と Linux x86_64 AppImage を初めて提供します。どちらの Preview も4言語 Avalonia 画面を起動できますが、Facebook ZIP の完全移行、画像処理、OS 資格情報保管庫、WordPress 公開フローは引き続き Windows 完全版だけで利用できます。

各パッケージは対応するネイティブ GitHub runner で作成します。CI は最終 DMG または AppImage からアプリを起動し、6秒以上存続することを確認します。ダウンロードには SHA256 と SPDX SBOM も含み、PR 以外のビルドでは GitHub が対応する場合に来歴・SBOM 証明を生成します。これはクラウド上のビルドと起動の証拠に限られ、作者所有の macOS／Linux 実機検証でも正式な互換性表明でもありません。

macOS アプリには Apple Developer 署名がありません。初回は `.app` を Control クリックして「開く」を選ぶか、「プライバシーとセキュリティ」で許可してください。現在の DMG は Intel x64 版で、Apple Silicon では Rosetta 2 が必要になる可能性があり、未検証です。Linux ではダウンロード後に `chmod +x FB2WordPress-*-Preview.AppImage` を実行してください。制限を理解し、環境と結果を報告できる場合にのみ Preview をお試しください。
