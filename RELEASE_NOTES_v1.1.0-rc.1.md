# FB2WordPress v1.1.0-rc.1 Preview release notes

> Windows remains the complete product. The macOS and Linux downloads are clearly bounded Preview migration entry points.

## 繁體中文｜第一次把可啟動的入口交到三個平台手上

這次候選版本保留 Windows x64 完整搬家 EXE，並首次提供真正的 macOS x64、Apple Silicon arm64 兩種未簽章 DMG，以及 Linux x86_64 AppImage。macOS／Linux Preview 都能開啟四語 Avalonia 介面，但完整 Facebook ZIP 搬家、圖片處理、系統憑證保管庫及 WordPress 發布流程仍只在 Windows 完整版可用。

Windows EXE、兩種 DMG 與 AppImage 均在相符架構的 GitHub 原生 runner 製作，CI 會從每一份最終成品啟動程式並確認至少存活六秒。只有精確 `v1.1.0-rc.N`（`N > 0`）標籤、版本相符且標籤提交屬於 `origin/main` 時，才會彙整 SHA256、各平台 SPDX SBOM 及來源／SBOM 證明，並以本四語文件建立 prerelease；任一成品或驗證失敗都不發布，一般 `main` 推送也不發布。這是雲端建置與啟動證據，不是作者持有 macOS／Linux 實機的驗證，也不是正式相容承諾。

macOS 未購買 Apple 開發者簽章；首次使用請按住 Control 點選 `.app` 後選擇「打開」，或到「隱私權與安全性」允許開啟。Intel Mac 請下載 x64 DMG，Apple Silicon 請下載原生 arm64 DMG，不需依賴 Rosetta 2。Linux 下載後請先執行 `chmod +x FB2WordPress-*-Preview.AppImage`。只有在理解上述限制並願意回報環境與結果時，才建議試用 Preview。

## 简体中文｜首次将可启动入口带到三个平台

本候选版本保留 Windows x64 完整迁移 EXE，并首次提供真正的 macOS x64、Apple Silicon arm64 两种未签名 DMG，以及 Linux x86_64 AppImage。macOS／Linux Preview 都能打开四语 Avalonia 界面，但完整 Facebook ZIP 迁移、图片处理、系统凭据存储和 WordPress 发布流程仍只在 Windows 完整版中可用。

Windows EXE、两种 DMG 与 AppImage 都在架构匹配的 GitHub 原生 runner 中制作，CI 会从每一份最终成品启动程序并确认至少存活六秒。只有标签严格匹配 `v1.1.0-rc.N`（`N > 0`）、版本一致且标签提交属于 `origin/main` 时，才会汇总 SHA256、各平台 SPDX SBOM 与来源／SBOM 证明，并使用本四语文件创建 prerelease；任一成品或验证失败都不会发布，普通 `main` 推送也不会发布。这是云端构建及启动证据，不是作者在 macOS／Linux 实机上的验证，也不是正式兼容承诺。

macOS 版本没有 Apple 开发者签名；首次使用请按住 Control 点击 `.app` 并选择“打开”，或在“隐私与安全性”中允许打开。Intel Mac 请下载 x64 DMG，Apple Silicon 请下载原生 arm64 DMG，不需要依赖 Rosetta 2。Linux 下载后请先运行 `chmod +x FB2WordPress-*-Preview.AppImage`。只有在理解上述限制并愿意反馈环境和结果时，才建议试用 Preview。

## English | A launchable entry point reaches all three platforms

This release candidate preserves the complete Windows x64 migration EXE and adds separate genuine unsigned macOS x64 and Apple Silicon arm64 DMGs plus a Linux x86_64 AppImage. The macOS/Linux Previews open the four-language Avalonia interface, but the complete Facebook ZIP migration, image processing, operating-system credential vault, and WordPress publishing workflow remain available only in the full Windows application.

The Windows EXE, both DMGs, and the AppImage are produced on matching native GitHub runners. CI launches every final package and requires it to remain alive for at least six seconds. Only an exact `v1.1.0-rc.N` tag (`N > 0`) whose version matches the source and whose commit belongs to `origin/main` can aggregate SHA256, per-platform SPDX SBOMs, and provenance/SBOM attestations, then create a prerelease from this four-language file. Any artifact or verification failure prevents publication, and an ordinary `main` push never publishes. This proves cloud build and startup only. It is not author-owned real-device validation and not a formal compatibility claim.

The macOS app has no Apple Developer signature. On first use, Control-click the `.app` and choose **Open**, or allow it under **Privacy & Security**. Intel Macs use the x64 DMG; Apple Silicon uses the native arm64 DMG and does not depend on Rosetta 2. On Linux, run `chmod +x FB2WordPress-*-Preview.AppImage` after download. Use these Previews only if you understand the limitations and are willing to report your environment and results.

## 日本語｜3つのプラットフォームへ起動可能な入口を届ける

このリリース候補は Windows x64 完全移行 EXE を維持し、真正な未署名 macOS x64 DMG、Apple Silicon arm64 DMG、Linux x86_64 AppImage を初めて提供します。macOS／Linux Preview は4言語 Avalonia 画面を起動できますが、Facebook ZIP の完全移行、画像処理、OS 資格情報保管庫、WordPress 公開フローは引き続き Windows 完全版だけで利用できます。

Windows EXE、2種類の DMG、AppImage は、対応するネイティブ GitHub runner で作成します。CI は各最終成果物からアプリを起動し、6秒以上存続することを確認します。`v1.1.0-rc.N`（`N > 0`）に厳密一致し、ソースのバージョンとも一致し、タグの commit が `origin/main` に含まれる場合に限り、SHA256、各プラットフォームの SPDX SBOM、来歴／SBOM 証明をまとめ、この4言語ファイルから prerelease を作成します。成果物または検証のどれか一つでも失敗すれば公開せず、通常の `main` push でも公開しません。これはクラウド上のビルドと起動の証拠に限られ、作者所有の macOS／Linux 実機検証でも正式な互換性表明でもありません。

macOS アプリには Apple Developer 署名がありません。初回は `.app` を Control クリックして「開く」を選ぶか、「プライバシーとセキュリティ」で許可してください。Intel Mac は x64 DMG、Apple Silicon はネイティブ arm64 DMG を使用するため、Rosetta 2 に依存しません。Linux ではダウンロード後に `chmod +x FB2WordPress-*-Preview.AppImage` を実行してください。制限を理解し、環境と結果を報告できる場合にのみ Preview をお試しください。
