using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

public interface ICsvImportService
{
    Task<(List<Transaction> Transactions, List<string> Errors)> ParseCsvAsync(string filePath);
    Task<ImportResult> ImportFromCsvAsync(string filePath);
}

public class ImportResult
{
    public int SuccessCount { get; set; }
    public int SkipCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public string SourceType { get; set; } = "通用CSV";
    public int? ImportedCount { get; set; }
    public int? AutoMatchedCount { get; set; }
}
