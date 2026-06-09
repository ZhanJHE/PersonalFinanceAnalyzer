using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PersonalFinanceAnalyzer.Models;
using PersonalFinanceAnalyzer.Services;

namespace PersonalFinanceAnalyzer.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ITransactionService _txn;

    [ObservableProperty]
    private decimal _monthlyIncome;

    [ObservableProperty]
    private decimal _monthlyExpense;

    [ObservableProperty]
    private decimal _monthlyBalance;

    [ObservableProperty]
    private ObservableCollection<Transaction> _recentTransactions = new();

    public DashboardViewModel(ITransactionService txn)
    {
        _txn = txn;
    }

    public async Task LoadAsync()
    {
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        MonthlyIncome = await _txn.GetTotalAsync("Income", monthStart, monthEnd);
        MonthlyExpense = await _txn.GetTotalAsync("Expense", monthStart, monthEnd);
        MonthlyBalance = MonthlyIncome - MonthlyExpense;

        var transactions = await _txn.GetTransactionsAsync(monthStart, monthEnd);
        RecentTransactions = new ObservableCollection<Transaction>(transactions.Take(10));
    }
}
