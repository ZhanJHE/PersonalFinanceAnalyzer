using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

public class CloudDataService : ICloudDataService
{
    private readonly HttpClient _http;
    private readonly string _serverUrl;
    private readonly IAuthService _auth;

    public CloudDataService(IAuthService auth, IConfiguration config)
    {
        _auth = auth;
        _serverUrl = config["ServerUrl"] ?? "http://localhost:5000";
        _http = new HttpClient();
    }

    private void EnsureAuth()
    {
        if (!_auth.IsLoggedIn || _auth.Token == null)
            throw new InvalidOperationException("未登录。");
        _http.DefaultRequestHeaders.Remove("Authorization");
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_auth.Token}");
    }

    public async Task<List<Transaction>> GetTransactionsAsync(DateTime? start = null, DateTime? end = null)
    {
        EnsureAuth();
        var url = $"{_serverUrl}/api/sync/transactions";
        if (start.HasValue)
            url += $"?since={start.Value:yyyy-MM-dd}";

        var dtos = await _http.GetFromJsonAsync<List<TransactionDto>>(url);
        return dtos?.Select(MapToModel).ToList() ?? new List<Transaction>();
    }

    public async Task AddTransactionAsync(Transaction transaction)
    {
        EnsureAuth();
        var dto = MapToDto(transaction);
        await _http.PostAsJsonAsync($"{_serverUrl}/api/sync/transactions",
            new { Transactions = new[] { dto } });
    }

    public async Task UpdateTransactionAsync(Transaction transaction)
    {
        // Same as add - upsert based on Id
        await AddTransactionAsync(transaction);
    }

    public async Task DeleteTransactionAsync(Guid id)
    {
        EnsureAuth();
        await _http.DeleteAsync($"{_serverUrl}/api/sync/transactions/{id}");
    }

    public async Task<decimal> GetTotalAsync(string type, DateTime start, DateTime end)
    {
        var transactions = await GetTransactionsAsync(start, end);
        return transactions.Where(t => t.Type == type).Sum(t => t.Amount);
    }

    public async Task<List<Transaction>> GetTransactionsGroupedByCategoryAsync(DateTime start, DateTime end, string type = "Expense")
    {
        var transactions = await GetTransactionsAsync(start, end);
        return transactions
            .Where(t => t.Type == type)
            .GroupBy(t => t.CategoryName ?? "未知")
            .Select(g => new Transaction
            {
                CategoryName = g.Key,
                Amount = g.Sum(t => t.Amount),
                CategoryId = g.First().CategoryId
            })
            .OrderByDescending(t => t.Amount)
            .ToList();
    }

    public async Task<List<Transaction>> DownloadAllAsync()
    {
        return await GetTransactionsAsync();
    }

    public async Task<int> UploadAllAsync(List<Transaction> transactions)
    {
        EnsureAuth();
        var dtos = transactions.Select(MapToDto).ToList();
        var response = await _http.PostAsJsonAsync(
            $"{_serverUrl}/api/sync/transactions",
            new { Transactions = dtos });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
        return result?.SyncedCount ?? 0;
    }

    private static TransactionDto MapToDto(Transaction t) => new(
        t.Id, t.Amount, t.Type, t.CategoryId,
        t.TransactionDate, t.Note,
        t.CreatedAt != null ? DateTime.Parse(t.CreatedAt) : DateTime.UtcNow,
        t.UpdatedAt
    );

    private static Transaction MapToModel(TransactionDto dto) => new()
    {
        Id = dto.Id,
        Amount = dto.Amount,
        Type = dto.Type,
        CategoryId = dto.CategoryId,
        TransactionDate = dto.TransactionDate,
        Note = dto.Note,
        CreatedAt = dto.CreatedAt.ToString("O"),
        UpdatedAt = dto.UpdatedAt
    };

    private record TransactionDto(
        Guid Id, decimal Amount, string Type, int CategoryId,
        string TransactionDate, string? Note,
        DateTime CreatedAt, DateTime UpdatedAt
    );

    private record UploadResponse(int SyncedCount);
}
