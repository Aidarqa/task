using System.ComponentModel.DataAnnotations;

namespace TrelloClone.Shared.Models;

// ── Board ───────────────────────────────────────────────
public class Board
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public string? BackgroundColor { get; set; } = "#1565C0";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string OwnerId { get; set; } = string.Empty;

    public List<BoardColumn> Columns { get; set; } = [];
    public List<BoardMember> Members { get; set; } = [];
}

// ── Column ──────────────────────────────────────────────
public class BoardColumn
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    public int Position { get; set; }
    public Guid BoardId { get; set; }

    public List<CardItem> Cards { get; set; } = [];
}

// ── Card ────────────────────────────────────────────────
public class CardItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    public int Position { get; set; }
    public CardPriority Priority { get; set; } = CardPriority.Medium;
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Guid ColumnId { get; set; }
    public string? AssigneeId { get; set; }

    public List<CardLabel> Labels { get; set; } = [];
    public List<CardComment> Comments { get; set; } = [];
    public List<ChecklistItem> Checklist { get; set; } = [];
}

// ── Comment ─────────────────────────────────────────────
public class CardComment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(2000)]
    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CardId { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
}

// ── Label ───────────────────────────────────────────────
public class CardLabel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    public string Color { get; set; } = "#42A5F5";
    public Guid BoardId { get; set; }
}

// ── Checklist ───────────────────────────────────────────
public class ChecklistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Text { get; set; } = string.Empty;

    public bool IsChecked { get; set; }
    public int Position { get; set; }
    public Guid CardId { get; set; }
}

// ── Board Member ────────────────────────────────────────
public class BoardMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BoardId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public MemberRole Role { get; set; } = MemberRole.Member;
}

// ── App User (for auth) ────────────────────────────────
public class AppUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required, MaxLength(100)]
    public string UserName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}

// ── Enums ───────────────────────────────────────────────
public enum CardPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum MemberRole
{
    Viewer,
    Member,
    Admin
}
