using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

public class ServerAiService : IAiService
{
    private readonly HttpClient _http;
    private readonly string _serverUrl;
    private readonly IAuthService _auth;

    public ServerAiService(IAuthService auth, IConfiguration config)
    {
        _auth = auth;
        _serverUrl = config["ServerUrl"] ?? "http://localhost:5000";
        _http = new HttpClient();
    }

    public async Task<string> GetFinancialAdviceAsync(List<Transaction> recentTransactions, string reportType = "1month")
    {
        if (!_auth.IsLoggedIn)
            return "AI 分析功能需要登录后才能使用。请先登录账户。";

        if (_auth.Token == null)
            return "登录状态异常，请重新登录。";

        try
        {
            _http.DefaultRequestHeaders.Remove("Authorization");
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_auth.Token}");

            var dtos = recentTransactions.Select(t => new
            {
                t.Amount, t.Type, t.CategoryId,
                t.TransactionDate, t.Note, t.CategoryName
            }).ToList();

            var response = await _http.PostAsJsonAsync(
                $"{_serverUrl}/api/ai/advice",
                new { Transactions = dtos, ReportType = reportType });

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                var errorResult = await response.Content.ReadFromJsonAsync<AiAdviceResponse>();
                return errorResult?.Message ?? "本月 AI 分析次数已用完。";
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AiAdviceResponse>();
            return result?.Advice ?? "未能获取分析结果。";
        }
        catch (Exception ex)
        {
            return $"AI 分析失败：{ex.Message}\n\n请检查服务器是否已启动。";
        }
    }

    public async Task<string?> SuggestCategoryAsync(string description)
    {
        if (!_auth.IsLoggedIn || _auth.Token == null) return null;

        try
        {
            _http.DefaultRequestHeaders.Remove("Authorization");
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_auth.Token}");

            var response = await _http.PostAsJsonAsync(
                $"{_serverUrl}/api/ai/classify",
                new { Description = description });

            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<ClassifyResponse>();
            return result?.Category;
        }
        catch
        {
            return null;
        }
    }

    private record AiAdviceResponse(bool Success, string? Advice, string? Message, int RemainingQuota);
    private record ClassifyResponse(string? Category);
}
