namespace FB2WordPress;

internal sealed class SetupDialog : Form
{
    readonly TextBox site = new() { Width = 540, PlaceholderText = L.T("site_placeholder") };
    readonly TextBox user = new() { Width = 540, PlaceholderText = L.T("user_placeholder") };
    readonly TextBox password = new() { Width = 540, PlaceholderText = L.T("password_placeholder"), UseSystemPasswordChar = true };
    readonly TextBox clientId = new() { Width = 540, PlaceholderText = L.T("google_client_placeholder") };
    readonly TextBox secret = new() { Width = 540, PlaceholderText = L.T("google_secret_placeholder"), UseSystemPasswordChar = true };
    readonly ComboBox privacy = new() { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox language = new() { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly CheckBox draft = new() { Text = L.T("draft_default"), AutoSize = true };
    public AppSettings Settings { get; }

    public SetupDialog(AppSettings settings)
    {
        Settings = settings; Text = L.T("setup_title"); Width = 640; Height = 650;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; Font = new(PlatformPresentation.FontName, 10);
        site.Text = settings.SiteUrl; user.Text = settings.WordPressUser; password.Text = settings.WordPressAppPassword;
        clientId.Text = settings.ClientId; secret.Text = settings.ClientSecret;
        language.DataSource = L.Supported.ToArray();
        language.SelectedIndex = Math.Max(0, Array.FindIndex(L.Supported, item => item.Code == (string.IsNullOrEmpty(settings.InterfaceLanguage) ? L.Language : settings.InterfaceLanguage)));
        privacy.Items.AddRange([L.T("privacy_private"), L.T("privacy_unlisted"), L.T("privacy_public")]);
        privacy.SelectedIndex = settings.VideoPrivacy switch { "public" => 2, "unlisted" => 1, _ => 0 }; draft.Checked = settings.CreateAsDraft;
        var note = new Label { AutoSize = false, Width = 560, Height = 135, Text = L.T("setup_note") + "\r\n\r\n" + PlatformPresentation.SecureStorageNote };
        var save = new Button { Text = L.T("save_test"), AutoSize = true, Height = 38 };
        var cancel = new Button { Text = L.T("cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) =>
        {
            if (!Uri.TryCreate(site.Text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(user.Text) || string.IsNullOrWhiteSpace(password.Text))
            { MessageBox.Show(L.T("connection_required"), "FB2WordPress"); return; }
            settings.SiteUrl = site.Text.Trim().TrimEnd('/'); settings.WordPressUser = user.Text.Trim(); settings.WordPressAppPassword = password.Text.Replace(" ", "").Trim();
            if (settings.ClientId != clientId.Text.Trim()) { settings.RefreshToken = ""; settings.AuthorizedScopeVersion = 0; }
            settings.ClientId = clientId.Text.Trim(); settings.ClientSecret = secret.Text.Trim();
            settings.VideoPrivacy = privacy.SelectedIndex switch { 2 => "public", 1 => "unlisted", _ => "private" }; settings.CreateAsDraft = draft.Checked;
            settings.InterfaceLanguage = ((LanguageOption)language.SelectedItem!).Code;
            SettingsStore.Save(settings); DialogResult = DialogResult.OK;
        };
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new(22), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var languageRow = new FlowLayoutPanel { Width = 560, Height = 58 };
        languageRow.Controls.Add(new Label { Text = L.T("language"), AutoSize = true, Padding = new(0, 8, 0, 0) }); languageRow.Controls.Add(language);
        languageRow.Controls.Add(new Label { Text = L.T("language_restart"), AutoSize = true, MaximumSize = new(330, 0), Padding = new(8, 8, 0, 0) });
        panel.Controls.Add(note); panel.Controls.Add(languageRow); panel.Controls.Add(site); panel.Controls.Add(user); panel.Controls.Add(password); panel.Controls.Add(clientId); panel.Controls.Add(secret);
        var row = new FlowLayoutPanel { Width = 560, Height = 42 }; row.Controls.Add(new Label { Text = "YouTube：", AutoSize = true, Padding = new(0, 8, 0, 0) }); row.Controls.Add(privacy); row.Controls.Add(draft); panel.Controls.Add(row);
        var buttons = new FlowLayoutPanel { Width = 560, Height = 48 }; buttons.Controls.Add(save); buttons.Controls.Add(cancel); panel.Controls.Add(buttons);
        Controls.Add(panel); AcceptButton = save; CancelButton = cancel;
    }
}
