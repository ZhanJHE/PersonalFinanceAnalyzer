namespace PersonalFinanceAnalyzer.Models;

public class DailyBalance
{
    public string Date { get; set; } = "";       // yyyy-MM-dd
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public decimal Net => Income - Expense;
}
