using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrelloClone.Api.Data;
using TrelloClone.Api.Hubs;
using TrelloClone.Api.Services;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class ChatsController(
    AppDbContext db,
    IHubContext<ChatHub> chatHub,
    IHubContext<NotificationHub> notificationHub,
    IFileStorageService storage) : ControllerBase
{
    private const long MaxFileSize = FileUploadPolicy.MaxDocumentFileSize;

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string CurrentUserName => User.Identity!.Name!;

    [HttpGet]
    public async Task<IActionResult> GetMyChats()
    {
        var chats = await db.Chats
            .Include(c => c.Members)
            .Include(c => c.Messages)
                .ThenInclude(m => m.Attachments)
            .Where(c => c.Members.Any(m => m.UserId == CurrentUserId))
            .ToListAsync();

        var result = chats.Select(c =>
        {
            var lastMessage = c.Messages.OrderByDescending(m => m.SentAt).FirstOrDefault();
            var currentMember = c.Members.FirstOrDefault(m => m.UserId == CurrentUserId);
            var unreadCount = currentMember?.LastReadAt is null
                ? c.Messages.Count(m => m.SenderId != CurrentUserId && !m.IsDeleted)
                : c.Messages.Count(m => m.SentAt > currentMember.LastReadAt && m.SenderId != CurrentUserId && !m.IsDeleted);

            return new ChatSummaryDto(
                c.Id,
                c.Name ?? GetDirectChatName(c, CurrentUserId),
                c.ChatType,
                c.AvatarColor,
                BuildPreview(lastMessage),
                lastMessage?.SentAt,
                unreadCount,
                c.Members.Select(m => new ChatMemberDto(m.UserId, m.UserName)).ToList());
        })
        .OrderByDescending(c => c.LastMessageAt)
        .ToList();

        return Ok(result);
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid id, [FromQuery] int take = 50, [FromQuery] int skip = 0)
    {
        var isMember = await db.ChatMembers.AnyAsync(m => m.ChatId == id && m.UserId == CurrentUserId);
        if (!isMember)
            return Forbid();

        var messages = await db.ChatMessages
            .Include(m => m.Attachments)
            .Where(m => m.ChatId == id)
            .OrderByDescending(m => m.SentAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        var member = await db.ChatMembers.FirstOrDefaultAsync(m => m.ChatId == id && m.UserId == CurrentUserId);
        if (member is not null)
        {
            member.LastReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return Ok(messages
            .OrderBy(m => m.SentAt)
            .Select(ToMessageDto)
            .ToList());
    }

    [HttpPost("direct")]
    public async Task<IActionResult> CreateDirect(CreateDirectChatRequest req)
    {
        var target = await db.Users.FindAsync(req.TargetUserId);
        if (target is null)
            return NotFound();

        var existing = await db.Chats
            .Include(c => c.Members)
            .Where(c => c.ChatType == ChatType.Direct
                && c.Members.Any(m => m.UserId == CurrentUserId)
                && c.Members.Any(m => m.UserId == req.TargetUserId))
            .FirstOrDefaultAsync();

        if (existing is not null)
            return Ok(existing.Id);

        var chat = new Chat { ChatType = ChatType.Direct };
        chat.Members =
        [
            new ChatMember { ChatId = chat.Id, UserId = CurrentUserId, UserName = CurrentUserName },
            new ChatMember { ChatId = chat.Id, UserId = target.Id, UserName = target.UserName! }
        ];

        db.Chats.Add(chat);
        await db.SaveChangesAsync();
        return Ok(chat.Id);
    }

    [HttpPost("group")]
    public async Task<IActionResult> CreateGroup(CreateGroupChatRequest req)
    {
        var chat = new Chat { Name = req.Name, ChatType = ChatType.Group };
        var memberIds = req.MemberIds.Append(CurrentUserId).Distinct().ToArray();
        var users = await db.Users.Where(u => memberIds.Contains(u.Id)).ToListAsync();
        chat.Members = users.Select(u => new ChatMember
        {
            ChatId = chat.Id,
            UserId = u.Id,
            UserName = u.UserName!
        }).ToList();

        db.Chats.Add(chat);
        await db.SaveChangesAsync();
        return Ok(chat.Id);
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid id, SendMessageRequest req)
    {
        var isMember = await db.ChatMembers.AnyAsync(m => m.ChatId == id && m.UserId == CurrentUserId);
        if (!isMember)
            return Forbid();

        var message = new ChatMessage
        {
            ChatId = id,
            Text = req.Text,
            SenderId = CurrentUserId,
            SenderName = CurrentUserName
        };

        db.ChatMessages.Add(message);
        await db.SaveChangesAsync();

        var dto = await BroadcastMessageAsync(id, message, req.Text);
        return Ok(dto);
    }

    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(MaxFileSize * 5)]
    public async Task<IActionResult> SendFiles(Guid id, [FromForm] List<IFormFile> files, [FromForm] string? text, CancellationToken cancellationToken = default)
    {
        var isMember = await db.ChatMembers.AnyAsync(m => m.ChatId == id && m.UserId == CurrentUserId, cancellationToken);
        if (!isMember)
            return Forbid();

        if (files.Count == 0)
            return BadRequest("No files uploaded.");

        var message = new ChatMessage
        {
            ChatId = id,
            Text = text?.Trim() ?? string.Empty,
            SenderId = CurrentUserId,
            SenderName = CurrentUserName
        };

        var attachments = new List<FileAttachment>();
        foreach (var file in files.Where(f => f.Length > 0))
        {
            var validationError = FileUploadPolicy.Validate(file);
            if (validationError is not null)
                return BadRequest(validationError);

            await using var stream = file.OpenReadStream();
            var storagePath = await storage.SaveAsync(stream, file.FileName, $"chats/{id}", cancellationToken);

            attachments.Add(new FileAttachment
            {
                FileName = file.FileName,
                StoragePath = storagePath,
                ContentType = file.ContentType ?? "application/octet-stream",
                Size = file.Length,
                UploadedById = CurrentUserId,
                UploadedByName = CurrentUserName,
                ChatId = id,
                ChatMessageId = message.Id
            });
        }

        message.Attachments = attachments;
        db.ChatMessages.Add(message);
        db.FileAttachments.AddRange(attachments);
        await db.SaveChangesAsync(cancellationToken);

        var preview = !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : attachments.Count == 1
                ? $"Файл: {attachments[0].FileName}"
                : $"Отправлено файлов: {attachments.Count}";

        var dto = await BroadcastMessageAsync(id, message, preview);
        return Ok(dto);
    }

    [HttpDelete("{id:guid}/messages/{msgId:guid}")]
    public async Task<IActionResult> DeleteMessage(Guid id, Guid msgId)
    {
        var message = await db.ChatMessages.FirstOrDefaultAsync(m => m.Id == msgId && m.ChatId == id);
        if (message is null)
            return NotFound();

        if (message.SenderId != CurrentUserId)
            return Forbid();

        message.IsDeleted = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var member = await db.ChatMembers.FirstOrDefaultAsync(m => m.ChatId == id && m.UserId == CurrentUserId);
        if (member is null)
            return Forbid();

        member.LastReadAt = DateTime.UtcNow;

        await db.Notifications
            .Where(n => n.UserId == CurrentUserId
                     && !n.IsRead
                     && n.NotificationType == NotificationType.Chat
                     && n.RelatedEntityId == id.ToString())
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteChat(Guid id, CancellationToken cancellationToken = default)
    {
        var chat = await db.Chats
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (chat is null)
            return NotFound();

        if (!chat.Members.Any(m => m.UserId == CurrentUserId))
            return Forbid();

        await db.Notifications
            .Where(n => n.NotificationType == NotificationType.Chat && n.RelatedEntityId == id.ToString())
            .ExecuteDeleteAsync(cancellationToken);

        db.Chats.Remove(chat);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<ChatMessageDto> BroadcastMessageAsync(Guid chatId, ChatMessage message, string preview)
    {
        var recipients = await db.ChatMembers
            .Where(m => m.ChatId == chatId && m.UserId != CurrentUserId)
            .ToListAsync();

        var chatLink = $"/chats?chatId={chatId}";
        var notifications = recipients.Select(recipient => new Notification
        {
            Title = CurrentUserName,
            Body = preview,
            NotificationType = NotificationType.Chat,
            UserId = recipient.UserId,
            Link = chatLink,
            RelatedEntityId = chatId.ToString()
        }).ToList();

        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync();

        var dto = ToMessageDto(message);
        await chatHub.Clients.Group(chatId.ToString())
            .SendAsync("NewMessage", new NewMessageEvent(chatId, dto with { IsOwn = false }));

        foreach (var (recipient, notification) in recipients.Zip(notifications))
        {
            var notifDto = new NotificationDto(
                notification.Id,
                notification.Title,
                notification.Body,
                notification.NotificationType,
                false,
                notification.CreatedAt,
                notification.Link,
                notification.RelatedEntityId);

            await notificationHub.Clients.Group(recipient.UserId)
                .SendAsync("Notification", new NotificationEvent(notifDto));
        }

        return dto;
    }

    private ChatMessageDto ToMessageDto(ChatMessage message)
        => new(
            message.Id,
            message.IsDeleted ? "Сообщение удалено" : message.Text,
            message.SenderId,
            message.SenderName,
            message.SentAt,
            message.IsDeleted,
            message.SenderId == CurrentUserId,
            message.IsDeleted
                ? []
                : message.Attachments
                    .OrderByDescending(a => a.UploadedAt)
                    .Select(ToAttachmentDto)
                    .ToList());

    private FileAttachmentDto ToAttachmentDto(FileAttachment attachment)
        => new(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.Size,
            attachment.UploadedById,
            attachment.UploadedByName,
            attachment.UploadedAt,
            attachment.IsPinned,
            $"{Request.Scheme}://{Request.Host}/api/files/{attachment.Id}");

    private static string BuildPreview(ChatMessage? message)
    {
        if (message is null)
            return string.Empty;

        if (message.IsDeleted)
            return "Сообщение удалено";

        if (!string.IsNullOrWhiteSpace(message.Text))
            return message.Text;

        if (message.Attachments.Count == 1)
            return $"Файл: {message.Attachments[0].FileName}";

        if (message.Attachments.Count > 1)
            return $"Файлы: {message.Attachments.Count}";

        return string.Empty;
    }

    private static string GetDirectChatName(Chat chat, string currentUserId)
        => chat.Members.FirstOrDefault(m => m.UserId != currentUserId)?.UserName ?? "Чат";
}
