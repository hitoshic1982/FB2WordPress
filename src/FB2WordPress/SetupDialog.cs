namespace FB2WordPress;

internal sealed class SetupDialog : Form
{
    readonly TextBox site = new() { Width = 500, PlaceholderText = "https://你的網域" };
    readonly TextBox user = new() { Width = 500, PlaceholderText = "WordPress 使用者名稱" };
    readonly TextBox password = new() { Width = 500, PlaceholderText = "WordPress 應用程式密碼", UseSystemPasswordChar = true };
    readonly TextBox clientId = new() { Width = 500, PlaceholderText = "Google OAuth Desktop Client ID（上傳影片用）" };
    readonly TextBox secret = new() { Width = 500, PlaceholderText = "Google Client Secret（如有）", UseSystemPasswordChar = true };
    readonly ComboBox privacy = new() { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly CheckBox draft = new() { Text = "文章先存為草稿", AutoSize = true };
    public AppSettings Settings { get; }

    public SetupDialog(AppSettings settings)
    {
        Settings = settings; Text = "FB2WordPress 連線設定"; Width = 590; Height = 510;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; Font = new("Microsoft JhengHei UI", 10);
        site.Text = settings.SiteUrl; user.Text = settings.WordPressUser; password.Text = settings.WordPressAppPassword;
        clientId.Text = settings.ClientId; secret.Text = settings.ClientSecret;
        privacy.Items.AddRange(["不公開", "知道連結者可看", "公開"]);
        privacy.SelectedIndex = settings.VideoPrivacy switch { "public" => 2, "unlisted" => 1, _ => 0 }; draft.Checked = settings.CreateAsDraft;
        var note = new Label { AutoSize = false, Width = 520, Height = 100, Text = "第一次設定：在 WordPress 後台 → 使用者 → 個人資料 → 應用程式密碼，建立名稱 FB2WordPress。\r\n\r\n請貼入應用程式密碼，不要填 WordPress 登入密碼。資料會由 Windows 加密保存在這台電腦。" };
        var save = new Button { Text = "測試連線並儲存", AutoSize = true, Height = 38 };
        var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) =>
        {
            if (!Uri.TryCreate(site.Text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(user.Text) || string.IsNullOrWhiteSpace(password.Text))
            { MessageBox.Show("請填入 HTTPS 網站網址、WordPress 使用者名稱及應用程式密碼。", "FB2WordPress"); return; }
            settings.SiteUrl = site.Text.Trim().TrimEnd('/'); settings.WordPressUser = user.Text.Trim(); settings.WordPressAppPassword = password.Text.Replace(" ", "").Trim();
            if (settings.ClientId != clientId.Text.Trim()) { settings.RefreshToken = ""; settings.AuthorizedScopeVersion = 0; }
            settings.ClientId = clientId.Text.Trim(); settings.ClientSecret = secret.Text.Trim();
            settings.VideoPrivacy = privacy.SelectedIndex switch { 2 => "public", 1 => "unlisted", _ => "private" }; settings.CreateAsDraft = draft.Checked;
            SettingsStore.Save(settings); DialogResult = DialogResult.OK;
        };
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new(22), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        panel.Controls.Add(note); panel.Controls.Add(site); panel.Controls.Add(user); panel.Controls.Add(password); panel.Controls.Add(clientId); panel.Controls.Add(secret);
        var row = new FlowLayoutPanel { Width = 520, Height = 38 }; row.Controls.Add(new Label { Text = "YouTube：", AutoSize = true, Padding = new(0, 8, 0, 0) }); row.Controls.Add(privacy); row.Controls.Add(draft); panel.Controls.Add(row);
        var buttons = new FlowLayoutPanel { Width = 520, Height = 48 }; buttons.Controls.Add(save); buttons.Controls.Add(cancel); panel.Controls.Add(buttons);
        Controls.Add(panel); AcceptButton = save; CancelButton = cancel;
    }
}
