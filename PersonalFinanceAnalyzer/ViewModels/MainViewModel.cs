using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinanceAnalyzer.Models;
using PersonalFinanceAnalyzer.Services;

namespace PersonalFinanceAnalyzer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "个人收支趋势分析器";

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string _loginStatus = "未登录";

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string _syncStatus = "";

    [ObservableProperty]
    private string _lastSyncTime = "";

    public DashboardViewModel Dashboard { get; }
    public TransactionViewModel Transaction { get; }
    public AnalysisViewModel Analysis { get; }
    public BudgetViewModel Budget { get; }

    private readonly ICsvImportService _csvService;
    private readonly IAuthService _auth;
    private readonly ISyncService _sync;
    private readonly ITransactionService _txn;
    private readonly IExcelExportService _excel;
    private readonly IDailyViewService _daily;

    public MainViewModel(
        ITransactionService txn,
        IChartService chart,
        IAiService ai,
        ICsvImportService csvService,
        IAuthService auth,
        ISyncService sync,
        IDatabaseService db,
        IExcelExportService excel,
        IBudgetService budget,
        IDailyViewService daily)
    {
        Dashboard = new DashboardViewModel(txn);
        Transaction = new TransactionViewModel(txn, db, budget, ai);
        Analysis = new AnalysisViewModel(txn, chart, ai, auth, daily);
        _daily = daily;
        Budget = new BudgetViewModel(budget, db);
        _csvService = csvService;
        _auth = auth;
        _sync = sync;
        _txn = txn;
        _excel = excel;
        _isLoggedIn = auth.IsLoggedIn;

        auth.LoginStateChanged += (_, _) =>
        {
            IsLoggedIn = auth.IsLoggedIn;
            LoginStatus = auth.IsLoggedIn
                ? $"已登录：{auth.Username}"
                : "未登录";
            if (!auth.IsLoggedIn)
                SyncStatus = "";
        };
    }

    [RelayCommand]
    private async Task LoadAllAsync()
    {
        await Task.WhenAll(
            Dashboard.LoadAsync(),
            Transaction.LoadAsync(),
            Analysis.LoadAsync(),
            Budget.LoadAsync()
        );
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        var dialog = new Views.LoginWindow(_auth);
        dialog.Owner = Application.Current.MainWindow;
        dialog.ShowDialog();

        if (_auth.IsLoggedIn)
        {
            await SyncNowAsync();
            await LoadAllAsync();
        }
    }

    [RelayCommand]
    private void Logout()
    {
        _auth.Logout();
        _ = LoadAllAsync();
    }

    [RelayCommand]
    private void SwitchToTransactionTab()
    {
        SelectedTabIndex = 1; // 记录 Tab
    }

    [RelayCommand]
    private void Exit()
    {
        Application.Current.Shutdown();
    }

    [RelayCommand]
    private void ShowAbout()
    {
        var win = new Views.AboutWindow();
        win.Owner = Application.Current.MainWindow;
        win.ShowDialog();
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        if (!IsLoggedIn)
        {
            SyncStatus = "请先登录";
            return;
        }

        SyncStatus = "检查中...";
        LastSyncTime = "";

        try
        {
            // Step 1: Compare hash
            var hashResult = await _sync.CheckHashAsync();
            if (hashResult.IsMatch)
            {
                SyncStatus = "已是最新 ✓";
                LastSyncTime = $"上次同步：{DateTime.Now:HH:mm}";
                MessageBox.Show("本地与云端数据一致，无需同步。", "同步检查",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Step 2: Hash mismatch — show conflict window with three choices
            var conflicts = await FindConflictsAsync();
            var (mode, keepLocal, keepCloud) = await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var win = new Views.SyncConflictWindow(hashResult.LocalMaxUpdatedAt, hashResult.ServerMaxUpdatedAt, conflicts);
                win.Owner = Application.Current.MainWindow;
                win.ShowDialog();
                return (win.SelectedMode, win.KeepLocalIds, win.KeepCloudIds);
            });

            if (mode == Views.SyncConflictWindow.SyncModeChoice.KeepLocal)
            {
                // Upload local data to cloud
                var uploaded = await _sync.UploadLocalAsync();
                SyncStatus = $"已同步 ✓（上传 {uploaded} 条）";
            }
            else if (mode == Views.SyncConflictWindow.SyncModeChoice.KeepCloud)
            {
                // Download cloud data to local
                var downloaded = await _sync.DownloadCloudAsync();
                SyncStatus = $"已同步 ✓（下载 {downloaded} 条）";
            }
            else if (mode == Views.SyncConflictWindow.SyncModeChoice.PerItem && conflicts != null && conflicts.Count > 0)
            {
                // Per-item conflict resolution
                var localToUpload = conflicts
                    .Where(c => keepLocal.Contains(c.Local.Id)).Select(c => c.Local).ToList();
                var cloudToDownload = conflicts
                    .Where(c => keepCloud.Contains(c.Cloud.Id)).Select(c => c.Cloud).ToList();

                if (localToUpload.Count > 0)
                    await _sync.UploadLocalAsync();
                if (cloudToDownload.Count > 0)
                    await _sync.DownloadCloudAsync();
            }

            LastSyncTime = $"上次同步：{DateTime.Now:HH:mm}";
            await LoadAllAsync();
        }
        catch
        {
            SyncStatus = "同步失败";
            LastSyncTime = "";
        }
    }

    private async Task<List<(Transaction Local, Transaction Cloud)>?> FindConflictsAsync()
    {
        try
        {
            var localDict = (await _txn.GetTransactionsAsync()).ToDictionary(t => t.Id);
            var cloud = App.Services.GetRequiredService<ICloudDataService>();
            var cloudList = await cloud.DownloadAllAsync();
            var conflicts = new List<(Transaction Local, Transaction Cloud)>();
            foreach (var c in cloudList)
            {
                if (localDict.TryGetValue(c.Id, out var local))
                {
                    var diff = Math.Abs((local.UpdatedAt - c.UpdatedAt).TotalSeconds);
                    if (diff > 1)
                        conflicts.Add((local, c));
                }
            }
            return conflicts;
        }
        catch
        {
            return null;
        }
    }

    [RelayCommand]
    private async Task ExportToExcelAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel 文件 (*.xlsx)|*.xlsx",
            FileName = $"收支报表_{DateTime.Now:yyyyMMdd}.xlsx"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var transactions = await _txn.GetTransactionsAsync();
                await _excel.ExportTransactionsAsync(transactions, dialog.FileName);
                MessageBox.Show($"导出成功！\n共 {transactions.Count} 条记录\n{dialog.FileName}",
                    "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // 数据库文件路径（安装到 Program Files 后仍需可写）
    private static string DbPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PersonalFinanceAnalyzer");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "finance.db");
        }
    }

    [RelayCommand]
    private void BackupData()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "数据库文件 (*.db)|*.db",
            FileName = $"finance_backup_{DateTime.Now:yyyyMMdd_HHmm}.db"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                File.Copy(DbPath, dialog.FileName, overwrite: true);
                MessageBox.Show($"备份成功！\n{dialog.FileName}",
                    "备份完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"备份失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void RestoreData()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "数据库文件 (*.db)|*.db"
        };

        if (dialog.ShowDialog() == true)
        {
            var confirm = MessageBox.Show(
                "恢复备份会覆盖当前所有数据，确定继续？",
                "确认恢复",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    File.Copy(dialog.FileName, DbPath, overwrite: true);
                    MessageBox.Show("恢复完成，请重新启动程序。", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"恢复失败：{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    [RelayCommand]
    private async Task ImportCsvAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
            Title = "选择 CSV 文件导入"
        };

        if (dialog.ShowDialog() == true)
        {
            var result = await _csvService.ImportFromCsvAsync(dialog.FileName);
            var msg = $"从 {result.SourceType} 导入完成！\n成功：{result.SuccessCount} 条\n跳过：{result.SkipCount} 条";

            if (result.Errors.Count > 0)
            {
                msg += $"\n\n警告/错误（共 {result.Errors.Count} 条）：\n" +
                       string.Join("\n", result.Errors.Take(10));
                if (result.Errors.Count > 10)
                    msg += $"\n...及其他 {result.Errors.Count - 10} 条错误";
            }

            MessageBox.Show(msg, "导入结果",
                MessageBoxButton.OK,
                result.Errors.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            await LoadAllAsync();
        }
    }

    [RelayCommand]
    private async Task ImportWeChatBillAsync()
    {
        var guide = new Views.WeChatImportGuide(_csvService);
        guide.Owner = Application.Current.MainWindow;
        guide.ShowDialog();

        if (guide.ImportCompleted)
            await LoadAllAsync();
    }
}
