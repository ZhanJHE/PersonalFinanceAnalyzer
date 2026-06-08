using Microsoft.EntityFrameworkCore;
using PersonalFinanceAnalyzer.Server.Data;
using PersonalFinanceAnalyzer.Server.Models;
using PersonalFinanceAnalyzer.Server.Services;

namespace PersonalFinanceAnalyzer.Tests;

public class QuotaServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly QuotaService _service;

    public QuotaServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _service = new QuotaService(_db);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task CheckQuota_NoUser_ReturnsNotAllowed()
    {
        var (allowed, remaining) = await _service.CheckQuotaAsync(999);
        Assert.False(allowed);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task CheckQuota_NewNonMember_Returns10()
    {
        _db.Users.Add(new User { Id = 1, Username = "test", IsMember = false, PasswordHash = "hash" });
        await _db.SaveChangesAsync();

        var (allowed, remaining) = await _service.CheckQuotaAsync(1);
        Assert.True(allowed);
        Assert.Equal(10, remaining);
    }

    [Fact]
    public async Task CheckQuota_NewMember_Returns100()
    {
        _db.Users.Add(new User { Id = 2, Username = "member", IsMember = true, PasswordHash = "hash" });
        await _db.SaveChangesAsync();

        var (allowed, remaining) = await _service.CheckQuotaAsync(2);
        Assert.True(allowed);
        Assert.Equal(100, remaining);
    }

    [Fact]
    public async Task RecordUsage_IncrementsCount()
    {
        _db.Users.Add(new User { Id = 3, Username = "usage", IsMember = false, PasswordHash = "hash" });
        await _db.SaveChangesAsync();

        await _service.RecordUsageAsync(3);
        var (allowed, remaining) = await _service.CheckQuotaAsync(3);
        Assert.True(allowed);
        Assert.Equal(9, remaining); // 10 - 1 = 9

        await _service.RecordUsageAsync(3);
        (allowed, remaining) = await _service.CheckQuotaAsync(3);
        Assert.True(allowed);
        Assert.Equal(8, remaining); // 10 - 2 = 8
    }

    [Fact]
    public async Task RecordUsage_ExceedsQuota_ReturnsNotAllowed()
    {
        _db.Users.Add(new User { Id = 4, Username = "exceed", IsMember = false, PasswordHash = "hash" });
        await _db.SaveChangesAsync();

        // Use all 10 quota
        for (int i = 0; i < 10; i++)
            await _service.RecordUsageAsync(4);

        var (allowed, remaining) = await _service.CheckQuotaAsync(4);
        Assert.False(allowed);
        Assert.Equal(0, remaining);
    }
}
