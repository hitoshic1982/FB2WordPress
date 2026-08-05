using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FB2WordPress;

var childExitCode = await TryRunSettingsLockChildAsync(Environment.GetCommandLineArgs().Skip(1).ToArray());
if (childExitCode is not null) return childExitCode.Value;

var failures = new List<string>();
void Check(bool condition, string name)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
    if (!condition) failures.Add(name);
}

var references = typeof(FacebookParser).Assembly.GetReferencedAssemblies().Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
Check(!references.Contains("System.Windows.Forms"), "Core does not reference WinForms");
Check(!references.Contains("System.Drawing.Common"), "Core does not reference Windows image APIs");
Check(!references.Contains("Microsoft.Win32.Registry"), "Core does not reference the Windows registry");
Check(Path.IsPathFullyQualified(PlatformPaths.LocalDataDirectory), "Local data path is absolute");
Check(Path.IsPathFullyQualified(PlatformPaths.ReportsDirectory), "Report path is absolute");
if (OperatingSystem.IsWindows())
{
    Check(PlatformPaths.LocalDataDirectory == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FB2WordPress"), "Windows keeps the existing LocalAppData location");
    Check(PlatformPaths.ReportsDirectory == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FB2WordPress Reports"), "Windows keeps the existing report location");
}
Check(L.SupportedCodes.SequenceEqual(new[] { "zh-TW", "zh-CN", "en", "ja" }), "Core exposes one four-language catalog");
Check(typeof(L).GetProperty("FontName", BindingFlags.Public | BindingFlags.Static) is null, "Shared localization does not prescribe Windows-only fonts");
var initialLanguage = L.Language;
var setupNotesArePortable = true;
foreach (var languageCode in L.SupportedCodes)
{
    L.Configure(languageCode);
    var note = L.T("setup_note");
    setupNotesArePortable &= !note.Contains("Windows", StringComparison.OrdinalIgnoreCase) && !note.Contains("DPAPI", StringComparison.OrdinalIgnoreCase);
}
L.Configure(initialLanguage);
Check(setupNotesArePortable, "Shared setup guidance contains no Windows-only encryption claim");

var root = Path.Combine(Directory.GetCurrentDirectory(), ".core-audit-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var export = Path.Combine(root, "export");
    Directory.CreateDirectory(export);
    var payload = new[]
    {
        new
        {
            timestamp = 1700000001L,
            data = new[] { new { post = "Cross-platform post #portable" } },
            attachments = new[] { new { data = new[] { new { media = new { uri = "media/photo.webp" } } } } }
        }
    };
    await File.WriteAllTextAsync(Path.Combine(export, "your_posts_1.json"), JsonSerializer.Serialize(payload), new UTF8Encoding(false));
    var posts = FacebookParser.Read(export, _ => { });
    Check(posts.Count == 1 && posts[0].Text == "Cross-platform post #portable", "Facebook export parser runs without a desktop UI");
    Check(posts[0].Labels.SequenceEqual(new[] { "portable" }), "Facebook hashtags remain available to every UI");
    Check(posts[0].Media.Single().RelativePath == Path.Combine("media", "photo.webp"), "Media paths use the current platform separator");

    var stateRoot = Path.Combine(root, "state");
    var migrationStore = new MigrationStateStore(stateRoot);
    var source = Path.Combine(root, "source.zip");
    await File.WriteAllTextAsync(source, "fixture");
    var first = new MigrationState { Posts = { ["post"] = new PostState { Complete = false } } };
    migrationStore.Save(source, first);
    first.Posts["post"].Complete = true;
    migrationStore.Save(source, first);
    File.WriteAllText(migrationStore.DetailedStateFile(source), "corrupt");
    var recovered = migrationStore.Load(source);
    Check(recovered.Posts.TryGetValue("post", out var recoveredPost) && !recoveredPost.Complete, "Migration state recovers from its previous portable backup");

    var settings = new AppSettings
    {
        InterfaceLanguage = "ja",
        SiteUrl = "https://example.invalid",
        WordPressUser = "writer",
        WordPressAppPassword = "private-password",
        ClientSecret = "private-client-secret",
        RefreshToken = "private-refresh-token"
    };
    var vault = new MemoryVault();
    var settingsRoot = Path.Combine(root, "settings");
    var settingsStore = new CrossPlatformSettingsStore(vault, settingsRoot);
    await settingsStore.SaveAsync(settings, CredentialChange.ReplaceFrom(settings));
    var publicJson = await File.ReadAllTextAsync(Path.Combine(settingsRoot, "settings.json"));
    Check(!publicJson.Contains("private-", StringComparison.Ordinal), "Credential values never enter the portable JSON file");
    using var publicDocument = JsonDocument.Parse(publicJson);
    var publicSettingsRoot = publicDocument.RootElement;
    var credentialGeneration = publicSettingsRoot.GetProperty("CredentialGeneration").GetString() ?? "";
    Check(publicSettingsRoot.GetProperty("SchemaVersion").GetInt32() == 2 && credentialGeneration.Length == 32, "Public settings point to one versioned credential generation");
    Check(publicSettingsRoot.GetProperty("HasWordPressPassword").GetBoolean() && publicSettingsRoot.GetProperty("HasGoogleClientSecret").GetBoolean() && publicSettingsRoot.GetProperty("HasGoogleRefreshToken").GetBoolean(), "Public credential-presence flags describe all three staged values");
    Check(vault.Values.Keys.Count(key => key.EndsWith(":" + credentialGeneration, StringComparison.Ordinal)) == 3, "All three credentials are committed under the same vault generation");
    var loaded = await settingsStore.LoadAsync();
    Check(loaded.WordPressAppPassword == settings.WordPressAppPassword && loaded.RefreshToken == settings.RefreshToken, "OS-vault contract restores credentials separately");
    var lockRecord = await File.ReadAllTextAsync(Path.Combine(settingsRoot, "settings.transaction.lock"));
    using (var lockDocument = JsonDocument.Parse(lockRecord))
    {
        Check(lockDocument.RootElement.GetProperty("processId").GetInt32() == Environment.ProcessId &&
              lockDocument.RootElement.GetProperty("ownerToken").GetString()?.Length == 32 &&
              !lockRecord.Contains("private-", StringComparison.Ordinal),
            "Transaction lock records non-secret ownership metadata and no credentials");
    }

    var twoInstanceRoot = Path.Combine(root, "two-store-interleave");
    var twoInstanceVault = new MemoryVault();
    var firstStore = new CrossPlatformSettingsStore(twoInstanceVault, twoInstanceRoot, transactionLockTimeout: TimeSpan.FromSeconds(5));
    var secondStore = new CrossPlatformSettingsStore(twoInstanceVault, twoInstanceRoot, transactionLockTimeout: TimeSpan.FromMilliseconds(250));
    var interleaveOriginal = new AppSettings
    {
        SiteUrl = "https://interleave-old.example.invalid",
        WordPressAppPassword = "interleave-old-wordpress",
        ClientSecret = "interleave-old-client",
        RefreshToken = "interleave-old-refresh"
    };
    var interleaveReplacement = new AppSettings
    {
        SiteUrl = "https://interleave-new.example.invalid",
        WordPressAppPassword = "interleave-new-wordpress",
        ClientSecret = "interleave-new-client",
        RefreshToken = "interleave-new-refresh"
    };
    await firstStore.SaveAsync(interleaveOriginal, CredentialChange.ReplaceFrom(interleaveOriginal));
    var interleaveGeneration = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(twoInstanceRoot, "settings.json"))).RootElement.GetProperty("CredentialGeneration").GetString();
    twoInstanceVault.PauseAfterWrites(3);
    var firstSave = firstStore.SaveAsync(interleaveReplacement, CredentialChange.ReplaceFrom(interleaveReplacement));
    await twoInstanceVault.PauseReached.WaitAsync(TimeSpan.FromSeconds(5));
    var secondStoreRejected = false;
    try { _ = await secondStore.LoadAsync(); }
    catch (TimeoutException) { secondStoreRejected = true; }
    var interleavePublicWhilePaused = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(twoInstanceRoot, "settings.json"))).RootElement;
    Check(secondStoreRejected && interleavePublicWhilePaused.GetProperty("CredentialGeneration").GetString() == interleaveGeneration,
        "Two store instances cannot interleave recovery with a staged credential transaction");
    Check(File.Exists(Path.Combine(twoInstanceRoot, "settings.pending.json")) && twoInstanceVault.Values.Count == 6,
        "Rejected same-process contender neither deletes staged secrets nor advances the public pointer");
    twoInstanceVault.ResumePausedWrite();
    await firstSave;
    var interleaveLoaded = await secondStore.LoadAsync();
    Check(interleaveLoaded.WordPressAppPassword == interleaveReplacement.WordPressAppPassword && twoInstanceVault.Values.Count == 3,
        "The owning store completes normally after the same-process contender is refused");

    var processRoot = Path.Combine(root, "cross-process-interleave");
    var processVaultRoot = Path.Combine(root, "cross-process-vault");
    var processVault = new SharedFileVault(processVaultRoot);
    var parentProcessStore = new CrossPlatformSettingsStore(processVault, processRoot, transactionLockTimeout: TimeSpan.FromSeconds(5));
    await parentProcessStore.SaveAsync(interleaveOriginal, CredentialChange.ReplaceFrom(interleaveOriginal));
    var processOriginalGeneration = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(processRoot, "settings.json"))).RootElement.GetProperty("CredentialGeneration").GetString();
    var processSignal = Path.Combine(root, "process-stage-ready");
    var processRelease = Path.Combine(root, "process-stage-release");
    using (var ownerProcess = StartAuditChild("--settings-lock-save", processRoot, processVaultRoot, processSignal, processRelease, "process-new"))
    {
        await WaitForFileAsync(processSignal, TimeSpan.FromSeconds(10));
        using var contenderProcess = StartAuditChild("--settings-lock-probe", processRoot, processVaultRoot);
        var contenderResult = await WaitForChildAsync(contenderProcess, TimeSpan.FromSeconds(10));
        var processPublicWhilePaused = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(processRoot, "settings.json"))).RootElement;
        Check(contenderResult.ExitCode == 0 && processPublicWhilePaused.GetProperty("CredentialGeneration").GetString() == processOriginalGeneration,
            "A real second process times out fail-closed while another process owns the transaction");
        Check(File.Exists(Path.Combine(processRoot, "settings.pending.json")) && Directory.GetFiles(processVaultRoot, "*.secret").Length == 6,
            "The refused process cannot treat the owner's journal as residue or delete its staged generation");
        await File.WriteAllTextAsync(processRelease, "release");
        var ownerResult = await WaitForChildAsync(ownerProcess, TimeSpan.FromSeconds(15));
        Check(ownerResult.ExitCode == 0, "The owning process commits after the controlled interleave is released");
    }
    var processLoaded = await parentProcessStore.LoadAsync();
    Check(processLoaded.WordPressAppPassword == "process-new-wordpress" && Directory.GetFiles(processVaultRoot, "*.secret").Length == 3,
        "Cross-process serialization leaves one complete active credential generation");

    File.Delete(processSignal);
    File.Delete(processRelease);
    using (var crashingOwner = StartAuditChild("--settings-lock-save", processRoot, processVaultRoot, processSignal, processRelease, "process-crash"))
    {
        await WaitForFileAsync(processSignal, TimeSpan.FromSeconds(10));
        crashingOwner.Kill(entireProcessTree: true);
        await crashingOwner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }
    var afterCrashedOwner = await new CrossPlatformSettingsStore(processVault, processRoot, transactionLockTimeout: TimeSpan.FromSeconds(5)).LoadAsync();
    Check(afterCrashedOwner.WordPressAppPassword == "process-new-wordpress" && !File.Exists(Path.Combine(processRoot, "settings.pending.json")),
        "A crashed lock owner releases the OS lease and its uncommitted generation is rolled back");
    Check(Directory.GetFiles(processVaultRoot, "*.secret").Length == 3,
        "Crash recovery removes only the interrupted generation and preserves the prior active secrets");

    var symlinkRoot = Path.Combine(root, "symlink-lock-root");
    var symlinkTarget = Path.Combine(root, "symlink-lock-target");
    Directory.CreateDirectory(symlinkTarget);
    var symlinkFixtureAvailable = false;
    var symlinkRejected = false;
    try
    {
        Directory.CreateSymbolicLink(symlinkRoot, symlinkTarget);
        symlinkFixtureAvailable = true;
    }
    catch (UnauthorizedAccessException) { }
    catch (PlatformNotSupportedException) { }
    catch (IOException exception) when (IsSymlinkFixtureUnavailable(exception)) { }
    if (symlinkFixtureAvailable)
    {
        try { await new CrossPlatformSettingsStore(new MemoryVault(), symlinkRoot, transactionLockTimeout: TimeSpan.FromMilliseconds(250)).SavePublicAsync(new AppSettings()); }
        catch (UnsafeSettingsTransactionPathException) { symlinkRejected = true; }
        Check(symlinkRejected, "A symbolic-link settings root is rejected before a transaction begins");
        Check(!File.Exists(Path.Combine(symlinkTarget, "settings.json")), "Rejected symbolic-link lock paths do not write public settings");
    }
    else
    {
        Console.WriteLine("SKIP Symbolic-link lock-path fixture is unavailable on this runner");
    }

    var unsafeLockRoot = Path.Combine(root, "unsafe-lock-entry");
    Directory.CreateDirectory(Path.Combine(unsafeLockRoot, "settings.transaction.lock"));
    var unsafeLockTimer = Stopwatch.StartNew();
    var unsafeLockRejected = false;
    try
    {
        await new CrossPlatformSettingsStore(new MemoryVault(), unsafeLockRoot, transactionLockTimeout: TimeSpan.FromSeconds(2))
            .SavePublicAsync(new AppSettings());
    }
    catch (UnsafeSettingsTransactionPathException) { unsafeLockRejected = true; }
    Check(unsafeLockRejected && unsafeLockTimer.Elapsed < TimeSpan.FromSeconds(1),
        "An unsafe lock entry fails immediately instead of being retried as ordinary contention");
    Check(!File.Exists(Path.Combine(unsafeLockRoot, "settings.json")),
        "A lock-path safety failure cannot continue into a public settings write");

    var preserveRoot = Path.Combine(root, "preserve-unavailable");
    var preserveVault = new MemoryVault();
    var preserveStore = new CrossPlatformSettingsStore(preserveVault, preserveRoot);
    await preserveStore.SaveAsync(settings, CredentialChange.ReplaceFrom(settings));
    var beforePreserve = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(preserveRoot, "settings.json"))).RootElement.Clone();
    var preservedGeneration = beforePreserve.GetProperty("CredentialGeneration").GetString();
    preserveVault.Available = false;
    await preserveStore.SavePublicAsync(new AppSettings { InterfaceLanguage = "en", SiteUrl = "https://public-only.example.invalid", WordPressUser = "writer" });
    var unavailableLoad = await preserveStore.LoadAsync();
    var afterPreserve = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(preserveRoot, "settings.json"))).RootElement.Clone();
    Check(unavailableLoad.SiteUrl == "https://public-only.example.invalid" && unavailableLoad.WordPressAppPassword.Length == 0, "Public-only settings save works while the OS vault is unavailable");
    Check(afterPreserve.GetProperty("CredentialGeneration").GetString() == preservedGeneration && afterPreserve.GetProperty("HasWordPressPassword").GetBoolean(), "Preserve keeps the active credential generation and presence flags");
    preserveVault.Available = true;
    var preservedAfterRecovery = await preserveStore.LoadAsync();
    Check(preservedAfterRecovery.WordPressAppPassword == settings.WordPressAppPassword && preservedAfterRecovery.RefreshToken == settings.RefreshToken, "Vault recovery restores credentials after a public-only save");

    var rollbackRoot = Path.Combine(root, "staging-rollback");
    var rollbackVault = new MemoryVault();
    var rollbackStore = new CrossPlatformSettingsStore(rollbackVault, rollbackRoot);
    var original = new AppSettings
    {
        SiteUrl = "https://old.example.invalid",
        WordPressAppPassword = "old-wordpress",
        ClientSecret = "old-client",
        RefreshToken = "old-refresh"
    };
    var replacement = new AppSettings
    {
        SiteUrl = "https://new.example.invalid",
        WordPressAppPassword = "new-wordpress",
        ClientSecret = "new-client",
        RefreshToken = "new-refresh"
    };
    await rollbackStore.SaveAsync(original, CredentialChange.ReplaceFrom(original));
    rollbackVault.FailWriteNumber = rollbackVault.WriteCount + 2;
    rollbackVault.FailAllDeletes = true;
    var stagingRejected = false;
    try { await rollbackStore.SaveAsync(replacement, CredentialChange.ReplaceFrom(replacement)); }
    catch (IOException) { stagingRejected = true; }
    Check(stagingRejected && File.Exists(Path.Combine(rollbackRoot, "settings.pending.json")), "Staging plus rollback-delete failure leaves a durable recovery journal");
    rollbackVault.FailWriteNumber = null;
    rollbackVault.FailAllDeletes = false;
    var afterRollback = await rollbackStore.LoadAsync();
    Check(afterRollback.SiteUrl == original.SiteUrl && afterRollback.WordPressAppPassword == original.WordPressAppPassword, "Next load retries journaled staging rollback and retains the previous generation");
    Check(!File.Exists(Path.Combine(rollbackRoot, "settings.pending.json")) && !rollbackVault.Values.Any(pair => pair.Value.StartsWith("new-", StringComparison.Ordinal)), "Recovered staging transaction removes its journal and incomplete generation");

    for (var failedSecret = 1; failedSecret <= 3; failedSecret++)
    {
        var secretFailureRoot = Path.Combine(root, "secret-stage-" + failedSecret);
        var secretFailureVault = new MemoryVault();
        var secretFailureStore = new CrossPlatformSettingsStore(secretFailureVault, secretFailureRoot);
        await secretFailureStore.SaveAsync(original, CredentialChange.ReplaceFrom(original));
        secretFailureVault.FailWriteNumber = secretFailureVault.WriteCount + failedSecret;
        var secretFailureRejected = false;
        try { await secretFailureStore.SaveAsync(replacement, CredentialChange.ReplaceFrom(replacement)); }
        catch (IOException) { secretFailureRejected = true; }
        secretFailureVault.FailWriteNumber = null;
        var afterSecretFailure = await secretFailureStore.LoadAsync();
        Check(secretFailureRejected && afterSecretFailure.WordPressAppPassword == original.WordPressAppPassword, $"Credential staging failure at secret {failedSecret} retains the previous complete generation");
        Check(!secretFailureVault.Values.Any(pair => pair.Value.StartsWith("new-", StringComparison.Ordinal)) && !File.Exists(Path.Combine(secretFailureRoot, "settings.pending.json")), $"Credential staging failure at secret {failedSecret} removes every partial value and journal");
    }

    var publicFailureRoot = Path.Combine(root, "public-write-failure");
    var publicFailureVault = new MemoryVault();
    var publicDocuments = new FaultingDocumentStore(new FileAtomicDocumentStore(Path.Combine(publicFailureRoot, "settings.json")));
    var publicFailureStore = new CrossPlatformSettingsStore(publicFailureVault, publicFailureRoot, publicDocuments);
    await publicFailureStore.SaveAsync(original, CredentialChange.ReplaceFrom(original));
    publicDocuments.FailNextWrite = true;
    publicFailureVault.FailAllDeletes = true;
    var publicWriteRejected = false;
    try { await publicFailureStore.SaveAsync(replacement, CredentialChange.ReplaceFrom(replacement)); }
    catch (IOException) { publicWriteRejected = true; }
    Check(publicWriteRejected && File.Exists(Path.Combine(publicFailureRoot, "settings.pending.json")), "Public-pointer write failure remains journaled when immediate rollback deletion fails");
    publicFailureVault.FailAllDeletes = false;
    var afterPublicFailureRecovery = await publicFailureStore.LoadAsync();
    Check(afterPublicFailureRecovery.SiteUrl == original.SiteUrl && afterPublicFailureRecovery.RefreshToken == original.RefreshToken, "Journal recovery after public-write failure preserves the previous complete state");
    Check(!File.Exists(Path.Combine(publicFailureRoot, "settings.pending.json")) && !publicFailureVault.Values.Any(pair => pair.Value.StartsWith("new-", StringComparison.Ordinal)), "Public-write rollback is retried and completed on the next load");

    var cleanupRoot = Path.Combine(root, "cleanup-retry");
    var cleanupVault = new MemoryVault();
    var cleanupStore = new CrossPlatformSettingsStore(cleanupVault, cleanupRoot);
    await cleanupStore.SaveAsync(original, CredentialChange.ReplaceFrom(original));
    cleanupVault.FailAllDeletes = true;
    await cleanupStore.SaveAsync(replacement, CredentialChange.ReplaceFrom(replacement));
    var replacementWithPendingCleanup = await cleanupStore.LoadAsync();
    Check(replacementWithPendingCleanup.WordPressAppPassword == replacement.WordPressAppPassword && File.Exists(Path.Combine(cleanupRoot, "settings.pending.json")), "Committed replacement stays usable while old-generation deletion is journaled");
    cleanupVault.FailAllDeletes = false;
    _ = await cleanupStore.LoadAsync();
    Check(!File.Exists(Path.Combine(cleanupRoot, "settings.pending.json")) && !cleanupVault.Values.Any(pair => pair.Value.StartsWith("old-", StringComparison.Ordinal)), "Old-generation delete failure is retried to completion");

    var committedThrowRoot = Path.Combine(root, "committed-then-throw");
    var committedThrowVault = new MemoryVault();
    var committedThrowDocuments = new FaultingDocumentStore(new FileAtomicDocumentStore(Path.Combine(committedThrowRoot, "settings.json")));
    var committedThrowStore = new CrossPlatformSettingsStore(committedThrowVault, committedThrowRoot, committedThrowDocuments);
    await committedThrowStore.SaveAsync(original, CredentialChange.ReplaceFrom(original));
    committedThrowDocuments.ThrowAfterWriteNumber = committedThrowDocuments.WriteCount + 1;
    var committedThrowReported = false;
    try { await committedThrowStore.SaveAsync(replacement, CredentialChange.ReplaceFrom(replacement)); }
    catch (IOException) { committedThrowReported = true; }
    var committedGeneration = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(committedThrowRoot, "settings.json"))).RootElement.GetProperty("CredentialGeneration").GetString() ?? "";
    Check(committedThrowReported && committedThrowVault.Values.Keys.Count(key => key.EndsWith(":" + committedGeneration, StringComparison.Ordinal)) == 3, "A write that commits before throwing never deletes the new active generation");
    var committedThrowRecovered = await committedThrowStore.LoadAsync();
    Check(committedThrowRecovered.WordPressAppPassword == replacement.WordPressAppPassword && !File.Exists(Path.Combine(committedThrowRoot, "settings.pending.json")), "Journal recovery completes a public pointer that committed before its exception");

    var noDowngradeRoot = Path.Combine(root, "no-generation-downgrade");
    var noDowngradeVault = new MemoryVault();
    var noDowngradeDocuments = new FaultingDocumentStore(new FileAtomicDocumentStore(Path.Combine(noDowngradeRoot, "settings.json")));
    var noDowngradeStore = new CrossPlatformSettingsStore(noDowngradeVault, noDowngradeRoot, noDowngradeDocuments);
    await noDowngradeStore.SaveAsync(original, CredentialChange.ReplaceFrom(original));
    var retiredGeneration = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(noDowngradeRoot, "settings.json"))).RootElement.GetProperty("CredentialGeneration").GetString() ?? "";
    noDowngradeDocuments.FailBeforeWriteNumber = noDowngradeDocuments.WriteCount + 3;
    await noDowngradeStore.SaveAsync(replacement, CredentialChange.ReplaceFrom(replacement));
    var primaryBeforeCorruption = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(noDowngradeRoot, "settings.json"))).RootElement.Clone();
    var backupBeforeCorruption = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(noDowngradeRoot, "settings.json.bak"))).RootElement.Clone();
    var protectedGeneration = primaryBeforeCorruption.GetProperty("CredentialGeneration").GetString() ?? "";
    Check(protectedGeneration.Length == 32 && backupBeforeCorruption.GetProperty("CredentialGeneration").GetString() == protectedGeneration, "Primary and backup are fortified to the new generation before retired secrets are deleted");
    Check(!noDowngradeVault.Values.Keys.Any(key => key.EndsWith(":" + retiredGeneration, StringComparison.Ordinal)), "Retired generation is deleted only after both public copies become non-downgradable");
    await File.WriteAllTextAsync(Path.Combine(noDowngradeRoot, "settings.json"), "{ corrupt-after-retirement");
    noDowngradeDocuments.FailBeforeWriteNumber = null;
    var noDowngradeRecovered = await noDowngradeStore.LoadAsync();
    Check(noDowngradeRecovered.WordPressAppPassword == replacement.WordPressAppPassword, "Cleanup-state write failure plus primary corruption recovers the new generation instead of deleted credentials");
    Check(JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(noDowngradeRoot, "settings.json"))).RootElement.GetProperty("CredentialGeneration").GetString() == protectedGeneration, "Recovered primary never downgrades to the deleted generation");

    var clearRoot = Path.Combine(root, "explicit-clear");
    var clearVault = new MemoryVault();
    var clearStore = new CrossPlatformSettingsStore(clearVault, clearRoot);
    await clearStore.SaveAsync(settings, CredentialChange.ReplaceFrom(settings));
    clearVault.Available = false;
    await clearStore.SaveAsync(new AppSettings { SiteUrl = settings.SiteUrl, WordPressUser = settings.WordPressUser }, CredentialChange.Clear);
    var clearedWhileUnavailable = await clearStore.LoadAsync();
    Check(clearedWhileUnavailable.WordPressAppPassword.Length == 0 && File.Exists(Path.Combine(clearRoot, "settings.pending.json")), "Explicit clear is authoritative and journaled while the vault is unavailable");
    clearVault.Available = true;
    var clearedAfterRecovery = await clearStore.LoadAsync();
    using var clearedDocument = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(clearRoot, "settings.json")));
    var clearedSettingsRoot = clearedDocument.RootElement;
    Check(clearedAfterRecovery.ClientSecret.Length == 0 && clearedAfterRecovery.RefreshToken.Length == 0, "Explicitly cleared credentials never resurrect after vault recovery");
    Check(!clearedSettingsRoot.GetProperty("HasWordPressPassword").GetBoolean() && !clearedSettingsRoot.GetProperty("HasGoogleClientSecret").GetBoolean() && !clearedSettingsRoot.GetProperty("HasGoogleRefreshToken").GetBoolean(), "Public settings retain an explicit three-credential clear state");
    Check(!File.Exists(Path.Combine(clearRoot, "settings.pending.json")) && !clearVault.Values.Any(pair => pair.Value.StartsWith("private-", StringComparison.Ordinal)), "Vault recovery completes the journaled explicit deletion");

    var backupRoot = Path.Combine(root, "settings-backup");
    var backupVault = new MemoryVault();
    var backupDocuments = new FaultingDocumentStore(new FileAtomicDocumentStore(Path.Combine(backupRoot, "settings.json")));
    var backupStore = new CrossPlatformSettingsStore(backupVault, backupRoot, backupDocuments);
    await backupStore.SaveAsync(original, CredentialChange.ReplaceFrom(original));
    await backupStore.SavePublicAsync(new AppSettings { SiteUrl = "https://changed.example.invalid", WordPressUser = "writer" });
    File.WriteAllText(Path.Combine(backupRoot, "settings.json"), "{ corrupt");
    backupDocuments.FailNextRestore = true;
    var restoreFailureRejected = false;
    try { await backupStore.LoadAsync(); } catch (InvalidDataException) { restoreFailureRejected = true; }
    Check(restoreFailureRejected && await File.ReadAllTextAsync(Path.Combine(backupRoot, "settings.json")) == "{ corrupt", "A failed backup restore reports failure without overwriting the corrupt primary");
    var recoveredSettings = await backupStore.LoadAsync();
    Check(recoveredSettings.SiteUrl == original.SiteUrl && recoveredSettings.WordPressAppPassword == original.WordPressAppPassword, "Backup restoration retries successfully without losing the active generation");
    Check(JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(backupRoot, "settings.json"))).RootElement.GetProperty("CredentialGeneration").GetString()?.Length == 32, "Recovered backup is restored as the primary public document");

    var journalRecoveryRoot = Path.Combine(root, "journal-backup-recovery");
    var journalRecoveryVault = new MemoryVault();
    var journalPath = Path.Combine(journalRecoveryRoot, "settings.pending.json");
    var journalDocuments = new FaultingDocumentStore(new FileAtomicDocumentStore(journalPath));
    var journalRecoveryStore = new CrossPlatformSettingsStore(journalRecoveryVault, journalRecoveryRoot, journalDocuments: journalDocuments);
    await journalRecoveryStore.SaveAsync(original, CredentialChange.ReplaceFrom(original));
    journalRecoveryVault.FailWriteNumber = journalRecoveryVault.WriteCount + 2;
    journalRecoveryVault.FailAllDeletes = true;
    try { await journalRecoveryStore.SaveAsync(replacement, CredentialChange.ReplaceFrom(replacement)); } catch (IOException) { }
    File.Copy(journalPath, journalPath + ".bak", true);
    await File.WriteAllTextAsync(journalPath, "{ corrupt-journal-primary");
    journalRecoveryVault.FailWriteNumber = null;
    journalRecoveryVault.FailAllDeletes = false;
    journalDocuments.FailNextRestore = true;
    var journalRestoreFailureRejected = false;
    try { await journalRecoveryStore.LoadAsync(); } catch (InvalidDataException) { journalRestoreFailureRejected = true; }
    Check(journalRestoreFailureRejected && File.Exists(journalPath + ".bak"), "Journal backup restore failure remains retryable and keeps the valid backup");
    var journalRecovered = await journalRecoveryStore.LoadAsync();
    Check(journalRecovered.WordPressAppPassword == original.WordPressAppPassword && !File.Exists(journalPath) && !File.Exists(journalPath + ".bak"), "Corrupt journal primary recovers from its valid backup and completes rollback");

    var corruptJournalRoot = Path.Combine(root, "journal-corrupt-no-backup");
    var corruptJournalVault = new MemoryVault();
    var corruptJournalStore = new CrossPlatformSettingsStore(corruptJournalVault, corruptJournalRoot);
    await corruptJournalStore.SaveAsync(original, CredentialChange.ReplaceFrom(original));
    var corruptJournalPath = Path.Combine(corruptJournalRoot, "settings.pending.json");
    await File.WriteAllTextAsync(corruptJournalPath, "{ corrupt-primary");
    await File.WriteAllTextAsync(corruptJournalPath + ".bak", "{ corrupt-backup");
    var corruptJournalLoadRejected = false;
    var corruptJournalSaveRejected = false;
    try { await corruptJournalStore.LoadAsync(); } catch (InvalidDataException) { corruptJournalLoadRejected = true; }
    try { await corruptJournalStore.SavePublicAsync(new AppSettings { SiteUrl = "https://must-not-write.invalid" }); } catch (InvalidDataException) { corruptJournalSaveRejected = true; }
    Check(corruptJournalLoadRejected && corruptJournalSaveRejected && await File.ReadAllTextAsync(corruptJournalPath) == "{ corrupt-primary" && await File.ReadAllTextAsync(corruptJournalPath + ".bak") == "{ corrupt-backup", "Corrupt journal primary and backup fail closed without overwriting either artifact");

    var partialDeleteRoot = Path.Combine(root, "partial-journal-delete");
    Directory.CreateDirectory(partialDeleteRoot);
    var partialDeletePath = Path.Combine(partialDeleteRoot, "settings.pending.json");
    var partialDeleteStore = new PrimaryDeleteFaultStore(partialDeletePath);
    await partialDeleteStore.WriteAsync("older journal");
    await partialDeleteStore.WriteAsync("current journal");
    partialDeleteStore.FailPrimaryDeleteOnce = true;
    var partialDeleteRejected = false;
    try { await partialDeleteStore.DeleteAsync(); } catch (IOException) { partialDeleteRejected = true; }
    Check(partialDeleteRejected && await partialDeleteStore.ReadPrimaryAsync() == "current journal" && await partialDeleteStore.ReadBackupAsync() is null, "Interrupted journal deletion removes backup first and cannot revive the older journal");
    await partialDeleteStore.DeleteAsync();
    Check(await partialDeleteStore.ReadPrimaryAsync() is null && await partialDeleteStore.ReadBackupAsync() is null, "Partial journal deletion is idempotently completed on retry");

    var corruptRoot = Path.Combine(root, "settings-corrupt-no-backup");
    Directory.CreateDirectory(corruptRoot);
    var corruptPath = Path.Combine(corruptRoot, "settings.json");
    await File.WriteAllTextAsync(corruptPath, "{ definitely-corrupt");
    var corruptStore = new CrossPlatformSettingsStore(new MemoryVault(), corruptRoot);
    var corruptLoadRejected = false;
    var corruptSaveRejected = false;
    try { await corruptStore.LoadAsync(); } catch (InvalidDataException) { corruptLoadRejected = true; }
    try { await corruptStore.SavePublicAsync(new AppSettings { SiteUrl = "https://must-not-overwrite.invalid" }); } catch (InvalidDataException) { corruptSaveRejected = true; }
    Check(corruptLoadRejected && corruptSaveRejected && await File.ReadAllTextAsync(corruptPath) == "{ definitely-corrupt", "Unreadable public settings fail closed and are never treated as a new document");

    var unavailableRoot = Path.Combine(root, "unavailable-replace");
    var rejected = false;
    try { await new CrossPlatformSettingsStore(new MemoryVault(false), unavailableRoot).SaveAsync(settings, CredentialChange.ReplaceFrom(settings)); }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected && !File.Exists(Path.Combine(unavailableRoot, "settings.json")) && !File.Exists(Path.Combine(unavailableRoot, "settings.pending.json")), "Replacing credentials without secure storage fails closed before writing state");

    using var api = new GoogleApi(new AppSettings(), _ => { }, (_, _) => Task.CompletedTask);
    Check(api is not null, "WordPress and YouTube API client composes without a Windows UI");

    var saveEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var releaseSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var apiSettings = new AppSettings
    {
        SiteUrl = "https://wordpress.example.invalid",
        WordPressUser = "writer",
        WordPressAppPassword = "application-password"
    };
    using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
    {
        Content = new StringContent("{\"name\":\"Awaited Writer\"}")
    }));
    using var awaitedApi = new GoogleApi(apiSettings, _ => { }, async (_, cancellationToken) =>
    {
        saveEntered.TrySetResult();
        await releaseSave.Task.WaitAsync(cancellationToken);
    }, http);
    var authorization = awaitedApi.EnsureAuthorizedAsync(CancellationToken.None);
    await saveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Check(!authorization.IsCompleted, "WordPress authorization awaits durable settings persistence");
    releaseSave.TrySetResult();
    await authorization;
    Check(apiSettings.BlogName == "Awaited Writer", "Awaited API persistence keeps the returned WordPress identity");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine("FAILED: " + string.Join(", ", failures));
    return 1;
}

