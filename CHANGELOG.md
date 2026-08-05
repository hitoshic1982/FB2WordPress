# Changelog / 變更紀錄 / 更新日志 / 変更履歴

## Unreleased / 尚未發布 / 尚未发布 / 未公開

### 繁體中文

- 新增 Windows x64 完整 EXE、macOS x64 與 Apple Silicon arm64 兩種未簽章 Preview DMG，以及 Linux x86_64 Preview AppImage；四份最終成品均在相符的原生 GitHub runner 執行啟動存活測試。
- 只有精確 `v1.1.0-rc.N`（`N > 0`）、版本相符且標籤提交屬於 `origin/main` 時，才會彙整 SHA256、各平台 SPDX SBOM 與證明並建立四語 prerelease；任一失敗或一般 `main` 推送都不發布。macOS／Linux 仍明確標示為移植入口，尚非完整搬家版或作者實機驗證。
- 建立 `net10.0` 共用核心與 Avalonia 12 最小桌面入口，並新增 Windows、macOS、Linux CI 矩陣。
- Windows WinForms 完整版、DPAPI、圖片壓縮、搬家進度與單一 EXE 流程維持不變。
- 跨平台安全設定改採憑證世代切換：三組機密完整寫入後才更新公開設定，失敗可回到上一個一致狀態；清空憑證後舊值不會復活。
- 公開偏好更新明確保留既有憑證；未完成寫入由持久日誌接續清理或回復。刪除舊憑證前會先確認主檔與備份都鎖定新世代，日誌則採備份優先刪除；公開設定毀損時只從有效備份復原，沒有有效備份就安全拒絕覆寫。
- 同一路徑的設定交易加入跨程序獨占檔案鎖；鎖逾時、路徑權限或符號連結安全檢查失敗時直接拒絕，程序崩潰後由作業系統釋放鎖並讓日誌安全復原。
- 共用版本 fallback 校正為 `1.1.0`；日後的三平台預覽標籤可採 `v1.1.0-rc.1`，由 CI 依標籤覆寫套件版本。
- API 設定保存改為可等待的非同步流程，連線完成前會真正等候保存完成。
- 新增跨平台 CI、CodeQL、NuGet 弱點稽核、Dependency Review 與固定版本 Gitleaks 機密掃描，並採用[炎劍開源軟體家族品質標準](CONTRIBUTING.md)。
- macOS／Linux 目前僅有可建置的移植基礎，尚未提供完整功能或實機相容承諾。

### 简体中文

- 新增 Windows x64 完整 EXE、macOS x64 与 Apple Silicon arm64 两种未签名 Preview DMG，以及 Linux x86_64 Preview AppImage；四份最终成品都在架构匹配的原生 GitHub runner 中执行启动存活测试。
- 只有标签严格匹配 `v1.1.0-rc.N`（`N > 0`）、版本一致且标签提交属于 `origin/main` 时，才会汇总 SHA256、各平台 SPDX SBOM 与证明并创建四语 prerelease；任一失败或普通 `main` 推送都不会发布。macOS／Linux 仍明确标记为移植入口，不是完整迁移版或作者实机验证。
- 建立 `net10.0` 共享核心与 Avalonia 12 最小桌面入口，并新增 Windows、macOS、Linux CI 矩阵。
- Windows WinForms 完整版、DPAPI、图片压缩、迁移进度和单一 EXE 流程保持不变。
- 跨平台安全设置改用凭据世代切换：三组机密完整写入后才更新公开设置，失败时可回到上一个一致状态；清空凭据后旧值不会重新出现。
- 更新公开偏好时会明确保留现有凭据；未完成写入由持久日志继续清理或恢复。删除旧凭据前会先确认主文件与备份都锁定新世代，日志则优先删除备份；公开设置损坏时只从有效备份恢复，没有有效备份就安全拒绝覆盖。
- 同一路径的设置事务加入跨进程独占文件锁；锁超时、路径权限或符号链接安全检查失败时直接拒绝，进程崩溃后由操作系统释放锁并让日志安全恢复。
- 共享版本 fallback 校正为 `1.1.0`；后续三平台预览标签可采用 `v1.1.0-rc.1`，由 CI 根据标签覆盖软件包版本。
- API 设置保存改为可等待的异步流程，连接完成前会真正等待保存完成。
- 新增跨平台 CI、CodeQL、NuGet 漏洞审计、Dependency Review 和固定版本 Gitleaks 机密扫描，并采用[炎剑开源软件家族质量标准](CONTRIBUTING.md)。
- macOS／Linux 目前只有可构建的迁移基础，尚未提供完整功能或实机兼容承诺。

### English

