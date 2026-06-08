using System.Windows;
using System.Windows.Controls;
using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Views;

public partial class SyncConflictWindow : Window
{
    private readonly List<(Transaction Local, Transaction Cloud)>? _conflicts;
    private readonly Dictionary<Guid, bool> _choices = new();

    // Sync mode result
    public enum SyncModeChoice { KeepLocal, KeepCloud, PerItem }
    public SyncModeChoice SelectedMode { get; private set; } = SyncModeChoice.KeepLocal;

    // Per-item results
    public HashSet<Guid> KeepLocalIds { get; private set; } = new();
    public HashSet<Guid> KeepCloudIds { get; private set; } = new();

    /// <summary>
    /// Constructor for hash-based sync (three-mode choice).
    /// </summary>
    public SyncConflictWindow(DateTime localTime, DateTime serverTime, List<(Transaction Local, Transaction Cloud)>? conflicts = null)
    {
        InitializeComponent();
        _conflicts = conflicts;

        DateInfoText.Text = $"本地数据最后修改：{localTime:yyyy-MM-dd HH:mm}　　云端数据最后修改：{serverTime:yyyy-MM-dd HH:mm}";

        if (conflicts != null && conflicts.Count > 0)
        {
            ConflictList.ItemsSource = conflicts.Select(c => new { Local = c.Local, Cloud = c.Cloud, c.Local.Id }).ToList();
        }
    }

    /// <summary>
    /// Constructor for per-item conflict resolution (existing flow).
    /// </summary>
    public SyncConflictWindow(List<(Transaction Local, Transaction Cloud)> conflicts) : this(DateTime.MinValue, DateTime.MinValue, conflicts)
    {
        RadioPerItem.IsChecked = true;
        OnSyncModeChanged(null, null);
    }

    private void OnSyncModeChanged(object? sender, RoutedEventArgs? e)
    {
        ConflictScrollViewer.Visibility = RadioPerItem.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;

        ConfirmButton.Content = RadioPerItem.IsChecked == true
            ? "应用选择"
            : "确定";
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (RadioKeepLocal.IsChecked == true)
        {
            SelectedMode = SyncModeChoice.KeepLocal;
            DialogResult = true;
            Close();
        }
        else if (RadioKeepCloud.IsChecked == true)
        {
            SelectedMode = SyncModeChoice.KeepCloud;
            DialogResult = true;
            Close();
        }
        else if (RadioPerItem.IsChecked == true)
        {
            // Read per-item selections
            KeepLocalIds = new HashSet<Guid>();
            KeepCloudIds = new HashSet<Guid>();

            if (_conflicts != null)
            {
                foreach (var item in ConflictList.Items)
                {
                    var container = ConflictList.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                    if (container == null) continue;

                    var localRadio = FindChild<RadioButton>(container, "LocalRadio");
                    var cloudRadio = FindChild<RadioButton>(container, "CloudRadio");

                    dynamic dynamicItem = item;
                    Guid id = dynamicItem.Id;

                    if (localRadio?.IsChecked == true)
                        KeepLocalIds.Add(id);
                    else
                        KeepCloudIds.Add(id);
                }
            }

            SelectedMode = SyncModeChoice.PerItem;
            DialogResult = true;
            Close();
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static T? FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
    {
        if (parent == null) return null;

        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t && (child is FrameworkElement fe && fe.Name == childName || string.IsNullOrEmpty(childName)))
                return t;
            var result = FindChild<T>(child, childName);
            if (result != null) return result;
        }
        return null;
    }
}
