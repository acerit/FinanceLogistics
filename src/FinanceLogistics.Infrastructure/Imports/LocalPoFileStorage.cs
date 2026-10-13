using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FinanceLogistics.Application.Imports;

namespace FinanceLogistics.Infrastructure.Imports;

public sealed class LocalPoFileStorage : IFileStorage
{
    private readonly string root;
    public LocalPoFileStorage(string storageRoot)
    {
        if (string.IsNullOrWhiteSpace(storageRoot) || !Path.IsPathFullyQualified(storageRoot))
            throw new InvalidOperationException("PoImport:StorageRoot must be an absolute, private directory.");
        root = Path.GetFullPath(storageRoot);
        if (root.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => string.Equals(part, "wwwroot", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("PO original files must be stored outside wwwroot.");
        Directory.CreateDirectory(root);
    }
    private string PathFor(string key)
    {
        if (!Regex.IsMatch(key, "^[a-f0-9]{32}\\.bin$")) throw new InvalidOperationException("Invalid storage key.");
        return Path.Combine(root, key);
    }
    public async Task<StoredObject> SaveAsync(Stream content, string displayName, long maxBytes, CancellationToken ct)
    {
        var key = Guid.NewGuid().ToString("N") + ".bin";
        var path = PathFor(key);
        var temporary = path + ".uploading";
        long size = 0;
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous))
            {
                var buffer = new byte[81920];
                int count;
                while ((count = await content.ReadAsync(buffer.AsMemory(), ct)) > 0)
                {
                    size += count;
                    if (size > maxBytes) throw new PoImportFault(413, "FILE_TOO_LARGE", "File vượt giới hạn dung lượng.");
                    hash.AppendData(buffer, 0, count);
                    await file.WriteAsync(buffer.AsMemory(0, count), ct);
                }
                await file.FlushAsync(ct);
                file.Flush(flushToDisk: true);
            }
            if (size == 0) throw new PoImportFault(400, "EMPTY_FILE", "File không có nội dung.");
            File.Move(temporary, path); // Same volume, never overwrite an existing source.
            return new StoredObject(key, size, hash.GetHashAndReset());
        }
        catch { if (File.Exists(temporary)) File.Delete(temporary); throw; }
    }
    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Stream stream = new FileStream(PathFor(storageKey), FileMode.Open, FileAccess.Read,
            FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }
    // Only call for a NEW candidate file that the transaction definitely did not reference.
    public void DeleteUnusedCandidate(string storageKey) => File.Delete(PathFor(storageKey));
}
