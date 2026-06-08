using PersonalFinanceAnalyzer.Models;
using ScottPlot;
using ScottPlot.WPF;

namespace PersonalFinanceAnalyzer.Services;

public class ChartService : IChartService
{
    public void PlotNetTrend(WpfPlot plot, List<DailyBalance> buckets, string dateRangeLabel)
    {
        plot.Plot.Clear();
        if (buckets.Count == 0) { plot.Refresh(); return; }

        var pos = Enumerable.Range(0, buckets.Count).Select(x => (double)x).ToArray();
        var nets = buckets.Select(b => (double)b.Net).ToArray();
        var labels = buckets.Select(b =>
        {
            var parts = b.Date.Split('-');
            return $"{int.Parse(parts[1])}/{int.Parse(parts[2])}";
        }).ToArray();

        // Line segments between consecutive points, colored by sign
        for (int i = 0; i < pos.Length - 1; i++)
        {
            var lineColor = nets[i + 1] >= 0 ? Colors.Green : Colors.Red;
            var line = plot.Plot.Add.Line(pos[i], nets[i], pos[i + 1], nets[i + 1]);
            line.Color = lineColor;
            line.LineWidth = 2.5f;
        }

        // Colored markers at each data point
        for (int i = 0; i < pos.Length; i++)
        {
            var marker = plot.Plot.Add.Marker(pos[i], nets[i]);
            marker.MarkerSize = 10;
            marker.Color = nets[i] >= 0 ? Colors.Green : Colors.Red;
            marker.MarkerShape = MarkerShape.FilledCircle;
        }

        // Zero line
        plot.Plot.Add.HorizontalLine(0, 1, Colors.Black);

        // Show value labels
        for (int i = 0; i < pos.Length; i++)
        {
            var txt = plot.Plot.Add.Text($"¥{nets[i]:N0}", pos[i], nets[i]);
            txt.LabelFontSize = 24;
            txt.LabelBold = true;
            txt.LabelFontColor = nets[i] >= 0 ? Colors.Green : Colors.Red;
            txt.OffsetX = 0;
            txt.OffsetY = nets[i] >= 0 ? -28 : 8;
        }

        // Axis labels
        plot.Plot.Axes.Bottom.SetTicks(pos, labels);
        plot.Plot.HideLegend();
        plot.Refresh();
        DisableMouseInteraction(plot);
    }

    private static void DisableMouseInteraction(WpfPlot plot)
    {
        try { plot.UserInputProcessor.IsEnabled = false; } catch { }
    }

    public void PlotCategoryPie(WpfPlot plot, List<Transaction> categoryGroups)
    {
        plot.Plot.Clear();
        if (categoryGroups.Count == 0) { plot.Refresh(); return; }

        var total = categoryGroups.Sum(c => c.Amount);
        var slices = categoryGroups.Select((c, i) => new PieSlice
        {
            Value = (double)c.Amount,
            Label = "",
            FillColor = GetCategoryColor(c.CategoryName ?? "", i)
        }).ToList();

        var pie = plot.Plot.Add.Pie(slices);
        pie.ExplodeFraction = 0.05;
        plot.Plot.Axes.AutoScale();
        plot.Refresh();
        DisableMouseInteraction(plot);

        var limits = plot.Plot.Axes.GetLimits();
        var cx = (limits.Left + limits.Right) / 2;
        var cy = (limits.Bottom + limits.Top) / 2;
        var r = Math.Min((limits.Right - limits.Left) / 2, (limits.Top - limits.Bottom) / 2) * 0.55;
        var totalVal = (double)total;
        if (totalVal > 0 && r > 0)
        {
            var startAngle = -90.0;
            foreach (var slice in slices)
            {
                var sweep = slice.Value / totalVal * 360;
                var mid = (startAngle + sweep / 2) * Math.PI / 180;
                var x = cx + r * Math.Cos(mid);
                var y = cy - r * Math.Sin(mid);
                var pct = slice.Value / totalVal * 100;
                var txt = plot.Plot.Add.Text($"{pct:N1}%", x, y);
                txt.LabelFontSize = 26; txt.LabelBold = true;
                txt.LabelFontColor = Colors.Black;
                txt.LabelAlignment = ScottPlot.Alignment.MiddleCenter;
                startAngle += sweep;
            }
        }
    }

    private static Color GetCategoryColor(string categoryName, int index)
    {
        if (!string.IsNullOrEmpty(categoryName) &&
            Helpers.CategoryColorConverter.ColorMap.TryGetValue(categoryName, out var c) && c.Length >= 7)
        {
            try { return new Color(Convert.ToByte(c[1..3], 16), Convert.ToByte(c[3..5], 16), Convert.ToByte(c[5..7], 16)); }
            catch { }
        }
        var fb = new[] { new Color(255, 99, 132), new Color(54, 162, 235), new Color(255, 206, 86), new Color(75, 192, 192), new Color(153, 102, 255), new Color(255, 159, 64), new Color(199, 199, 199), new Color(83, 102, 255), new Color(255, 102, 255), new Color(102, 204, 153) };
        return fb[index % fb.Length];
    }
}
