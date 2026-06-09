using PersonalFinanceAnalyzer.Server.Data;

namespace PersonalFinanceAnalyzer.Server.Services;

public class QuotaService
{
    private readonly AppDbContext _db;

    public QuotaService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Allowed, int Remaining)> CheckQuotaAsync(int userId)
    {
        var now = DateTime.UtcNow;
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, 0);

        var maxQuota = user.IsMember ? 100 : 10;

        var usage = _db.AiUsageLogs
            .Where(l => l.UserId == userId && l.Year == now.Year && l.Month == now.Month)
            .Select(l => (int?)l.UsageCount)
            .FirstOrDefault() ?? 0;

        var remaining = maxQuota - usage;
        return (usage < maxQuota, Math.Max(0, remaining));
    }

    public async Task RecordUsageAsync(int userId)
    {
        var now = DateTime.UtcNow;
        var log = _db.AiUsageLogs
            .FirstOrDefault(l => l.UserId == userId && l.Year == now.Year && l.Month == now.Month);

        if (log == null)
        {
            log = new Models.AiUsageLog
            {
                UserId = userId,
                Year = now.Year,
                Month = now.Month,
                UsageCount = 1
            };
            _db.AiUsageLogs.Add(log);
        }
        else
        {
            log.UsageCount++;
        }

        await _db.SaveChangesAsync();
    }
}
