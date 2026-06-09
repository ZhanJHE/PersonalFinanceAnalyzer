namespace PersonalFinanceAnalyzer.Services;

public interface IAuthService
{
    bool IsLoggedIn { get; }
    string? Token { get; }
    string? Username { get; }
    event EventHandler? LoginStateChanged;

    Task<(bool Success, string Message)> LoginAsync(string username, string password, string? serverUrl = null);
    Task<(bool Success, string Message)> RegisterAsync(string username, string password, string? serverUrl = null);
    void Logout();
    void SetServerUrl(string url);
}
