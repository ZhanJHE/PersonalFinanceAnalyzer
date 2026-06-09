using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinanceAnalyzer.Models;
using PersonalFinanceAnalyzer.Services;
using PersonalFinanceAnalyzer.ViewModels;

namespace PersonalFinanceAnalyzer.Views;

public partial class AnalysisView : UserControl
{
    public AnalysisView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AnalysisViewModel vm)
        {
            await vm.LoadChartDataAsync();
            RenderAllCharts(vm);
            ShowToast("图表已加载完成");
        }
    }

    private void RenderAllCharts(AnalysisViewModel vm)
    {
        var chartService = App.Services.GetRequiredService<IChartService>();

        // Render all 6 charts once — no more switching
        chartService.PlotNetTrend(TrendPlot1, vm.TrendBuckets1Month, vm.DateRangeLabel1);
        chartService.PlotCategoryPie(PiePlot1, vm.CategoryBreakdown1Month.ToList());

        chartService.PlotNetTrend(TrendPlot3, vm.TrendBuckets3Months, vm.DateRangeLabel3);
        chartService.PlotCategoryPie(PiePlot3, vm.CategoryBreakdown3Months.ToList());

        chartService.PlotNetTrend(TrendPlot6, vm.TrendBuckets6Months, vm.DateRangeLabel6);
        chartService.PlotCategoryPie(PiePlot6, vm.CategoryBreakdown6Months.ToList());
    }

    private void ShowToast(string message)
    {
        ToastText.Text = message;

        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 0.85,
            Duration = new Duration(System.TimeSpan.FromMilliseconds(300)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        var fadeOut = new DoubleAnimation
        {
            From = 0.85,
            To = 0,
            Duration = new Duration(System.TimeSpan.FromMilliseconds(500)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
            BeginTime = System.TimeSpan.FromSeconds(2.5)
        };

        var storyboard = new Storyboard();
        Storyboard.SetTarget(fadeIn, ToastBar);
        Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));
        storyboard.Children.Add(fadeIn);

        Storyboard.SetTarget(fadeOut, ToastBar);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));
        storyboard.Children.Add(fadeOut);

        ToastBar.Visibility = Visibility.Visible;
        storyboard.Begin();
    }
}
