using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PersonalFinanceAnalyzer.Server.Data;
using PersonalFinanceAnalyzer.Server.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/finance-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("启动服务器...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

// ---- Database (supports SQLite and MySQL) ----
var dbProvider = builder.Configuration["DevDatabase:Provider"] ?? "SQLite";
var resetOnStartup = bool.Parse(builder.Configuration["DevDatabase:ResetOnStartup"] ?? "false");

if (dbProvider == "MySQL")
{
    var mySqlConn = builder.Configuration.GetConnectionString("MySQL")
        ?? throw new InvalidOperationException("MySQL 连接串未配置 (ConnectionStrings:MySQL)");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseMySQL(mySqlConn));
}
else
{
    var sqliteConn = builder.Configuration.GetConnectionString("Default")
        ?? "Data Source=finance_server.db";
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(sqliteConn));
}

// ---- JWT Authentication ----
var jwtKey = builder.Configuration["Jwt:SecretKey"] ?? "DefaultSuperSecretKeyAtLeast32CharsLong!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "PersonalFinanceAnalyzer",
            ValidAudience = builder.Configuration["Jwt:Issuer"] ?? "PersonalFinanceAnalyzer",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// ---- Rate Limiting ----
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetConcurrencyLimiter(
            partitionKey: "global",
            factory: _ => new ConcurrencyLimiterOptions { PermitLimit = 100, QueueLimit = 0 }));

    options.AddFixedWindowLimiter("LoginPolicy", config =>
    {
        config.PermitLimit = 10;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("RegisterPolicy", config =>
    {
        config.PermitLimit = 5;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("AiPolicy", config =>
    {
        config.PermitLimit = 30;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueLimit = 0;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ---- Services ----
builder.Services.AddSingleton<JwtService>();
builder.Services.AddScoped<QuotaService>();
builder.Services.AddHttpClient();

// ---- Controllers ----
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ---- CORS ----
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// ---- Database initialization ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (resetOnStartup && app.Environment.IsDevelopment())
    {
        Log.Information("[Dev] 开发模式：删除旧数据库并重建...");
        db.Database.EnsureDeleted();
        Log.Information("[Dev] 数据库已删除，正在重建...");
    }

    db.Database.EnsureCreated();

    // ---- Seed test data for Development mode ----
    if (app.Environment.IsDevelopment() && !await db.Users.AnyAsync(u => u.Username == "张三"))
    {
        Log.Information("[Dev] 导入测试数据...");

        var testUser = new PersonalFinanceAnalyzer.Server.Models.User
        {
            Username = "张三",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            IsMember = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(testUser);
        await db.SaveChangesAsync();

        // Add sample transactions for the test user
        // 覆盖 2026年3月~6月，跨Q1/Q2，共 20 条记录
        var sampleTransactions = new List<PersonalFinanceAnalyzer.Server.Models.ServerTransaction>
        {
            // ── 3月（Q1）─ 4条 ──
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-000000000001"), UserId = testUser.Id, Amount = 15000m, Type = "Income",  CategoryId = 6, TransactionDate = "2026-03-10", Note = "3月工资",   CreatedAt = new DateTime(2026, 3, 10, 10, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 3, 10, 10, 0, 0, DateTimeKind.Utc) },
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-000000000002"), UserId = testUser.Id, Amount = 3200m,  Type = "Expense", CategoryId = 1, TransactionDate = "2026-03-15", Note = "本月餐饮",   CreatedAt = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc) },
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-000000000003"), UserId = testUser.Id, Amount = 1200m,  Type = "Expense", CategoryId = 2, TransactionDate = "2026-03-18", Note = "换季衣服",   CreatedAt = new DateTime(2026, 3, 18, 14, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 3, 18, 14, 0, 0, DateTimeKind.Utc) },
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-000000000004"), UserId = testUser.Id, Amount = 850m,   Type = "Expense", CategoryId = 3, TransactionDate = "2026-03-22", Note = "地铁公交充值", CreatedAt = new DateTime(2026, 3, 22, 8, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 3, 22, 8, 0, 0, DateTimeKind.Utc) },

            // ── 4月（Q2）─ 5条 ──
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-000000000005"), UserId = testUser.Id, Amount = 15000m, Type = "Income",  CategoryId = 6, TransactionDate = "2026-04-10", Note = "4月工资",   CreatedAt = new DateTime(2026, 4, 10, 10, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 4, 10, 10, 0, 0, DateTimeKind.Utc) },
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-000000000006"), UserId = testUser.Id, Amount = 3000m,  Type = "Income",  CategoryId = 7, TransactionDate = "2026-04-15", Note = "周末兼职",   CreatedAt = new DateTime(2026, 4, 15, 9, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 4, 15, 9, 0, 0, DateTimeKind.Utc) },
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-000000000007"), UserId = testUser.Id, Amount = 3500m,  Type = "Expense", CategoryId = 1, TransactionDate = "2026-04-12", Note = "朋友聚餐+外卖", CreatedAt = new DateTime(2026, 4, 12, 18, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 4, 12, 18, 0, 0, DateTimeKind.Utc) },
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-000000000008"), UserId = testUser.Id, Amount = 1800m,  Type = "Expense", CategoryId = 2, TransactionDate = "2026-04-20", Note = "京东购物",   CreatedAt = new DateTime(2026, 4, 20, 15, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 4, 20, 15, 0, 0, DateTimeKind.Utc) },
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-000000000009"), UserId = testUser.Id, Amount = 600m,   Type = "Expense", CategoryId = 4, TransactionDate = "2026-04-25", Note = "看电影+唱K", CreatedAt = new DateTime(2026, 4, 25, 20, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 4, 25, 20, 0, 0, DateTimeKind.Utc) },

            // ── 5月（Q2）─ 5条 ──
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-00000000000a"), UserId = testUser.Id, Amount = 16000m, Type = "Income",  CategoryId = 6, TransactionDate = "2026-05-10", Note = "5月工资",         CreatedAt = new DateTime(2026, 5, 10, 10, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 5, 10, 10, 0, 0, DateTimeKind.Utc) },
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-00000000000b"), UserId = testUser.Id, Amount = 2000m,  Type = "Income",  CategoryId = 8, TransactionDate = "2026-05-20", Note = "基金收益",         CreatedAt = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc) },
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-00000000000c"), UserId = testUser.Id, Amount = 2800m,  Type = "Expense", CategoryId = 1, TransactionDate = "2026-05-08", Note = "五一聚餐+日常餐饮", CreatedAt = new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc) },
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-00000000000d"), UserId = testUser.Id, Amount = 1500m,  Type = "Expense", CategoryId = 2, TransactionDate = "2026-05-15", Note = "淘宝购物",         CreatedAt = new DateTime(2026, 5, 15, 16, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 5, 15, 16, 0, 0, DateTimeKind.Utc) },
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-00000000000e"), UserId = testUser.Id, Amount = 400m,   Type = "Expense", CategoryId = 5, TransactionDate = "2026-05-22", Note = "体检挂号",         CreatedAt = new DateTime(2026, 5, 22, 8, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 5, 22, 8, 0, 0, DateTimeKind.Utc) },

            // ── 6月（当前月，Q2）─ 6条 ──
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-00000000000f"), UserId = testUser.Id, Amount = 16000m, Type = "Income",  CategoryId = 6, TransactionDate = "2026-06-05", Note = "6月工资",         CreatedAt = new DateTime(2026, 6, 5, 10, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 6, 5, 10, 0, 0, DateTimeKind.Utc) },
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-000000000010"), UserId = testUser.Id, Amount = 2500m,  Type = "Expense", CategoryId = 1, TransactionDate = "2026-06-03", Note = "日常餐饮",         CreatedAt = new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Utc) },
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-000000000011"), UserId = testUser.Id, Amount = 3200m,  Type = "Expense", CategoryId = 2, TransactionDate = "2026-06-06", Note = "618 购物节",        CreatedAt = new DateTime(2026, 6, 6, 14, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 6, 6, 14, 0, 0, DateTimeKind.Utc) },
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-000000000012"), UserId = testUser.Id, Amount = 350m,   Type = "Expense", CategoryId = 3, TransactionDate = "2026-06-04", Note = "打车出行",         CreatedAt = new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 6, 4, 9, 0, 0, DateTimeKind.Utc) },
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-000000000013"), UserId = testUser.Id, Amount = 150m,   Type = "Expense", CategoryId = 4, TransactionDate = "2026-06-07", Note = "视频会员续费",     CreatedAt = new DateTime(2026, 6, 7, 18, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 6, 7, 18, 0, 0, DateTimeKind.Utc) },
            new() { Id = Guid.Parse("a0000001-0000-0000-0000-000000000014"), UserId = testUser.Id, Amount = 280m,   Type = "Expense", CategoryId = 5, TransactionDate = "2026-06-02", Note = "药店买药",         CreatedAt = new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc) },
        };
        db.Transactions.AddRange(sampleTransactions);
        await db.SaveChangesAsync();

        var totalIncome = sampleTransactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
        var totalExpense = sampleTransactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
        Log.Information($"[Dev] 测试用户「张三」已创建（密码: 123456，会员: 是）");
        Log.Information($"[Dev] 已导入 {sampleTransactions.Count} 条示例交易（收入 ¥{totalIncome}，支出 ¥{totalExpense}）");
    }

    if (resetOnStartup && app.Environment.IsDevelopment())
    {
        Log.Information("[Dev] 数据库重建完成！");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "服务器意外终止");
}
finally
{
    Log.CloseAndFlush();
}
