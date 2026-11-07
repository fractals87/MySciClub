using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MySciClub.Application.Abstractions.FileStorage;
using MySciClub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MySciClub.Infrastructure.Files;

public interface IPhotoStorageMaintenance
{
    Task<int> RemoveOrphansAsync(TimeSpan minimumAge, CancellationToken cancellationToken = default);
}

internal sealed partial class FilePhotoStorage : IFileStorageService, IPhotoStorageMaintenance
{
    private readonly string _root;
    private readonly MySciClubDbContext _db;

    public FilePhotoStorage(string root, MySciClubDbContext db)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("Storage root is required.", nameof(root));
        if (!Path.IsPathRooted(root)) throw new ArgumentException("Storage root must be an absolute path.", nameof(root));
        _root = Path.GetFullPath(root);
        _db = db;
        Directory.CreateDirectory(_root);
    }

    public async Task<string> StoreAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead || contentType != "image/webp") throw new InvalidDataException("Only processed WebP images can be stored.");
        var photosDirectory = Path.Combine(_root, "photos");
        Directory.CreateDirectory(photosDirectory);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var finalPath = Path.Combine(photosDirectory, token + ".webp");
        var temporaryPath = Path.Combine(photosDirectory, ".tmp-" + token);
        try
        {
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await content.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
            File.Move(temporaryPath, finalPath, overwrite: false);
            return "photos/" + token + ".webp";
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(storageKey);
        if (!File.Exists(path)) return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan));
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public async Task<int> RemoveOrphansAsync(TimeSpan minimumAge, CancellationToken cancellationToken = default)
    {
        if (minimumAge < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(minimumAge));
        var referenced = (await _db.Soci.AsNoTracking().Where(x => x.PhotoStorageKey != null)
            .Select(x => x.PhotoStorageKey!).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var photosDirectory = Path.Combine(_root, "photos");
        if (!Directory.Exists(photosDirectory)) return 0;
        var cutoff = DateTime.UtcNow - minimumAge;
        var removed = 0;
        foreach (var file in Directory.EnumerateFiles(photosDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = "photos/" + Path.GetFileName(file);
            if (File.GetLastWriteTimeUtc(file) >= cutoff) continue;
            if (Path.GetFileName(file).StartsWith(".tmp-", StringComparison.Ordinal) || !referenced.Contains(key))
            {
                File.Delete(file);
                removed++;
            }
        }
        return removed;
    }

    private string Resolve(string key)
    {
        if (!SafePhotoKeyRegex().IsMatch(key)) throw new InvalidDataException("Invalid photo storage reference.");
        return Path.Combine(_root, "photos", Path.GetFileName(key));
    }

    [GeneratedRegex("^photos/[a-f0-9]{32}\\.webp$", RegexOptions.CultureInvariant)]
    private static partial Regex SafePhotoKeyRegex();
}
