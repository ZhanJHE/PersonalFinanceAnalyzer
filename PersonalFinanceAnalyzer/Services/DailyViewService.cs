using System.IO;
using Microsoft.Data.Sqlite;
using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

public class DailyViewService : IDailyViewService
{
    private readonly string _connectionString;

    public DailyViewService(string dbPath = "")
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

    public async Task<List<DailyBalance>> GetDailyBalancesAsync(DateTime start, DateTime end)
    {
        var result = new List<DailyBalance>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Date, Income, Expense FROM DailyView
            WHERE Date >= @start AND Date <= @end
            ORDER BY Date
        ";
        cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd"));

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new DailyBalance
            {
                Date = reader.GetString(0),
                Income = reader.GetDecimal(1),
                Expense = reader.GetDecimal(2)
            });
        }
        return result;
    }
}
