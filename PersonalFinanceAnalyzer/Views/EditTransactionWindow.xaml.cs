using System.Windows;
using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Views;

public partial class EditTransactionWindow : Window
{
    public Transaction EditedTransaction { get; private set; }

    private readonly List<Category> _categories;

    public EditTransactionWindow(Transaction transaction, List<Category> categories)
    {
        InitializeComponent();

        _categories = categories;
        EditedTransaction = transaction;

        // Populate type combo
        TypeCombo.ItemsSource = new[] { "Expense", "Income" };
        TypeCombo.SelectedItem = transaction.Type;

        // Populate category combo (filtered by type)
        UpdateCategoryList(transaction.Type);
        CategoryCombo.SelectedItem = categories.FirstOrDefault(c => c.Id == transaction.CategoryId);

        // Fill fields
        AmountBox.Text = transaction.Amount.ToString("F2");
        if (DateTime.TryParse(transaction.TransactionDate, out var date))
            DatePickerCtrl.SelectedDate = date;
        NoteBox.Text = transaction.Note ?? string.Empty;

        // Filter categories when type changes
        TypeCombo.SelectionChanged += (_, _) =>
        {
            UpdateCategoryList(TypeCombo.SelectedItem?.ToString() ?? "Expense");
        };
    }

    private void UpdateCategoryList(string type)
    {
        CategoryCombo.ItemsSource = _categories
            .Where(c => c.Type == type)
            .ToList();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        // Validate
        if (!decimal.TryParse(AmountBox.Text, out var amount) || amount <= 0)
        {
            ErrorText.Text = "请输入有效的金额。";
            return;
        }
        if (CategoryCombo.SelectedItem == null)
        {
            ErrorText.Text = "请选择类别。";
            return;
        }
        if (DatePickerCtrl.SelectedDate == null)
        {
            ErrorText.Text = "请选择日期。";
            return;
        }

        EditedTransaction.Amount = amount;
        EditedTransaction.Type = TypeCombo.SelectedItem?.ToString() ?? "Expense";
        EditedTransaction.CategoryId = ((Category)CategoryCombo.SelectedItem).Id;
        EditedTransaction.TransactionDate = DatePickerCtrl.SelectedDate.Value.ToString("yyyy-MM-dd");
        EditedTransaction.Note = NoteBox.Text.Trim();
        EditedTransaction.UpdatedAt = DateTime.UtcNow;

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