- Added the complete Windows x64 EXE, separate unsigned macOS x64 and Apple Silicon arm64 Preview DMGs, and a Linux x86_64 Preview AppImage. All four final packages are launch-smoke-tested on matching native GitHub runners.
- Only an exact `v1.1.0-rc.N` tag (`N > 0`) with a matching source version and a commit contained in `origin/main` can aggregate SHA256, per-platform SPDX SBOMs, and attestations into a four-language prerelease. Any failure or ordinary `main` push never publishes. macOS/Linux remain explicitly incomplete migration entry points without author-owned real-device validation.
- Added a shared `net10.0` core, a minimal Avalonia 12 desktop entry point, and a Windows/macOS/Linux CI matrix.
- Preserved the complete Windows WinForms application, DPAPI, image compression, migration progress, and single-EXE flow.
- Added generation-based secure settings: all three secrets are staged before the public settings pointer changes, failed writes retain the previous consistent state, and explicitly cleared credentials cannot reappear.
- Public-only preference saves explicitly preserve active credentials. A durable journal completes cleanup or rollback after interruption. Both public copies are pinned to the new generation before retired credentials are deleted, and journal backups are removed before journal primaries. Corrupt public settings recover only from a valid backup and otherwise fail closed without overwriting evidence.
- Same-path settings transactions now use an owned, exclusive cross-process file lock. Timeout, access, symbolic-link, and reparse-path failures refuse the operation; an OS-released crash lease allows the journal to recover safely on the next run.
- Corrected the shared fallback package version to `1.1.0`; a future three-platform preview may use the `v1.1.0-rc.1` tag, which CI resolves as the package version.
- Replaced synchronous API settings callbacks with an awaitable persistence flow, so connection completion waits for the save to finish.
- Added cross-platform CI, CodeQL, NuGet vulnerability auditing, Dependency Review, and commit-pinned Gitleaks secret defense under the [Flameblade Open Source Software Family Quality Standard](CONTRIBUTING.md).
- macOS and Linux currently have a buildable migration foundation only; complete functionality and real-device compatibility are not yet claimed.

### 日本語

- Windows x64 完全版 EXE、macOS x64 と Apple Silicon arm64 の個別の未署名 Preview DMG、Linux x86_64 Preview AppImage を追加し、4つの最終成果物を対応するネイティブ GitHub runner 上で起動・存続テストします。
- `v1.1.0-rc.N`（`N > 0`）に厳密一致し、ソースのバージョンとも一致し、タグの commit が `origin/main` に含まれる場合に限り、SHA256、各プラットフォームの SPDX SBOM、証明を4言語 prerelease にまとめます。いずれかの失敗時や通常の `main` push では公開しません。macOS／Linux は未完成の移植入口であり、作者による実機検証済み完全版ではありません。
- `net10.0` 共有コア、Avalonia 12 の最小デスクトップ入口、Windows／macOS／Linux の CI マトリクスを追加しました。
- Windows WinForms 完全版、DPAPI、画像圧縮、移行進捗、単一 EXE の手順を維持しました。
- クロスプラットフォーム設定に資格情報の世代切り替えを導入しました。3つの機密情報をすべて保存してから公開設定の参照先を更新し、失敗時は以前の一貫した状態を維持します。明示的に削除した資格情報が後から復活することもありません。
- 公開設定だけを保存する場合は既存の資格情報を明示的に維持します。中断した書き込みは永続ジャーナルで後処理またはロールバックを再開します。旧資格情報を削除する前に公開主ファイルとバックアップを新世代へ固定し、ジャーナルはバックアップから先に削除します。公開設定が破損した場合は有効なバックアップからのみ復元し、有効なバックアップがなければ証拠を上書きせず安全側で拒否します。
- 同一保存先の設定取引に、所有権を持つ排他的なプロセス間ファイルロックを追加しました。タイムアウト、権限、シンボリックリンク、reparse path の安全確認に失敗した場合は処理を拒否し、プロセス停止時は OS がロックを解放して次回のジャーナル復旧を可能にします。
- 共有 fallback バージョンを `1.1.0` に修正しました。将来の3プラットフォーム向けプレビューは `v1.1.0-rc.1` タグを使用でき、CI がタグからパッケージ版を上書きします。
- API の設定保存を待機可能な非同期処理に変更し、接続完了前に保存完了を確実に待つようにしました。
- クロスプラットフォーム CI、CodeQL、NuGet 脆弱性監査、Dependency Review、commit 固定の Gitleaks 機密情報検査を追加し、[炎剣オープンソースソフトウェアファミリー品質基準](CONTRIBUTING.md)を採用しました。
- macOS／Linux は現在ビルド可能な移植基盤のみで、完全機能や実機互換性はまだ表明していません。

## v1.0.0 — 2026-08-05

### 繁體中文

FB2WordPress 首次正式開源。這不是單純把貼文倒進資料庫的腳本，而是炎劍文化工作室建立 WordPress 品牌總部時使用的完整搬家流程：REST API 連線、應用程式密碼、媒體庫上傳、標籤建立、草稿審閱、YouTube 選用整合、空白行安全整理、接續移轉與重複防護。

### 简体中文

FB2WordPress 首次正式开源。它不是简单的数据库导入脚本，而是建立 WordPress 品牌总部时使用的完整迁移流程：REST API、应用程序密码、媒体库上传、标签、草稿审阅、可选 YouTube 集成、安全空行整理、断点续传和重复防护。

### English

First public MIT release of FB2WordPress. This is not a raw database importer: it is the complete workflow used to establish Flameblade Studio's WordPress headquarters, including REST API authentication, media uploads, tags, draft review, optional YouTube integration, scoped whitespace cleanup, resumable migration, and duplicate protection.

### 日本語

FB2WordPress 初の MIT 公開版です。単なるデータベース投入スクリプトではなく、WordPress をブランド本部として構築する際に使った完全な移行フローです。REST API 認証、メディア、タグ、下書き確認、任意の YouTube 連携、安全な空行整理、再開、重複防止を含みます。

