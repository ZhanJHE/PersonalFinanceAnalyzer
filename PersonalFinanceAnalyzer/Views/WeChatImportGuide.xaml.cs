using System.Windows;
using PersonalFinanceAnalyzer.Services;

namespace PersonalFinanceAnalyzer.Views;

public partial class WeChatImportGuide : Window
{
    private readonly ICsvImportService _csvService;
    public bool ImportCompleted { get; private set; }

    public WeChatImportGuide(ICsvImportService csvService)
    {
        InitializeComponent();
        _csvService = csvService;
    }

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "微信账单 (*.xlsx)|*.xlsx|所有文件 (*.*)|*.*",
            Title = "选择微信导出的账单文件"
        };

        if (dialog.ShowDialog() != true) return;

        ImportButton.IsEnabled = false;
        StatusText.Text = "正在导入，请稍候...";

        try
        {
            var result = await _csvService.ImportFromCsvAsync(dialog.FileName);

            var msg = $"从 {result.SourceType} 导入完成！\n" +
                      $"成功：{result.SuccessCount} 条";

            if (result.SkipCount > 0)
                msg += $"\n跳过：{result.SkipCount} 条";
            if (result.AutoMatchedCount.HasValue)
                msg += $"\n自动匹配类别：{result.AutoMatchedCount} 条";
            if (result.Errors.Count > 0)
                msg += $"\n\n警告/错误（{result.Errors.Count} 条）：\n" +
                       string.Join("\n", result.Errors.Take(5));
            if (result.Errors.Count > 5)
                msg += $"\n...及其他 {result.Errors.Count - 5} 条";

            MessageBox.Show(msg, "导入结果",
                MessageBoxButton.OK,
                result.Errors.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            ImportCompleted = true;
            StatusText.Text = $"已导入 {result.SuccessCount} 条记录";
            ImportButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导入失败：{ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            ImportButton.IsEnabled = true;
            StatusText.Text = "导入失败";
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = ImportCompleted;
        Close();
    }
}
