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
        => Ok(await db.Projects.OrderByDescending(p => p.CreatedAt).ToListAsync());

    // GET /api/projects/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var p = await db.Projects.FindAsync(id);
        return p is null ? NotFound() : Ok(p);
    }

    // POST /api/projects
    [HttpPost]
    public async Task<IActionResult> Create(CreateProjectRequest req)
    {
        var p = new Project
        {
            Name = req.Name, Description = req.Description, Color = req.Color,
            StartDate = req.StartDate, EndDate = req.EndDate,
            OwnerId = CurrentUserId, OwnerName = CurrentUserName
        };
        db.Projects.Add(p);
        await db.SaveChangesAsync();

        await notif.SendAsync(CurrentUserId, "Проект создан",
            $"Проект «{p.Name}» успешно создан",
            NotificationType.System, "/projects", p.Id.ToString());

        return Ok(p);
    }

    // PUT /api/projects/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateProjectRequest req)
    {
        var p = await db.Projects.FindAsync(id);
        if (p is null) return NotFound();

        var prevStatus = p.Status;
        p.Name = req.Name; p.Description = req.Description; p.Color = req.Color;
        p.StartDate = req.StartDate; p.EndDate = req.EndDate; p.Status = req.Status;
        await db.SaveChangesAsync();

        // Notify owner if status changed by someone else
        if (p.Status != prevStatus && p.OwnerId != CurrentUserId)
            await notif.SendAsync(p.OwnerId, "Статус проекта изменён",
                $"«{p.Name}» → {p.Status}",
                NotificationType.System, "/projects", p.Id.ToString());

        return Ok(p);
    }

    // DELETE /api/projects/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var p = await db.Projects.FindAsync(id);
        if (p is null) return NotFound();
        db.Projects.Remove(p);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/projects/{id}/tasks
    [HttpGet("{id:guid}/tasks")]
    public async Task<IActionResult> GetTasks(Guid id)
    {
        var tasks = await db.WorkTasks
            .Include(t => t.SubTasks)
            .Include(t => t.Comments)
            .Include(t => t.Checklist)
            .Where(t => t.ProjectId == id
                && t.ParentTaskId == null
                && (t.AssigneeId == CurrentUserId || t.AuthorId == CurrentUserId))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
        return Ok(tasks.Select(t => new WorkTaskSummary(
            t.Id, t.Title, t.Status, t.Priority,
            t.AuthorId, t.AuthorName, t.AssigneeId, t.AssigneeName,
            t.DueDate, t.CreatedAt, t.SubTasks.Count, t.Comments.Count,
            t.Checklist.Count, t.Checklist.Count(c => c.IsChecked), t.ProjectId, null)));
    }
}
