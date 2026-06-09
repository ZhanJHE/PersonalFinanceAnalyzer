using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalFinanceAnalyzer.Models;
using PersonalFinanceAnalyzer.Services;

namespace PersonalFinanceAnalyzer.ViewModels;

public partial class TransactionViewModel : ObservableObject
{
    private readonly ITransactionService _txn;
    private readonly IDatabaseService _db;
    private readonly IBudgetService _budget;
    private readonly IAiService _ai;

    [ObservableProperty]
    private ObservableCollection<Transaction> _transactions = new();

    [ObservableProperty]
    private ObservableCollection<Category> _categories = new();

    // New transaction form fields
    [ObservableProperty]
    private decimal _newAmount;

    [ObservableProperty]
    private string _newType = "Expense";

    [ObservableProperty]
    private Category? _selectedCategory;

    [ObservableProperty]
    private DateTime _newDate = DateTime.Today;

    [ObservableProperty]
    private string? _newNote;

    [ObservableProperty]
    private DateTime? _filterStartDate;

    [ObservableProperty]
    private DateTime? _filterEndDate;

    // Category management
    [ObservableProperty]
    private string _newCategoryName = string.Empty;

    [ObservableProperty]
    private string _newCategoryType = "Expense";

    [ObservableProperty]
    private string _newCategoryColor = "#FF6384";

    // Search
    [ObservableProperty]
    private string _searchText = string.Empty;

    // Display helper for type dropdown
    public class TypeOption
    {
        public string Value { get; set; } = "";
        public string Display { get; set; } = "";
    }

    public class ColorOption
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public List<TypeOption> TransactionTypes { get; } = new()
    {
        new() { Value = "Expense", Display = "支出" },
        new() { Value = "Income", Display = "收入" },
    };

    public List<ColorOption> PresetColors { get; } = new()
    {
        new() { Code = "#FF6384", Name = "粉红" },
        new() { Code = "#36A2EB", Name = "蓝色" },
        new() { Code = "#FFCE56", Name = "黄色" },
        new() { Code = "#4BC0C0", Name = "青色" },
        new() { Code = "#9966FF", Name = "紫色" },
        new() { Code = "#FF9F40", Name = "橙色" },
        new() { Code = "#C9CBCF", Name = "灰色" },
        new() { Code = "#5366FF", Name = "深蓝" },
        new() { Code = "#FF66FF", Name = "粉紫" },
        new() { Code = "#66CC99", Name = "翠绿" },
    };

    public TransactionViewModel(ITransactionService txn, IDatabaseService db, IBudgetService budget, IAiService ai)
    {
        _txn = txn;
        _db = db;
        _budget = budget;
        _ai = ai;
    }

    public async Task LoadAsync()
    {
        await LoadTransactionsAsync();
        await LoadCategoriesAsync();
    }

    private async Task LoadTransactionsAsync()
    {
        var transactions = await _txn.GetTransactionsAsync(FilterStartDate, FilterEndDate);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var keyword = SearchText.Trim();
            transactions = transactions
                .Where(t => (t.Note?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true)
                         || (t.CategoryName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true))
                .ToList();
        }

