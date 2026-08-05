# Contributing / 參與貢獻 / 参与贡献 / コントリビューション

## 炎劍開源軟體家族品質標準 / Flameblade Open Source Software Family Quality Standard

### 繁體中文

炎劍文化工作室的開源軟體不是一次性作品。所有可合併、可發行的變更必須同時遵守下列標準：

> 「劍，我已鍛成；餘下的路，就交給你們了。」

- 使用者可見行為、支援範圍與發行說明，必須同步維護繁體中文、簡體中文、英文與日文四語版本。
- CI、CodeQL、相依套件弱點稽核與 Gitleaks 機密掃描必須是真實執行的檢查，不以裝飾性徽章代替驗證。
- 不得提交、封裝或發布 API 金鑰、OAuth Secret、Token、網站帳密、私人匯出檔或個人資料庫。
- 發行成品須能追溯到明確版本、Git commit、CI 紀錄、SHA256 與對應原始碼。
- 不得為新功能破壞既有正常功能，也不得降低安全閘門、測試、權限或確認流程來換取通過。
- CI 建置成功只證明乾淨環境可建置；沒有真實裝置證據時，不誇大 macOS、Linux 或其他平台的完整相容性。
- 使用者可見的版本、下載方式、支援狀態或安全行為改變時，專案文件與炎劍官網資料必須同步更新。
- 拒絕只能靠某次人工記憶完成的例外流程；可重複工作應寫成可測試、可稽核、可自動化的長期機制。

### 简体中文：炎剑开源软件家族质量标准

炎剑文化工作室的开源软件不是一次性作品。所有可合并、可发布的变更必须同时遵守以下标准：

> 「剑，我已锻成；余下的路，就交给你们了。」

- 用户可见行为、支持范围和发布说明，必须同步维护繁体中文、简体中文、英文和日文四语版本。
- CI、CodeQL、依赖项漏洞审计和 Gitleaks 机密扫描必须是真实运行的检查，不能用装饰性徽章代替验证。
- 不得提交、打包或发布 API 密钥、OAuth Secret、Token、网站账号、私人导出文件或个人数据库。
- 发布成品必须能够追溯到明确版本、Git commit、CI 记录、SHA256 和对应源代码。
- 不得为了新功能破坏原有正常功能，也不得降低安全关卡、测试、权限或确认流程来换取通过。
- CI 构建成功只证明能够在干净环境中构建；没有真实设备证据时，不夸大 macOS、Linux 或其他平台的完整兼容性。
- 用户可见的版本、下载方式、支持状态或安全行为发生变化时，项目文档和炎剑官网资料必须同步更新。
- 拒绝只能依赖某次人工记忆完成的例外流程；可重复工作应建立为可测试、可审计、可自动化的长期机制。

### English

Flameblade Studio open-source software is maintained as a long-lived product, not a one-off artifact. Every mergeable and releasable change must meet all of these requirements:

> “I have forged this sword. What comes next is up to you.”

- Keep user-visible behavior, support scope, and release notes consistent across Traditional Chinese, Simplified Chinese, English, and Japanese.
- Run real CI, CodeQL, dependency-vulnerability audits, and Gitleaks secret scans; badges never substitute for validation.
- Never commit, package, or publish API keys, OAuth secrets, tokens, site credentials, private exports, or personal databases.
- Make every release artifact traceable to an explicit version, Git commit, CI record, SHA256, and corresponding source.
- Preserve all existing working behavior; do not weaken security gates, tests, permissions, or confirmation flows to make a change pass.
- Treat a successful cloud build only as evidence of a clean build. Do not overstate complete macOS, Linux, or other platform compatibility without real-device evidence.
- Synchronize project documentation and the Flameblade website whenever user-visible versions, downloads, support status, or security behavior change.
- Reject one-off manual exceptions that depend on someone remembering a special step; recurring work belongs in a testable, auditable, automated long-term mechanism.

### 日本語：炎剣オープンソースソフトウェアファミリー品質基準

炎剣文化工作室のオープンソースソフトウェアは、一度限りの成果物ではなく長期運用する製品です。マージ・公開可能な変更は、次の基準をすべて満たす必要があります。

> 「この剣は、私が鍛え上げました。あとは皆さんに託します。」

- 利用者向けの動作、対応範囲、リリース説明を、繁体字中国語・簡体字中国語・英語・日本語の4言語で同期します。
- CI、CodeQL、依存関係の脆弱性監査、Gitleaks の機密情報検査を実際に実行し、装飾用バッジを検証の代わりにしません。
- API キー、OAuth Secret、トークン、サイト認証情報、非公開エクスポート、個人データベースをコミット、梱包、公開しません。
- 公開成果物を、明確なバージョン、Git commit、CI 記録、SHA256、対応するソースコードまで追跡可能にします。
- 既存の正常な機能を維持し、変更を通すために安全ゲート、テスト、権限、確認手順を弱めません。
- クラウド CI の成功はクリーン環境でのビルド証拠としてのみ扱い、実機証拠なしに macOS、Linux、その他の完全互換性を誇張しません。
- 利用者向けのバージョン、ダウンロード方法、対応状況、安全動作が変わる場合、プロジェクト文書と炎剣公式サイトを同期します。
- 特別な手順を人が覚えていることに依存する一度限りの例外運用を認めず、反復作業はテスト可能・監査可能・自動化可能な長期機構にします。

