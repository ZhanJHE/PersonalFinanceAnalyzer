using System.Windows;

namespace PersonalFinanceAnalyzer.Views;

public partial class AiReportWindow : Window
{
    public string ContentMarkdown { get; }

    public AiReportWindow(string title, string period, string content)
    {
        InitializeComponent();
        TitleText.Text = title;
        PeriodText.Text = period;
        ContentMarkdown = content;
        DataContext = this;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