        Transactions = new ObservableCollection<Transaction>(transactions);
    }

    private async Task LoadCategoriesAsync()
    {
        var cats = await _db.GetCategoriesAsync();
        Categories = new ObservableCollection<Category>(cats);
    }

    [RelayCommand]
    private async Task AddTransactionAsync()
    {
        if (NewAmount <= 0)
        {
            System.Windows.MessageBox.Show("请输入有效金额。", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        if (SelectedCategory == null)
        {
            System.Windows.MessageBox.Show("请选择类别。", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        var transaction = new Transaction
        {
            Amount = NewAmount,
            Type = NewType,
            CategoryId = SelectedCategory.Id,
            TransactionDate = NewDate.ToString("yyyy-MM-dd"),
            Note = NewNote
        };

        await _txn.AddTransactionAsync(transaction);
        await LoadTransactionsAsync();

        // Check budget after adding an expense
        if (transaction.Type == "Expense")
        {
            var now = DateTime.Now;
            await _budget.CheckAndWarnOverBudgetAsync(transaction.CategoryId, now.Year, now.Month);
        }

        // Reset form
        NewAmount = 0;
        NewNote = null;
        SelectedCategory = null;
    }

    [RelayCommand]
    private async Task SuggestCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewNote))
        {
            System.Windows.MessageBox.Show("请先输入备注，然后点击「AI 推荐」自动匹配类别。", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        var suggested = await _ai.SuggestCategoryAsync(NewNote.Trim());
        if (string.IsNullOrEmpty(suggested))
        {
            System.Windows.MessageBox.Show("AI 推荐失败，请检查登录状态或网络连接。", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        // Try to match the suggested category name
        var match = Categories.FirstOrDefault(c =>
            c.Name.Equals(suggested, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            SelectedCategory = match;
            System.Windows.MessageBox.Show($"AI 推荐类别：{match.Name}", "AI 推荐结果",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        else
        {
            System.Windows.MessageBox.Show($"AI 推荐的类别「{suggested}」未找到，请手动选择。", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private async Task DeleteTransactionAsync(Transaction? transaction)
    {
        if (transaction == null) return;
        var result = System.Windows.MessageBox.Show(
            $"确定要删除 {transaction.TransactionDate} 的这笔记录吗？",
            "确认删除",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            await _txn.DeleteTransactionAsync(transaction.Id);
            await LoadTransactionsAsync();
        }
    }

    [RelayCommand]
    private async Task EditTransactionAsync(Transaction? transaction)
    {
        if (transaction == null) return;

        var dialog = new Views.EditTransactionWindow(transaction, Categories.ToList());
        dialog.Owner = System.Windows.Application.Current.MainWindow;

        if (dialog.ShowDialog() == true)
        {
            await _txn.UpdateTransactionAsync(dialog.EditedTransaction);
            await LoadTransactionsAsync();

            if (dialog.EditedTransaction.Type == "Expense")
            {
                var now = DateTime.Now;
                await _budget.CheckAndWarnOverBudgetAsync(dialog.EditedTransaction.CategoryId, now.Year, now.Month);
            }
        }
    }

    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        await LoadTransactionsAsync();
    }

    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        FilterStartDate = null;
        FilterEndDate = null;
        await LoadTransactionsAsync();
    }

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            System.Windows.MessageBox.Show("请输入类别名称。", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            await _db.AddCategoryAsync(new Models.Category
            {
                Name = NewCategoryName.Trim(),
                Type = NewCategoryType,
                Color = NewCategoryColor
            });
            NewCategoryName = string.Empty;
            NewCategoryColor = "#FF6384";
            await LoadCategoriesAsync();
            // Refresh color map
            await RefreshColorMapAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"添加类别失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync(Models.Category? category)
    {
        if (category == null) return;

        var result = System.Windows.MessageBox.Show(
            $"确定要删除类别「{category.Name}」吗？",
            "确认删除",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                await _db.DeleteCategoryAsync(category.Id);
                await LoadCategoriesAsync();
                await RefreshColorMapAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "无法删除",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
    }

    [RelayCommand]
    private async Task ChangeCategoryColorAsync(Models.Category? category)
    {
        if (category == null) return;

        // Show a simple input dialog-like approach using color list
        var currentColor = category.Color ?? "#FF6384";
        var options = PresetColors.Select(c => c.Code).ToList();
        var names = PresetColors.Select(c => c.Name).ToList();
        var defaultIdx = options.IndexOf(currentColor);
        if (defaultIdx < 0) defaultIdx = 0;

        // Use a simple ComboBox dialog approach
        var picker = new System.Windows.Controls.ComboBox
        {
            ItemsSource = PresetColors.Select(c => $"{c.Name} ({c.Code})").ToList(),
            SelectedIndex = defaultIdx,
            Width = 200
        };

        var msgBox = new System.Windows.Controls.StackPanel
        {
            Margin = new System.Windows.Thickness(10),
            Children =
            {
                new System.Windows.Controls.TextBlock { Text = $"选择「{category.Name}」的新颜色：", Margin = new System.Windows.Thickness(0,0,0,10) },
                picker
            }
        };

        var win = new System.Windows.Window
        {
            Title = "更换颜色",
            Content = msgBox,
            Width = 300,
            Height = 150,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
            Owner = System.Windows.Application.Current.MainWindow
        };

        var okBtn = new System.Windows.Controls.Button
        {
            Content = "确定",
            Width = 80,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new System.Windows.Thickness(0, 10, 0, 0)
        };
        okBtn.Click += async (_, _) =>
        {
            if (picker.SelectedIndex >= 0)
            {
                var newColor = options[picker.SelectedIndex];
                await _db.UpdateCategoryColorAsync(category.Id, newColor);
                await LoadCategoriesAsync();
                await RefreshColorMapAsync();
            }
            win.Close();
        };
        msgBox.Children.Add(okBtn);

        win.ShowDialog();
    }

    private async Task RefreshColorMapAsync()
    {
        var cats = await _db.GetCategoriesAsync();
        foreach (var cat in cats)
            Helpers.CategoryColorConverter.ColorMap[cat.Name] = cat.Color;
    }
}
