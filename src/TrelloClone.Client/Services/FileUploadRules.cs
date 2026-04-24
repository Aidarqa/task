using Microsoft.AspNetCore.Components.Forms;

namespace TrelloClone.Client.Services;

public static class FileUploadRules
{
    public const long MaxDocumentFileSize = 20 * 1024 * 1024;
    public const long MaxImageFileSize = 5 * 1024 * 1024;
    public const string AcceptAttribute = ".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg,.webp,.gif";

    private static readonly HashSet<string> AllowedDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx"
    };

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".gif"
    };

    public static string? Validate(IBrowserFile file, LocalizationService l)
    {
        var extension = Path.GetExtension(file.Name);
        var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;

        if (contentType.StartsWith("video/"))
            return string.Format(l["upload_error_video"], file.Name);

        if (AllowedImageExtensions.Contains(extension))
        {
            if (file.Size > MaxImageFileSize)
                return string.Format(l["upload_error_image_size"], file.Name);

            return null;
        }

        if (AllowedDocumentExtensions.Contains(extension))
        {
            if (file.Size > MaxDocumentFileSize)
                return string.Format(l["upload_error_doc_size"], file.Name);

            return null;
        }

        return string.Format(l["upload_error_unsupported"], file.Name);
    }

    public static long GetMaxReadSize(IBrowserFile file)
    {
        var extension = Path.GetExtension(file.Name);
        return AllowedImageExtensions.Contains(extension) ? MaxImageFileSize : MaxDocumentFileSize;
    }
}
