using System.Globalization;
using System.IO;
using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

public class CsvImportService : ICsvImportService
{
    private readonly IDatabaseService _db;

    public CsvImportService(IDatabaseService db)
    {
        _db = db;
    }

    // ── 主入口：自动检测文件类型 ──

    public async Task<ImportResult> ImportFromCsvAsync(string filePath)
    {
        if (filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) && WeChatXlsxBillParser.IsWeChatBill(filePath))
        {
            return await ImportWeChatXlsxAsync(filePath);
        }
        if (WeChatBillParser.IsWeChatBill(filePath))
        {
            return await ImportWeChatCsvAsync(filePath);
        }
        return await ImportGenericCsvAsync(filePath);
    }

    public async Task<(List<Transaction> Transactions, List<string> Errors)> ParseCsvAsync(string filePath)
    {
        if (filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) && WeChatXlsxBillParser.IsWeChatBill(filePath))
        {
            return await WeChatXlsxBillParser.ParseAsync(filePath, _db);
        }
        if (WeChatBillParser.IsWeChatBill(filePath))
        {
            return await WeChatBillParser.ParseAsync(filePath, _db);
        }
        return await ParseGenericCsvAsync(filePath);
    }

    // ── 通用 CSV 解析（原有逻辑，提取为独立方法） ──

    public async Task<(List<Transaction> Transactions, List<string> Errors)> ParseGenericCsvAsync(string filePath)
    {
        var transactions = new List<Transaction>();
        var errors = new List<string>();
        var categories = await _db.GetCategoriesAsync();
        var categoryLookup = categories.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

        var lines = await File.ReadAllLinesAsync(filePath);
        if (lines.Length == 0)
        {
            errors.Add("文件为空。");
            return (transactions, errors);
        }

        int startLine = 0;
        var firstLine = lines[0].Trim().ToLowerInvariant();
        if (firstLine.Contains("日期") || firstLine.Contains("date") || firstLine.Contains("amount"))
        {
            startLine = 1;
        }

        for (int i = startLine; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            try
            {
                var parts = SplitCsvLine(line);
                if (parts.Length < 3)
                {
                    errors.Add($"第 {i + 1} 行格式错误：至少需要 日期,金额,类别 三列。");
                    continue;
                }

                var dateStr = parts[0].Trim();
                var amountStr = parts[1].Trim();
                var categoryName = parts[2].Trim();
                var note = parts.Length > 3 ? parts[3].Trim() : null;

                if (!DateTime.TryParse(dateStr, out var date))
                {
                    errors.Add($"第 {i + 1} 行日期格式无效：{dateStr}");
                    continue;
                }

                if (!decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                {
                    errors.Add($"第 {i + 1} 行金额格式无效：{amountStr}");
                    continue;
                }

                if (!categoryLookup.TryGetValue(categoryName, out var category))
                {
                    errors.Add($"第 {i + 1} 行类别不存在：{categoryName}");
                    continue;
                }

                transactions.Add(new Transaction
                {
                    Amount = Math.Abs(amount),
                    Type = category.Type,
                    CategoryId = category.Id,
                    TransactionDate = date.ToString("yyyy-MM-dd"),
                    Note = note
                });
            }
            catch (Exception ex)
            {
                errors.Add($"第 {i + 1} 行处理异常：{ex.Message}");
            }
        }

        return (transactions, errors);
    }

    private async Task<ImportResult> ImportGenericCsvAsync(string filePath)
    {
        var result = new ImportResult { SourceType = "通用CSV" };
        var (transactions, errors) = await ParseGenericCsvAsync(filePath);
        result.Errors = errors;

        foreach (var t in transactions)
        {
            try { await _db.AddTransactionAsync(t); result.SuccessCount++; }
            catch (Exception ex) { result.Errors.Add($"导入失败：{ex.Message}"); }
        }

        result.SkipCount = transactions.Count - result.SuccessCount;
        return result;
    }

    private async Task<ImportResult> ImportWeChatCsvAsync(string filePath)
    {
        var result = new ImportResult { SourceType = "微信账单" };
        var (transactions, errors) = await WeChatBillParser.ParseAsync(filePath, _db);
        result.Errors = errors;
        result.ImportedCount = transactions.Count;

        foreach (var t in transactions)
        {
            try { await _db.AddTransactionAsync(t); result.SuccessCount++; }
            catch (Exception ex) { result.Errors.Add($"导入失败（{t.TransactionDate} {t.CategoryName}）：{ex.Message}"); }
        }

        result.SkipCount = transactions.Count - result.SuccessCount;
        return result;
    }

    private async Task<ImportResult> ImportWeChatXlsxAsync(string filePath)
    {
        var result = new ImportResult { SourceType = "微信账单(XLSX)" };
        var (transactions, errors) = await WeChatXlsxBillParser.ParseAsync(filePath, _db);
        result.Errors = errors;
        result.ImportedCount = transactions.Count;

        foreach (var t in transactions)
        {
            try { await _db.AddTransactionAsync(t); result.SuccessCount++; }
            catch (Exception ex) { result.Errors.Add($"导入失败（{t.TransactionDate} {t.CategoryName}）：{ex.Message}"); }
        }

        result.SkipCount = transactions.Count - result.SuccessCount;
        return result;
    }

    private static string[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result.ToArray();
    }
}
