using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace PersonalFinanceAnalyzer.Views;

public partial class ServerConfigWindow : Window
{
    private readonly HttpClient _http = new();
    public bool IsConnected { get; private set; }
    public string ServerUrl => ServerUrlBox.Text.Trim();

    public ServerConfigWindow(string currentUrl)
    {
        InitializeComponent();
        ServerUrlBox.Text = currentUrl;
        _http.Timeout = TimeSpan.FromSeconds(5);
    }

    private async void OnRetryClick(object sender, RoutedEventArgs e)
    {
        var url = ServerUrl.TrimEnd('/');
        if (string.IsNullOrEmpty(url))
        {
            StatusText.Text = "请输入服务器地址。";
            return;
        }

        RetryButton.IsEnabled = false;
        StatusText.Text = "正在连接...";

        try
        {
            // Try a simple GET to check if server is alive
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _http.GetAsync($"{url}/api/auth/login", cts.Token);

            // Any response (even 401/400) means the server is alive
            IsConnected = true;
            StatusText.Text = "✅ 连接成功！";
            StatusText.Foreground = System.Windows.Media.Brushes.Green;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"❌ 连接失败：{ex.Message}";
            StatusText.Foreground = System.Windows.Media.Brushes.Red;
        }
        finally
        {
            RetryButton.IsEnabled = true;
        }
    }

    private void OnSkipClick(object sender, RoutedEventArgs e)
    {
        IsConnected = false;
        DialogResult = true;
        Close();
    }
}
