using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalFinanceAnalyzer.Models;
using PersonalFinanceAnalyzer.Services;

namespace PersonalFinanceAnalyzer.ViewModels;

public partial class BudgetViewModel : ObservableObject
{
    private readonly IBudgetService _budget;
    private readonly IDatabaseService _db;

    [ObservableProperty]
    private ObservableCollection<Budget> _budgets = new();

    [ObservableProperty]
    private ObservableCollection<Category> _expenseCategories = new();

    [ObservableProperty]
    private Category? _selectedCategory;

    [ObservableProperty]
    private decimal _newLimit;

    [ObservableProperty]
    private int _currentYear = DateTime.Now.Year;

    [ObservableProperty]
    private int _currentMonth = DateTime.Now.Month;

    public BudgetViewModel(IBudgetService budget, IDatabaseService db)
    {
        _budget = budget;
        _db = db;
    }

    public async Task LoadAsync()
    {
        var cats = await _db.GetCategoriesAsync("Expense");
        ExpenseCategories = new ObservableCollection<Category>(cats);

        var budgets = await _budget.GetBudgetsAsync(CurrentYear, CurrentMonth);
        
        // Merge with categories: show all expense categories, mark which have budgets
        var budgetDict = budgets.ToDictionary(b => b.CategoryId);
        var merged = cats.Select(c =>
        {
            if (budgetDict.TryGetValue(c.Id, out var b))
                return b;
            return new Budget
            {
                CategoryId = c.Id,
                CategoryName = c.Name,
                MonthlyLimit = 0,
                Spent = 0,
                Year = CurrentYear,
                Month = CurrentMonth
            };
        }).ToList();

        Budgets = new ObservableCollection<Budget>(merged);
    }

    [RelayCommand]
    private async Task SetBudgetAsync()
    {
        if (SelectedCategory == null || NewLimit <= 0)
        {
            System.Windows.MessageBox.Show("请选择类别并输入有效的预算金额。", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        await _budget.SetBudgetAsync(SelectedCategory.Id, NewLimit, CurrentYear, CurrentMonth);
        NewLimit = 0;
        SelectedCategory = null;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteBudgetAsync(Budget? budget)
    {
        if (budget == null || budget.Id == 0) return;
        var result = System.Windows.MessageBox.Show(
            $"确定要清除类别「{budget.CategoryName}」的预算设置吗？", "确认",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
        if (result == System.Windows.MessageBoxResult.Yes)
        {
            await _budget.DeleteBudgetAsync(budget.Id);
            await LoadAsync();
        }
    }
}
