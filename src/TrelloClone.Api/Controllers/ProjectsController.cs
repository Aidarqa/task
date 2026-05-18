using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrelloClone.Api.Data;
using TrelloClone.Api.Services;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class ProjectsController(AppDbContext db, INotificationService notif) : ControllerBase
{
    private string CurrentUserId   => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string CurrentUserName => User.Identity!.Name!;

    // GET /api/projects
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var projects = await AccessibleProjects()
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return Ok(projects);
    }

    // GET /api/projects/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var p = await db.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (p is not null && !CanAccess(p))
            return Forbid();

        return p is null ? NotFound() : Ok(p);
    }

    // POST /api/projects
    [HttpPost]
    public async Task<IActionResult> Create(CreateProjectRequest req)
    {
        var p = new Project
        {
            Name = req.Name, Description = req.Description, Color = req.Color,
            StartDate = KyrgyzstanTime.NormalizeUtc(req.StartDate), EndDate = KyrgyzstanTime.NormalizeUtc(req.EndDate),
            OwnerId = CurrentUserId, OwnerName = CurrentUserName,
            Visibility = req.Visibility
        };

        db.Projects.Add(p);
        await SetMembersAsync(p.Id, req.Visibility, req.MemberIds ?? []);
        await db.SaveChangesAsync();

        await notif.SendAsync(CurrentUserId, "Проект создан",
            $"Проект «{p.Name}» успешно создан",
            NotificationType.Task, "/projects", p.Id.ToString(),
            senderUserId: CurrentUserId);

        return Ok(p);
    }

    // PUT /api/projects/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateProjectRequest req)
    {
        var p = await db.Projects.FirstOrDefaultAsync(p => p.Id == id);
        if (p is null) return NotFound();
        if (p.OwnerId != CurrentUserId) return Forbid();

        var prevStatus = p.Status;
        p.Name = req.Name; p.Description = req.Description; p.Color = req.Color;
        p.StartDate = KyrgyzstanTime.NormalizeUtc(req.StartDate); p.EndDate = KyrgyzstanTime.NormalizeUtc(req.EndDate); p.Status = req.Status;
        p.Visibility = req.Visibility;
        await SetMembersAsync(p.Id, req.Visibility, req.MemberIds ?? []);
        await db.SaveChangesAsync();

        await db.Entry(p).Collection(project => project.Members).LoadAsync();

        // Notify owner if status changed by someone else
        if (p.Status != prevStatus && p.OwnerId != CurrentUserId)
            await notif.SendAsync(p.OwnerId, "Статус проекта изменён",
                $"«{p.Name}» → {p.Status}",
                NotificationType.Task, "/projects", p.Id.ToString(),
                senderUserId: CurrentUserId);

        return Ok(p);
    }

    // DELETE /api/projects/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var p = await db.Projects.FindAsync(id);
        if (p is null) return NotFound();

        var isAdmin = User.HasClaim("perm", Permissions.AdminAccess);
        if (p.OwnerId != CurrentUserId && !isAdmin) return Forbid();

        db.Projects.Remove(p);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/projects/{id}/tasks
    [HttpGet("{id:guid}/tasks")]
    public async Task<IActionResult> GetTasks(Guid id)
    {
        var project = await db.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (project is null) return NotFound();
        if (!CanAccess(project)) return Forbid();

        var tasks = await db.WorkTasks
            .Include(t => t.SubTasks)
            .Include(t => t.Comments)
            .Include(t => t.Checklist)
            .Include(t => t.Assignees)
            .Where(t => t.ProjectId == id
                && t.ParentTaskId == null
                && (t.Assignees.Any(a => a.UserId == CurrentUserId) || t.AuthorId == CurrentUserId))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
        return Ok(tasks.Select(t => new WorkTaskSummary(
            t.Id, t.Title, t.Status, t.Priority,
            t.AuthorId, t.AuthorName,
            t.Assignees.Select(a => new WorkTaskAssigneeDto(a.UserId, a.UserName, a.IsOwnerAssigned, a.AssignedById, a.AssignedByName)).ToList(),
            t.DueDate, t.CreatedAt, t.SubTasks.Count, t.Comments.Count,
            t.Checklist.Count, t.Checklist.Count(c => c.IsChecked), t.ProjectId, null)));
    }

    private IQueryable<Project> AccessibleProjects()
    {
        var userId = CurrentUserId;
        return db.Projects
            .Include(p => p.Members)
            .Where(p =>
                p.OwnerId == userId ||                                         // created by user
                p.Visibility == ProjectVisibility.AllUsers ||                  // visible to all
                db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == userId)); // user is member
    }

    private bool CanAccess(Project project)
        => project.OwnerId == CurrentUserId
            || project.Visibility == ProjectVisibility.AllUsers
            || project.Members.Any(m => m.UserId == CurrentUserId);

    private async Task SetMembersAsync(Guid projectId, ProjectVisibility visibility, IEnumerable<string> memberIds)
    {
        await db.ProjectMembers
            .Where(member => member.ProjectId == projectId)
            .ExecuteDeleteAsync();

        if (visibility != ProjectVisibility.SelectedUsers)
            return;

        var distinctIds = memberIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && id != CurrentUserId)
            .Distinct()
            .ToArray();

        if (distinctIds.Length == 0)
            return;

        var users = await db.Users
            .Where(u => distinctIds.Contains(u.Id))
            .ToListAsync();

        db.ProjectMembers.AddRange(users.Select(u => new ProjectMember
        {
            ProjectId = projectId,
            UserId = u.Id,
            UserName = u.UserName
        }));
    }
}