Console.WriteLine("ALL CROSS-PLATFORM CORE TESTS PASSED");
return 0;

static async Task<int?> TryRunSettingsLockChildAsync(string[] arguments)
{
    if (arguments.Length == 0 || !arguments[0].StartsWith("--settings-lock-", StringComparison.Ordinal)) return null;
    try
    {
        if (arguments[0] == "--settings-lock-save" && arguments.Length == 6)
        {
            var vault = new SharedFileVault(arguments[2], 3, arguments[3], arguments[4]);
            var store = new CrossPlatformSettingsStore(vault, arguments[1], transactionLockTimeout: TimeSpan.FromSeconds(5));
            var label = arguments[5];
            var settings = new AppSettings
            {
                SiteUrl = $"https://{label}.example.invalid",
                WordPressAppPassword = label + "-wordpress",
                ClientSecret = label + "-client",
                RefreshToken = label + "-refresh"
            };
            await store.SaveAsync(settings, CredentialChange.ReplaceFrom(settings));
            return 0;
        }

        if (arguments[0] == "--settings-lock-probe" && arguments.Length == 3)
        {
            var store = new CrossPlatformSettingsStore(
                new SharedFileVault(arguments[2]),
                arguments[1],
                transactionLockTimeout: TimeSpan.FromMilliseconds(300));
            try
            {
                _ = await store.LoadAsync();
                Console.Error.WriteLine("The contender unexpectedly acquired the settings transaction lock.");
                return 71;
            }
            catch (TimeoutException)
            {
                return 0;
            }
        }

        Console.Error.WriteLine("Invalid settings-lock child arguments.");
        return 72;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception);
        return 73;
    }
}

