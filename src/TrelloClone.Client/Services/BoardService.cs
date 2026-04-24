using System.Net.Http.Json;
using TrelloClone.Shared.Models;

namespace TrelloClone.Client.Services;

public interface IBoardService
{
    // Boards
    Task<List<Board>> GetBoardsAsync();
    Task<Board?> GetBoardAsync(Guid id);
    Task<Board?> CreateBoardAsync(CreateBoardRequest request);
    Task UpdateBoardAsync(Guid id, UpdateBoardRequest request);
    Task DeleteBoardAsync(Guid id);

    // Columns
    Task<BoardColumn?> CreateColumnAsync(CreateColumnRequest request);
    Task UpdateColumnAsync(Guid id, UpdateColumnRequest request);
    Task MoveColumnAsync(Guid id, int newPosition);
    Task DeleteColumnAsync(Guid id);

    // Cards
    Task<CardItem?> CreateCardAsync(CreateCardRequest request);
    Task<CardItem?> GetCardAsync(Guid id);
    Task UpdateCardAsync(Guid id, UpdateCardRequest request);
    Task MoveCardAsync(Guid id, MoveCardRequest request);
    Task DeleteCardAsync(Guid id);

    // Comments
    Task<CardComment?> AddCommentAsync(Guid cardId, CreateCommentRequest request);
    Task DeleteCommentAsync(Guid cardId, Guid commentId);

    // Checklist
    Task<ChecklistItem?> AddChecklistItemAsync(Guid cardId, CreateChecklistItemRequest request);
    Task ToggleChecklistItemAsync(Guid cardId, Guid itemId);

    // Labels
    Task<List<CardLabel>> GetBoardLabelsAsync(Guid boardId);
    Task<CardLabel?> CreateLabelAsync(CreateLabelRequest request);
    Task DeleteLabelAsync(Guid id);
}

public class BoardService : IBoardService
{
    private readonly HttpClient _http;

    public BoardService(HttpClient http) => _http = http;

    // ── Boards ──────────────────────────────────────────
    public async Task<List<Board>> GetBoardsAsync()
    {
        var result = await _http.GetFromJsonAsync<List<Board>>("api/boards");
        return (result ?? [])
            .Select(NormalizeBoard)
            .ToList();
    }

    public async Task<Board?> GetBoardAsync(Guid id)
    {
        var board = await _http.GetFromJsonAsync<Board>($"api/boards/{id}");
        return board is null ? null : NormalizeBoard(board);
    }

    public async Task<Board?> CreateBoardAsync(CreateBoardRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/boards", request);
        response.EnsureSuccessStatusCode();
        var board = await response.Content.ReadFromJsonAsync<Board>();
        return board is null ? null : NormalizeBoard(board);
    }

    public async Task UpdateBoardAsync(Guid id, UpdateBoardRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/boards/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteBoardAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"api/boards/{id}");
        response.EnsureSuccessStatusCode();
    }

    // ── Columns ─────────────────────────────────────────
    public async Task<BoardColumn?> CreateColumnAsync(CreateColumnRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/columns", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BoardColumn>();
    }

    public async Task UpdateColumnAsync(Guid id, UpdateColumnRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/columns/{id}", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task MoveColumnAsync(Guid id, int newPosition)
    {
        var response = await _http.PutAsJsonAsync($"api/columns/{id}/move", new MoveColumnRequest(newPosition));
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteColumnAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"api/columns/{id}");
        response.EnsureSuccessStatusCode();
    }

    // ── Cards ───────────────────────────────────────────
    public async Task<CardItem?> CreateCardAsync(CreateCardRequest request)
    {
        var normalizedRequest = request with
        {
            DueDate = KyrgyzstanTime.ConvertToUtc(request.DueDate)
        };

        var response = await _http.PostAsJsonAsync("api/cards", normalizedRequest);
        response.EnsureSuccessStatusCode();
        var card = await response.Content.ReadFromJsonAsync<CardItem>();
        return card is null ? null : NormalizeCard(card);
    }

    public async Task<CardItem?> GetCardAsync(Guid id)
    {
        var card = await _http.GetFromJsonAsync<CardItem>($"api/cards/{id}");
        return card is null ? null : NormalizeCard(card);
    }

    public async Task UpdateCardAsync(Guid id, UpdateCardRequest request)
    {
        var normalizedRequest = request with
        {
            DueDate = KyrgyzstanTime.ConvertToUtc(request.DueDate)
        };

        var response = await _http.PutAsJsonAsync($"api/cards/{id}", normalizedRequest);
        response.EnsureSuccessStatusCode();
    }

    public async Task MoveCardAsync(Guid id, MoveCardRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/cards/{id}/move", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteCardAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"api/cards/{id}");
        response.EnsureSuccessStatusCode();
    }

    // ── Comments ────────────────────────────────────────
    public async Task<CardComment?> AddCommentAsync(Guid cardId, CreateCommentRequest request)
    {
        var response = await _http.PostAsJsonAsync($"api/cards/{cardId}/comments", request);
        response.EnsureSuccessStatusCode();
        var comment = await response.Content.ReadFromJsonAsync<CardComment>();
        return comment is null ? null : NormalizeComment(comment);
    }

    public async Task DeleteCommentAsync(Guid cardId, Guid commentId)
    {
        var response = await _http.DeleteAsync($"api/cards/{cardId}/comments/{commentId}");
        response.EnsureSuccessStatusCode();
    }

    // ── Checklist ───────────────────────────────────────
    public async Task<ChecklistItem?> AddChecklistItemAsync(Guid cardId, CreateChecklistItemRequest request)
    {
        var response = await _http.PostAsJsonAsync($"api/cards/{cardId}/checklist", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChecklistItem>();
    }

    public async Task ToggleChecklistItemAsync(Guid cardId, Guid itemId)
    {
        var response = await _http.PutAsync($"api/cards/{cardId}/checklist/{itemId}/toggle", null);
        response.EnsureSuccessStatusCode();
    }

    // ── Labels ──────────────────────────────────────────
    public async Task<List<CardLabel>> GetBoardLabelsAsync(Guid boardId)
    {
        var result = await _http.GetFromJsonAsync<List<CardLabel>>($"api/labels/board/{boardId}");
        return result ?? [];
    }

    public async Task<CardLabel?> CreateLabelAsync(CreateLabelRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/labels", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CardLabel>();
    }

    public async Task DeleteLabelAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"api/labels/{id}");
        response.EnsureSuccessStatusCode();
    }

    private static Board NormalizeBoard(Board board)
    {
        board.CreatedAt = KyrgyzstanTime.ConvertFromApi(board.CreatedAt);
        board.UpdatedAt = KyrgyzstanTime.ConvertFromApi(board.UpdatedAt);
        board.Columns = board.Columns.Select(NormalizeColumn).ToList();
        return board;
    }

    private static BoardColumn NormalizeColumn(BoardColumn column)
    {
        column.Cards = column.Cards.Select(NormalizeCard).ToList();
        return column;
    }

    private static CardItem NormalizeCard(CardItem card)
    {
        card.DueDate = KyrgyzstanTime.ConvertFromApi(card.DueDate);
        card.CreatedAt = KyrgyzstanTime.ConvertFromApi(card.CreatedAt);
        card.UpdatedAt = KyrgyzstanTime.ConvertFromApi(card.UpdatedAt);
        card.Comments = card.Comments.Select(NormalizeComment).ToList();
        return card;
    }

    private static CardComment NormalizeComment(CardComment comment)
    {
        comment.CreatedAt = KyrgyzstanTime.ConvertFromApi(comment.CreatedAt);
        return comment;
    }
}
