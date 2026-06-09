using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinanceAnalyzer.Services;
using PersonalFinanceAnalyzer.ViewModels;
using PersonalFinanceAnalyzer.Views;

namespace PersonalFinanceAnalyzer;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"发生未处理的异常：{args.Exception.Message}\n\n程序可继续运行，但建议重新启动。",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            // Initialize local database
            var db = Services.GetRequiredService<IDatabaseService>();
            await db.InitializeAsync();

            // Load category colors for chart and DataGrid
            var allCats = await db.GetCategoriesAsync();
            foreach (var cat in allCats)
                Helpers.CategoryColorConverter.ColorMap[cat.Name] = cat.Color;

            var mainWindow = new MainWindow();
            var mainVm = Services.GetRequiredService<MainViewModel>();
            mainWindow.DataContext = mainVm;
            mainWindow.Show();

            // Load initial data
            await mainVm.LoadAllCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"启动失败：{ex.Message}\n\n{ex.StackTrace}",
                "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        services.AddSingleton<IConfiguration>(config);

        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<ICloudDataService, CloudDataService>();
        services.AddSingleton<ITransactionService, TransactionService>();
        services.AddSingleton<ISyncService, SyncService>();
        services.AddSingleton<IBudgetService, BudgetService>();
        services.AddSingleton<IExcelExportService, ExcelExportService>();
        services.AddSingleton<IChartService, ChartService>();
        services.AddSingleton<ICategoryService, CategoryService>();
        services.AddSingleton<IAiService, ServerAiService>();
        services.AddTransient<ICsvImportService, CsvImportService>();
        services.AddSingleton<IDailyViewService, DailyViewService>();

        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<TransactionViewModel>();
        services.AddTransient<AnalysisViewModel>();
        services.AddTransient<BudgetViewModel>();
    }
}
