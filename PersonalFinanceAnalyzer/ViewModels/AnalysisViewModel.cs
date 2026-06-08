using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalFinanceAnalyzer.Models;
using PersonalFinanceAnalyzer.Services;
using PersonalFinanceAnalyzer.Views;

namespace PersonalFinanceAnalyzer.ViewModels;

public partial class AnalysisViewModel : ObservableObject
{
    private readonly ITransactionService _txn;
    private readonly IAiService _ai;
    private readonly IAuthService _auth;
    private readonly IDailyViewService _daily;

    // Pre-computed trend data for three time ranges
    public List<DailyBalance> TrendBuckets1Month { get; private set; } = new();
    public List<DailyBalance> TrendBuckets3Months { get; private set; } = new();
    public List<DailyBalance> TrendBuckets6Months { get; private set; } = new();

    // Pre-computed pie data for three time ranges
    [ObservableProperty] private ObservableCollection<Transaction> _categoryBreakdown1Month = new();
    [ObservableProperty] private ObservableCollection<Transaction> _categoryBreakdown3Months = new();
    [ObservableProperty] private ObservableCollection<Transaction> _categoryBreakdown6Months = new();

    // Date range labels
    [ObservableProperty] private string _dateRangeLabel1 = "";
    [ObservableProperty] private string _dateRangeLabel3 = "";
    [ObservableProperty] private string _dateRangeLabel6 = "";

    [ObservableProperty]
    private string _refreshStatus = "";

    [ObservableProperty]
    private bool _isLoggedIn;

    public AnalysisViewModel(ITransactionService txn, IChartService chart, IAiService ai, IAuthService auth, IDailyViewService daily)
    {
        _txn = txn;
        _ai = ai;
        _auth = auth;
        _daily = daily;
        _isLoggedIn = auth.IsLoggedIn;
        auth.LoginStateChanged += (_, _) => IsLoggedIn = auth.IsLoggedIn;
    }

    public async Task LoadAsync() => await LoadChartDataAsync();

    /// <summary>
    /// Pre-compute data for all three time ranges at once.
    /// </summary>
    public async Task LoadChartDataAsync()
    {
        var now = DateTime.Now;
        RefreshStatus = "正在加载图表数据...";

        var task1 = ComputeFullRangeAsync(1, now);
        var task3 = ComputeFullRangeAsync(3, now);
        var task6 = ComputeFullRangeAsync(6, now);
        await Task.WhenAll(task1, task3, task6);

        var (buckets1, pie1, label1) = task1.Result;
        var (buckets3, pie3, label3) = task3.Result;
        var (buckets6, pie6, label6) = task6.Result;

        TrendBuckets1Month = buckets1;
        TrendBuckets3Months = buckets3;
        TrendBuckets6Months = buckets6;

        _categoryBreakdown1Month.Clear();
        foreach (var t in pie1) _categoryBreakdown1Month.Add(t);
        _categoryBreakdown3Months.Clear();
        foreach (var t in pie3) _categoryBreakdown3Months.Add(t);
        _categoryBreakdown6Months.Clear();
        foreach (var t in pie6) _categoryBreakdown6Months.Add(t);

        DateRangeLabel1 = label1;
        DateRangeLabel3 = label3;
        DateRangeLabel6 = label6;

        RefreshStatus = "";
    }

    private async Task<(List<DailyBalance> Buckets, List<Transaction> PieData, string DateRangeLabel)> ComputeFullRangeAsync(int months, DateTime now)
    {
        var start = now.AddMonths(-months);
        var totalDays = (now - start).Days;

        // Buckets for trend chart
        var dailyData = await _daily.GetDailyBalancesAsync(start, now);
        int points = 15;
        int daysPerBucket = Math.Max(1, totalDays / points);
        var buckets = new List<DailyBalance>();
        int actualBuckets = Math.Min(points, (int)Math.Ceiling((double)totalDays / daysPerBucket));

        for (int i = 0; i < actualBuckets && i * daysPerBucket < dailyData.Count; i++)
        {
            var bucketStart = i * daysPerBucket;
            var bucketEnd = Math.Min((i + 1) * daysPerBucket, dailyData.Count);
            var slice = dailyData.Skip(bucketStart).Take(bucketEnd - bucketStart).ToList();
            if (slice.Count > 0)
            {
                buckets.Add(new DailyBalance
                {
                    Date = $"{slice[0].Date}",
                    Income = slice.Sum(s => s.Income),
                    Expense = slice.Sum(s => s.Expense)
                });
            }
        }

        // Pie chart data for this range (expense breakdown)
        var pieData = await _txn.GetTransactionsGroupedByCategoryAsync(start, now);

        return (buckets, pieData, $"{start:yyyy-MM-dd} ~ {now:yyyy-MM-dd}");
    }

    private async Task<string> GenerateReportForRangeAsync(DateTime start, DateTime end, string periodLabel, string reportType)
    {
        if (!IsLoggedIn)
        {
            var result = MessageBox.Show("AI 分析功能需要登录后才能使用。是否前往登录？",
                "需要登录", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (result == MessageBoxResult.Yes)
            {
                if (Application.Current.MainWindow?.DataContext is ViewModels.MainViewModel mainVm)
                    await mainVm.LoginCommand.ExecuteAsync(null);
            }
            return "";
        }
        RefreshStatus = "正在分析...";
        try
        {
            var data = await _txn.GetTransactionsAsync(start, end);
            var advice = await _ai.GetFinancialAdviceAsync(data, reportType);
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var win = new AiReportWindow("AI 分析报告", $"报告期间：{periodLabel}（{start:yyyy-MM-dd} ~ {end:yyyy-MM-dd}）", advice);
                win.Owner = Application.Current.MainWindow;
                win.ShowDialog();
            });
            return advice;
        }
        catch (Exception ex) { return $"分析失败：{ex.Message}"; }
        finally { RefreshStatus = ""; }
    }

    [RelayCommand]
    private async Task GenerateAiReport1MonthAsync()
    {
        var now = DateTime.Now;
        await GenerateReportForRangeAsync(now.AddMonths(-1), now, "最近一个月", "1month");
    }

    [RelayCommand]
    private async Task GenerateAiReport3MonthsAsync()
    {
        var now = DateTime.Now;
        await GenerateReportForRangeAsync(now.AddMonths(-3), now, "最近三个月", "3months");
    }

    [RelayCommand]
    private async Task GenerateAiReport6MonthsAsync()
    {
        var now = DateTime.Now;
        await GenerateReportForRangeAsync(now.AddMonths(-6), now, "最近半年", "6months");
    }
}
