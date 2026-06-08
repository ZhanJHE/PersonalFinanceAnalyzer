namespace PersonalFinanceAnalyzer.Models;

public class Budget
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public decimal MonthlyLimit { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }

    // Display properties (populated by service)
    public string? CategoryName { get; set; }
    public decimal Spent { get; set; }
    public decimal Remaining => MonthlyLimit - Spent;
    public double Percentage => MonthlyLimit > 0 ? Math.Round((double)(Spent / MonthlyLimit) * 100, 1) : 0;
    public bool IsOverBudget => Spent > MonthlyLimit;
}
