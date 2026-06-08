namespace PersonalFinanceAnalyzer.Server.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsMember { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
