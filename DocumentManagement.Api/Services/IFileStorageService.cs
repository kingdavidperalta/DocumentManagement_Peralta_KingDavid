namespace DocumentManagement.Api.Services;

public interface IFileStorageService
{
    // Saves the content under a new generated name and returns that stored file name.
    // Upload
    Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default);

    // Opens a stored file for reading. Returns null if the file does not exist.
    // Download
    Stream? OpenRead(string storedFileName);

    // Deletes a stored file. Returns false if the file did not exist.
    bool Delete(string storedFileName);
}