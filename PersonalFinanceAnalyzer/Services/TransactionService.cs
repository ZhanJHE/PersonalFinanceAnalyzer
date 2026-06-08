using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

public class TransactionService : ITransactionService
{
    private readonly IDatabaseService _local;
    private readonly ICloudDataService _cloud;
    private readonly IAuthService _auth;

    public TransactionService(IDatabaseService local, ICloudDataService cloud, IAuthService auth)
    {
        _local = local;
        _cloud = cloud;
        _auth = auth;
    }

    public async Task<List<Transaction>> GetTransactionsAsync(DateTime? start = null, DateTime? end = null)
    {
        if (_auth.IsLoggedIn)
        {
            var remote = await _cloud.GetTransactionsAsync(start, end);
            await _local.CacheTransactionsAsync(remote);
            // 从本地读取（含 Categories JOIN，能获取 CategoryName）
            return await _local.GetTransactionsAsync(start, end);
        }
        return await _local.GetTransactionsAsync(start, end);
    }

    public async Task AddTransactionAsync(Transaction transaction)
    {
        await _local.AddTransactionAsync(transaction);
        if (_auth.IsLoggedIn)
        {
            try { await _cloud.AddTransactionAsync(transaction); }
            catch { /* silent fail for offline scenarios */ }
        }
    }

    public async Task UpdateTransactionAsync(Transaction transaction)
    {
        transaction.UpdatedAt = DateTime.UtcNow;
        await _local.UpdateTransactionAsync(transaction);
        if (_auth.IsLoggedIn)
        {
            try { await _cloud.UpdateTransactionAsync(transaction); }
            catch { }
        }
    }

    public async Task DeleteTransactionAsync(Guid id)
    {
        await _local.DeleteTransactionAsync(id);
        if (_auth.IsLoggedIn)
        {
            try { await _cloud.DeleteTransactionAsync(id); }
            catch { }
        }
    }

    public async Task<decimal> GetTotalAsync(string type, DateTime start, DateTime end)
    {
        return await _local.GetTotalAsync(type, start, end);
    }

    public async Task<List<Transaction>> GetTransactionsGroupedByCategoryAsync(DateTime start, DateTime end, string type = "Expense")
    {
        // 从本地读取（含 Categories JOIN），登录时云端数据已通过 GetTransactionsAsync 缓存
        return await _local.GetTransactionsGroupedByCategoryAsync(start, end, type);
    }
}
