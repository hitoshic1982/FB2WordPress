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

一款 Windows 桌面工具，用于整理 Facebook 官方下载资料中的帖子、图片和视频，并迁移到自己管理的 WordPress 网站。

> 社交平台适合接触读者，自己的网站才是内容长期安家的地方。FB2WordPress 帮助创作者把文章和媒体转化为自己掌控的数字资产。

## 网站才是品牌总部

2026 年 7 月，炎剑文化工作室的 Facebook 粉丝专页突然被停权。与其继续把时间耗在没有明确回应的申诉流程，作者选择在 Bluehost 建立 WordPress 主站，把文章、图片、搜索能见度和品牌入口重新掌握在自己手中。FB2WordPress 是这场三日重建行动中，把旧内容送回品牌总部的核心工具。

本工具只读取由你本人通过 Facebook“下载你的信息”取得的 ZIP，并通过 WordPress 官方 REST API 写入你自己的网站。它不会登录、抓取或绕过 Facebook，也无法恢复被停权的账号或专页。

## 从社交备份走向可经营的网站

- 解析 Facebook 官方导出的帖子 JSON，并修复部分旧版编码乱码。
- 保留时间、正文、Emoji 和 Hashtag；Hashtag 可建立为 WordPress 标签。
- 图片经过安全优化后上传媒体库，视频可选上传 YouTube 并嵌入文章。
- 支持草稿模式和隐藏识别标记，避免重复导入。
- 保存迁移进度，可中断后继续，并能从备份恢复损坏的进度文件。
- 只整理本工具导入文章的多余空行，不随意修改网站其他内容。
- 阻止恶意 ZIP 路径穿越，不修改原始 ZIP 和原图。

## 开始前先保护你的网站

1. Windows 10/11 x64。
2. 由你管理、启用 HTTPS 和 WordPress REST API 的网站。
3. 在 WordPress 个人资料中建立专用“应用程序密码”，不要使用主登录密码。
4. 从 Facebook 下载自己的 JSON 数据 ZIP。
5. 只有需要上传 YouTube 视频时，才需 Google Desktop OAuth Client 和 YouTube Data API v3。
6. 从 [GitHub Releases](https://github.com/hitoshic1982/FB2WordPress/releases/latest) 下载最新版 `FB2WordPress.exe` 并核对 SHA256。

## 第一次迁移建议这样做

1. 输入 WordPress 网站地址、用户名和专用应用程序密码。
2. 选择 Facebook ZIP。
3. 选择公开或草稿模式，首次建议使用草稿。
4. 迁移后查看报告，并抽查文章、标签、媒体和日期。

请先备份 WordPress，并使用测试站或草稿模式验证。主机限制、REST API 防火墙、Facebook 导出格式差异和大量媒体可能需要分批处理。

## 网站凭证如何处理

- WordPress 应用程序密码、OAuth 凭证和刷新令牌只存放于当前 Windows 用户的 LocalAppData，并以 Windows DPAPI 加密。
- 仓库不包含作者的账号、密钥、令牌、Facebook 导出文件或个人文章。
- 软件直接连接你的网站和选用的 Google API，不通过炎剑文化工作室的中转服务器。
- 建议为本工具建立可撤销的专用应用程序密码，完成迁移后即可撤销。

详见 [PRIVACY.md](PRIVACY.md) 与 [SECURITY.md](SECURITY.md)。

## 跨平台开发状态

目前只有 Windows WinForms 版具备完整迁移、图片处理和安全凭据流程。`v1.1.0-rc.1` 将另外提供 **macOS x64 未签名 Preview DMG** 与 **Linux x86_64 Preview AppImage**；两者只是可启动的 Avalonia 移植入口，不含完整迁移流程，不能取代 Windows 完整版。

- DMG 由原生 macOS Intel GitHub runner 制作，内含真正的 x64 `.app`。因为没有 Apple 开发者签名，首次打开时可能需要按住 Control 点击“打开”，或在“隐私与安全性”中允许打开。Apple Silicon 目前不是原生版本，可能需要 Rosetta 2，且尚未验证。
- AppImage 由原生 Linux x86_64 runner 制作。下载后运行 `chmod +x FB2WordPress-*-Preview.AppImage`，然后直接启动；仍需要常见 Linux 桌面图形库。
- CI 会从挂载后的 DMG 与最终 AppImage 启动窗口并确认进程存活，同时附带 `SHA256SUMS.txt` 和 SPDX SBOM；非 PR 构建在 GitHub 支持时还会生成来源证明。
- 这些结果只证明干净的云端 runner 能够构建、打包并启动，**不代表作者已经在 macOS／Linux 实机完成验证，也不代表全部功能可用**。

完整支持矩阵、安全存储原则与后续路线请参阅 [跨平台开发说明](docs/CROSS_PLATFORM.md)，本次 Preview 发布文字见 [`RELEASE_NOTES_v1.1.0-rc.1.md`](RELEASE_NOTES_v1.1.0-rc.1.md)。

## 开发者入口

> 「剑，我已锻成；余下的路，就交给你们了。」详见[炎剑开源软件家族质量标准](CONTRIBUTING.md)。

```powershell
dotnet build src/FB2WordPress/FB2WordPress.csproj -c Release
dotnet run --project tests/AuditHarness/WordPressAuditHarness.csproj -c Release
```

需要 .NET 10 SDK。Release 提供自包含单文件 EXE。

## 开源与责任

本项目采用 [MIT License](LICENSE)，与 Meta、Facebook、Automattic、WordPress Foundation、Google 或 YouTube 没有隶属或背书关系。请只迁移你有权处理的内容，并遵守平台条款、版权和个人信息法规。

## 自由赞助

FB2WordPress 依 MIT 许可证完整开放，迁移、图片优化与文章整理功能不会因是否赞助而有差别。如果它帮助你将内容真正带回自己的网站，欢迎自愿支持炎剑文化工作室继续维护与改进：

- [Buy Me a Coffee](https://buymeacoffee.com/flameblade_studio)
- [PayPal.Me](https://www.paypal.com/paypalme/flamebladestudio)

赞助不是使用条件；分享实际经验、报告兼容性问题或参与 PR，也能帮助更多创作者。

作者：CHOU MING HUA／炎剑文化工作室 · [官方网站](https://www.flamebladestudio.com.tw/)
