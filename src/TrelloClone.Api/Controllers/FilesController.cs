using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrelloClone.Api.Data;
using TrelloClone.Api.Services;

namespace TrelloClone.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class FilesController(AppDbContext db, IFileStorageService storage) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] bool download = false, CancellationToken cancellationToken = default)
    {
        var attachment = await db.FileAttachments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (attachment is null)
            return NotFound();

        if (attachment.WorkTaskId.HasValue)
        {
            var task = await db.WorkTasks
                .Include(t => t.Assignees)
                .FirstOrDefaultAsync(t => t.Id == attachment.WorkTaskId.Value, cancellationToken);

            if (task is null || (task.AuthorId != CurrentUserId && !task.Assignees.Any(a => a.UserId == CurrentUserId)))
                return Forbid();
        }
        else if (attachment.ChatId.HasValue)
        {
            var isMember = await db.ChatMembers
                .AnyAsync(m => m.ChatId == attachment.ChatId.Value && m.UserId == CurrentUserId, cancellationToken);

            if (!isMember)
                return Forbid();

            if (attachment.ChatMessageId.HasValue)
            {
                var messageDeleted = await db.ChatMessages
                    .Where(m => m.Id == attachment.ChatMessageId.Value)
                    .Select(m => m.IsDeleted)
                    .FirstOrDefaultAsync(cancellationToken);

                if (messageDeleted)
                    return NotFound();
            }
        }
        else
        {
            return NotFound();
        }

        Stream stream;
        try
        {
            stream = await storage.OpenReadAsync(attachment.StoragePath, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return NotFound("Файл был удалён или недоступен. Загрузите файл повторно.");
        }

        var escapedName = Uri.EscapeDataString(attachment.FileName);
        Response.Headers["Content-Disposition"] = download
            ? $"attachment; filename*=UTF-8''{escapedName}"
            : $"inline; filename*=UTF-8''{escapedName}";

        return File(stream, attachment.ContentType, enableRangeProcessing: true);
    }
}
