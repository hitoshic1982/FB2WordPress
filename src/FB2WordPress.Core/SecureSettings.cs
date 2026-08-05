using System.Text.Json;

namespace FB2WordPress;

/// <summary>Platform secret stores implement this contract with DPAPI, Keychain, or Secret Service.</summary>
public interface ISecretVault
{
    bool IsAvailable { get; }
    ValueTask<string?> ReadAsync(string key, CancellationToken cancellationToken = default);
    ValueTask WriteAsync(string key, string value, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(string key, CancellationToken cancellationToken = default);
}

public enum CredentialChangeMode { Preserve, Clear, Replace }

/// <summary>Explicit credential intent; public-preference saves never infer clearing from empty fields.</summary>
public sealed record CredentialChange
{
    public CredentialChangeMode Mode { get; }
    public string WordPressAppPassword { get; }
    public string GoogleClientSecret { get; }
    public string GoogleRefreshToken { get; }

    CredentialChange(CredentialChangeMode mode, string wordpressPassword = "", string clientSecret = "", string refreshToken = "")
    {
        Mode = mode;
        WordPressAppPassword = wordpressPassword;
        GoogleClientSecret = clientSecret;
        GoogleRefreshToken = refreshToken;
    }

    public static CredentialChange Preserve { get; } = new(CredentialChangeMode.Preserve);
    public static CredentialChange Clear { get; } = new(CredentialChangeMode.Clear);

    public static CredentialChange Replace(string wordpressPassword, string clientSecret, string refreshToken) =>
        new(CredentialChangeMode.Replace, wordpressPassword ?? "", clientSecret ?? "", refreshToken ?? "");

    public static CredentialChange ReplaceFrom(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Replace(settings.WordPressAppPassword, settings.ClientSecret, settings.RefreshToken);
    }
}

/// <summary>
/// Stores public preferences in recoverable JSON and credentials in an OS-backed vault.
/// A durable journal records every credential generation before staging begins. Recovery
/// compares that journal with the public generation pointer, then either finishes cleanup
/// or rolls the uncommitted generation back.
/// </summary>
public sealed class CrossPlatformSettingsStore
{
    const int CurrentSchemaVersion = 2;
    const int JournalSchemaVersion = 1;
    const string LegacyGeneration = "legacy";
    const string WordPressPasswordKey = "wordpress-application-password";
    const string GoogleClientSecretKey = "google-client-secret";
    const string GoogleRefreshTokenKey = "google-refresh-token";
    static readonly string[] SecretKeys = [WordPressPasswordKey, GoogleClientSecretKey, GoogleRefreshTokenKey];

    readonly ISecretVault secrets;
    readonly IAtomicDocumentStore settingsDocuments;
    readonly IAtomicDocumentStore journalDocuments;
    readonly ISettingsTransactionLock transactionLock;
    readonly SemaphoreSlim gate = new(1, 1);
    readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

    public CrossPlatformSettingsStore(
        ISecretVault secrets,
        string? folder = null,
        IAtomicDocumentStore? settingsDocuments = null,
        IAtomicDocumentStore? journalDocuments = null,
        ISettingsTransactionLock? transactionLock = null,
        TimeSpan? transactionLockTimeout = null)
    {
        this.secrets = secrets;
        var root = Path.GetFullPath(folder ?? PlatformPaths.LocalDataDirectory);
        this.settingsDocuments = settingsDocuments ?? new FileAtomicDocumentStore(Path.Combine(root, "settings.json"));
        this.journalDocuments = journalDocuments ?? new FileAtomicDocumentStore(Path.Combine(root, "settings.pending.json"));
        this.transactionLock = transactionLock ?? new FileSettingsTransactionLock(root, transactionLockTimeout);
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await transactionLock.AcquireAsync(cancellationToken);
            await RecoverPendingAsync(cancellationToken);
            var document = await ReadPublicAsync(cancellationToken);
            var settings = document.Settings.ToAppSettings();
            if (!secrets.IsAvailable) return settings;

            var generation = ActiveGeneration(document);
            if (generation is null) return settings;
            if (generation == LegacyGeneration)
            {
                settings.WordPressAppPassword = await secrets.ReadAsync(WordPressPasswordKey, cancellationToken) ?? "";
                settings.ClientSecret = await secrets.ReadAsync(GoogleClientSecretKey, cancellationToken) ?? "";
                settings.RefreshToken = await secrets.ReadAsync(GoogleRefreshTokenKey, cancellationToken) ?? "";
                return settings;
            }

            settings.WordPressAppPassword = await ReadSecretAsync(WordPressPasswordKey, generation, document.Settings.HasWordPressPassword, cancellationToken);
            settings.ClientSecret = await ReadSecretAsync(GoogleClientSecretKey, generation, document.Settings.HasGoogleClientSecret, cancellationToken);
            settings.RefreshToken = await ReadSecretAsync(GoogleRefreshTokenKey, generation, document.Settings.HasGoogleRefreshToken, cancellationToken);
            return settings;
        }
        finally { gate.Release(); }
    }

