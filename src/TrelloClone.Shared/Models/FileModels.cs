using System.ComponentModel.DataAnnotations;

namespace TrelloClone.Shared.Models;

public class FileAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string StoragePath { get; set; } = string.Empty;

    [MaxLength(200)]
    public string ContentType { get; set; } = "application/octet-stream";

    public long Size { get; set; }
    public string UploadedById { get; set; } = string.Empty;
    public string UploadedByName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsPinned { get; set; }

    public Guid? WorkTaskId { get; set; }
    public Guid? ChatId { get; set; }
    public Guid? ChatMessageId { get; set; }
}

public record FileAttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long Size,
    string UploadedById,
    string UploadedByName,
    DateTime UploadedAt,
    bool IsPinned,
    string DownloadUrl
);
