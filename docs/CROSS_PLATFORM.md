# FB2WordPress cross-platform foundation / 跨平台基礎 / 跨平台基础 / クロスプラットフォーム基盤

Cloud builds prove that source code compiles in a clean environment; they do not replace real-device validation. The four language sections below describe the same scope and limitations.

## 繁體中文

### 已完成

- 保留既有 Windows WinForms 完整版、DPAPI 憑證保護、圖片壓縮與單一 EXE 發行流程，沒有刪除既有功能。
- 將 Facebook 匯出解析、WordPress REST／媒體 API、YouTube OAuth、搬家模型、四語目錄、進度保存與報告路徑抽成 `net10.0` 共用核心。
- 以跨平台路徑 API 決定設定與報告位置，不手工拼接 Windows 路徑。
- 建立安全設定合約：公開偏好可存 JSON；WordPress 應用程式密碼、Google Client Secret 與 Refresh Token 必須交給作業系統安全保管庫。三組憑證先完整寫入同一個新世代，再切換公開設定指標；失敗時保留上一個完整世代，不混用新舊值。安全保管庫不可用時，含憑證的保存會被拒絕；清空憑證則會留下明確清除狀態，舊值不會日後復活。
- 憑證變更採「保留／清除／取代」三種明確狀態。只更新公開偏好時，即使保管庫暫時不可用，也會保留既有憑證世代，不會把空欄位誤當成刪除。
- 設定切換使用持久交易日誌；程式中斷、公開檔寫入失敗或舊世代刪除失敗時，下次啟動會依公開指標完成清理或安全回復。刪除舊憑證前，必須先驗證公開主檔與備份都指向同一個新世代；清除日誌時則先刪備份再刪主檔，避免中斷後舊日誌復活。公開 JSON 無法讀取時只接受有效備份；主檔與備份都無效就停止保存，不覆寫現場。
- 同一路徑的載入、保存、回復與舊憑證清理，全程共用具所有權的跨程序檔案鎖。鎖由作業系統獨占檔案控制代碼持有，程序崩潰會自動釋放；超時、權限不足、符號連結或 reparse 路徑一律安全拒絕，不會在無鎖狀態繼續交易。
- 建立 Avalonia 12 相對佈局的最小桌面入口，以及 Windows、macOS、Linux 三平台的共用核心建置與測試矩陣。
- 建立原生封裝管線：Windows runner 產生完整自含式 x64 EXE；Intel x64 與 Apple Silicon arm64 macOS runner 各自使用系統 `hdiutil` 產生架構相符的未簽章 `.dmg`（內含 `.app`）；Linux x86_64 runner 使用官方且經 SHA256 鎖定的 `appimagetool` 產生真正 `.AppImage`。四份最終封裝成品都會在相符 runner 啟動並執行存活測試。
- PR 固定聚合檢查必須等 Windows、macOS x64、macOS arm64、Linux 四份成品全數成功；PR、一般 `main` 推送、排程與手動執行都維持唯讀且不發布。只有精確 `v1.1.0-rc.N`（`N > 0`）、版本相符且標籤提交屬於 `origin/main` 時，可信工作才獲得發布與證明權限，彙整單一 SHA256、各平台 SPDX SBOM 與來源／SBOM 證明，最後以四語說明建立 prerelease。任一步驟失敗就不發布。所有第三方 Actions 都固定到完整 commit SHA，Microsoft SBOM Tool 固定為 `4.1.5`。

### 支援與驗證矩陣

| 能力 | Windows | macOS | Linux |
|---|---|---|---|
| 共用核心建置／測試 | CI | CI | CI |
| 現有完整操作介面 | WinForms 已保留並在 Windows 驗證 | 尚未完成 | 尚未完成 |
| Avalonia 最小入口 | 可建置；僅為移植基礎 | 可建置；僅為移植基礎 | 可建置；僅為移植基礎 |
| 原生 Preview 封裝 | 完整自含式 x64 EXE；runner 啟動測試 | 未簽章 x64 與原生 Apple Silicon arm64 DMG；各自在相符 runner 啟動測試 | x86_64 AppImage；原生 runner 啟動測試 |
| Facebook ZIP 完整搬家流程 | 已有 | 尚待移植 | 尚待移植 |
| 憑證保護 | Windows DPAPI 已有 | Keychain 尚待實作 | Secret Service 尚待實作 |
| 圖片壓縮 | 既有 Windows 引擎已驗證 | 尚待跨平台引擎 | 尚待跨平台引擎 |
| 真實裝置驗證 | Windows 本機已執行 | 尚無 | 尚無 |