    /// <summary>Updates only non-sensitive preferences and always preserves the current credential pointer.</summary>
    public Task SavePublicAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
        SaveAsync(settings, CredentialChange.Preserve, cancellationToken);

    public async Task SaveAsync(AppSettings settings, CredentialChange credentialChange, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(credentialChange);
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await transactionLock.AcquireAsync(cancellationToken);
            var unresolved = await RecoverPendingAsync(cancellationToken);
            var previous = await ReadPublicAsync(cancellationToken);

            if (credentialChange.Mode == CredentialChangeMode.Preserve)
            {
                await WritePublicAsync(PublicSettings.WithPublicValues(previous.Settings, settings), cancellationToken);
                return;
            }

            if (unresolved)
                throw new InvalidOperationException(L.P(
                    "前一次憑證更新仍在安全復原中；請在系統安全保管庫可用後重試。",
                    "上一次凭据更新仍在安全恢复中；请在系统安全存储可用后重试。",
                    "A previous credential update is still awaiting safe recovery. Retry when the OS vault is available.",
                    "以前の資格情報更新を安全に復旧中です。OS の安全な保管庫が利用可能になってから再試行してください。"));

            if (credentialChange.Mode == CredentialChangeMode.Replace && !secrets.IsAvailable)
                throw new InvalidOperationException(L.P(
                    "安全憑證儲存目前不可用；新憑證未儲存。",
                    "安全凭据存储目前不可用；新凭据未保存。",
                    "Secure credential storage is unavailable; the new credentials were not saved.",
                    "安全な資格情報ストレージを利用できないため、新しい資格情報は保存されませんでした。"));

            var generation = Guid.NewGuid().ToString("N");
            var previousGeneration = ActiveGeneration(previous);
            var journal = new PendingJournal
            {
                SchemaVersion = JournalSchemaVersion,
                Operation = credentialChange.Mode == CredentialChangeMode.Clear ? JournalOperation.Clear : JournalOperation.Replace,
                PendingGeneration = generation,
                PreviousGeneration = previousGeneration ?? ""
            };
            await WriteJournalAsync(journal, cancellationToken);

            try
            {
                if (credentialChange.Mode == CredentialChangeMode.Replace)
                {
                    await StageSecretAsync(WordPressPasswordKey, credentialChange.WordPressAppPassword, generation, cancellationToken);
                    await StageSecretAsync(GoogleClientSecretKey, credentialChange.GoogleClientSecret, generation, cancellationToken);
                    await StageSecretAsync(GoogleRefreshTokenKey, credentialChange.GoogleRefreshToken, generation, cancellationToken);
                }
            }
            catch
            {
                await TryRollbackUncommittedAsync(generation, credentialChange.Mode);
                throw;
            }

            var retired = previous.Settings.RetiredCredentialGenerations.ToHashSet(StringComparer.Ordinal);
            if (previousGeneration is not null) retired.Add(previousGeneration);
            retired.Remove(generation);
            var next = PublicSettings.From(
                settings,
                generation,
                credentialChange.Mode == CredentialChangeMode.Replace && credentialChange.WordPressAppPassword.Length > 0,
                credentialChange.Mode == CredentialChangeMode.Replace && credentialChange.GoogleClientSecret.Length > 0,
                credentialChange.Mode == CredentialChangeMode.Replace && credentialChange.GoogleRefreshToken.Length > 0,
                retired);
            try { await WritePublicAsync(next, cancellationToken); }
            catch
            {
                // A document store may durably commit and then report an I/O
                // error. Never delete the pending generation unless neither
                // public copy can possibly reference it.
                await TryRollbackUncommittedAsync(generation, credentialChange.Mode);
                throw;
            }

            // The new public pointer is authoritative. Cleanup failures are left in the
            // durable journal and retried by the next load or save.
            try { await RecoverPendingAsync(CancellationToken.None); }
            catch { }
        }
        finally { gate.Release(); }
    }

    async Task<string> ReadSecretAsync(string baseKey, string generation, bool expected, CancellationToken cancellationToken)
    {
        if (!expected) return "";
        var value = await secrets.ReadAsync(VaultKey(baseKey, generation), cancellationToken);
        return value ?? throw new InvalidDataException(L.P(
            "安全保管庫中的憑證世代不完整；已停止載入以避免混用資料。",
            "安全存储中的凭据世代不完整；已停止加载以避免混用数据。",
            "The credential generation in the secure vault is incomplete; loading stopped to avoid mixing data.",
            "安全な保管庫の資格情報世代が不完全なため、データの混在を防ぐため読み込みを停止しました。"));
    }

    async Task StageSecretAsync(string baseKey, string value, string generation, CancellationToken cancellationToken)
    {
        if (value.Length == 0) return;
        await secrets.WriteAsync(VaultKey(baseKey, generation), value, cancellationToken);
    }

    async Task<bool> RecoverPendingAsync(CancellationToken cancellationToken)
    {
        var journal = await ReadJournalAsync(cancellationToken);
        var document = await ReadPublicAsync(cancellationToken);

        if (journal is null && document.Settings.RetiredCredentialGenerations.Count > 0)
        {
            journal = new PendingJournal
            {
                SchemaVersion = JournalSchemaVersion,
                Operation = JournalOperation.Cleanup,
                PendingGeneration = ActiveGeneration(document) ?? "",
                PreviousGeneration = ""
            };
            await WriteJournalAsync(journal, cancellationToken);
        }
        if (journal is null) return false;

        var active = ActiveGeneration(document) ?? "";
        var committed = journal.Operation == JournalOperation.Cleanup || string.Equals(active, journal.PendingGeneration, StringComparison.Ordinal);
        if (committed)
        {
            if (!await TryCleanupRetiredAsync(document.Settings, active)) return true;
            return !await TryDeleteJournalAsync();
        }

        // Before rolling back an uncommitted generation, make the authoritative
        // public state occupy both primary and backup. This removes a stale
        // backup that could otherwise point at the pending generation.
        if (document.Exists && !await TryFortifyPublicCopiesAsync(document.Settings, ActiveGeneration(document))) return true;
        if (await ProbeGenerationReferenceAsync(journal.PendingGeneration, cancellationToken) != GenerationReference.NotReferenced) return true;

        if (journal.Operation != JournalOperation.Clear && (!secrets.IsAvailable || !await TryDeleteGenerationAsync(journal.PendingGeneration))) return true;
        return !await TryDeleteJournalAsync();
    }

    async Task<bool> TryCleanupRetiredAsync(PublicSettings settings, string activeGeneration)
    {
        if (settings.RetiredCredentialGenerations.Count == 0) return true;
        if (!secrets.IsAvailable || string.IsNullOrWhiteSpace(activeGeneration)) return false;

        // The first identical write rotates the current primary into backup.
        // Verification then proves that corrupting either copy can no longer
        // downgrade to a generation whose secrets are about to be deleted.
        if (!await TryFortifyPublicCopiesAsync(settings, activeGeneration)) return false;

        var remaining = new List<string>();
        foreach (var generation in settings.RetiredCredentialGenerations.Distinct(StringComparer.Ordinal))
        {
            if (string.Equals(generation, activeGeneration, StringComparison.Ordinal)) continue;
            if (!await TryDeleteGenerationAsync(generation)) remaining.Add(generation);
        }

        if (remaining.SequenceEqual(settings.RetiredCredentialGenerations, StringComparer.Ordinal)) return remaining.Count == 0;
        var updated = settings.CopyWithRetired(remaining);
        try { await WritePublicAsync(updated, CancellationToken.None); }
        catch { return false; }
        return remaining.Count == 0;
    }

    async Task TryRollbackUncommittedAsync(string generation, CredentialChangeMode mode)
    {
        if (await ProbeGenerationReferenceAsync(generation, CancellationToken.None) != GenerationReference.NotReferenced) return;
        var rolledBack = mode == CredentialChangeMode.Clear || await TryDeleteGenerationAsync(generation);
        if (rolledBack) await TryDeleteJournalAsync();
    }

    async Task<bool> TryFortifyPublicCopiesAsync(PublicSettings settings, string? expectedGeneration)
    {
        try
        {
            await WritePublicAsync(settings, CancellationToken.None);
            return await BothPublicCopiesReferenceAsync(expectedGeneration);
        }
        catch { return false; }
    }

    async Task<bool> BothPublicCopiesReferenceAsync(string? expectedGeneration)
    {
        foreach (var read in new Func<CancellationToken, ValueTask<string?>>[]
                 { settingsDocuments.ReadPrimaryAsync, settingsDocuments.ReadBackupAsync })
        {
            string? json;
            try { json = await read(CancellationToken.None); }
            catch { return false; }
            if (json is null) return false;
            try
            {
                var actual = ActiveGeneration(new PublicDocument(true, ParsePublic(json)));
                if (!string.Equals(actual, expectedGeneration, StringComparison.Ordinal)) return false;
            }
            catch { return false; }
        }
        return true;
    }

    async Task<GenerationReference> ProbeGenerationReferenceAsync(string generation, CancellationToken cancellationToken)
    {
        var uncertain = false;
        foreach (var read in new Func<CancellationToken, ValueTask<string?>>[]
                 { settingsDocuments.ReadPrimaryAsync, settingsDocuments.ReadBackupAsync })
        {
            string? json;
            try { json = await read(cancellationToken); }
            catch (OperationCanceledException) { throw; }
            catch { uncertain = true; continue; }
            if (json is null) continue;
            try
            {
                var actual = ActiveGeneration(new PublicDocument(true, ParsePublic(json)));
                if (string.Equals(actual, generation, StringComparison.Ordinal)) return GenerationReference.Referenced;
            }
            catch { uncertain = true; }
        }
        return uncertain ? GenerationReference.Unknown : GenerationReference.NotReferenced;
    }

    async Task<bool> TryDeleteGenerationAsync(string generation)
    {
        if (!secrets.IsAvailable || string.IsNullOrWhiteSpace(generation)) return false;
        var complete = true;
        foreach (var baseKey in SecretKeys)
        {
            try { await secrets.DeleteAsync(VaultKey(baseKey, generation), CancellationToken.None); }
            catch { complete = false; }
        }
        return complete;
    }

    async Task<bool> TryDeleteJournalAsync()
    {
        try { await journalDocuments.DeleteAsync(CancellationToken.None); return true; }
        catch { return false; }
    }

    async Task<PublicDocument> ReadPublicAsync(CancellationToken cancellationToken)
    {
        string? primary = null;
        Exception? failure = null;
        try
        {
            primary = await settingsDocuments.ReadPrimaryAsync(cancellationToken);
            if (primary is not null) return new(true, ParsePublic(primary));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) { failure = exception; }

        try
        {
            var backup = await settingsDocuments.ReadBackupAsync(cancellationToken);
            if (backup is not null)
            {
                var recovered = ParsePublic(backup);
                await settingsDocuments.RestoreBackupAsync(cancellationToken);
                return new(true, recovered);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) { failure = exception; }

        if (primary is null && failure is null) return new(false, PublicSettings.New());
        throw CorruptDocument("settings.json", failure);
    }

    async Task<PendingJournal?> ReadJournalAsync(CancellationToken cancellationToken)
    {
        string? primary = null;
        Exception? failure = null;
        try
        {
            primary = await journalDocuments.ReadPrimaryAsync(cancellationToken);
            if (primary is not null) return ParseJournal(primary);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) { failure = exception; }

        try
        {
            var backup = await journalDocuments.ReadBackupAsync(cancellationToken);
            if (backup is not null)
            {
                var recovered = ParseJournal(backup);
                await journalDocuments.RestoreBackupAsync(cancellationToken);
                return recovered;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) { failure = exception; }

        if (primary is null && failure is null) return null;
        throw CorruptDocument("settings.pending.json", failure);
    }

    Task WritePublicAsync(PublicSettings settings, CancellationToken cancellationToken) =>
        settingsDocuments.WriteAsync(JsonSerializer.Serialize(settings, jsonOptions), cancellationToken).AsTask();

    Task WriteJournalAsync(PendingJournal journal, CancellationToken cancellationToken) =>
        journalDocuments.WriteAsync(JsonSerializer.Serialize(journal, jsonOptions), cancellationToken).AsTask();

    static PublicSettings ParsePublic(string json)
    {
        var settings = JsonSerializer.Deserialize<PublicSettings>(json) ?? throw new JsonException("The settings document is empty.");
        settings.Validate();
        return settings;
    }

    static PendingJournal ParseJournal(string json)
    {
        var journal = JsonSerializer.Deserialize<PendingJournal>(json) ?? throw new JsonException("The settings journal is empty.");
        if (journal.SchemaVersion != JournalSchemaVersion || !Enum.IsDefined(journal.Operation) ||
            (journal.Operation is JournalOperation.Replace or JournalOperation.Clear && string.IsNullOrWhiteSpace(journal.PendingGeneration)))
            throw new JsonException("The settings journal is invalid.");
        return journal;
    }

    static InvalidDataException CorruptDocument(string name, Exception? inner) => new(L.P(
        "{0} 無法讀取，且沒有可用的有效備份；為保護憑證，程式拒絕覆寫。",
        "{0} 无法读取，并且没有可用的有效备份；为保护凭据，程序拒绝覆盖。",
        "{0} could not be read and no valid backup is available. The app refused to overwrite it to protect credentials.",
        "{0} を読み込めず、有効なバックアップもありません。資格情報を保護するため上書きを拒否しました。",
        name), inner);

    static string? ActiveGeneration(PublicDocument document)
    {
        if (!document.Exists) return null;
        if (document.Settings.SchemaVersion < CurrentSchemaVersion) return LegacyGeneration;
        return string.IsNullOrWhiteSpace(document.Settings.CredentialGeneration) ? null : document.Settings.CredentialGeneration;
    }

    static string VaultKey(string baseKey, string generation) =>
        generation == LegacyGeneration ? baseKey : $"{baseKey}:{generation}";

    enum JournalOperation { Replace, Clear, Cleanup }
    enum GenerationReference { NotReferenced, Referenced, Unknown }
    sealed record PublicDocument(bool Exists, PublicSettings Settings);

    sealed class PendingJournal
    {
        public int SchemaVersion { get; set; }
        public JournalOperation Operation { get; set; }
        public string PendingGeneration { get; set; } = "";
        public string PreviousGeneration { get; set; } = "";
    }

    sealed class PublicSettings
    {
        public int SchemaVersion { get; set; }
        public string CredentialGeneration { get; set; } = "";
        public bool HasWordPressPassword { get; set; }
        public bool HasGoogleClientSecret { get; set; }
        public bool HasGoogleRefreshToken { get; set; }
        public List<string> RetiredCredentialGenerations { get; set; } = [];
        public string InterfaceLanguage { get; set; } = "";
        public string SiteUrl { get; set; } = "";
        public string WordPressUser { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string BlogId { get; set; } = "wordpress";
        public string BlogName { get; set; } = "";
        public string VideoPrivacy { get; set; } = "unlisted";
        public bool CreateAsDraft { get; set; }
        public int AuthorizedScopeVersion { get; set; }

        public static PublicSettings New() => new() { SchemaVersion = CurrentSchemaVersion };

        public static PublicSettings WithPublicValues(PublicSettings credentials, AppSettings value) => new()
        {
            SchemaVersion = credentials.SchemaVersion,
            CredentialGeneration = credentials.CredentialGeneration,
            HasWordPressPassword = credentials.HasWordPressPassword,
            HasGoogleClientSecret = credentials.HasGoogleClientSecret,
            HasGoogleRefreshToken = credentials.HasGoogleRefreshToken,
            RetiredCredentialGenerations = [.. credentials.RetiredCredentialGenerations],
            InterfaceLanguage = value.InterfaceLanguage,
            SiteUrl = value.SiteUrl,
            WordPressUser = value.WordPressUser,
            ClientId = value.ClientId,
            BlogId = value.BlogId,
            BlogName = value.BlogName,
            VideoPrivacy = value.VideoPrivacy,
            CreateAsDraft = value.CreateAsDraft,
            AuthorizedScopeVersion = value.AuthorizedScopeVersion
        };

        public static PublicSettings From(AppSettings value, string generation, bool hasWordPressPassword, bool hasClientSecret, bool hasRefreshToken, IEnumerable<string> retired) => new()
        {
            SchemaVersion = CurrentSchemaVersion,
            CredentialGeneration = generation,
            HasWordPressPassword = hasWordPressPassword,
            HasGoogleClientSecret = hasClientSecret,
            HasGoogleRefreshToken = hasRefreshToken,
            RetiredCredentialGenerations = retired.OrderBy(item => item, StringComparer.Ordinal).ToList(),
            InterfaceLanguage = value.InterfaceLanguage,
            SiteUrl = value.SiteUrl,
            WordPressUser = value.WordPressUser,
            ClientId = value.ClientId,
            BlogId = value.BlogId,
            BlogName = value.BlogName,
            VideoPrivacy = value.VideoPrivacy,
            CreateAsDraft = value.CreateAsDraft,
            AuthorizedScopeVersion = value.AuthorizedScopeVersion
        };

        public PublicSettings CopyWithRetired(IEnumerable<string> retired) => new()
        {
            SchemaVersion = SchemaVersion,
            CredentialGeneration = CredentialGeneration,
            HasWordPressPassword = HasWordPressPassword,
            HasGoogleClientSecret = HasGoogleClientSecret,
            HasGoogleRefreshToken = HasGoogleRefreshToken,
            RetiredCredentialGenerations = retired.OrderBy(item => item, StringComparer.Ordinal).ToList(),
            InterfaceLanguage = InterfaceLanguage,
            SiteUrl = SiteUrl,
            WordPressUser = WordPressUser,
            ClientId = ClientId,
            BlogId = BlogId,
            BlogName = BlogName,
            VideoPrivacy = VideoPrivacy,
            CreateAsDraft = CreateAsDraft,
            AuthorizedScopeVersion = AuthorizedScopeVersion
        };

        public void Validate()
        {
            if (SchemaVersion < 0 || SchemaVersion > CurrentSchemaVersion || RetiredCredentialGenerations is null ||
                CredentialGeneration is null || InterfaceLanguage is null || SiteUrl is null || WordPressUser is null ||
                ClientId is null || BlogId is null || BlogName is null || VideoPrivacy is null)
                throw new JsonException("The settings document contains unsupported or missing fields.");
            if (SchemaVersion >= CurrentSchemaVersion)
            {
                if ((HasWordPressPassword || HasGoogleClientSecret || HasGoogleRefreshToken) && string.IsNullOrWhiteSpace(CredentialGeneration))
                    throw new JsonException("Credential presence flags require an active generation.");
                if (RetiredCredentialGenerations.Any(item => string.IsNullOrWhiteSpace(item) || string.Equals(item, CredentialGeneration, StringComparison.Ordinal)))
                    throw new JsonException("The settings document contains an invalid retired generation.");
            }
        }

        public AppSettings ToAppSettings() => new()
        {
            InterfaceLanguage = InterfaceLanguage,
            SiteUrl = SiteUrl,
            WordPressUser = WordPressUser,
            ClientId = ClientId,
            BlogId = BlogId,
            BlogName = BlogName,
            VideoPrivacy = VideoPrivacy,
            CreateAsDraft = CreateAsDraft,
            AuthorizedScopeVersion = AuthorizedScopeVersion
        };
    }
}
