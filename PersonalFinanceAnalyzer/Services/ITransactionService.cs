using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

public interface ITransactionService
{
    Task<List<Transaction>> GetTransactionsAsync(DateTime? start = null, DateTime? end = null);
    Task AddTransactionAsync(Transaction transaction);
    Task UpdateTransactionAsync(Transaction transaction);
    Task DeleteTransactionAsync(Guid id);
    Task<decimal> GetTotalAsync(string type, DateTime start, DateTime end);
    Task<List<Transaction>> GetTransactionsGroupedByCategoryAsync(DateTime start, DateTime end, string type = "Expense");
}
