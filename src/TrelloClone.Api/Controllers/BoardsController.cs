using System.Security.Claims;
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
public class BoardsController(AppDbContext db, IHubContext<BoardHub> hub) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string UserName => User.FindFirstValue(ClaimTypes.Name)!;

    [HttpGet]
    public async Task<ActionResult<List<Board>>> GetBoards()
    {
        var boards = await db.Boards
            .Include(b => b.Members)
            .Where(b => b.OwnerId == UserId || b.Members.Any(m => m.UserId == UserId))
            .OrderByDescending(b => b.UpdatedAt)
            .ToListAsync();
        return Ok(boards);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Board>> GetBoard(Guid id)
    {
        var board = await db.Boards
            .Include(b => b.Columns.OrderBy(c => c.Position))
                .ThenInclude(c => c.Cards.OrderBy(c => c.Position))
                    .ThenInclude(c => c.Labels)
            .Include(b => b.Columns)
                .ThenInclude(c => c.Cards)
                    .ThenInclude(c => c.Comments.OrderByDescending(cm => cm.CreatedAt))
            .Include(b => b.Columns)
                .ThenInclude(c => c.Cards)
                    .ThenInclude(c => c.Checklist.OrderBy(cl => cl.Position))
            .Include(b => b.Members)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (board is null) return NotFound();
        return Ok(board);
    }

    [HttpPost]
    public async Task<ActionResult<Board>> CreateBoard(CreateBoardRequest req)
    {
        var board = new Board
        {
            Title = req.Title,
            Description = req.Description,
            BackgroundColor = req.BackgroundColor ?? "#1565C0",
            OwnerId = UserId,
            Columns =
            [
                new() { Title = "To Do", Position = 0 },
                new() { Title = "In Progress", Position = 1 },
                new() { Title = "Done", Position = 2 }
            ],
            Members =
            [
                new() { UserId = UserId, UserName = UserName, Role = MemberRole.Admin }
            ]
        };

        db.Boards.Add(board);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetBoard), new { id = board.Id }, board);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateBoard(Guid id, UpdateBoardRequest req)
    {
        var board = await db.Boards.FindAsync(id);
        if (board is null) return NotFound();

        board.Title = req.Title;
        board.Description = req.Description;
        board.BackgroundColor = req.BackgroundColor;
        board.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        await hub.Clients.Group(id.ToString())
            .SendAsync("BoardUpdated", new BoardUpdatedEvent(id, "updated"));
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteBoard(Guid id)
    {
        var board = await db.Boards.FindAsync(id);
        if (board is null) return NotFound();
        if (board.OwnerId != UserId) return Forbid();

        db.Boards.Remove(board);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
