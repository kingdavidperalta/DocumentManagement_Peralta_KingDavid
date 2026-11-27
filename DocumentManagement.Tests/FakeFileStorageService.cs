using DocumentManagement.Api.Services;

namespace DocumentManagement.Tests;

// A fake storage that keeps files in memory, so tests never touch the real disk.
public class FakeFileStorageService : IFileStorageService
{
    public Dictionary<string, byte[]> Files { get; } = new();

    // Set to true to simulate a disk error when deleting.
    public bool ThrowOnDelete { get; set; }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default)
    {
        var storedFileName = Guid.NewGuid().ToString("N") + extension;

        using var memory = new MemoryStream();
        await content.CopyToAsync(memory, cancellationToken);
        Files[storedFileName] = memory.ToArray();

        return storedFileName;
    }

    public Stream? OpenRead(string storedFileName)
    {
        if (!Files.ContainsKey(storedFileName))
        {
            return null;
        }

        return new MemoryStream(Files[storedFileName]);
    }

    public bool Delete(string storedFileName)
    {
        if (ThrowOnDelete)
        {
            throw new IOException("Simulated disk error.");
        }

        return Files.Remove(storedFileName);
    }
}