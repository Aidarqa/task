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
public class CardsController(AppDbContext db, IHubContext<BoardHub> hub) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string UserName => User.FindFirstValue(ClaimTypes.Name)!;

    [HttpPost]
    public async Task<ActionResult<CardItem>> CreateCard(CreateCardRequest req)
    {
        var maxPos = await db.Cards
            .Where(c => c.ColumnId == req.ColumnId)
            .MaxAsync(c => (int?)c.Position) ?? -1;

        var card = new CardItem
        {
            Title = req.Title,
            Description = req.Description,
            ColumnId = req.ColumnId,
            Priority = req.Priority,
            DueDate = req.DueDate,
            Position = maxPos + 1
        };

        db.Cards.Add(card);
        await db.SaveChangesAsync();

        var col = await db.Columns.FindAsync(req.ColumnId);
        if (col is not null)
            await hub.Clients.Group(col.BoardId.ToString())
                .SendAsync("BoardUpdated", new BoardUpdatedEvent(col.BoardId, "card_added"));

        return CreatedAtAction(null, new { id = card.Id }, card);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CardItem>> GetCard(Guid id)
    {
        var card = await db.Cards
            .Include(c => c.Labels)
            .Include(c => c.Comments.OrderByDescending(cm => cm.CreatedAt))
            .Include(c => c.Checklist.OrderBy(cl => cl.Position))
            .FirstOrDefaultAsync(c => c.Id == id);

        if (card is null) return NotFound();
        return Ok(card);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCard(Guid id, UpdateCardRequest req)
    {
        var card = await db.Cards.Include(c => c.Labels).FirstOrDefaultAsync(c => c.Id == id);
        if (card is null) return NotFound();

        card.Title = req.Title;
        card.Description = req.Description;
        card.Priority = req.Priority;
        card.DueDate = req.DueDate;
        card.AssigneeId = req.AssigneeId;
        card.UpdatedAt = DateTime.UtcNow;

        if (req.LabelIds is not null)
        {
            card.Labels.Clear();
            var labels = await db.Labels.Where(l => req.LabelIds.Contains(l.Id)).ToListAsync();
            card.Labels = labels;
        }

        await db.SaveChangesAsync();

        var col = await db.Columns.FindAsync(card.ColumnId);
        if (col is not null)
            await hub.Clients.Group(col.BoardId.ToString())
                .SendAsync("BoardUpdated", new BoardUpdatedEvent(col.BoardId, "card_updated"));

        return NoContent();
    }

    [HttpPut("{id:guid}/move")]
    public async Task<IActionResult> MoveCard(Guid id, MoveCardRequest req)
    {
        var card = await db.Cards.FindAsync(id);
        if (card is null) return NotFound();

        var sourceColumnId = card.ColumnId;

        // Remove from source
        var sourceCards = await db.Cards
            .Where(c => c.ColumnId == sourceColumnId && c.Id != id)
            .OrderBy(c => c.Position)
            .ToListAsync();
        for (var i = 0; i < sourceCards.Count; i++)
            sourceCards[i].Position = i;

        // Insert into target
        var targetCards = await db.Cards
            .Where(c => c.ColumnId == req.TargetColumnId && c.Id != id)
            .OrderBy(c => c.Position)
            .ToListAsync();

        card.ColumnId = req.TargetColumnId;
        card.UpdatedAt = DateTime.UtcNow;
        targetCards.Insert(Math.Min(req.NewPosition, targetCards.Count), card);
        for (var i = 0; i < targetCards.Count; i++)
            targetCards[i].Position = i;

        await db.SaveChangesAsync();

        var col = await db.Columns.FindAsync(req.TargetColumnId);
        if (col is not null)
        {
            var evt = new CardMovedEvent(id, sourceColumnId, req.TargetColumnId, req.NewPosition);
            await hub.Clients.Group(col.BoardId.ToString()).SendAsync("CardMoved", evt);
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCard(Guid id)
    {
        var card = await db.Cards.FindAsync(id);
        if (card is null) return NotFound();

        var col = await db.Columns.FindAsync(card.ColumnId);
        db.Cards.Remove(card);
        await db.SaveChangesAsync();

        if (col is not null)
            await hub.Clients.Group(col.BoardId.ToString())
                .SendAsync("BoardUpdated", new BoardUpdatedEvent(col.BoardId, "card_deleted"));

        return NoContent();
    }

    // ── Comments ────────────────────────────────────────
    [HttpPost("{cardId:guid}/comments")]
    public async Task<ActionResult<CardComment>> AddComment(Guid cardId, CreateCommentRequest req)
    {
        var card = await db.Cards.FindAsync(cardId);
        if (card is null) return NotFound();

        var comment = new CardComment
        {
            Text = req.Text,
            CardId = cardId,
            AuthorId = UserId,
            AuthorName = UserName
        };

        db.Comments.Add(comment);
        await db.SaveChangesAsync();

        return CreatedAtAction(null, new { id = comment.Id }, comment);
    }

    [HttpDelete("{cardId:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid cardId, Guid commentId)
    {
        var comment = await db.Comments.FindAsync(commentId);
        if (comment is null || comment.CardId != cardId) return NotFound();
        if (comment.AuthorId != UserId) return Forbid();

        db.Comments.Remove(comment);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Checklist ───────────────────────────────────────
    [HttpPost("{cardId:guid}/checklist")]
    public async Task<ActionResult<ChecklistItem>> AddChecklistItem(Guid cardId, CreateChecklistItemRequest req)
    {
        var maxPos = await db.ChecklistItems
            .Where(c => c.CardId == cardId)
            .MaxAsync(c => (int?)c.Position) ?? -1;

        var item = new ChecklistItem
        {
            Text = req.Text,
            CardId = cardId,
            Position = maxPos + 1
        };

        db.ChecklistItems.Add(item);
        await db.SaveChangesAsync();
        return CreatedAtAction(null, new { id = item.Id }, item);
    }

    [HttpPut("{cardId:guid}/checklist/{itemId:guid}/toggle")]
    public async Task<IActionResult> ToggleChecklistItem(Guid cardId, Guid itemId)
    {
        var item = await db.ChecklistItems.FindAsync(itemId);
        if (item is null || item.CardId != cardId) return NotFound();

        item.IsChecked = !item.IsChecked;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
