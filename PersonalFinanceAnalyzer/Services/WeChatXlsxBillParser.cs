using System.Globalization;
using ClosedXML.Excel;
using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

/// <summary>
/// 微信 XLSX 账单专用解析器（微信最新导出格式）。
/// 微信导出路径：我 → 服务 → 钱包 → 账单 → ... → 账单下载 → 用作个人对账
/// </summary>
public static class WeChatXlsxBillParser
{
    /// <summary>
    /// 自动检测文件是否为微信导出的 XLSX 账单
    /// </summary>
    public static bool IsWeChatBill(string filePath)
    {
        try
        {
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheet(1);
            for (int r = 1; r <= 25; r++)
            {
                var rowText = "";
                for (int c = 1; c <= 11; c++)
                    rowText += (ws.Cell(r, c).GetString() ?? "").Trim() + " ";
                if (rowText.Contains("交易时间") && rowText.Contains("收/支"))
                    return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 解析微信 XLSX 账单，返回交易列表
    /// </summary>
    public static async Task<(List<Transaction> Transactions, List<string> Errors)> ParseAsync(
        string filePath, IDatabaseService db)
    {
        var result = await Task.Run(() => Parse(filePath, db));
        return result;
    }

    private static (List<Transaction> Transactions, List<string> Errors) Parse(
        string filePath, IDatabaseService db)
    {
        var transactions = new List<Transaction>();
        var errors = new List<string>();

        var categories = db.GetCategoriesAsync().Result;
        var defaultExpense = categories.FirstOrDefault(c => c.Type == "Expense");
        var defaultIncome = categories.FirstOrDefault(c => c.Type == "Income");

        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheet(1);
        int maxRow = ws.LastRowUsed()?.RowNumber() ?? 0;

        // 找到表头行
        int headerRow = 0;
        for (int r = 1; r <= 20; r++)
        {
            var rowText = "";
            for (int c = 1; c <= 11; c++)
                rowText += (ws.Cell(r, c).GetString() ?? "").Trim() + " ";
            if (rowText.Contains("交易时间") && rowText.Contains("收/支"))
            {
                headerRow = r;
                break;
            }
        }

        if (headerRow == 0)
        {
            errors.Add("无法识别微信账单格式。");
            return (transactions, errors);
        }

        // 建立列索引映射
        var colMap = new Dictionary<string, int>();
        for (int c = 1; c <= 11; c++)
        {
            var h = (ws.Cell(headerRow, c).GetString() ?? "").Trim();
            if (h == "交易时间") colMap["交易时间"] = c;
            else if (h == "收/支") colMap["收/支"] = c;
            else if (h == "金额(元)") colMap["金额"] = c;
            else if (h == "交易对方") colMap["交易对方"] = c;
            else if (h == "备注") colMap["备注"] = c;
        }

        if (!colMap.ContainsKey("交易时间") || !colMap.ContainsKey("收/支") || !colMap.ContainsKey("金额"))
        {
            errors.Add("无法识别微信账单格式：缺少必要列。");
            return (transactions, errors);
        }

        // 解析数据行
        for (int r = headerRow + 1; r <= maxRow; r++)
        {
            try
            {
                var dateStr = (ws.Cell(r, colMap["交易时间"]).GetString() ?? "").Trim();
                var typeStr = (ws.Cell(r, colMap["收/支"]).GetString() ?? "").Trim();
                var amountStr = (ws.Cell(r, colMap["金额"]).GetString() ?? "").Trim();

                if (string.IsNullOrEmpty(dateStr) || string.IsNullOrEmpty(typeStr))
                    continue;

                // 跳过非收支行（如"中性交易"）
                if (typeStr != "收入" && typeStr != "支出")
                    continue;

                // 日期
                if (!DateTime.TryParse(dateStr, out var date))
                {
                    errors.Add($"第 {r} 行日期无效：{dateStr}");
                    continue;
                }

                // 收支类型
                var isIncome = typeStr == "收入";
                var transactionType = isIncome ? "Income" : "Expense";

                // 金额
                amountStr = amountStr.Replace("¥", "").Replace("￥", "").Replace(",", "");
                if (!decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                {
                    errors.Add($"第 {r} 行金额无效：{amountStr}");
                    continue;
                }
                amount = Math.Abs(amount);

                // 交易对方
                var counterparty = colMap.ContainsKey("交易对方")
                    ? (ws.Cell(r, colMap["交易对方"]).GetString() ?? "").Trim()
                    : "";

                // 备注
                var note = colMap.ContainsKey("备注")
                    ? (ws.Cell(r, colMap["备注"]).GetString() ?? "").Trim()
                    : "";
                if (!string.IsNullOrEmpty(counterparty) && string.IsNullOrEmpty(note))
                    note = counterparty;
                else if (!string.IsNullOrEmpty(counterparty) && !string.IsNullOrEmpty(note))
                    note = $"{counterparty} | {note}";

                // 类别匹配
                var category = TryMatchCategory(counterparty, transactionType, categories);
                var defaultCat = transactionType == "Expense" ? defaultExpense : defaultIncome;

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
                errors.Add($"第 {r} 行处理异常：{ex.Message}");
            }
        }

        return (transactions, errors);
    }

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
            ["餐"] = "餐饮", ["饭"] = "餐饮", ["食"] = "餐饮", ["菜"] = "餐饮",
            ["外卖"] = "餐饮", ["餐厅"] = "餐饮", ["咖啡"] = "餐饮", ["美食"] = "餐饮",
            ["超市"] = "购物", ["商场"] = "购物", ["京东"] = "购物", ["淘宝"] = "购物",
            ["拼多多"] = "购物", ["天猫"] = "购物", ["快递"] = "购物", ["便利店"] = "购物",
            ["地铁"] = "交通", ["公交"] = "交通", ["打车"] = "交通", ["加油"] = "交通",
            ["滴滴"] = "交通", ["火车"] = "交通", ["出行"] = "交通",
            ["电影"] = "娱乐", ["游戏"] = "娱乐", ["视频"] = "娱乐", ["音乐"] = "娱乐",
            ["健身"] = "娱乐", ["旅游"] = "娱乐",
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
}
