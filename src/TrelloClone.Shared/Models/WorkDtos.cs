using System.ComponentModel.DataAnnotations;

namespace TrelloClone.Shared.Models;

// ── Employee DTOs ─────────────────────────────────────────
public record UserDto(string Id, string UserName, string Email, string? Position,
    string? Phone, Guid? DepartmentId, string? DepartmentName, SystemRole Role, bool IsActive, string? AvatarUrl);

public record UpdateProfileRequest(
    [MaxLength(100)] string? Position,
    [MaxLength(30)] string? Phone,
    Guid? DepartmentId
);

public record SetRoleRequest(SystemRole Role);

// ── Department DTOs ───────────────────────────────────────
public record CreateDepartmentRequest([Required, MaxLength(100)] string Name, string? Description, Guid? ParentId);
public record UpdateDepartmentRequest([Required, MaxLength(100)] string Name, string? Description, Guid? ParentId);

// ── Work Task DTOs ────────────────────────────────────────
public record CreateWorkTaskRequest(
    [Required, MaxLength(200)] string Title,
    string? Description,
    WorkTaskPriority Priority = WorkTaskPriority.Medium,
    string? AssigneeId = null,
    DateTime? StartDate = null,
    DateTime? DueDate = null,
    Guid? ParentTaskId = null,
    Guid? ProjectId = null,
    string[]? Tags = null
);

public record UpdateWorkTaskRequest(
    [Required, MaxLength(200)] string Title,
    string? Description,
    WorkTaskStatus Status,
    WorkTaskPriority Priority,
    string? AssigneeId,
    DateTime? StartDate,
    DateTime? DueDate,
    string[]? Tags
);

public record WorkTaskSummary(
    Guid Id, string Title, WorkTaskStatus Status, WorkTaskPriority Priority,
    string AuthorId, string AuthorName, string? AssigneeId, string? AssigneeName,
    DateTime? DueDate, DateTime CreatedAt, int SubTaskCount, int CommentCount,
    int ChecklistTotal, int ChecklistDone, Guid? ProjectId, string? ProjectName
);

public record WorkTaskDetailDto(
    Guid Id,
    string Title,
    string? Description,
    WorkTaskStatus Status,
    WorkTaskPriority Priority,
    string AuthorId,
    string AuthorName,
    string? AssigneeId,
    string? AssigneeName,
    DateTime? StartDate,
    DateTime? DueDate,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? ParentTaskId,
    Guid? ProjectId,
    string? TagsJson,
    List<WorkTaskComment> Comments,
    List<WorkTaskChecklistItem> Checklist,
    List<FileAttachmentDto> Attachments
);

public record CreateWorkTaskCommentRequest([Required, MaxLength(2000)] string Text);
public record CreateWorkTaskChecklistItemRequest([Required, MaxLength(200)] string Text);
public record UpdateWorkTaskStatusRequest(WorkTaskStatus Status);

// ── Todo DTOs ─────────────────────────────────────────────
public record CreateTodoListRequest([Required, MaxLength(100)] string Title);
public record UpdateTodoListRequest([Required, MaxLength(100)] string Title);
public record CreateTodoItemRequest([Required, MaxLength(200)] string Text, DateTime? DueDate = null);
public record UpdateTodoItemRequest([Required, MaxLength(200)] string Text, bool IsCompleted, DateTime? DueDate);

// ── Calendar DTOs ─────────────────────────────────────────
public record CreateCalendarEventRequest(
    [Required, MaxLength(200)] string Title,
    string? Description,
    DateTime StartTime,
    DateTime EndTime,
    bool IsAllDay = false,
    string Color = "#579DFF",
    CalendarEventType EventType = CalendarEventType.Meeting,
    Guid? ResourceId = null,
    string[]? ParticipantIds = null
);

public record UpdateCalendarEventRequest(
    [Required, MaxLength(200)] string Title,
    string? Description,
    DateTime StartTime,
    DateTime EndTime,
    bool IsAllDay,
    string Color,
    CalendarEventType EventType,
    Guid? ResourceId,
    string[]? ParticipantIds = null
);

public record CalendarEventDto(
    Guid Id, string Title, string? Description,
    DateTime StartTime, DateTime EndTime, bool IsAllDay,
    string Color, CalendarEventType EventType,
    string OrganizerId, string OrganizerName,
    Guid? ResourceId, string? ResourceName,
    List<EventParticipant> Participants
);

// ── Resource DTOs ─────────────────────────────────────────
public record CreateResourceRequest(
    [Required, MaxLength(100)] string Name,
    ResourceType ResourceType,
    string? Description,
    int? Capacity,
    string? Location
);

public record UpdateResourceRequest(
    [Required, MaxLength(100)] string Name,
    string? Description,
    int? Capacity,
    string? Location,
    bool IsActive
);

// ── Booking DTOs ──────────────────────────────────────────
public record CreateBookingRequest(
    Guid ResourceId,
    DateTime StartTime,
    DateTime EndTime,
    [MaxLength(200)] string? Title = null,
    Guid? EventId = null
);

public record BookingDto(
    Guid Id, Guid ResourceId, string ResourceName, string ResourceLocation,
    string BookedById, string BookedByName,
    DateTime StartTime, DateTime EndTime,
    string? Title, BookingStatus Status, DateTime CreatedAt
);

// ── Chat DTOs ─────────────────────────────────────────────
public record CreateDirectChatRequest(string TargetUserId);
public record CreateGroupChatRequest([Required, MaxLength(100)] string Name, string[] MemberIds);
public record SendMessageRequest([Required, MaxLength(4000)] string Text);

public record ChatSummaryDto(
    Guid Id, string? Name, ChatType ChatType, string? AvatarColor,
    string? LastMessage, DateTime? LastMessageAt, int UnreadCount,
    List<ChatMemberDto> Members
);

public record ChatMemberDto(string UserId, string UserName);

public record ChatMessageDto(
    Guid Id, string Text, string SenderId, string SenderName,
    DateTime SentAt, bool IsDeleted, bool IsOwn, List<FileAttachmentDto>? Attachments = null,
    bool IsReadByOthers = false
);

// ── Notification DTOs ─────────────────────────────────────
public record NotificationDto(
    Guid Id, string Title, string? Body, NotificationType NotificationType,
    bool IsRead, DateTime CreatedAt, string? Link, string? RelatedEntityId = null
);

// ── Project DTOs ──────────────────────────────────────────
public record CreateProjectRequest(
    [Required, MaxLength(200)] string Name,
    string? Description,
    string Color = "#579DFF",
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    ProjectVisibility Visibility = ProjectVisibility.AllUsers,
    string[]? MemberIds = null
);

public record UpdateProjectRequest(
    [Required, MaxLength(200)] string Name,
    string? Description,
    string Color,
    DateTime? StartDate,
    DateTime? EndDate,
    ProjectStatus Status,
    ProjectVisibility Visibility,
    string[]? MemberIds
);

// ── Dashboard ─────────────────────────────────────────────
public record DashboardStats(
    int TotalTasks, int MyTasks, int OverdueTasks, int TasksDueToday,
    int UnreadNotifications, int UpcomingEvents,
    List<WorkTaskSummary> RecentTasks,
    List<CalendarEventDto> TodayEvents
);

// ── SignalR Events ────────────────────────────────────────
public record NewMessageEvent(Guid ChatId, ChatMessageDto Message);
public record ChatReadEvent(Guid ChatId, string UserId, DateTime ReadAt);
public record ChatTypingEvent(Guid ChatId, string UserId, string UserName, bool IsTyping);
public record NotificationEvent(NotificationDto Notification);