### 尚未完成與發布界線

- macOS／Linux Preview 是真正可下載、可啟動的 DMG／AppImage，但目前不是完整搬家產品；Avalonia 專案仍只是能逐步承接功能的入口。
- CI 成功只代表 GitHub 原生 runner 完成建置、封裝與六秒啟動存活測試，不得寫成作者持有的 macOS／Linux 實機驗證。沒有真實裝置、系統安全保管庫與完整搬家流程證據前，不提供正式相容承諾。
- 既有 Windows 版仍是唯一完整使用路徑；跨平台工作不得降低 DPAPI、防重複、暫停續傳、草稿模式或既有測試門檻。

### 下一步

1. 實作 macOS Keychain 與 Linux Secret Service，並維持「無安全保管庫就不保存憑證」。
2. 將檔案挑選、搬家進度、暫停續傳、文章工具與錯誤報告逐頁移到 Avalonia。
3. 以跨平台影像函式庫取代 `System.Drawing`，用固定測試圖比較品質、方向、透明度與檔案大小。
4. 由 macOS／Linux 貢獻者提供系統版本、桌面環境、操作步驟、畫面與結果；通過後才提升支援等級。

## 简体中文

### 已完成

- 保留现有 Windows WinForms 完整版、DPAPI 凭据保护、图片压缩和单一 EXE 发布流程，没有删除原有功能。
- 将 Facebook 导出解析、WordPress REST／媒体 API、YouTube OAuth、迁移模型、四语目录、进度保存与报告路径拆分为 `net10.0` 共享核心。
- 使用跨平台路径 API 决定设置与报告位置，不手工拼接 Windows 路径。
- 建立安全设置契约：公开偏好可以保存为 JSON；WordPress 应用程序密码、Google Client Secret 和 Refresh Token 必须交给操作系统安全存储。三组凭据会先完整写入同一个新世代，再切换公开设置指针；失败时继续使用上一个完整世代，不混用新旧值。安全存储不可用时，含凭据的保存会被拒绝；清空凭据则会留下明确清除状态，旧值以后不会重新出现。
- 凭据变更采用“保留／清除／替换”三种明确状态。只更新公开偏好时，即使安全存储暂时不可用，也会保留现有凭据世代，不会把空白字段误当成删除。
- 设置切换使用持久事务日志；程序中断、公开文件写入失败或旧世代删除失败时，下次启动会根据公开指针完成清理或安全恢复。删除旧凭据前，必须先验证公开主文件与备份都指向同一个新世代；清除日志时先删除备份再删除主文件，避免中断后旧日志复活。公开 JSON 无法读取时只接受有效备份；主文件与备份都无效时停止保存，不覆盖现场。
- 同一路径的加载、保存、恢复与旧凭据清理，全程共用具所有权的跨进程文件锁。锁由操作系统独占文件句柄持有，进程崩溃会自动释放；超时、权限不足、符号链接或 reparse 路径一律安全拒绝，不会在未持锁时继续事务。
- 建立采用相对布局的 Avalonia 12 最小桌面入口，以及 Windows、macOS、Linux 三平台共享核心的构建与测试矩阵。
- 建立原生打包流水线：Windows runner 生成完整自包含 x64 EXE；Intel x64 与 Apple Silicon arm64 macOS runner 分别使用系统 `hdiutil` 生成架构匹配的未签名 `.dmg`（内含 `.app`）；Linux x86_64 runner 使用官方且经过 SHA256 锁定的 `appimagetool` 生成真正的 `.AppImage`。四份最终成品都会在架构匹配的 runner 上启动并执行存活测试。
- PR 的固定聚合检查必须等待 Windows、macOS x64、macOS arm64、Linux 四份成品全部成功；PR、普通 `main` 推送、定时与手动执行都保持只读且不会发布。只有标签严格匹配 `v1.1.0-rc.N`（`N > 0`）、版本一致且标签提交属于 `origin/main` 时，可信任务才获得发布与证明权限，汇总单一 SHA256、各平台 SPDX SBOM 与来源／SBOM 证明，最后使用四语说明创建 prerelease。任一步骤失败都不会发布。所有第三方 Actions 都固定到完整 commit SHA，Microsoft SBOM Tool 固定为 `4.1.5`。

