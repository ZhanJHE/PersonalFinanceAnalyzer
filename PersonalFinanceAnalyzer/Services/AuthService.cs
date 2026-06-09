using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace PersonalFinanceAnalyzer.Services;

public class AuthService : IAuthService
{
    private readonly HttpClient _http;
    private string _serverUrl;

    private static readonly string TokenFilePath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PersonalFinanceAnalyzer", "token.dat");

    public bool IsLoggedIn { get; private set; }
    public string? Token { get; private set; }
    public string? Username { get; private set; }
    public event EventHandler? LoginStateChanged;

    public AuthService(IConfiguration config)
    {
        _serverUrl = config["ServerUrl"] ?? "http://localhost:5000";
        _http = new HttpClient();

        // Restore session from disk
        LoadTokenFromDisk();
    }

    public async Task<(bool Success, string Message)> LoginAsync(string username, string password, string? serverUrl = null)
    {
        if (!string.IsNullOrEmpty(serverUrl))
            SetServerUrl(serverUrl);
        try
        {
            var response = await _http.PostAsJsonAsync(
                $"{_serverUrl}/api/auth/login",
                new { username, password });

            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (result?.Success == true && result.Token != null)
            {
                ApplyToken(result.Token, username);
                SaveTokenToDisk();
                return (true, result.Message ?? "登录成功。");
            }
            return (false, result?.Message ?? "登录失败。");
        }
        catch (Exception ex)
        {
            return (false, $"无法连接服务器：{ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> RegisterAsync(string username, string password, string? serverUrl = null)
    {
        if (!string.IsNullOrEmpty(serverUrl))
            SetServerUrl(serverUrl);
        try
        {
            var response = await _http.PostAsJsonAsync(
                $"{_serverUrl}/api/auth/register",
                new { username, password });

            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (result?.Success == true && result.Token != null)
            {
                ApplyToken(result.Token, username);
                SaveTokenToDisk();
                return (true, result.Message ?? "注册成功。");
            }
            return (false, result?.Message ?? "注册失败。");
        }
        catch (Exception ex)
        {
            return (false, $"无法连接服务器：{ex.Message}");
        }
    }

    public void Logout()
    {
        Token = null;
        Username = null;
        IsLoggedIn = false;
        _http.DefaultRequestHeaders.Remove("Authorization");
        ClearTokenFromDisk();
        LoginStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyToken(string token, string username)
    {
        Token = token;
        Username = username;
        IsLoggedIn = true;
        _http.DefaultRequestHeaders.Remove("Authorization");
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {Token}");
        LoginStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SaveTokenToDisk()
    {
        if (Token == null || Username == null) return;
        try
        {
            var dir = Path.GetDirectoryName(TokenFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes($"{Username}|{Token}"),
                null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(TokenFilePath, encrypted);
        }
        catch
        {
            // Token 持久化失败不影响主流程
        }
    }

    private void LoadTokenFromDisk()
    {
        if (!File.Exists(TokenFilePath)) return;
        try
        {
            var decrypted = ProtectedData.Unprotect(
                File.ReadAllBytes(TokenFilePath),
                null, DataProtectionScope.CurrentUser);
            var parts = Encoding.UTF8.GetString(decrypted).Split('|', 2);
            if (parts.Length == 2)
            {
                ApplyToken(parts[1], parts[0]);
            }
        }
        catch
        {
            // 解密失败（如换了用户账户），清除损坏文件
            try { File.Delete(TokenFilePath); } catch { }
        }
    }

    private void ClearTokenFromDisk()
    {
        try
        {
            if (File.Exists(TokenFilePath))
                File.Delete(TokenFilePath);
        }
        catch { }
    }

    public void SetServerUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        url = url.TrimEnd('/');
        if (_serverUrl == url) return;
        _serverUrl = url;

        // Persist to appsettings.json
        try
        {
            var appSettingsPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (System.IO.File.Exists(appSettingsPath))
            {
                var json = System.IO.File.ReadAllText(appSettingsPath);
                var doc = System.Text.Json.JsonDocument.Parse(json);
                using var stream = new System.IO.MemoryStream();
                using var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions { Indented = true });
                writer.WriteStartObject();
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Name == "ServerUrl")
                        writer.WriteString("ServerUrl", url);
                    else
                        prop.WriteTo(writer);
                }
                writer.WriteEndObject();
                writer.Flush();
                System.IO.File.WriteAllText(appSettingsPath,
                    System.Text.Encoding.UTF8.GetString(stream.ToArray()));
            }
        }
        catch { }
    }

    private record AuthResponse(bool Success, string? Token, string? Message);
}
