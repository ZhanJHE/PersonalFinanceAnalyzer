using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

public interface IBudgetService
{
    Task<List<Budget>> GetBudgetsAsync(int year, int month);
    Task SetBudgetAsync(int categoryId, decimal monthlyLimit, int year, int month);
    Task DeleteBudgetAsync(int id);
    Task<Budget?> GetBudgetForCategoryAsync(int categoryId, int year, int month);
    Task CheckAndWarnOverBudgetAsync(int categoryId, int year, int month);
}
