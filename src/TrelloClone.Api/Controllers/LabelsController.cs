using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrelloClone.Api.Data;
using TrelloClone.Shared.Models;

namespace TrelloClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LabelsController(AppDbContext db) : ControllerBase
{
    [HttpGet("board/{boardId:guid}")]
    public async Task<ActionResult<List<CardLabel>>> GetBoardLabels(Guid boardId)
    {
        var labels = await db.Labels
            .Where(l => l.BoardId == boardId)
            .OrderBy(l => l.Name)
            .ToListAsync();
        return Ok(labels);
    }

    [HttpPost]
    public async Task<ActionResult<CardLabel>> CreateLabel(CreateLabelRequest req)
    {
        var label = new CardLabel
        {
            Name = req.Name,
            Color = req.Color,
            BoardId = req.BoardId
        };

        db.Labels.Add(label);
        await db.SaveChangesAsync();
        return CreatedAtAction(null, new { id = label.Id }, label);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLabel(Guid id)
    {
        var label = await db.Labels.FindAsync(id);
        if (label is null) return NotFound();

        db.Labels.Remove(label);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
