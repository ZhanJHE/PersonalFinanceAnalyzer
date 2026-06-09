using System.Globalization;
using System.IO;
using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

/// <summary>
/// 微信账单 CSV 专用解析器。
/// 微信导出路径：我 → 服务 → 钱包 → 账单 → ... → 账单下载 → 用作个人对账
/// </summary>
public static class WeChatBillParser
{
    // 微信导出 CSV 的标准列头（可能出现顺序变化）
    private static readonly string[] RequiredHeaders = { "交易时间", "收/支", "金额" };

    /// <summary>
    /// 自动检测文件是否为微信导出的账单 CSV
    /// </summary>
    public static bool IsWeChatBill(string filePath)
    {
        try
        {
            var firstLine = File.ReadLines(filePath).FirstOrDefault() ?? string.Empty;
            return RequiredHeaders.All(h => firstLine.Contains(h));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 解析微信账单 CSV，返回交易列表和错误信息
    /// </summary>
    public static async Task<(List<Transaction> Transactions, List<string> Errors)> ParseAsync(
        string filePath, IDatabaseService db)
    {
        var transactions = new List<Transaction>();
        var errors = new List<string>();

        var categories = await db.GetCategoriesAsync();
        // 按类型分组，取第一个作为默认
        var defaultExpenseCategory = categories.FirstOrDefault(c => c.Type == "Expense");
        var defaultIncomeCategory = categories.FirstOrDefault(c => c.Type == "Income");

        var lines = await File.ReadAllLinesAsync(filePath);
        if (lines.Length < 2)
        {
            errors.Add("微信账单文件为空或只有表头。");
            return (transactions, errors);
        }

        // 解析表头 → 建立列索引映射
        var headers = SplitCsvLine(lines[0]);
        var colMap = BuildColumnMap(headers);
        if (colMap == null)
        {
            errors.Add("无法识别微信账单格式，请确认文件为「用作个人对账」导出的 CSV。");
            return (transactions, errors);
        }

        int matchedCount = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            try
            {
                var parts = SplitCsvLine(line);
                if (parts.Length < colMap.Count)
                {
                    errors.Add($"第 {i + 1} 行列数不足，跳过。");
                    continue;
                }

                // 日期
                var dateTimeStr = parts[colMap["交易时间"]].Trim();
                var dateStr = dateTimeStr.Length >= 10 ? dateTimeStr[..10] : dateTimeStr;
                if (!DateTime.TryParse(dateStr, out var date))
                {
                    errors.Add($"第 {i + 1} 行日期无效：{dateTimeStr}");
                    continue;
                }

                // 收/支类型
                var typeStr = parts[colMap["收/支"]].Trim();
                var isIncome = typeStr switch
                {
                    "收入" => true,
                    "支出" => false,
                    _ => throw new FormatException($"无法识别的收/支类型：{typeStr}")
                };
                var transactionType = isIncome ? "Income" : "Expense";

                // 金额
                var amountStr = parts[colMap["金额"]].Trim()
                    .Replace("¥", "").Replace("￥", "").Replace(",", "");
                if (!decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var rawAmount))
                {
                    errors.Add($"第 {i + 1} 行金额无效：{amountStr}");
                    continue;
                }
                var amount = Math.Abs(rawAmount);

                // 交易对方
                var counterparty = colMap.ContainsKey("交易对方") ? parts[colMap["交易对方"]].Trim() : string.Empty;

                // 备注（微信的备注在最后一列）
                var note = colMap.ContainsKey("备注") ? parts[colMap["备注"]].Trim() : string.Empty;
                if (!string.IsNullOrEmpty(counterparty) && string.IsNullOrEmpty(note))
                    note = counterparty;
                else if (!string.IsNullOrEmpty(counterparty) && !string.IsNullOrEmpty(note))
                    note = $"{counterparty} | {note}";

                // 类别匹配
                var category = TryMatchCategory(counterparty, transactionType, categories);
                if (category != null)
                    matchedCount++;

                var defaultCat = transactionType == "Expense" ? defaultExpenseCategory : defaultIncomeCategory;

                transactions.Add(new Transaction
                {
                    Amount = amount,
                    Type = transactionType,
                    CategoryId = category?.Id ?? defaultCat?.Id ?? 1,
                    TransactionDate = date.ToString("yyyy-MM-dd"),
                    Note = note,
                    CategoryName = category?.Name ?? defaultCat?.Name ?? "未分类"
                });
            }
            catch (Exception ex)
            {
                errors.Add($"第 {i + 1} 行处理异常：{ex.Message}");
            }
        }

        return (transactions, errors);
    }

    /// <summary>
    /// 根据交易对方名称尝试匹配已有类别
    /// </summary>
    private static Category? TryMatchCategory(string counterparty, string type, List<Category> categories)
    {
        if (string.IsNullOrEmpty(counterparty)) return null;

        var typeCategories = categories.Where(c => c.Type == type).ToList();

        // 精确匹配
        var exact = typeCategories.FirstOrDefault(c =>
            counterparty.Contains(c.Name, StringComparison.OrdinalIgnoreCase) ||
            c.Name.Contains(counterparty, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // 关键词匹配
        var keywordMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // 餐饮类关键词
            ["餐"] = "餐饮", ["饭"] = "餐饮", ["食"] = "餐饮", ["菜"] = "餐饮",
            ["外卖"] = "餐饮", ["餐厅"] = "餐饮", ["饭店"] = "餐饮", ["咖啡"] = "餐饮",
            ["茶"] = "餐饮", ["面包"] = "餐饮", ["蛋糕"] = "餐饮", ["美食"] = "餐饮",
            ["超市"] = "购物", ["商场"] = "购物", ["百货"] = "购物", ["商店"] = "购物",
            ["京东"] = "购物", ["淘宝"] = "购物", ["拼多多"] = "购物", ["天猫"] = "购物",
            ["快递"] = "购物", ["便利店"] = "购物",
            ["出行"] = "交通", ["地铁"] = "交通", ["公交"] = "交通", ["打车"] = "交通",
            ["加油"] = "交通", ["滴滴"] = "交通", ["火车"] = "交通", ["机票"] = "交通",
            ["电影"] = "娱乐", ["游戏"] = "娱乐", ["视频"] = "娱乐", ["音乐"] = "娱乐",
            ["健身"] = "娱乐", ["旅游"] = "娱乐", ["酒店"] = "娱乐",
            ["医院"] = "医疗", ["药店"] = "医疗", ["药房"] = "医疗", ["体检"] = "医疗",
        };

        foreach (var kv in keywordMap)
        {
            if (counterparty.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
            {
                var match = typeCategories.FirstOrDefault(c => c.Name == kv.Value);
                if (match != null) return match;
            }
        }

        return null;
    }

    /// <summary>
    /// 建立列名到索引的映射
    /// </summary>
    private static Dictionary<string, int>? BuildColumnMap(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            var h = headers[i].Trim();
            if (h == "交易时间") map["交易时间"] = i;
            else if (h == "收/支") map["收/支"] = i;
            else if (h == "金额(元)" || h == "金额") map["金额"] = i;
            else if (h == "交易对方") map["交易对方"] = i;
            else if (h == "备注") map["备注"] = i;
        }

        // 必须包含的列
        if (map.ContainsKey("交易时间") && map.ContainsKey("收/支") && map.ContainsKey("金额"))
            return map;
        return null;
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
