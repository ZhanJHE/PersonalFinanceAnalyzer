using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceAnalyzer.Server.Data;
using PersonalFinanceAnalyzer.Server.Models;

namespace PersonalFinanceAnalyzer.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly AppDbContext _db;

    public SyncController(AppDbContext db)
    {
        _db = db;
    }

    public record TransactionDto(
        Guid Id, decimal Amount, string Type, int CategoryId,
        string TransactionDate, string? Note,
        DateTime CreatedAt, DateTime UpdatedAt
    );

    public record UploadRequest(List<TransactionDto> Transactions);
    public record UploadResponse(int SyncedCount);

    /// <summary>
    /// GET /api/sync/hash
    /// Returns SHA256 hash of user's transactions + last modified time.
    /// Used by client to quickly check if sync is needed.
    /// </summary>
    [HttpGet("hash")]
    public async Task<ActionResult> GetSyncHash()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var transactions = await _db.Transactions
            .Where(t => t.UserId == userId.Value)
            .OrderBy(t => t.Id)
            .ToListAsync();

        var hash = ComputeHash(transactions);
        var maxUpdatedAt = transactions.Count > 0
            ? transactions.Max(t => t.UpdatedAt)
            : DateTime.MinValue;

        return Ok(new { hash, maxUpdatedAt });
    }

    private static string ComputeHash(List<ServerTransaction> transactions)
    {
        var sb = new StringBuilder();
        foreach (var t in transactions)
        {
            sb.AppendLine($"{t.Id}|{t.Amount}|{t.Type}|{t.CategoryId}|{t.TransactionDate}|{t.Note ?? ""}|{t.UpdatedAt:O}");
        }
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>
    /// GET /api/sync/transactions?since=2024-01-01
    /// Download all transactions for the current user (optionally since a date)
    /// </summary>
    [HttpGet("transactions")]
    public async Task<ActionResult<List<TransactionDto>>> DownloadTransactions(
        [FromQuery] DateTime? since)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var query = _db.Transactions
            .Where(t => t.UserId == userId.Value);

        if (since.HasValue)
            query = query.Where(t => t.UpdatedAt > since.Value);

        var transactions = await query
            .OrderByDescending(t => t.TransactionDate)
            .Select(t => new TransactionDto(
                t.Id, t.Amount, t.Type, t.CategoryId,
                t.TransactionDate, t.Note,
                t.CreatedAt, t.UpdatedAt
            ))
            .ToListAsync();

        return Ok(transactions);
    }

    /// <summary>
    /// POST /api/sync/transactions
    /// Upload transactions from client. Server inserts or updates based on UpdatedAt.
    /// </summary>
    [HttpPost("transactions")]
    public async Task<ActionResult<UploadResponse>> UploadTransactions(
        [FromBody] UploadRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        int synced = 0;

        foreach (var dto in request.Transactions)
        {
            var existing = await _db.Transactions.FindAsync(dto.Id);
            if (existing == null)
            {
                // New record - insert
                _db.Transactions.Add(new ServerTransaction
                {
                    Id = dto.Id,
                    UserId = userId.Value,
                    Amount = dto.Amount,
                    Type = dto.Type,
                    CategoryId = dto.CategoryId,
                    TransactionDate = dto.TransactionDate,
                    Note = dto.Note,
                    CreatedAt = dto.CreatedAt,
                    UpdatedAt = dto.UpdatedAt
                });
                synced++;
            }
            else if (dto.UpdatedAt > existing.UpdatedAt)
            {
                // Client has a newer version - update
                existing.Amount = dto.Amount;
                existing.Type = dto.Type;
                existing.CategoryId = dto.CategoryId;
                existing.TransactionDate = dto.TransactionDate;
                existing.Note = dto.Note;
                existing.UpdatedAt = dto.UpdatedAt;
                synced++;
            }
            // else: server version is newer, keep it
        }

        await _db.SaveChangesAsync();
        return Ok(new UploadResponse(synced));
    }

    private int? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && int.TryParse(claim, out var id) ? id : null;
    }
}
