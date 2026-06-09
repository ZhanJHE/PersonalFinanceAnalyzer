using PersonalFinanceAnalyzer.Models;
using ScottPlot.WPF;

namespace PersonalFinanceAnalyzer.Services;

public interface IChartService
{
    void PlotNetTrend(WpfPlot plot, List<DailyBalance> buckets, string dateRangeLabel);
    void PlotCategoryPie(WpfPlot plot, List<Transaction> categoryGroups);
}
