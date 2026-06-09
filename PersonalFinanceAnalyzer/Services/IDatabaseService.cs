using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

public interface IDatabaseService
{
    Task InitializeAsync();
    Task<List<Transaction>> GetTransactionsAsync(DateTime? start = null, DateTime? end = null);
    Task AddTransactionAsync(Transaction transaction);
    Task UpdateTransactionAsync(Transaction transaction);
    Task DeleteTransactionAsync(Guid id);
    Task<List<Category>> GetCategoriesAsync(string? type = null);
    Task<Category?> GetCategoryByIdAsync(int id);
    Task AddCategoryAsync(Category category);
    Task DeleteCategoryAsync(int id);
    Task<decimal> GetTotalAsync(string type, DateTime start, DateTime end);
    Task<List<Transaction>> GetTransactionsGroupedByCategoryAsync(DateTime start, DateTime end, string type = "Expense");
    Task CacheTransactionsAsync(List<Transaction> transactions);
    Task UpdateCategoryColorAsync(int categoryId, string color);
    Task<string> ComputeHashAsync();
}
