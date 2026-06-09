namespace PersonalFinanceAnalyzer.Server.Models;

public class AiUsageLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int UsageCount { get; set; }
}