static Process StartAuditChild(params string[] arguments)
{
    var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("The current process path is unavailable.");
    var start = new ProcessStartInfo(processPath)
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WorkingDirectory = Directory.GetCurrentDirectory()
    };
    if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    foreach (var argument in arguments) start.ArgumentList.Add(argument);
    return Process.Start(start) ?? throw new InvalidOperationException("The settings-lock child process could not be started.");
}

static async Task<(int ExitCode, string StandardOutput, string StandardError)> WaitForChildAsync(Process process, TimeSpan timeout)
{
    await process.WaitForExitAsync().WaitAsync(timeout);
    return (process.ExitCode, await process.StandardOutput.ReadToEndAsync(), await process.StandardError.ReadToEndAsync());
}

static async Task WaitForFileAsync(string path, TimeSpan timeout)
{
    var timer = Stopwatch.StartNew();
    while (!File.Exists(path))
    {
        if (timer.Elapsed >= timeout) throw new TimeoutException($"Timed out waiting for controlled child signal: {Path.GetFileName(path)}");
        await Task.Delay(25);
    }
}

static bool IsSymlinkFixtureUnavailable(IOException exception)
{
    if (!OperatingSystem.IsWindows()) return false;
    var nativeError = exception.HResult & 0xffff;
    return nativeError is 5 or 1314;
}

