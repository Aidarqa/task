namespace TrelloClone.Api.Services;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream stream, string fileName, string scope, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
}
