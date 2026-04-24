using Microsoft.AspNetCore.Http;

namespace TrelloClone.Api.Services;

public static class FileUploadPolicy
{
    public const long MaxDocumentFileSize = 20 * 1024 * 1024;
    public const long MaxImageFileSize = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx"
    };

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".gif"
    };

    public static string? Validate(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;

        if (contentType.StartsWith("video/"))
            return $"Файл '{file.FileName}' отклонён: видео загружать нельзя.";

        if (AllowedImageExtensions.Contains(extension))
        {
            if (file.Length > MaxImageFileSize)
                return $"Файл '{file.FileName}' превышает лимит 5 MB для изображений.";

            return null;
        }

        if (AllowedDocumentExtensions.Contains(extension))
        {
            if (file.Length > MaxDocumentFileSize)
                return $"Файл '{file.FileName}' превышает лимит 20 MB для документов.";

            return null;
        }

        return $"Файл '{file.FileName}' не поддерживается. Разрешены только Word, Excel, PDF и небольшие изображения.";
    }
}
