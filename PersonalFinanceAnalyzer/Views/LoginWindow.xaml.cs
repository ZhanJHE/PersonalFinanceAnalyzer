using System.IO;
using System.Windows;
using PersonalFinanceAnalyzer.Services;

namespace PersonalFinanceAnalyzer.Views;

public partial class LoginWindow : Window
{
    private readonly IAuthService _auth;

    public LoginWindow(IAuthService auth)
    {
        InitializeComponent();
        _auth = auth;

        // Load default server URL from appsettings.json
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        if (File.Exists(configPath))
        {
            var json = File.ReadAllText(configPath);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("ServerUrl", out var urlProp))
                ServerUrlBox.Text = urlProp.GetString() ?? "https://localhost:5001";
        }
        if (string.IsNullOrEmpty(ServerUrlBox.Text))
            ServerUrlBox.Text = "https://localhost:5001";
    }

    private async void OnLoginClick(object sender, RoutedEventArgs e)
    {
        var username = UsernameBox.Text.Trim();
        var password = PasswordBox.Password;
        var serverUrl = ServerUrlBox.Text.Trim();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ErrorText.Text = "请输入用户名和密码。";
            return;
        }

        if (string.IsNullOrEmpty(serverUrl))
        {
            ErrorText.Text = "请输入服务器地址。";
            return;
        }

        SetButtonsEnabled(false);
        ErrorText.Text = "正在登录...";

        var (success, message) = await _auth.LoginAsync(username, password, serverUrl);
        ErrorText.Text = message;

        if (success)
        {
            DialogResult = true;
            Close();
        }
        else
        {
            SetButtonsEnabled(true);
        }
    }

    private async void OnRegisterClick(object sender, RoutedEventArgs e)
    {
        var username = UsernameBox.Text.Trim();
        var password = PasswordBox.Password;
        var serverUrl = ServerUrlBox.Text.Trim();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ErrorText.Text = "请输入用户名和密码。";
            return;
        }

        if (password.Length < 6)
        {
            ErrorText.Text = "密码长度至少6位。";
            return;
        }

        if (string.IsNullOrEmpty(serverUrl))
        {
            ErrorText.Text = "请输入服务器地址。";
            return;
        }

        SetButtonsEnabled(false);
        ErrorText.Text = "正在注册...";

        var (success, message) = await _auth.RegisterAsync(username, password, serverUrl);
        ErrorText.Text = message;

        if (success)
        {
            DialogResult = true;
            Close();
        }
        else
        {
            SetButtonsEnabled(true);
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        LoginButton.IsEnabled = enabled;
        RegisterButton.IsEnabled = enabled;
        UsernameBox.IsEnabled = enabled;
        PasswordBox.IsEnabled = enabled;
        ServerUrlBox.IsEnabled = enabled;
    }
}