## 繁體中文

歡迎 Issue 與 Pull Request。請先在最新版重現問題，且不要附上私人 Facebook ZIP、網站帳密、OAuth Client Secret、Token 或個人文章資料庫。PR 必須說明問題、做法、使用者影響及實際驗證結果；不得為了通過測試而降低 DPAPI、安全保管庫、防重複、暫停續傳或確認流程。

共用核心或 Avalonia 變更至少要執行：

```powershell
dotnet build src/FB2WordPress.Core/FB2WordPress.Core.csproj -c Release
dotnet run --project tests/CoreAudit/FB2WordPress.Core.Audit.csproj -c Release
dotnet build src/FB2WordPress.Desktop/FB2WordPress.Desktop.csproj -c Release
```

Windows 完整版變更另須執行 `dotnet build src/FB2WordPress/FB2WordPress.csproj -c Release` 與 `dotnet run --project tests/AuditHarness/WordPressAuditHarness.csproj -c Release`。macOS／Linux 相容性回報請附作業系統版本、桌面環境、重現步驟、預期與實際結果；UI 問題請附畫面。雲端 CI 成功不能取代實機證據。使用者可見行為或支援狀態改變時，繁中、簡中、英文、日文文件必須一起更新。

## 简体中文

欢迎提交 Issue 和 Pull Request。请先在最新版复现问题，并且不要附上私人 Facebook ZIP、网站账号、OAuth Client Secret、Token 或个人文章数据库。PR 必须说明问题、解决方式、用户影响和实际验证结果；不得为了通过测试而降低 DPAPI、安全存储、防重复、断点续传或确认流程。

共享核心或 Avalonia 变更至少要运行：

```powershell
dotnet build src/FB2WordPress.Core/FB2WordPress.Core.csproj -c Release
dotnet run --project tests/CoreAudit/FB2WordPress.Core.Audit.csproj -c Release
dotnet build src/FB2WordPress.Desktop/FB2WordPress.Desktop.csproj -c Release
```

Windows 完整版变更还必须运行 `dotnet build src/FB2WordPress/FB2WordPress.csproj -c Release` 和 `dotnet run --project tests/AuditHarness/WordPressAuditHarness.csproj -c Release`。macOS／Linux 兼容性报告请附操作系统版本、桌面环境、复现步骤、预期与实际结果；界面问题请附画面。云端 CI 成功不能代替实机证据。用户可见行为或支持状态发生变化时，繁中、简中、英文、日文文档必须一起更新。

## English

Issues and pull requests are welcome. Reproduce the issue on the latest version and never attach a private Facebook ZIP, site credentials, OAuth client secrets, tokens, or personal content databases. A PR must explain the problem, approach, user impact, and evidence from actual validation. Do not weaken DPAPI, secure-vault rules, duplicate prevention, resumable progress, or confirmation flows merely to make a test pass.

Shared-core or Avalonia changes must run at least:

```powershell
dotnet build src/FB2WordPress.Core/FB2WordPress.Core.csproj -c Release
dotnet run --project tests/CoreAudit/FB2WordPress.Core.Audit.csproj -c Release
dotnet build src/FB2WordPress.Desktop/FB2WordPress.Desktop.csproj -c Release
```

Changes to the complete Windows app must also run `dotnet build src/FB2WordPress/FB2WordPress.csproj -c Release` and `dotnet run --project tests/AuditHarness/WordPressAuditHarness.csproj -c Release`. macOS or Linux compatibility reports must include the OS version, desktop environment, reproduction steps, expected result, and actual result; include screenshots for UI defects. Cloud CI is not a substitute for real-device evidence. User-visible behavior or support-status changes must update Traditional Chinese, Simplified Chinese, English, and Japanese documentation together.

## 日本語

Issue と Pull Request を歓迎します。最新版で問題を再現し、非公開の Facebook ZIP、サイト認証情報、OAuth Client Secret、トークン、個人記事データベースを添付しないでください。PR には問題、対応方法、利用者への影響、実際の検証結果を記載してください。テストを通すためだけに DPAPI、安全な保管庫、重複防止、再開可能な進捗、確認手順を弱めてはいけません。

共有コアまたは Avalonia を変更した場合、少なくとも次を実行してください。

```powershell
dotnet build src/FB2WordPress.Core/FB2WordPress.Core.csproj -c Release
dotnet run --project tests/CoreAudit/FB2WordPress.Core.Audit.csproj -c Release
dotnet build src/FB2WordPress.Desktop/FB2WordPress.Desktop.csproj -c Release
```

Windows 完全版の変更では、さらに `dotnet build src/FB2WordPress/FB2WordPress.csproj -c Release` と `dotnet run --project tests/AuditHarness/WordPressAuditHarness.csproj -c Release` を実行してください。macOS／Linux の互換性報告には OS バージョン、デスクトップ環境、再現手順、期待結果、実際の結果を含め、UI 問題には画面を添付してください。クラウド CI は実機証拠の代わりになりません。利用者向け動作や対応状況を変更する場合は、繁体字中国語、簡体字中国語、英語、日本語の文書を同時に更新してください。
