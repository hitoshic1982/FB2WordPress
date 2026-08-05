# FB2WordPress
<p align="center">
  <a href="https://github.com/hitoshic1982/FB2WordPress/actions/workflows/ci.yml"><img alt="Cross-platform CI" src="https://github.com/hitoshic1982/FB2WordPress/actions/workflows/ci.yml/badge.svg"></a>
  <a href="https://github.com/hitoshic1982/FB2WordPress/actions/workflows/preview-packages.yml"><img alt="Native Preview Packages" src="https://github.com/hitoshic1982/FB2WordPress/actions/workflows/preview-packages.yml/badge.svg"></a>
  <a href="https://github.com/hitoshic1982/FB2WordPress/actions/workflows/codeql.yml"><img alt="CodeQL" src="https://github.com/hitoshic1982/FB2WordPress/actions/workflows/codeql.yml/badge.svg"></a>
  <a href="https://github.com/hitoshic1982/FB2WordPress/actions/workflows/security-audit.yml"><img alt="Security Audit / NuGet" src="https://github.com/hitoshic1982/FB2WordPress/actions/workflows/security-audit.yml/badge.svg"></a>
  <a href="https://github.com/hitoshic1982/FB2WordPress/actions/workflows/secret-defense.yml"><img alt="Secret Defense / Gitleaks" src="https://github.com/hitoshic1982/FB2WordPress/actions/workflows/secret-defense.yml/badge.svg"></a>
  <a href="https://github.com/hitoshic1982/FB2WordPress/releases"><img alt="Latest release" src="https://img.shields.io/github/v/release/hitoshic1982/FB2WordPress?label=release"></a>
  <a href="LICENSE"><img alt="MIT License" src="https://img.shields.io/badge/license-MIT-blue.svg"></a>
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&amp;logoColor=white">
  <img alt="Four interface languages" src="https://img.shields.io/badge/interface%20languages-4-informational">
</p>

[繁體中文](README.md) · [简体中文](README.zh-CN.md) · [English](README.en.md) · [日本語](README.ja.md)

Facebook の公式ダウンロードデータに含まれる投稿・画像・動画を整理し、自分で管理する WordPress サイトへ移行する Windows デスクトップツールです。

> SNS は読者と出会う場所、自分のサイトは作品が長く暮らす場所です。FB2WordPress は、投稿とメディアを自分で管理できるデジタル資産へ移す手助けをします。

## 自分のサイトをブランドの本部にする

2026 年 7 月、Flameblade Studio の Facebook ページが突然停止されました。先の見えない異議申立てだけに時間を使う代わりに、作者は Bluehost 上に WordPress の本拠地を作り、記事、画像、検索での発見性、ブランドへの入口を自分の管理下へ戻しました。FB2WordPress は、3 日間の再建で長年のコンテンツを新しい本部へ運ぶ中核ツールになりました。

本アプリは、利用者本人が Facebook の「個人データをダウンロード」機能から取得した ZIP を読み、WordPress 公式 REST API で自分のサイトへ投稿します。Facebook への自動ログイン、スクレイピング、制限回避は行わず、停止されたアカウントやページを復旧する機能もありません。

## SNS のバックアップから運営できるサイトへ

- Facebook 公式エクスポートの投稿 JSON を解析し、一部の旧形式の文字化けも修復。
- 投稿日時、本文、絵文字、ハッシュタグを保持し、ハッシュタグを WordPress タグへ変換。
- 画像を安全に最適化してメディアライブラリへアップロード。動画は任意で YouTube へ送り、記事に埋め込み可能。
- 下書きと非表示の識別マーカーで、公開前確認と重複防止に対応。
- 中断後の再開、進捗ファイル破損時のバックアップ復元に対応。
- 本ツールが取り込んだ記事だけを対象に、過剰な空行を整理。
- ZIP パストラバーサルを防止し、元の ZIP と画像を変更しない設計。

## 始める前にサイトを守る