sealed class MemoryVault(bool available = true) : ISecretVault
{
    readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
    TaskCompletionSource pauseReached = NewSignal();
    TaskCompletionSource resumePausedWrite = NewSignal();
    int writeCount;
    public bool Available { get; set; } = available;
    public bool IsAvailable => Available;
    public int WriteCount => Volatile.Read(ref writeCount);
    public int? FailWriteNumber { get; set; }
    public bool FailAllDeletes { get; set; }
    public IReadOnlyDictionary<string, string> Values => values;
    public Task PauseReached => pauseReached.Task;
    public ValueTask<string?> ReadAsync(string key, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(values.TryGetValue(key, out var value) ? value : null);
    public async ValueTask WriteAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var currentWrite = Interlocked.Increment(ref writeCount);
        if (currentWrite == FailWriteNumber) throw new IOException("Injected vault write failure");
        values[key] = value;
        if (currentWrite == pauseWriteNumber)
        {
            pauseReached.TrySetResult();
            await resumePausedWrite.Task.WaitAsync(cancellationToken);
        }
    }
    public ValueTask DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        if (FailAllDeletes) throw new IOException("Injected vault delete failure");
        values.Remove(key);
        return ValueTask.CompletedTask;
    }

    int? pauseWriteNumber;

    public void PauseAfterWrites(int additionalWrites)
    {
        if (additionalWrites <= 0) throw new ArgumentOutOfRangeException(nameof(additionalWrites));
        pauseReached = NewSignal();
        resumePausedWrite = NewSignal();
        pauseWriteNumber = WriteCount + additionalWrites;
    }

    public void ResumePausedWrite()
    {
        pauseWriteNumber = null;
        resumePausedWrite.TrySetResult();
    }

    static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

