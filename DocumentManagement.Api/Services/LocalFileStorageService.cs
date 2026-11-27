namespace DocumentManagement.Api.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredPath = configuration["Storage:UploadPath"] ?? "uploads";

        // A relative path is placed under the app folder; an absolute path (e.g. /app/uploads in Docker) is used as is.
        _rootPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default)
    {
        // The name on disk is generated here, never taken from the user.
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = GetSafePath(storedFileName);

        try
        {
            await using var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await content.CopyToAsync(fileStream, cancellationToken);
        }
        catch
        {
            // Do not leave a half-written file behind if the copy fails or is cancelled.
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            throw;
        }

        return storedFileName;
    }

    public Stream? OpenRead(string storedFileName)
    {
        var fullPath = GetSafePath(storedFileName);

        if (!File.Exists(fullPath))
        {
            return null;
        }

        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public bool Delete(string storedFileName)
    {
        var fullPath = GetSafePath(storedFileName);

        if (!File.Exists(fullPath))
        {
            return false;
        }

        File.Delete(fullPath);
        return true;
    }

    // Builds the full path and refuses anything that would end up outside the storage folder.
    private string GetSafePath(string storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName) || storedFileName != Path.GetFileName(storedFileName))
        {
            throw new ArgumentException("Invalid stored file name.", nameof(storedFileName));
        }

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, storedFileName));

        if (!fullPath.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid stored file name.", nameof(storedFileName));
        }

        return fullPath;
    }
}