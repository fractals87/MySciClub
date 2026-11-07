namespace MySciClub.Application.Abstractions.FileStorage;

public interface IFileStorageService
{
    Task<string> StoreAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
