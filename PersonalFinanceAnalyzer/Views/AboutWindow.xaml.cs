using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace PersonalFinanceAnalyzer.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void OnHyperlinkClick(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch { }
        e.Handled = true;
    }
}
