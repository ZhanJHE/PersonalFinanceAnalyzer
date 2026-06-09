using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

public interface IDailyViewService
{
    Task<List<DailyBalance>> GetDailyBalancesAsync(DateTime start, DateTime end);
}
