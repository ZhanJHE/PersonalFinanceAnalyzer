namespace PersonalFinanceAnalyzer.Tests;

/// <summary>
/// Tests for WeChat bill CSV parsing logic (inline, no external dependencies).
/// WeChat export format: 交易时间,交易类型,交易对方,商品,收/支,金额(元),支付方式,当前状态,交易单号,商户单号,备注
/// </summary>
public class WeChatBillParserTests
{
    private static string[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"') inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        result.Add(current.ToString());
        return result.ToArray();
    }

    [Fact]
    public void SplitCsvLine_NormalLine_ReturnsCorrectParts()
    {
        var line = "2026-06-01 12:00,商户消费,华润万家,食品,支出,58.50,零钱,已完成,4200001234,,买零食";
        var parts = SplitCsvLine(line);

        Assert.Equal(11, parts.Length);
        Assert.Equal("2026-06-01 12:00", parts[0]);
        Assert.Equal("支出", parts[4]);
        Assert.Equal("58.50", parts[5]);
        Assert.Equal("买零食", parts[10]);
    }

    [Fact]
    public void SplitCsvLine_WithQuotedField_HandlesCorrectly()
    {
        var line = "2026-06-01,\"美团外卖,红包\",食品,支出,25.00";
        var parts = SplitCsvLine(line);

        Assert.Equal(5, parts.Length);
        Assert.Equal("美团外卖,红包", parts[1]); // comma inside quotes preserved
    }

    [Fact]
    public void SplitCsvLine_IncomeLine_ParsesAmount()
    {
        var line = "2026-06-10 09:00,转账红包,公司,,收入,15000.00,银行转账,已完成,,,6月工资";
        var parts = SplitCsvLine(line);

        Assert.Equal("收入", parts[4]);
        Assert.Equal("15000.00", parts[5]);
    }

    [Fact]
    public void IsWeChatBill_DetectsCorrectHeader()
    {
        var header = "交易时间,交易类型,交易对方,商品,收/支,金额(元),支付方式,当前状态,交易单号,商户单号,备注";
        var result = header.Contains("交易时间") && header.Contains("收/支") && header.Contains("金额");
        Assert.True(result);
    }

    [Fact]
    public void IsWeChatBill_RejectsNonWeChatHeader()
    {
        var header = "日期,金额,类别,备注";
        var result = header.Contains("交易时间") && header.Contains("收/支") && header.Contains("金额");
        Assert.False(result);
    }

    [Fact]
    public void ParseAmount_Expense_PositiveAmount()
    {
        var line = "2026-06-01 12:00,商户消费,麦当劳,餐饮,支出,35.50,零钱,已完成";
        var parts = SplitCsvLine(line);
        var amountStr = parts[5].Replace("¥", "").Replace("￥", "").Replace(",", "");
        var amount = decimal.Parse(amountStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture);
        var isIncome = parts[4] == "收入";

        Assert.False(isIncome);
        Assert.Equal(35.50m, Math.Abs(amount));
    }
}
