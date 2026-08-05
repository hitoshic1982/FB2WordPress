namespace FB2WordPress;

internal static class PlatformPresentation
{
    public static string FontName => L.Language == "ja" ? "Yu Gothic UI" : L.Language == "en" ? "Segoe UI" : "Microsoft JhengHei UI";

    public static string SecureStorageNote => L.P(
        "憑證會由 Windows DPAPI 加密，並只供目前的 Windows 使用者帳戶解密。",
        "凭据会由 Windows DPAPI 加密，并且只能由当前 Windows 用户账户解密。",
        "Credentials are encrypted with Windows DPAPI and can only be decrypted by the current Windows user account.",
        "資格情報は Windows DPAPI で暗号化され、現在の Windows ユーザーアカウントだけが復号できます。"
    );
}
