using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TrelloClone.Api.Data;
using TrelloClone.Api.Hubs;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ColumnsController(AppDbContext db, IHubContext<BoardHub> hub) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<BoardColumn>> CreateColumn(CreateColumnRequest req)
    {
        var maxPos = await db.Columns
            .Where(c => c.BoardId == req.BoardId)
            .MaxAsync(c => (int?)c.Position) ?? -1;

        var column = new BoardColumn
        {
            Title = req.Title,
            BoardId = req.BoardId,
            Position = maxPos + 1
        };

        db.Columns.Add(column);
        await db.SaveChangesAsync();

        await hub.Clients.Group(req.BoardId.ToString())
            .SendAsync("BoardUpdated", new BoardUpdatedEvent(req.BoardId, "column_added"));

        return CreatedAtAction(null, new { id = column.Id }, column);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateColumn(Guid id, UpdateColumnRequest req)
    {
        var col = await db.Columns.FindAsync(id);
        if (col is null) return NotFound();

        col.Title = req.Title;
        await db.SaveChangesAsync();

        await hub.Clients.Group(col.BoardId.ToString())
            .SendAsync("BoardUpdated", new BoardUpdatedEvent(col.BoardId, "column_updated"));
        return NoContent();
    }

    [HttpPut("{id:guid}/move")]
    public async Task<IActionResult> MoveColumn(Guid id, MoveColumnRequest req)
    {
        var col = await db.Columns.FindAsync(id);
        if (col is null) return NotFound();

        var siblings = await db.Columns
            .Where(c => c.BoardId == col.BoardId)
            .OrderBy(c => c.Position)
            .ToListAsync();

        siblings.Remove(col);
        siblings.Insert(Math.Min(req.NewPosition, siblings.Count), col);

        for (var i = 0; i < siblings.Count; i++)
            siblings[i].Position = i;

        await db.SaveChangesAsync();

        await hub.Clients.Group(col.BoardId.ToString())
            .SendAsync("BoardUpdated", new BoardUpdatedEvent(col.BoardId, "column_moved"));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteColumn(Guid id)
    {
        var col = await db.Columns
            .Include(c => c.Cards)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (col is null) return NotFound();

        var boardId = col.BoardId;
        db.Columns.Remove(col);
        await db.SaveChangesAsync();

        await hub.Clients.Group(boardId.ToString())
            .SendAsync("BoardUpdated", new BoardUpdatedEvent(boardId, "column_deleted"));
        return NoContent();
    }
}
