# FB2WordPress
<p align="center">
  <a href="https://github.com/hitoshic1982/FB2WordPress/actions/workflows/ci.yml"><img alt="Cross-platform CI" src="https://github.com/hitoshic1982/FB2WordPress/actions/workflows/ci.yml/badge.svg"></a>
  <a href="https://github.com/hitoshic1982/FB2WordPress/actions/workflows/codeql.yml"><img alt="CodeQL" src="https://github.com/hitoshic1982/FB2WordPress/actions/workflows/codeql.yml/badge.svg"></a>
  <a href="https://github.com/hitoshic1982/FB2WordPress/actions/workflows/security-audit.yml"><img alt="Security Audit / NuGet" src="https://github.com/hitoshic1982/FB2WordPress/actions/workflows/security-audit.yml/badge.svg"></a>
  <a href="https://github.com/hitoshic1982/FB2WordPress/actions/workflows/secret-defense.yml"><img alt="Secret Defense / Gitleaks" src="https://github.com/hitoshic1982/FB2WordPress/actions/workflows/secret-defense.yml/badge.svg"></a>
  <a href="https://github.com/hitoshic1982/FB2WordPress/releases"><img alt="Latest release" src="https://img.shields.io/github/v/release/hitoshic1982/FB2WordPress?label=release"></a>
  <a href="LICENSE"><img alt="MIT License" src="https://img.shields.io/badge/license-MIT-blue.svg"></a>
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&amp;logoColor=white">
  <img alt="Four interface languages" src="https://img.shields.io/badge/interface%20languages-4-informational">
</p>

[繁體中文](README.md) · [简体中文](README.zh-CN.md) · [English](README.en.md) · [日本語](README.ja.md)

A Windows desktop tool that moves posts, images, and videos from an official Facebook data export to a self-managed WordPress site.

> Social platforms are useful for reaching readers; your own website is where your work can live for the long term. FB2WordPress helps creators turn posts and media into digital assets they control.

## Make your website the brand headquarters

In July 2026, Flameblade Studio's Facebook Page was suddenly disabled. Instead of spending all his time inside an opaque appeal process, the creator built a WordPress headquarters on Bluehost and took back control of his articles, media, search presence, and brand entry point. FB2WordPress became the core tool that carried years of material into that new home during a three-day rebuild.

The app reads a ZIP that you obtained through Facebook's official “Download your information” feature and uses the official WordPress REST API to write to your own site. It does not sign in to, scrape, or bypass Facebook, and it cannot restore a disabled account or Page.

## From a social backup to a working publication

- Parses post JSON from official Facebook exports, including repair for some legacy encoding damage.
- Preserves dates, text, emoji, and hashtags; hashtags become WordPress tags.
- Optimizes images safely before uploading them to the media library; videos can optionally be uploaded to YouTube and embedded.
- Supports drafts and hidden migration markers to prevent duplicate imports.
- Saves resumable progress and recovers corrupted progress data from a backup.
- Normalizes excessive blank lines only in content imported by this tool.
- Blocks ZIP path traversal and never modifies the source ZIP or original images.

## Protect the site before you begin

1. Windows 10/11 x64.
2. A WordPress site you manage with HTTPS and the REST API enabled.
3. A dedicated WordPress Application Password created in your user profile. Do not use your primary login password.
4. Your own Facebook data export in JSON format.
5. A Google Desktop OAuth client and YouTube Data API v3 only if you want to upload videos to YouTube.
6. Download the latest `FB2WordPress.exe` from [GitHub Releases](https://github.com/hitoshic1982/FB2WordPress/releases/latest) and verify its SHA256.

## A safe first migration

1. Enter the WordPress URL, username, and dedicated Application Password.
2. Select the Facebook ZIP.
3. Choose draft or published mode; draft mode is strongly recommended for the first run.
4. Start the migration, review the report, and spot-check posts, tags, media, and dates.

Back up WordPress first and validate with a staging site or drafts. Hosting limits, REST API firewalls, Facebook export variations, and large media libraries may require multiple sessions.

## How site credentials are handled

- The WordPress Application Password, OAuth credentials, and refresh tokens are stored only in the current Windows user's LocalAppData and encrypted with Windows DPAPI.
- The repository contains no author credentials, tokens, Facebook exports, or private content databases.
- The app communicates directly with your WordPress site and optional Google APIs. Flameblade Studio operates no relay server that receives your content.
- Use a revocable Application Password dedicated to this tool, and revoke it after migration if no longer needed.

See [PRIVACY.md](PRIVACY.md) and [SECURITY.md](SECURITY.md).

## Cross-platform development status

Only the Windows WinForms application currently provides the complete migration, image-processing, and secure-credential workflow. The shared core and minimal Avalonia entry point build and run tests in Windows, macOS, and Linux CI, but that is not a claim that complete macOS or Linux releases exist or have passed real-device validation. See the [cross-platform development guide](docs/CROSS_PLATFORM.md) for the support matrix, vault policy, and roadmap.

## Developer entry point

> “I have forged this sword. What comes next is up to you.” See the [Flameblade Open Source Software Family Quality Standard](CONTRIBUTING.md).

```powershell
dotnet build src/FB2WordPress/FB2WordPress.csproj -c Release
dotnet run --project tests/AuditHarness/WordPressAuditHarness.csproj -c Release
```

.NET 10 SDK is required. Release builds are self-contained single-file executables.

## Open source and responsibility

Licensed under the [MIT License](LICENSE). This independent project is not affiliated with or endorsed by Meta, Facebook, Automattic, the WordPress Foundation, Google, or YouTube. Migrate only content you are authorized to handle, and comply with platform terms, copyright, and privacy laws.

## Voluntary support

FB2WordPress is fully available under the MIT License. Migration, image optimization, and article-cleanup features are identical for everyone, whether or not they donate. If the tool helped bring your work back under your own domain, you may voluntarily support Flameblade Studio's ongoing maintenance:

- [Buy Me a Coffee](https://buymeacoffee.com/flameblade_studio)
- [PayPal.Me](https://www.paypal.com/paypalme/flamebladestudio)

Donations are never a condition of use. Field reports, compatibility feedback, and pull requests also help the wider creator community.

Author: CHOU MING HUA / Flameblade Studio · [Official website](https://www.flamebladestudio.com.tw/)
