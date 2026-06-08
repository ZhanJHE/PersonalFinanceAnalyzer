using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PersonalFinanceAnalyzer.Server.Services;

namespace PersonalFinanceAnalyzer.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("AiPolicy")]
public class AiController : ControllerBase
{
    private readonly QuotaService _quota;
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public AiController(QuotaService quota, HttpClient http, IConfiguration config)
    {
        _quota = quota;
        _http = http;
        _config = config;
    }

    public record AiAdviceRequest(
        List<TransactionDto> Transactions,
        string? ReportType        // "1month", "3months", "6months"
    );

    public record TransactionDto(
        decimal Amount, string Type, int CategoryId,
        string TransactionDate, string? Note, string? CategoryName
    );

    public record AiAdviceResponse(bool Success, string? Advice, string? Message, int RemainingQuota);

    [HttpPost("advice")]
    public async Task<ActionResult<AiAdviceResponse>> GetAdvice([FromBody] AiAdviceRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        // Check quota
        var (allowed, remaining) = await _quota.CheckQuotaAsync(userId);
        if (!allowed)
            return StatusCode(403, new AiAdviceResponse(false, null, $"本月 AI 分析次数已用完（剩余 {remaining} 次）。", remaining));

        try
        {
            // Build prompt
            var prompt = BuildPrompt(request.Transactions);

            // Call DeepSeek API
            var apiKey = _config["AiApi:ApiKey"] ?? string.Empty;
            var baseUrl = _config["AiApi:BaseUrl"] ?? "https://gateway.ai.vercel.app/v1/chat/completions";
            var model = _config["AiApi:Model"] ?? "deepseek/deepseek-chat";

            var promptTemplate = request.ReportType switch
            {
                "1month" => "你是一位专业的财务分析助手。请根据用户过去1个月的收支数据，生成一份**月度财务总结报告**，包含：\n1. 本月收支概况（总收入、总支出、结余）\n2. 支出结构分析（各分类占比）\n3. 具体可执行的省钱建议（至少3条）\n回答请使用中文，控制在300字以内。",
                "3months" => "你是一位专业的财务分析助手。请根据用户过去3个月的收支数据，生成一份**季度财务分析报告**，包含：\n1. 季度收支概况（总收入、总支出、月均结余）\n2. 支出趋势分析（各月对比，找出支出波动较大的月份）\n3. 具体可执行的省钱建议（至少3条）\n回答请使用中文，控制在300字以内。",
                "6months" => "你是一位专业的财务分析助手。请根据用户过去半年的收支数据，生成一份**半年财务总结报告**，包含：\n1. 半年收支概况（总收入、总支出、月均结余）\n2. 长期消费趋势分析（逐月对比，指出整体变化趋势）\n3. 具体可执行的省钱建议（至少3条）\n回答请使用中文，控制在300字以内。",
                _ => "你是一位专业的财务分析助手。请根据用户近期的收支数据，给出简洁、实用的理财建议。重点分析消费结构是否合理，是否存在异常大额支出，以及如何优化消费习惯。回答请使用中文，控制在200字以内。"
            };

            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = promptTemplate },
                    new { role = "user", content = prompt }
                },
                temperature = 0.7,
                max_tokens = 500
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            _http.DefaultRequestHeaders.Remove("Authorization");
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var response = await _http.PostAsync(baseUrl, content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var advice = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            // Record usage
            await _quota.RecordUsageAsync(userId);

            return Ok(new AiAdviceResponse(true, advice ?? "未能获取分析结果。", null, remaining - 1));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new AiAdviceResponse(false, null, $"AI 分析失败：{ex.Message}", 0));
        }
    }

    public record ClassifyRequest(string Description);

    public record ClassifyResponse(string? Category);

    /// <summary>
    /// POST /api/ai/classify — AI 自动识别交易描述所属类别
    /// </summary>
    [HttpPost("classify")]
    public async Task<ActionResult<ClassifyResponse>> ClassifyCategory([FromBody] ClassifyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new ClassifyResponse(null));

        try
        {
            var apiKey = _config["AiApi:ApiKey"] ?? string.Empty;
            var baseUrl = _config["AiApi:BaseUrl"] ?? "https://gateway.ai.vercel.app/v1/chat/completions";
            var model = _config["AiApi:Model"] ?? "deepseek/deepseek-chat";

            // 可用类别列表（与客户端预置类别一致）
            var categories = new[] { "餐饮", "购物", "交通", "娱乐", "医疗" };
            var categoryList = string.Join("、", categories);

            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = $"你是一个分类助手。根据用户的交易描述，从以下类别中选出最匹配的一个，只返回类别名称，不要多余文字。\n可用类别：{categoryList}"
                    },
                    new { role = "user", content = $"交易描述：{request.Description}" }
                },
                temperature = 0.3,
                max_tokens = 50
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            _http.DefaultRequestHeaders.Remove("Authorization");
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var response = await _http.PostAsync(baseUrl, content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var result = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()?.Trim();

            return Ok(new ClassifyResponse(result));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ClassifyResponse(null));
        }
    }

    private static string BuildPrompt(List<TransactionDto> transactions)
    {
        if (transactions.Count == 0)
            return "当前没有最近的收支记录。";

        var totalIncome = transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
        var totalExpense = transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
        var balance = totalIncome - totalExpense;

        var expenseByCategory = transactions
            .Where(t => t.Type == "Expense")
            .GroupBy(t => t.CategoryName ?? "未知")
            .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
            .OrderByDescending(x => x.Total)
            .ToList();

        var categorySummary = string.Join("；", expenseByCategory.Select(c => $"{c.Category} ¥{c.Total:N2}"));

        var startDate = transactions.Min(t => t.TransactionDate);
        var endDate = transactions.Max(t => t.TransactionDate);

        return $"""
            近期收支数据概览：
            - 统计时间段：{startDate} 至 {endDate}
            - 总收入：¥{totalIncome:N2}
            - 总支出：¥{totalExpense:N2}
            - 结余：¥{balance:N2}
            - 支出分类：{categorySummary}

            请分析以上数据并给出理财建议。
            """;
    }
}