1. Windows 10/11 x64。
2. HTTPS と REST API が有効な、自分で管理する WordPress サイト。
3. WordPress のプロフィールで発行した専用の「アプリケーションパスワード」。通常のログインパスワードは使わないでください。
4. Facebook から自分で取得した JSON 形式のデータ ZIP。
5. YouTube へ動画を送る場合のみ、Google Desktop OAuth Client と YouTube Data API v3。
6. [GitHub Releases](https://github.com/hitoshic1982/FB2WordPress/releases/latest) から最新版 `FB2WordPress.exe` を取得し、SHA256 を確認してください。

## 初回移行の安全な進め方

1. WordPress の URL、ユーザー名、専用アプリケーションパスワードを入力します。
2. Facebook ZIP を選びます。
3. 下書き／公開モードを選びます。初回は下書きを推奨します。
4. 移行後にレポートを確認し、投稿・タグ・メディア・日付を抜き取り確認します。

事前に WordPress をバックアップし、ステージングサイトまたは下書きで検証してください。ホスティング制限、REST API のファイアウォール、Facebook 形式の差、メディア量により、複数回に分ける場合があります。

## サイト認証情報の扱い

- WordPress アプリケーションパスワード、OAuth 情報、更新トークンは現在の Windows ユーザーの LocalAppData のみに保存し、Windows DPAPI で暗号化します。
- リポジトリに作者の認証情報、トークン、Facebook データ、個人記事は含まれません。
- アプリは利用者の WordPress と任意の Google API に直接接続し、Flameblade Studio の中継サーバーへ内容を送りません。
- 本ツール専用の取消可能なアプリケーションパスワードを使い、不要になったら取り消してください。

[PRIVACY.md](PRIVACY.md) と [SECURITY.md](SECURITY.md) もご覧ください。

## クロスプラットフォーム開発状況

現在、完全な移行、画像処理、安全な資格情報保存を利用できるのは Windows WinForms 版だけです。`v1.1.0-rc.1` では、**Windows x64 完全版 EXE**、**macOS x64 と Apple Silicon arm64 の個別の未署名 Preview DMG**、および **Linux x86_64 Preview AppImage** を提供します。macOS／Linux 版は起動可能な Avalonia 移植入口であり、完全な移行製品でも Windows 完全版の代替でもありません。

- 2種類の DMG は Intel x64 と Apple Silicon arm64 のネイティブ macOS GitHub runner で個別に作成・起動検証します。Apple Silicon 利用者は Rosetta 2 に依存せず、ネイティブ arm64 版を利用できます。Apple Developer 署名がないため、初回は Control キーを押しながら「開く」を選ぶか、「プライバシーとセキュリティ」で許可する必要があります。
- AppImage はネイティブの Linux x86_64 runner で作成します。ダウンロード後に `chmod +x FB2WordPress-*-Preview.AppImage` を実行してから起動してください。一般的な Linux デスクトップ用グラフィックスライブラリは必要です。
- CI は最終 Windows EXE、マウントした2種類の DMG、最終 AppImage を起動し、各プロセスの存続を確認します。`v1.1.0-rc.N`（`N > 0`）に厳密一致し、ソースのバージョンとも一致し、タグの commit が `origin/main` に含まれる場合に限り、単一の `SHA256SUMS.txt`、各プラットフォームの SPDX SBOM、来歴証明をまとめ、この4言語説明から GitHub prerelease を作成します。PR と通常の `main` push は読み取り専用検証だけを行い、公開しません。
- これはクリーンなクラウド runner 上でのビルド、梱包、起動の証拠です。**作者による macOS／Linux 実機検証でも、全機能対応の表明でもありません**。

完全な対応表、安全な保管庫の原則、今後の計画は [クロスプラットフォーム開発ガイド](docs/CROSS_PLATFORM.md)、今回の4言語 Preview 公開文は [`RELEASE_NOTES_v1.1.0-rc.1.md`](RELEASE_NOTES_v1.1.0-rc.1.md) をご覧ください。

## 開発者向け

> 「この剣は、私が鍛え上げました。あとは皆さんに託します。」[炎剣オープンソースソフトウェアファミリー品質基準](CONTRIBUTING.md)もご覧ください。

```powershell
dotnet build src/FB2WordPress/FB2WordPress.csproj -c Release
dotnet run --project tests/AuditHarness/WordPressAuditHarness.csproj -c Release
```

.NET 10 SDK が必要です。Release の EXE は自己完結型の単一ファイルです。

## ライセンスと責任

[MIT License](LICENSE) で公開しています。本プロジェクトは Meta、Facebook、Automattic、WordPress Foundation、Google、YouTube と提携または公認されたものではありません。権利を持つコンテンツだけを移行し、各サービスの規約、著作権、個人情報保護法令を守ってください。

## 任意のご支援

FB2WordPress は MIT ライセンスのもとですべての機能を公開しています。移行、画像最適化、記事整理の機能は、支援の有無にかかわらず同一です。コンテンツを自分のドメインへ取り戻す助けになった場合は、炎剣文化工作室の継続的な保守を任意でご支援いただけます。

- [Buy Me a Coffee](https://buymeacoffee.com/flameblade_studio)
- [PayPal.Me](https://www.paypal.com/paypalme/flamebladestudio)

ご支援は利用条件ではありません。実環境での報告、互換性に関する情報、プルリクエストも多くのクリエイターを助けます。

作者：CHOU MING HUA／Flameblade Studio · [公式サイト](https://www.flamebladestudio.com.tw/)
