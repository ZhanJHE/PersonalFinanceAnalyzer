using System.IO;
using Microsoft.Data.Sqlite;
using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

public class BudgetService : IBudgetService
{
    private readonly string _connectionString;

    public BudgetService(string dbPath = "")
    {
        if (string.IsNullOrEmpty(dbPath))
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PersonalFinanceAnalyzer");
            Directory.CreateDirectory(dir);
            dbPath = Path.Combine(dir, "finance.db");
        }
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task<List<Budget>> GetBudgetsAsync(int year, int month)
    {
        var budgets = new List<Budget>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT b.Id, b.CategoryId, b.MonthlyLimit, b.Year, b.Month,
                   c.Name AS CategoryName,
                   COALESCE(SUM(t.Amount), 0) AS Spent
            FROM Budgets b
            LEFT JOIN Categories c ON b.CategoryId = c.Id
            LEFT JOIN Transactions t ON t.CategoryId = b.CategoryId
                AND t.Type = 'Expense'
                AND t.TransactionDate >= @monthStart
                AND t.TransactionDate <= @monthEnd
            WHERE b.Year = @year AND b.Month = @month
            GROUP BY b.Id
            ORDER BY Spent DESC
        ";
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        cmd.Parameters.AddWithValue("@year", year);
        cmd.Parameters.AddWithValue("@month", month);
        cmd.Parameters.AddWithValue("@monthStart", monthStart.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@monthEnd", monthEnd.ToString("yyyy-MM-dd"));

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            budgets.Add(new Budget
            {
                Id = reader.GetInt32(0),
                CategoryId = reader.GetInt32(1),
                MonthlyLimit = reader.GetDecimal(2),
                Year = reader.GetInt32(3),
                Month = reader.GetInt32(4),
                CategoryName = reader.IsDBNull(5) ? null : reader.GetString(5),
                Spent = reader.GetDecimal(6)
            });
        }
        return budgets;
    }

    public async Task SetBudgetAsync(int categoryId, decimal monthlyLimit, int year, int month)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Upsert: insert or update if exists for same category+month
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Budgets (CategoryId, MonthlyLimit, Year, Month)
            VALUES (@catId, @limit, @year, @month)
            ON CONFLICT(CategoryId, Year, Month) DO UPDATE SET MonthlyLimit = @limit
        ";
        cmd.Parameters.AddWithValue("@catId", categoryId);
        cmd.Parameters.AddWithValue("@limit", monthlyLimit);
        cmd.Parameters.AddWithValue("@year", year);
        cmd.Parameters.AddWithValue("@month", month);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteBudgetAsync(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Budgets WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<Budget?> GetBudgetForCategoryAsync(int categoryId, int year, int month)
    {
        var budgets = await GetBudgetsAsync(year, month);
        return budgets.FirstOrDefault(b => b.CategoryId == categoryId);
    }

    public async Task CheckAndWarnOverBudgetAsync(int categoryId, int year, int month)
    {
        var budget = await GetBudgetForCategoryAsync(categoryId, year, month);
        if (budget == null) return;

        // Recalculate spent
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COALESCE(SUM(Amount), 0) FROM Transactions
            WHERE CategoryId = @catId AND Type = 'Expense'
                AND TransactionDate >= @start AND TransactionDate <= @end
        ";
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        cmd.Parameters.AddWithValue("@catId", categoryId);
        cmd.Parameters.AddWithValue("@start", monthStart.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@end", monthEnd.ToString("yyyy-MM-dd"));

        var spent = Convert.ToDecimal(await cmd.ExecuteScalarAsync() ?? 0m);
        if (spent > budget.MonthlyLimit)
        {
            System.Windows.MessageBox.Show(
                $"⚠ 超预算提醒！\n\n类别「{budget.CategoryName}」本月已花费 ¥{spent:N2}，\n" +
                $"预算上限 ¥{budget.MonthlyLimit:N2}，超出 ¥{spent - budget.MonthlyLimit:N2}。",
                "预算超限", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }
}