### 支持与验证矩阵

| 能力 | Windows | macOS | Linux |
|---|---|---|---|
| 共享核心构建／测试 | CI | CI | CI |
| 现有完整操作界面 | 保留 WinForms，并已在 Windows 验证 | 尚未完成 | 尚未完成 |
| Avalonia 最小入口 | 可构建；仅作为迁移基础 | 可构建；仅作为迁移基础 | 可构建；仅作为迁移基础 |
| 原生 Preview 打包 | 完整自包含 x64 EXE；runner 启动测试 | 未签名 x64 与原生 Apple Silicon arm64 DMG；分别在匹配 runner 启动测试 | x86_64 AppImage；原生 runner 启动测试 |
| Facebook ZIP 完整迁移流程 | 已有 | 尚待迁移 | 尚待迁移 |
| 凭据保护 | 已有 Windows DPAPI | Keychain 尚待实现 | Secret Service 尚待实现 |
| 图片压缩 | 现有 Windows 引擎已验证 | 尚待跨平台引擎 | 尚待跨平台引擎 |
| 真实设备验证 | 已在 Windows 本机执行 | 暂无 | 暂无 |

### 尚未完成与发布边界

- macOS／Linux Preview 是真正可下载、可启动的 DMG／AppImage，但目前不是完整迁移产品；Avalonia 项目仍只是逐步承接功能的入口。
- CI 成功只代表 GitHub 原生 runner 完成构建、打包与六秒启动存活测试，不能写成作者持有的 macOS／Linux 实机验证。获得真实设备、系统安全存储和完整迁移流程证据之前，不提供正式兼容承诺。
- 现有 Windows 版仍是唯一完整使用路径；跨平台工作不得降低 DPAPI、防重复、断点续传、草稿模式或现有测试门槛。

### 下一步

1. 实现 macOS Keychain 与 Linux Secret Service，并维持“没有安全存储就不保存凭据”。
2. 将文件选择、迁移进度、暂停续传、文章工具和错误报告逐页迁移到 Avalonia。
3. 使用跨平台图片库取代 `System.Drawing`，用固定测试图片比较质量、方向、透明度和文件大小。
4. 由 macOS／Linux 贡献者提供系统版本、桌面环境、操作步骤、画面和结果；通过后才提升支持等级。

## English

### Completed

- Preserved the complete Windows WinForms application, DPAPI credential protection, image compression, and single-EXE release flow without removing existing behavior.
- Extracted Facebook export parsing, WordPress REST/media APIs, YouTube OAuth, migration models, the four-language catalog, progress persistence, and report paths into a shared `net10.0` core.
- Uses cross-platform path APIs for settings and reports instead of hand-built Windows paths.
- Added a secure settings contract: public preferences may use JSON, while the WordPress Application Password, Google Client Secret, and Refresh Token must use an operating-system vault. All three credentials are staged under one new generation before the public pointer changes; a failure keeps the previous complete generation instead of mixing values. Saving credentials fails closed when no secure vault is available. An explicit clear remains authoritative while the vault is unavailable, so retired credentials cannot reappear later.
- Credential changes use explicit Preserve, Clear, and Replace states. A public-preference-only save preserves the active credential generation even while the vault is temporarily unavailable; blank fields are never misread as a deletion request.
- A durable transaction journal covers settings transitions. After interruption, public-file failure, or retired-generation deletion failure, the next start finishes cleanup or rolls back according to the public pointer. Before retired credentials are deleted, both the public primary and backup must be verified against the same new active generation. Journal removal deletes its backup before its primary, preventing an interrupted deletion from reviving an older transaction. An unreadable public JSON file recovers only from a valid backup; if both copies are invalid, saving stops without overwriting the evidence.
- Every load, save, recovery, and retired-generation cleanup for the same path now shares an owned cross-process file lock. The operating system owns the exclusive file handle and releases it after a crash; timeout, access failure, symbolic-link, or reparse-path detection fails closed instead of continuing a transaction without the lock.
- Added a minimal Avalonia 12 desktop entry point with relative layout and a Windows/macOS/Linux build-and-test matrix for the shared core.
- Added native packaging: a Windows runner creates the complete self-contained x64 EXE; separate Intel x64 and Apple Silicon arm64 macOS runners use the system `hdiutil` to create architecture-matched unsigned `.dmg` files containing genuine `.app` bundles; a Linux x86_64 runner uses the official SHA256-pinned `appimagetool` to create a real `.AppImage`. All four final packages are launched and liveness-tested on matching runners.
- A fixed PR aggregate check waits for all Windows, macOS x64, macOS arm64, and Linux packages. Pull requests, ordinary `main` pushes, schedules, and manual runs stay read-only and never publish. Only an exact `v1.1.0-rc.N` tag (`N > 0`) whose version matches the source and whose commit is contained in `origin/main` grants the trusted job release and attestation permissions. It aggregates one SHA256 manifest, per-platform SPDX SBOMs, and provenance/SBOM attestations before creating a prerelease from four-language notes. Any failed step prevents publication. All third-party Actions use full commit SHAs, and Microsoft SBOM Tool is fixed at `4.1.5`.

