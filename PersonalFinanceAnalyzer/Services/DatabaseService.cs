using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

public class DatabaseService : IDatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string dbPath = "")
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

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Categories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                Type TEXT NOT NULL CHECK(Type IN ('Income', 'Expense')),
                Icon TEXT,
                Color TEXT DEFAULT '#9E9E9E'
            );

            CREATE TABLE IF NOT EXISTS Transactions (
                Id TEXT PRIMARY KEY,
                Amount REAL NOT NULL,
                Type TEXT NOT NULL CHECK(Type IN ('Income', 'Expense')),
                CategoryId INTEGER NOT NULL,
                TransactionDate TEXT NOT NULL,
                Note TEXT,
                CreatedAt TEXT DEFAULT (datetime('now')),
                UpdatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
            );

            CREATE INDEX IF NOT EXISTS idx_transactions_date ON Transactions(TransactionDate);
            CREATE INDEX IF NOT EXISTS idx_transactions_category ON Transactions(CategoryId);

            CREATE TABLE IF NOT EXISTS Budgets (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CategoryId INTEGER NOT NULL,
                MonthlyLimit REAL NOT NULL,
                Year INTEGER NOT NULL,
                Month INTEGER NOT NULL,
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
            );

            CREATE UNIQUE INDEX IF NOT EXISTS idx_budgets_cat_month ON Budgets(CategoryId, Year, Month);
        ";
        await cmd.ExecuteNonQueryAsync();

        // Create DailyView for chart aggregation
        try
        {
            cmd.CommandText = @"
                CREATE VIEW IF NOT EXISTS DailyView AS
                SELECT 
                    TransactionDate AS Date,
                    COALESCE(SUM(CASE WHEN Type = 'Income' THEN Amount ELSE 0 END), 0) AS Income,
                    COALESCE(SUM(CASE WHEN Type = 'Expense' THEN Amount ELSE 0 END), 0) AS Expense
                FROM Transactions
                GROUP BY TransactionDate
                ORDER BY TransactionDate
            ";
            await cmd.ExecuteNonQueryAsync();
        }
        catch { }

        // Migration: add Color column to Categories if missing (for old databases)
        try
        {
            cmd.CommandText = "ALTER TABLE Categories ADD COLUMN Color TEXT DEFAULT '#9E9E9E'";
            await cmd.ExecuteNonQueryAsync();
        }
        catch { }

        // Insert default categories + set colors
        cmd.CommandText = "SELECT COUNT(*) FROM Categories";
        var count = (long?)await cmd.ExecuteScalarAsync() ?? 0;
        if (count == 0)
        {
            cmd.CommandText = @"
                INSERT OR IGNORE INTO Categories (Name, Type, Color) VALUES
                ('餐饮', 'Expense', '#FF6384'),
                ('购物', 'Expense', '#36A2EB'),
                ('交通', 'Expense', '#FFCE56'),
                ('娱乐', 'Expense', '#4BC0C0'),
                ('医疗', 'Expense', '#9966FF'),
                ('工资', 'Income', '#FF9F40'),
                ('兼职', 'Income', '#C9CBCF'),
                ('理财收益', 'Income', '#5366FF'),
                ('其他', 'Expense', '#FF66FF'),
                ('备用', 'Expense', '#66CC99');
            ";
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            // Migration: set default colors for existing categories
            // (only runs for old databases that already have categories without colors)
            cmd.CommandText = @"
                UPDATE Categories SET Color = '#FF6384' WHERE Name = '餐饮' AND (Color IS NULL OR Color = '#9E9E9E');
                UPDATE Categories SET Color = '#36A2EB' WHERE Name = '购物' AND (Color IS NULL OR Color = '#9E9E9E');
                UPDATE Categories SET Color = '#FFCE56' WHERE Name = '交通' AND (Color IS NULL OR Color = '#9E9E9E');
                UPDATE Categories SET Color = '#4BC0C0' WHERE Name = '娱乐' AND (Color IS NULL OR Color = '#9E9E9E');
                UPDATE Categories SET Color = '#9966FF' WHERE Name = '医疗' AND (Color IS NULL OR Color = '#9E9E9E');
                UPDATE Categories SET Color = '#FF9F40' WHERE Name = '工资' AND (Color IS NULL OR Color = '#9E9E9E');
                UPDATE Categories SET Color = '#C9CBCF' WHERE Name = '兼职' AND (Color IS NULL OR Color = '#9E9E9E');
                UPDATE Categories SET Color = '#5366FF' WHERE Name = '理财收益' AND (Color IS NULL OR Color = '#9E9E9E');
            ";
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<List<Transaction>> GetTransactionsAsync(DateTime? start = null, DateTime? end = null)
    {
        var transactions = new List<Transaction>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        var sql = @"
            SELECT t.Id, t.Amount, t.Type, t.CategoryId, t.TransactionDate, t.Note, t.CreatedAt, t.UpdatedAt,
                   c.Name AS CategoryName, c.Icon AS CategoryIcon
            FROM Transactions t
            LEFT JOIN Categories c ON t.CategoryId = c.Id
            WHERE 1=1
        ";

        if (start.HasValue)
        {
            sql += " AND t.TransactionDate >= @start";
            cmd.Parameters.AddWithValue("@start", start.Value.ToString("yyyy-MM-dd"));
        }
        if (end.HasValue)
        {
            sql += " AND t.TransactionDate <= @end";
            cmd.Parameters.AddWithValue("@end", end.Value.ToString("yyyy-MM-dd"));
        }
        sql += " ORDER BY t.TransactionDate DESC, t.Id DESC";

        cmd.CommandText = sql;
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            transactions.Add(MapTransaction(reader));
        }
        return transactions;
    }

    public async Task AddTransactionAsync(Transaction transaction)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Transactions (Id, Amount, Type, CategoryId, TransactionDate, Note, UpdatedAt)
            VALUES (@id, @amount, @type, @categoryId, @date, @note, @updatedAt)
        ";
        cmd.Parameters.AddWithValue("@id", transaction.Id.ToString());
        cmd.Parameters.AddWithValue("@amount", transaction.Amount);
        cmd.Parameters.AddWithValue("@type", transaction.Type);
        cmd.Parameters.AddWithValue("@categoryId", transaction.CategoryId);
        cmd.Parameters.AddWithValue("@date", transaction.TransactionDate);
        cmd.Parameters.AddWithValue("@note", transaction.Note ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@updatedAt", transaction.UpdatedAt.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateTransactionAsync(Transaction transaction)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Transactions
            SET Amount = @amount, Type = @type, CategoryId = @categoryId,
                TransactionDate = @date, Note = @note, UpdatedAt = @updatedAt
            WHERE Id = @id
        ";
        cmd.Parameters.AddWithValue("@id", transaction.Id.ToString());
        cmd.Parameters.AddWithValue("@amount", transaction.Amount);
        cmd.Parameters.AddWithValue("@type", transaction.Type);
        cmd.Parameters.AddWithValue("@categoryId", transaction.CategoryId);
        cmd.Parameters.AddWithValue("@date", transaction.TransactionDate);
        cmd.Parameters.AddWithValue("@note", transaction.Note ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@updatedAt", transaction.UpdatedAt.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteTransactionAsync(Guid id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Transactions WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Category>> GetCategoriesAsync(string? type = null)
    {
        var categories = new List<Category>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        if (string.IsNullOrEmpty(type))
            cmd.CommandText = "SELECT * FROM Categories ORDER BY Type, Name";
        else
        {
            cmd.CommandText = "SELECT * FROM Categories WHERE Type = @type ORDER BY Name";
            cmd.Parameters.AddWithValue("@type", type);
        }

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            categories.Add(new Category
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Type = reader.GetString(2),
                Icon = reader.IsDBNull(3) ? null : reader.GetString(3),
                Color = reader.IsDBNull(4) ? "#9E9E9E" : reader.GetString(4)
            });
        }
        return categories;
    }

    public async Task<Category?> GetCategoryByIdAsync(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Categories WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Category
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Type = reader.GetString(2),
                Icon = reader.IsDBNull(3) ? null : reader.GetString(3),
                Color = reader.IsDBNull(4) ? "#9E9E9E" : reader.GetString(4)
            };
        }
        return null;
    }

    public async Task AddCategoryAsync(Category category)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Categories (Name, Type, Icon, Color) VALUES (@name, @type, @icon, @color)";
        cmd.Parameters.AddWithValue("@name", category.Name);
        cmd.Parameters.AddWithValue("@type", category.Type);
        cmd.Parameters.AddWithValue("@icon", category.Icon ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@color", category.Color ?? "#9E9E9E");
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteCategoryAsync(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Transactions WHERE CategoryId = @id";
        cmd.Parameters.AddWithValue("@id", id);
        var count = (long?)await cmd.ExecuteScalarAsync() ?? 0;
        if (count > 0)
            throw new InvalidOperationException("该类别下存在交易记录，无法删除。");

        cmd.CommandText = "DELETE FROM Categories WHERE Id = @id";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateCategoryColorAsync(int categoryId, string color)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Categories SET Color = @color WHERE Id = @id";
        cmd.Parameters.AddWithValue("@color", color);
        cmd.Parameters.AddWithValue("@id", categoryId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<decimal> GetTotalAsync(string type, DateTime start, DateTime end)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COALESCE(SUM(Amount), 0)
            FROM Transactions
            WHERE Type = @type AND TransactionDate >= @start AND TransactionDate <= @end
        ";
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd"));

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToDecimal(result ?? 0m);
    }

    public async Task<List<Transaction>> GetTransactionsGroupedByCategoryAsync(DateTime start, DateTime end, string type = "Expense")
    {
        var result = new List<Transaction>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT c.Name AS CategoryName, SUM(t.Amount) AS Amount, c.Id AS CategoryId
            FROM Transactions t
            JOIN Categories c ON t.CategoryId = c.Id
            WHERE t.Type = @type AND t.TransactionDate >= @start AND t.TransactionDate <= @end
            GROUP BY c.Id, c.Name
            ORDER BY Amount DESC
        ";
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd"));

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new Transaction
            {
                CategoryName = reader.GetString(0),
                Amount = reader.GetDecimal(1),
                CategoryId = reader.GetInt32(2)
            });
        }
        return result;
    }

    private static Transaction MapTransaction(SqliteDataReader reader)
    {
        return new Transaction
        {
            Id = Guid.Parse(reader.GetString(0)),
            Amount = reader.GetDecimal(1),
            Type = reader.GetString(2),
            CategoryId = reader.GetInt32(3),
            TransactionDate = reader.GetString(4),
            Note = reader.IsDBNull(5) ? null : reader.GetString(5),
            CreatedAt = reader.IsDBNull(6) ? null : reader.GetString(6),
            UpdatedAt = reader.IsDBNull(7) ? DateTime.MinValue : DateTime.Parse(reader.GetString(7)),
            CategoryName = reader.IsDBNull(8) ? null : reader.GetString(8),
            CategoryIcon = reader.IsDBNull(9) ? null : reader.GetString(9)
        };
    }

    public async Task CacheTransactionsAsync(List<Transaction> transactions)
    {
        // Upsert: insert or replace based on UpdatedAt
        foreach (var t in transactions)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Transactions (Id, Amount, Type, CategoryId, TransactionDate, Note, CreatedAt, UpdatedAt)
                VALUES (@id, @amount, @type, @categoryId, @date, @note, @createdAt, @updatedAt)
                ON CONFLICT(Id) DO UPDATE SET
                    Amount = excluded.Amount,
                    Type = excluded.Type,
                    CategoryId = excluded.CategoryId,
                    TransactionDate = excluded.TransactionDate,
                    Note = excluded.Note,
                    UpdatedAt = excluded.UpdatedAt
                    WHERE excluded.UpdatedAt > Transactions.UpdatedAt
            ";
            cmd.Parameters.AddWithValue("@id", t.Id.ToString());
            cmd.Parameters.AddWithValue("@amount", t.Amount);
            cmd.Parameters.AddWithValue("@type", t.Type);
            cmd.Parameters.AddWithValue("@categoryId", t.CategoryId);
            cmd.Parameters.AddWithValue("@date", t.TransactionDate);
            cmd.Parameters.AddWithValue("@note", t.Note ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@createdAt", t.CreatedAt ?? DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("@updatedAt", t.UpdatedAt.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<string> ComputeHashAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Amount, Type, CategoryId, TransactionDate, Note, UpdatedAt
            FROM Transactions
            ORDER BY Id
        ";

        var sb = new StringBuilder();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id = reader.GetString(0);
            var amount = reader.GetDecimal(1);
            var type = reader.GetString(2);
            var catId = reader.GetInt32(3);
            var date = reader.GetString(4);
            var note = reader.IsDBNull(5) ? "" : reader.GetString(5);
            var updatedAt = reader.GetString(6);
            sb.AppendLine($"{id}|{amount}|{type}|{catId}|{date}|{note}|{updatedAt}");
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexStringLower(bytes);
    }
}
