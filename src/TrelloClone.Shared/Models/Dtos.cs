using System.ComponentModel.DataAnnotations;

namespace TrelloClone.Shared.Models;

// ── Auth DTOs ───────────────────────────────────────────
public record RegisterRequest(
    [Required, MaxLength(100)] string UserName,
    [Required, EmailAddress] string Email,
    [Required, MinLength(6)] string Password
);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

public record AuthResponse(
    bool Success,
    string? Token,
    string? UserId,
    string? UserName,
    string? Error
);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(6)] string NewPassword
);

// ── Board DTOs ──────────────────────────────────────────
public record CreateBoardRequest(
    [Required, MaxLength(120)] string Title,
    string? Description,
    string? BackgroundColor
);

public record UpdateBoardRequest(
    [Required, MaxLength(120)] string Title,
    string? Description,
    string? BackgroundColor
);

// ── Column DTOs ─────────────────────────────────────────
public record CreateColumnRequest(
    [Required, MaxLength(100)] string Title,
    Guid BoardId
);

public record UpdateColumnRequest(
    [Required, MaxLength(100)] string Title
);

public record MoveColumnRequest(int NewPosition);

// ── Card DTOs ───────────────────────────────────────────
public record CreateCardRequest(
    [Required, MaxLength(200)] string Title,
    string? Description,
    Guid ColumnId,
    CardPriority Priority = CardPriority.Medium,
    DateTime? DueDate = null
);

public record UpdateCardRequest(
    [Required, MaxLength(200)] string Title,
    string? Description,
    CardPriority Priority,
    DateTime? DueDate,
    string? AssigneeId,
    List<Guid>? LabelIds
);

public record MoveCardRequest(
    Guid TargetColumnId,
    int NewPosition
);

// ── Comment DTOs ────────────────────────────────────────
public record CreateCommentRequest(
    [Required, MaxLength(2000)] string Text
);

// ── Label DTOs ──────────────────────────────────────────
public record CreateLabelRequest(
    [Required, MaxLength(50)] string Name,
    string Color,
    Guid BoardId
);

// ── Checklist DTOs ──────────────────────────────────────
public record CreateChecklistItemRequest(
    [Required, MaxLength(200)] string Text
);

// ── SignalR Events ──────────────────────────────────────
public record CardMovedEvent(Guid CardId, Guid SourceColumnId, Guid TargetColumnId, int NewPosition);
public record BoardUpdatedEvent(Guid BoardId, string Action);