### Support and validation matrix

| Capability | Windows | macOS | Linux |
|---|---|---|---|
| Shared-core build/tests | CI | CI | CI |
| Existing complete UI | WinForms preserved and validated on Windows | Not complete | Not complete |
| Minimal Avalonia entry point | Builds; migration foundation only | Builds; migration foundation only | Builds; migration foundation only |
| Native Preview package | Complete self-contained x64 EXE; runner launch test | Unsigned x64 and native Apple Silicon arm64 DMGs; matching-runner launch tests | x86_64 AppImage; native-runner launch test |
| Complete Facebook ZIP migration | Available | Port pending | Port pending |
| Credential protection | Windows DPAPI available | Keychain pending | Secret Service pending |
| Image compression | Existing Windows engine validated | Cross-platform engine pending | Cross-platform engine pending |
| Real-device validation | Performed on Windows | None yet | None yet |

### Incomplete work and release boundary

- The macOS/Linux Previews are genuine downloadable and launchable DMG/AppImage packages, but they are not complete migration products. Avalonia remains an incremental migration entry point.
- CI success proves build, packaging, and a six-second launch-liveness check on native GitHub runners only. It must not be described as validation on macOS/Linux hardware owned by the author. No formal compatibility claim will be made before real-device evidence, OS vault integration, and the complete migration workflow exist.
- Windows remains the only complete user path. Cross-platform work must not weaken DPAPI, duplicate prevention, resumable progress, draft mode, or existing regression gates.

### Next steps

1. Implement macOS Keychain and Linux Secret Service while preserving the “no secure vault, no credential save” rule.
2. Move file selection, migration progress, pause/resume, post tools, and error reports into Avalonia page by page.
3. Replace `System.Drawing` with a cross-platform image library and compare quality, orientation, transparency, and file size using fixed fixtures.
4. Require macOS/Linux contributors to report OS version, desktop environment, exact steps, screenshots, and results before support status is raised.

## 日本語

### 完了した内容

