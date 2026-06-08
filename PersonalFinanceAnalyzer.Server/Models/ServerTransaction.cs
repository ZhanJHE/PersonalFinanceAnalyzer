namespace PersonalFinanceAnalyzer.Server.Models;

public class ServerTransaction
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;  // Income / Expense
    public int CategoryId { get; set; }
    public string TransactionDate { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
