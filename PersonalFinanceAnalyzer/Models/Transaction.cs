namespace PersonalFinanceAnalyzer.Models;

public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;   // "Income" or "Expense"
    public int CategoryId { get; set; }
    public string TransactionDate { get; set; } = string.Empty; // yyyy-MM-dd
    public string? Note { get; set; }
    public string? CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property for display
    public string? CategoryName { get; set; }
    public string? CategoryIcon { get; set; }
}
