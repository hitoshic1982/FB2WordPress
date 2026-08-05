using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace FB2WordPress;

/// <summary>
/// Serializes a complete settings transaction across store instances and processes.
/// The returned lease owns the operating-system file handle; disposing any other
/// object cannot release it, and an abnormal process exit releases it automatically.
/// </summary>
public interface ISettingsTransactionLock
{
    ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default);
}

/// <summary>A lock path failed a safety check and must not be retried as ordinary contention.</summary>
public sealed class UnsafeSettingsTransactionPathException(string message) : IOException(message);

/// <summary>
/// Cross-process settings lock backed by an exclusive file handle. The lock file is
/// intentionally retained after release: an unlocked file is not a stale lease, and
/// reusing it avoids unsafe delete-and-recreate races between processes.
/// </summary>
public sealed class FileSettingsTransactionLock : ISettingsTransactionLock
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    const int RetryDelayMilliseconds = 40;
    const string LockFileName = "settings.transaction.lock";

    readonly string rootPath;
    readonly string lockPath;
    readonly TimeSpan timeout;

    public FileSettingsTransactionLock(string folder, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        rootPath = Path.GetFullPath(folder);
        lockPath = Path.Combine(rootPath, LockFileName);
        this.timeout = timeout ?? DefaultTimeout;
        if (this.timeout <= TimeSpan.Zero || this.timeout > TimeSpan.FromMinutes(2))
            throw new ArgumentOutOfRangeException(nameof(timeout), "The settings lock timeout must be between zero and two minutes.");
    }

    public string LockPath => lockPath;

    public async ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();
        IOException? lastContention = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Path validation errors are security failures, not contention.
            // Keep them outside the retry catch so they are reported at once.
            EnsureSafeLocation();
            FileStream? stream = null;
            try
            {
                stream = new FileStream(lockPath, new FileStreamOptions
                {
                    Mode = FileMode.OpenOrCreate,
                    Access = FileAccess.ReadWrite,
                    Share = FileShare.None,
                    BufferSize = 4096,
                    Options = FileOptions.WriteThrough
                });
            }
            catch (OperationCanceledException)
            {
                stream?.Dispose();
                throw;
            }
            catch (UnauthorizedAccessException exception)
            {
                stream?.Dispose();
                throw new IOException("The settings transaction lock is not accessible; the operation was refused.", exception);
            }
            catch (IOException exception)
            {
                stream?.Dispose();
                lastContention = exception;
            }

            if (stream is not null)
            {
                try
                {
                    // Recheck after opening to narrow a path-swap race. Any
                    // path-safety or ownership-record failure is immediate.
                    EnsureSafeLocation();
                    WriteOwnershipRecord(stream);
                    return new OwnedLease(stream);
                }
                catch
                {
                    stream.Dispose();
                    throw;
                }
            }

            if (timer.Elapsed >= timeout)
                throw new TimeoutException("The settings transaction lock could not be acquired before the safety timeout; the operation was refused without changing settings or credentials.", lastContention);

            var remaining = timeout - timer.Elapsed;
            var delay = remaining < TimeSpan.FromMilliseconds(RetryDelayMilliseconds)
                ? remaining
                : TimeSpan.FromMilliseconds(RetryDelayMilliseconds);
            if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
        }
    }

    void EnsureSafeLocation()
    {
        EnsureNoReparseDirectoryComponents(rootPath);
        Directory.CreateDirectory(rootPath);
        EnsureNoReparseDirectoryComponents(rootPath);

        var lockInfo = new FileInfo(lockPath);
        lockInfo.Refresh();
        if (IsReparsePoint(lockInfo))
            throw new UnsafeSettingsTransactionPathException("The settings transaction lock path is a symbolic link or reparse point; the operation was refused.");
        if (Directory.Exists(lockPath))
            throw new UnsafeSettingsTransactionPathException("The settings transaction lock path is not a regular file; the operation was refused.");
    }

    static void EnsureNoReparseDirectoryComponents(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var volumeRoot = Path.GetPathRoot(fullPath)
            ?? throw new UnsafeSettingsTransactionPathException("The settings transaction directory has no filesystem root.");
        var relative = Path.GetRelativePath(volumeRoot, fullPath);
        var current = volumeRoot;

        if (relative == ".") return;
        foreach (var component in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, component);
            var info = new DirectoryInfo(current);
            info.Refresh();
            if (IsReparsePoint(info))
                throw new UnsafeSettingsTransactionPathException("The settings transaction directory contains a symbolic link or reparse point; the operation was refused.");
            if (File.Exists(current) && !Directory.Exists(current))
                throw new UnsafeSettingsTransactionPathException("A settings transaction directory component is not a directory; the operation was refused.");
        }
    }

    static bool IsReparsePoint(FileSystemInfo info)
    {
        try
        {
            if (info.LinkTarget is not null) return true;
            return info.Exists && (info.Attributes & FileAttributes.ReparsePoint) != 0;
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    static void WriteOwnershipRecord(FileStream stream)
    {
        var record = JsonSerializer.Serialize(new
        {
            processId = Environment.ProcessId,
            acquiredUtc = DateTimeOffset.UtcNow,
            ownerToken = Guid.NewGuid().ToString("N")
        });
        var bytes = Encoding.UTF8.GetBytes(record);
        stream.SetLength(0);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    sealed class OwnedLease(FileStream stream) : IAsyncDisposable
    {
        FileStream? ownedStream = stream;

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref ownedStream, null)?.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
