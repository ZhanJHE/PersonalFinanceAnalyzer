using ClosedXML.Excel;
using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

public interface IExcelExportService
{
    Task ExportTransactionsAsync(List<Transaction> transactions, string filePath);
}

public class ExcelExportService : IExcelExportService
{
    public Task ExportTransactionsAsync(List<Transaction> transactions, string filePath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("收支记录");

        // Header
        ws.Cell(1, 1).Value = "日期";
        ws.Cell(1, 2).Value = "类型";
        ws.Cell(1, 3).Value = "类别";
        ws.Cell(1, 4).Value = "金额";
        ws.Cell(1, 5).Value = "备注";

        // Style header
        var headerRange = ws.Range(1, 1, 1, 5);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;

        // Data rows
        for (int i = 0; i < transactions.Count; i++)
        {
            var t = transactions[i];
            var row = i + 2;
            ws.Cell(row, 1).Value = t.TransactionDate;
            ws.Cell(row, 2).Value = t.Type == "Income" ? "收入" : "支出";
            ws.Cell(row, 3).Value = t.CategoryName ?? "";
            ws.Cell(row, 4).Value = (double)t.Amount;
            ws.Cell(row, 5).Value = t.Note ?? "";
        }

        // Summary row
        if (transactions.Count > 0)
        {
            var lastRow = transactions.Count + 2;
            ws.Cell(lastRow, 1).Value = "合计";
            ws.Cell(lastRow, 1).Style.Font.Bold = true;
            ws.Cell(lastRow, 4).FormulaA1 = $"=SUM(D2:D{lastRow - 1})";
            ws.Cell(lastRow, 4).Style.Font.Bold = true;
        }

        ws.Columns().AdjustToContents();

        wb.SaveAs(filePath);
        return Task.CompletedTask;
    }
}
