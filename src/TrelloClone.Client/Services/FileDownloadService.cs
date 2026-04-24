using Microsoft.JSInterop;

namespace TrelloClone.Client.Services;

public class FileDownloadService(HttpClient http, IJSRuntime js)
{
    public async Task DownloadAsync(string url, string fileName, CancellationToken cancellationToken = default)
    {
        var downloadUrl = url.Contains('?', StringComparison.Ordinal)
            ? $"{url}&download=true"
            : $"{url}?download=true";

        using var response = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        await js.InvokeVoidAsync("appFiles.downloadFile", cancellationToken, fileName, contentType, bytes);
    }
}
