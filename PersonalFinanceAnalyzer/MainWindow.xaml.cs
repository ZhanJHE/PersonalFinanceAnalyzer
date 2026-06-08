using System.ComponentModel;
using System.Windows;
using PersonalFinanceAnalyzer.Services;
using PersonalFinanceAnalyzer.ViewModels;

namespace PersonalFinanceAnalyzer;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.IsLoggedIn)
        {
            var result = MessageBox.Show("是否在退出前同步数据到云端？",
                "同步数据", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                e.Cancel = true; // 先阻止关闭
                try
                {
                    await vm.SyncNowCommand.ExecuteAsync(null);
                }
                catch { }
                Application.Current.Shutdown(); // 再关
                return;
            }

            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true; // 取消关闭
                return;
            }
        }
        // No or Cancel → 直接关
        Application.Current.Shutdown();
    }
}
