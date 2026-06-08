using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

public interface ICategoryService
{
    Task<List<Category>> GetIncomeCategoriesAsync();
    Task<List<Category>> GetExpenseCategoriesAsync();
    Task AddCategoryAsync(Category category);
    Task DeleteCategoryAsync(int id);
}
