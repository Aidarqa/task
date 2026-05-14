using System.ComponentModel.DataAnnotations;

namespace TrelloClone.Shared.Models;

// ── Department ───────────────────────────────────────────
public class Department
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
    public Guid? ParentId { get; set; }
}

// ── Employee Profile ─────────────────────────────────────
public class EmployeeProfile
{
    public string UserId { get; set; } = string.Empty;
    [MaxLength(100)] public string? Position { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public SystemRole Role { get; set; } = SystemRole.Employee;
    public bool IsActive { get; set; } = true;
}

// ── Work Task ────────────────────────────────────────────
public class WorkTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(4000)] public string? Description { get; set; }
    public WorkTaskStatus Status { get; set; } = WorkTaskStatus.New;
    public WorkTaskPriority Priority { get; set; } = WorkTaskPriority.Medium;
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? ParentTaskId { get; set; }
    public Guid? ProjectId { get; set; }
    public string? TagsJson { get; set; }
    public List<WorkTaskAssignee> Assignees { get; set; } = [];
    public List<WorkTask> SubTasks { get; set; } = [];
    public List<WorkTaskComment> Comments { get; set; } = [];
    public List<WorkTaskChecklistItem> Checklist { get; set; } = [];
    public List<FileAttachment> Attachments { get; set; } = [];
}

// ── Work Task Assignee ───────────────────────────────────
public class WorkTaskAssignee
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    // true = назначен автором задачи (защищён от удаления другими исполнителями)
    public bool IsOwnerAssigned { get; set; }
    public string AssignedById { get; set; } = string.Empty;
    public string AssignedByName { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}

// ── Work Task Comment ────────────────────────────────────
public class WorkTaskComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(2000)] public string Text { get; set; } = string.Empty;
    public Guid TaskId { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ── Work Task Checklist ──────────────────────────────────
public class WorkTaskChecklistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(200)] public string Text { get; set; } = string.Empty;
    public bool IsChecked { get; set; }
    public int Position { get; set; }
    public Guid TaskId { get; set; }
}

// ── Todo List ────────────────────────────────────────────
public class TodoList
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(100)] public string Title { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<TodoItem> Items { get; set; } = [];
}

// ── Todo Item ────────────────────────────────────────────
public class TodoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(200)] public string Text { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime? DueDate { get; set; }
    public int Position { get; set; }
    public Guid TodoListId { get; set; }
    public Guid? WorkTaskId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ── Calendar Event ───────────────────────────────────────
public class CalendarEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsAllDay { get; set; }
    public string Color { get; set; } = "#579DFF";
    public CalendarEventType EventType { get; set; } = CalendarEventType.Meeting;
    public string OrganizerId { get; set; } = string.Empty;
    public string OrganizerName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? ResourceId { get; set; }
    public List<EventParticipant> Participants { get; set; } = [];
}

// ── Event Participant ────────────────────────────────────
public class EventParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public ParticipantStatus Status { get; set; } = ParticipantStatus.Pending;
}

// ── Resource ─────────────────────────────────────────────
public class Resource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    public ResourceType ResourceType { get; set; } = ResourceType.Room;
    [MaxLength(500)] public string? Description { get; set; }
    public int? Capacity { get; set; }
    [MaxLength(200)] public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Booking> Bookings { get; set; } = [];
}

// ── Booking ──────────────────────────────────────────────
public class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResourceId { get; set; }
    public Resource? Resource { get; set; }
    public string BookedById { get; set; } = string.Empty;
    public string BookedByName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    [MaxLength(200)] public string? Title { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public Guid? EventId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ── Chat ─────────────────────────────────────────────────
public class Chat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(100)] public string? Name { get; set; }
    public ChatType ChatType { get; set; } = ChatType.Direct;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? AvatarColor { get; set; } = "#579DFF";
    public List<ChatMember> Members { get; set; } = [];
    public List<ChatMessage> Messages { get; set; } = [];
}

// ── Chat Member ──────────────────────────────────────────
public class ChatMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChatId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastReadAt { get; set; }
}

// ── Chat Message ─────────────────────────────────────────
public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(4000)] public string Text { get; set; } = string.Empty;
    public Guid ChatId { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public List<FileAttachment> Attachments { get; set; } = [];
}

// ── Notification ─────────────────────────────────────────
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(500)] public string? Body { get; set; }
    public NotificationType NotificationType { get; set; } = NotificationType.Info;
    public string UserId { get; set; } = string.Empty;
    public string? SenderUserId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(500)] public string? Link { get; set; }
    public string? RelatedEntityId { get; set; }
}

// ── Project ──────────────────────────────────────────────
public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Description { get; set; }
    public string Color { get; set; } = "#579DFF";
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public ProjectVisibility Visibility { get; set; } = ProjectVisibility.AllUsers;
    public List<ProjectMember> Members { get; set; } = [];
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ProjectMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}

// ── Enums ────────────────────────────────────────────────
public enum WorkTaskStatus   { New, InProgress, Review, Done, Cancelled }
public enum WorkTaskPriority { Low, Medium, High, Critical }
public enum SystemRole       { Admin, Manager, Employee, Viewer }
public enum CalendarEventType { Meeting, Task, Personal, TeamEvent }
public enum ParticipantStatus { Pending, Accepted, Declined }
public enum ResourceType     { Room, Equipment, Vehicle, Other }
public enum BookingStatus    { Confirmed, Cancelled, Pending }
public enum ChatType         { Direct, Group, TaskChat }
public enum NotificationType { Info, Task, Event, Booking, Chat, System }
public enum ProjectStatus    { Active, Paused, Completed, Archived }
public enum ProjectVisibility { AllUsers, SelectedUsers }
