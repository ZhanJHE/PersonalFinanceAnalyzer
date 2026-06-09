using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

public interface IAiService
{
    Task<string> GetFinancialAdviceAsync(List<Transaction> recentTransactions, string reportType = "1month");
    Task<string?> SuggestCategoryAsync(string description);
}
