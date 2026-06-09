using Microsoft.EntityFrameworkCore;
using PersonalFinanceAnalyzer.Server.Models;

namespace PersonalFinanceAnalyzer.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<ServerTransaction> Transactions => Set<ServerTransaction>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<ServerTransaction>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => new { t.UserId, t.TransactionDate });
        });

        modelBuilder.Entity<AiUsageLog>(e =>
        {
            e.HasIndex(l => new { l.UserId, l.Year, l.Month }).IsUnique();
        });
    }
}