sealed class SharedFileVault(
    string folder,
    int? pauseAfterWrite = null,
    string? pauseSignalPath = null,
    string? resumeSignalPath = null) : ISecretVault
{
    int writeCount;
    public bool IsAvailable => true;

    public async ValueTask<string?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = SecretPath(key);
        return File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : null;
    }

    public async ValueTask WriteAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(SecretPath(key), value, new UTF8Encoding(false), cancellationToken);
        if (Interlocked.Increment(ref writeCount) != pauseAfterWrite) return;
        if (pauseSignalPath is null || resumeSignalPath is null)
            throw new InvalidOperationException("A coordinated vault pause requires signal and resume paths.");
        await File.WriteAllTextAsync(pauseSignalPath, "ready", cancellationToken);
        var timer = Stopwatch.StartNew();
        while (!File.Exists(resumeSignalPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (timer.Elapsed >= TimeSpan.FromSeconds(45)) throw new TimeoutException("The coordinated vault pause was not released.");
            await Task.Delay(25, cancellationToken);
        }
    }

    public ValueTask DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = SecretPath(key);
        if (File.Exists(path)) File.Delete(path);
        return ValueTask.CompletedTask;
    }

    string SecretPath(string key)
    {
        var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(folder, name + ".secret");
    }
}

sealed class FaultingDocumentStore(IAtomicDocumentStore inner) : IAtomicDocumentStore
{
    public bool FailNextWrite { get; set; }
    public bool FailNextRestore { get; set; }
    public int WriteCount { get; private set; }
    public int? FailBeforeWriteNumber { get; set; }
    public int? ThrowAfterWriteNumber { get; set; }
    public ValueTask<string?> ReadPrimaryAsync(CancellationToken cancellationToken = default) => inner.ReadPrimaryAsync(cancellationToken);
    public ValueTask<string?> ReadBackupAsync(CancellationToken cancellationToken = default) => inner.ReadBackupAsync(cancellationToken);
    public ValueTask RestoreBackupAsync(CancellationToken cancellationToken = default)
    {
        if (!FailNextRestore) return inner.RestoreBackupAsync(cancellationToken);
        FailNextRestore = false;
        throw new IOException("Injected backup restore failure");
    }
    public ValueTask DeleteAsync(CancellationToken cancellationToken = default) => inner.DeleteAsync(cancellationToken);
    public async ValueTask WriteAsync(string content, CancellationToken cancellationToken = default)
    {
        WriteCount++;
        if (FailNextWrite || WriteCount == FailBeforeWriteNumber)
        {
            FailNextWrite = false;
            throw new IOException("Injected document write failure before commit");
        }
        await inner.WriteAsync(content, cancellationToken);
        if (WriteCount == ThrowAfterWriteNumber) throw new IOException("Injected document write failure after commit");
    }
}

sealed class PrimaryDeleteFaultStore(string primaryPath) : FileAtomicDocumentStore(primaryPath)
{
    public bool FailPrimaryDeleteOnce { get; set; }

    protected override void DeleteFile(string path)
    {
        if (FailPrimaryDeleteOnce && string.Equals(path, PrimaryPath, StringComparison.Ordinal))
        {
            FailPrimaryDeleteOnce = false;
            throw new IOException("Injected interruption after backup deletion");
        }
        base.DeleteFile(path);
    }
}

sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(respond(request));
}
