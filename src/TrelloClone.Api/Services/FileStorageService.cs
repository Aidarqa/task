using System.Text.RegularExpressions;

namespace TrelloClone.Api.Services;

public partial class FileStorageService(IWebHostEnvironment env) : IFileStorageService
{
    private readonly string _root = Path.Combine(env.ContentRootPath, "App_Data", "uploads");

    public async Task<string> SaveAsync(Stream stream, string fileName, string scope, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);

        var safeName = UnsafeFileNamePattern().Replace(Path.GetFileName(fileName), "_");
        var folder = Path.Combine(_root, scope);
        Directory.CreateDirectory(folder);

        var storedFileName = $"{Guid.NewGuid():N}_{safeName}";
        var relativePath = Path.Combine(scope, storedFileName);
        var absolutePath = Path.Combine(_root, relativePath);

        await using var fileStream = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fileStream, cancellationToken);

        return relativePath.Replace('\\', '/');
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = Resolve(storagePath);
        Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = Resolve(storagePath);
        if (File.Exists(absolutePath))
            File.Delete(absolutePath);

        return Task.CompletedTask;
    }

    private string Resolve(string storagePath)
    {
        var normalized = storagePath.Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.GetFullPath(Path.Combine(_root, normalized));
        var rootPath = Path.GetFullPath(_root);

        if (!absolutePath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid storage path.");

        return absolutePath;
    }

    [GeneratedRegex(@"[^A-Za-z0-9._-]+", RegexOptions.Compiled)]
    private static partial Regex UnsafeFileNamePattern();
}
