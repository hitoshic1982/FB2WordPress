using System.Text;

namespace FB2WordPress;

/// <summary>Recoverable atomic storage used by public settings and their transaction journal.</summary>
public interface IAtomicDocumentStore
{
    ValueTask<string?> ReadPrimaryAsync(CancellationToken cancellationToken = default);
    ValueTask<string?> ReadBackupAsync(CancellationToken cancellationToken = default);
    ValueTask WriteAsync(string content, CancellationToken cancellationToken = default);
    ValueTask RestoreBackupAsync(CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(CancellationToken cancellationToken = default);
}

public class FileAtomicDocumentStore(string primaryPath) : IAtomicDocumentStore
{
    static readonly UTF8Encoding Utf8 = new(false);
    public string PrimaryPath { get; } = primaryPath;
    public string BackupPath => PrimaryPath + ".bak";

    public async ValueTask<string?> ReadPrimaryAsync(CancellationToken cancellationToken = default) =>
        File.Exists(PrimaryPath) ? await File.ReadAllTextAsync(PrimaryPath, cancellationToken) : null;

    public async ValueTask<string?> ReadBackupAsync(CancellationToken cancellationToken = default) =>
        File.Exists(BackupPath) ? await File.ReadAllTextAsync(BackupPath, cancellationToken) : null;

    public async ValueTask WriteAsync(string content, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(PrimaryPath)!;
        Directory.CreateDirectory(directory);
        var temp = PrimaryPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllTextAsync(temp, content, Utf8, cancellationToken);
            if (!File.Exists(PrimaryPath)) File.Move(temp, PrimaryPath);
            else
            {
                try { File.Replace(temp, PrimaryPath, BackupPath, true); }
                catch (PlatformNotSupportedException) { PortableReplace(temp); }
                catch (IOException) { PortableReplace(temp); }
            }
        }
        finally { TryDelete(temp); }
    }

    public async ValueTask RestoreBackupAsync(CancellationToken cancellationToken = default)
    {
        var backup = await ReadBackupAsync(cancellationToken) ?? throw new FileNotFoundException("The recovery backup does not exist.", BackupPath);
        var directory = Path.GetDirectoryName(PrimaryPath)!;
        Directory.CreateDirectory(directory);
        var temp = PrimaryPath + ".restore-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllTextAsync(temp, backup, Utf8, cancellationToken);
            File.Move(temp, PrimaryPath, true);
        }
        finally { TryDelete(temp); }
    }

    public ValueTask DeleteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Delete the fallback first. If deletion is interrupted before the
        // primary disappears, recovery still sees the current journal rather
        // than reviving an older transaction from its backup.
        if (File.Exists(BackupPath)) DeleteFile(BackupPath);
        if (File.Exists(PrimaryPath)) DeleteFile(PrimaryPath);
        return ValueTask.CompletedTask;
    }

    protected virtual void DeleteFile(string path) => File.Delete(path);

    void PortableReplace(string temp)
    {
        File.Copy(PrimaryPath, BackupPath, true);
        File.Move(temp, PrimaryPath, true);
    }

    static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }
}