- 既存の Windows WinForms 完全版、DPAPI による資格情報保護、画像圧縮、単一 EXE の公開手順を維持し、既存機能を削除していません。
- Facebook エクスポート解析、WordPress REST／メディア API、YouTube OAuth、移行モデル、4言語カタログ、進捗保存、レポート保存先を `net10.0` の共有コアへ分離しました。
- 設定とレポートの保存先はクロスプラットフォームのパス API で決定し、Windows 固有のパスを手作業で連結しません。
- 安全な設定契約を追加しました。公開設定は JSON に保存できますが、WordPress アプリケーションパスワード、Google Client Secret、Refresh Token は OS の安全な保管庫へ保存します。3つの資格情報を同じ新しい世代へすべて書き込んでから公開設定の参照先を切り替えるため、失敗時に新旧の値が混在しません。安全な保管庫が利用できない場合、資格情報を含む保存は拒否されます。明示的な削除状態は保管庫が一時的に利用できなくても維持され、古い資格情報が後から復活しません。
- 資格情報の変更は「維持／削除／置換」の3状態を明示します。公開設定だけを更新する場合、保管庫が一時的に利用できなくても現在の資格情報世代を維持し、空欄を削除指示と誤認しません。
- 設定切り替えは永続トランザクションジャーナルで保護します。中断、公開ファイルの書き込み失敗、旧世代の削除失敗が起きても、次回起動時に公開ポインターに従って後処理または安全なロールバックを行います。旧資格情報を削除する前に、公開主ファイルとバックアップの両方が同じ新しい有効世代を指すことを検証します。ジャーナル削除はバックアップを先に、主ファイルを後に行い、中断後に古い取引が復活することを防ぎます。公開 JSON が読めない場合は有効なバックアップからだけ復元し、両方が無効なら証拠を上書きせず保存を停止します。
- 同じ保存先に対する読み込み、保存、復旧、旧世代の削除は、所有権を持つプロセス間ファイルロックで取引全体を直列化します。排他的ファイルハンドルは OS が管理し、プロセス停止時に自動解放します。タイムアウト、権限不足、シンボリックリンク、reparse path を検出した場合は、ロックなしで続行せず安全側で拒否します。
- 相対レイアウトを使う Avalonia 12 の最小デスクトップ入口と、Windows／macOS／Linux で共有コアをビルド・テストするマトリクスを追加しました。
- ネイティブ梱包を追加しました。Windows runner は完全な自己完結型 x64 EXE を作成します。Intel x64 と Apple Silicon arm64 の macOS runner は、それぞれシステムの `hdiutil` でアーキテクチャに合う真正な `.app` を収録した未署名 `.dmg` を作成します。Linux x86_64 runner は公式かつ SHA256 固定の `appimagetool` で真正な `.AppImage` を作成します。4つの最終成果物を対応する runner で起動し、存続テストを行います。
- PR の固定集約チェックは Windows、macOS x64、macOS arm64、Linux の全成果物を待ちます。PR、通常の `main` push、定期実行、手動実行は読み取り専用で、公開しません。`v1.1.0-rc.N`（`N > 0`）に厳密一致し、ソースのバージョンとも一致し、タグの commit が `origin/main` に含まれる場合に限り、信頼済み job に公開・証明権限を与えます。単一 SHA256、各プラットフォームの SPDX SBOM、来歴／SBOM 証明をまとめ、4言語説明から prerelease を作成します。どの手順でも失敗すれば公開しません。外部 Actions は完全な commit SHA に固定し、Microsoft SBOM Tool は `4.1.5` に固定します。

### 対応・検証マトリクス

| 機能 | Windows | macOS | Linux |
|---|---|---|---|
| 共有コアのビルド／テスト | CI | CI | CI |
| 既存の完全な操作画面 | WinForms を維持し Windows で検証済み | 未完成 | 未完成 |
| Avalonia 最小入口 | ビルド可能・移植基盤のみ | ビルド可能・移植基盤のみ | ビルド可能・移植基盤のみ |
| ネイティブ Preview 梱包 | 完全な自己完結型 x64 EXE・runner 起動テスト | 未署名 x64 とネイティブ Apple Silicon arm64 DMG・対応 runner 起動テスト | x86_64 AppImage・ネイティブ runner 起動テスト |
| Facebook ZIP の完全移行 | 利用可能 | 移植待ち | 移植待ち |
| 資格情報保護 | Windows DPAPI 実装済み | Keychain 未実装 | Secret Service 未実装 |
| 画像圧縮 | 既存 Windows エンジンを検証済み | クロスプラットフォーム版未実装 | クロスプラットフォーム版未実装 |
| 実機検証 | Windows 実機で実施 | 未実施 | 未実施 |

### 未完成部分と公開上の境界

- macOS／Linux Preview は真正な DMG／AppImage としてダウンロード・起動できますが、完全な移行製品ではありません。Avalonia は引き続き段階的な移植入口です。
- CI 成功が証明するのは GitHub のネイティブ runner 上でのビルド、梱包、6秒間の起動存続確認だけです。作者所有の macOS／Linux 実機検証と表現しません。実機結果、OS 保管庫、完全な移行フローがそろうまでは正式な互換性を表明しません。
- 完全な利用経路は引き続き Windows 版だけです。クロスプラットフォーム対応のために DPAPI、重複防止、再開可能な進捗、下書きモード、既存テストを弱めません。

### 次の段階

1. macOS Keychain と Linux Secret Service を実装し、「安全な保管庫がなければ資格情報を保存しない」原則を維持します。
2. ファイル選択、移行進捗、一時停止／再開、記事ツール、エラーレポートを Avalonia へ画面単位で移植します。
3. `System.Drawing` をクロスプラットフォーム画像ライブラリへ置き換え、固定テスト画像で品質、向き、透明度、容量を比較します。
4. macOS／Linux の協力者には OS バージョン、デスクトップ環境、正確な手順、画面、結果の提出を求め、確認後に対応レベルを引き上げます。
