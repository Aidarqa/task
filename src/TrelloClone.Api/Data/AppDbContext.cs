using Microsoft.EntityFrameworkCore;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ── Kanban (existing) ────────────────────────────────
    public DbSet<AppUser>      Users       => Set<AppUser>();
    public DbSet<Board>        Boards      => Set<Board>();
    public DbSet<BoardColumn>  Columns     => Set<BoardColumn>();
    public DbSet<CardItem>     Cards       => Set<CardItem>();
    public DbSet<CardComment>  Comments    => Set<CardComment>();
    public DbSet<CardLabel>    Labels      => Set<CardLabel>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<BoardMember>  BoardMembers => Set<BoardMember>();

    // ── Employees & Org ──────────────────────────────────
    public DbSet<Department>      Departments      => Set<Department>();
    public DbSet<EmployeeProfile> EmployeeProfiles => Set<EmployeeProfile>();

    // ── Work Tasks ───────────────────────────────────────
    public DbSet<WorkTask>                WorkTasks          => Set<WorkTask>();
    public DbSet<WorkTaskComment>         WorkTaskComments   => Set<WorkTaskComment>();
    public DbSet<WorkTaskChecklistItem>   WorkTaskChecklists => Set<WorkTaskChecklistItem>();
    public DbSet<FileAttachment>          FileAttachments    => Set<FileAttachment>();

    // ── Todo ─────────────────────────────────────────────
    public DbSet<TodoList> TodoLists => Set<TodoList>();
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    // ── Calendar ─────────────────────────────────────────
    public DbSet<CalendarEvent>   CalendarEvents  => Set<CalendarEvent>();
    public DbSet<EventParticipant> EventParticipants => Set<EventParticipant>();

    // ── Resources & Bookings ─────────────────────────────
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Booking>  Bookings  => Set<Booking>();

    // ── Chat ─────────────────────────────────────────────
    public DbSet<Chat>        Chats       => Set<Chat>();
    public DbSet<ChatMember>  ChatMembers => Set<ChatMember>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    // ── Notifications ────────────────────────────────────
    public DbSet<Notification> Notifications => Set<Notification>();

    // ── Projects ─────────────────────────────────────────
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    // ── RBAC ─────────────────────────────────────────────
    public DbSet<Role>           Roles           => Set<Role>();
    public DbSet<Permission>     Permissions     => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole>       UserRoles       => Set<UserRole>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ── User ─────────────────────────────────────────
        mb.Entity<AppUser>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.UserName).HasMaxLength(100);
            e.Property(u => u.Email).HasMaxLength(200);
        });

        // ── Board ─────────────────────────────────────────
        mb.Entity<Board>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.Title).HasMaxLength(120);
            e.HasMany(b => b.Columns).WithOne().HasForeignKey(c => c.BoardId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(b => b.Members).WithOne().HasForeignKey(m => m.BoardId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<BoardColumn>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasMany(c => c.Cards).WithOne().HasForeignKey(c => c.ColumnId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<CardItem>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasMany(c => c.Comments).WithOne().HasForeignKey(c => c.CardId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(c => c.Checklist).WithOne().HasForeignKey(c => c.CardId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(c => c.Labels).WithMany().UsingEntity("CardCardLabel");
        });

        mb.Entity<CardComment>(e => { e.HasKey(c => c.Id); e.Property(c => c.Text).HasMaxLength(2000); });
        mb.Entity<CardLabel>(e => { e.HasKey(l => l.Id); e.Property(l => l.Name).HasMaxLength(50); });
        mb.Entity<ChecklistItem>(e => { e.HasKey(c => c.Id); });
        mb.Entity<BoardMember>(e => { e.HasKey(m => m.Id); e.HasIndex(m => new { m.BoardId, m.UserId }).IsUnique(); });

        // ── Department ───────────────────────────────────
        mb.Entity<Department>(e => { e.HasKey(d => d.Id); e.Property(d => d.Name).HasMaxLength(100); });

        // ── Employee Profile ─────────────────────────────
        mb.Entity<EmployeeProfile>(e =>
        {
            e.HasKey(ep => ep.UserId);
            e.HasOne(ep => ep.Department).WithMany().HasForeignKey(ep => ep.DepartmentId).IsRequired(false);
        });

        // ── Work Task ────────────────────────────────────
        mb.Entity<WorkTask>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).HasMaxLength(200);
            e.HasMany(t => t.Comments).WithOne().HasForeignKey(c => c.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(t => t.Checklist).WithOne().HasForeignKey(c => c.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(t => t.Attachments).WithOne().HasForeignKey(a => a.WorkTaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(t => t.SubTasks).WithOne().HasForeignKey(t => t.ParentTaskId)
             .IsRequired(false).OnDelete(DeleteBehavior.NoAction);
        });

        mb.Entity<WorkTaskComment>(e => { e.HasKey(c => c.Id); });
        mb.Entity<WorkTaskChecklistItem>(e => { e.HasKey(c => c.Id); });

        // ── Todo ─────────────────────────────────────────
        mb.Entity<TodoList>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasMany(t => t.Items).WithOne().HasForeignKey(i => i.TodoListId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<TodoItem>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasIndex(i => i.WorkTaskId);
        });

        // ── Calendar ─────────────────────────────────────
        mb.Entity<CalendarEvent>(e =>
        {
            e.HasKey(ev => ev.Id);
            e.HasMany(ev => ev.Participants).WithOne().HasForeignKey(p => p.EventId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<EventParticipant>(e => { e.HasKey(p => p.Id); });

        // ── Resource & Booking ───────────────────────────
        mb.Entity<Resource>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasMany(r => r.Bookings).WithOne(b => b.Resource).HasForeignKey(b => b.ResourceId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<Booking>(e => { e.HasKey(b => b.Id); e.Navigation(b => b.Resource).AutoInclude(); });

        // ── Chat ─────────────────────────────────────────
        mb.Entity<Chat>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasMany(c => c.Members).WithOne().HasForeignKey(m => m.ChatId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(c => c.Messages).WithOne().HasForeignKey(m => m.ChatId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<ChatMember>(e => { e.HasKey(m => m.Id); e.HasIndex(m => new { m.ChatId, m.UserId }).IsUnique(); });
        mb.Entity<ChatMessage>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasMany(m => m.Attachments).WithOne().HasForeignKey(a => a.ChatMessageId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<FileAttachment>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.FileName).HasMaxLength(255);
            e.Property(a => a.StoragePath).HasMaxLength(500);
            e.Property(a => a.ContentType).HasMaxLength(200);
            e.HasIndex(a => a.WorkTaskId);
            e.HasIndex(a => a.ChatId);
            e.HasIndex(a => a.ChatMessageId);
        });

        // ── Notification ─────────────────────────────────
        mb.Entity<Notification>(e => { e.HasKey(n => n.Id); });

        // ── Project ──────────────────────────────────────
        mb.Entity<Project>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasMany(p => p.Members).WithOne().HasForeignKey(m => m.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<ProjectMember>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.ProjectId, m.UserId }).IsUnique();
        });

        // ── RBAC ─────────────────────────────────────────
        mb.Entity<Role>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.Name).IsUnique();
            e.Property(r => r.Name).HasMaxLength(100);
            e.Property(r => r.Description).HasMaxLength(400);
            e.HasMany(r => r.RolePermissions).WithOne()
             .HasForeignKey(rp => rp.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Permission>(e =>
        {
            e.HasKey(p => p.Code);
            e.Property(p => p.Code).HasMaxLength(80);
            e.Property(p => p.Description).HasMaxLength(200);
            e.Property(p => p.Category).HasMaxLength(80);
        });

        mb.Entity<RolePermission>(e =>
        {
            e.HasKey(rp => new { rp.RoleId, rp.PermissionCode });
            e.Property(rp => rp.PermissionCode).HasMaxLength(80);
        });

        mb.Entity<UserRole>(e =>
        {
            e.HasKey(ur => new { ur.UserId, ur.RoleId });
            e.HasIndex(ur => ur.UserId);
        });
    }
}
