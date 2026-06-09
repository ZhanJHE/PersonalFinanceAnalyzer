using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

public class CategoryService : ICategoryService
{
    private readonly IDatabaseService _db;

    public CategoryService(IDatabaseService db)
    {
        _db = db;
    }

    public Task<List<Category>> GetIncomeCategoriesAsync()
        => _db.GetCategoriesAsync("Income");

    public Task<List<Category>> GetExpenseCategoriesAsync()
        => _db.GetCategoriesAsync("Expense");

    public Task AddCategoryAsync(Category category)
        => _db.AddCategoryAsync(category);

    public Task DeleteCategoryAsync(int id)
        => _db.DeleteCategoryAsync(id);
}
