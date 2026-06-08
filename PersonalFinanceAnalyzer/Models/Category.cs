namespace PersonalFinanceAnalyzer.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;   // "Income" or "Expense"
    public string? Icon { get; set; }
    public string Color { get; set; } = "#9E9E9E";     // 默认灰色
}
