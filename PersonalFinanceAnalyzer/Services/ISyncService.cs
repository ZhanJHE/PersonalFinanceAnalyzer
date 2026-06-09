using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

public interface ISyncService
{
    Task<(int Uploaded, int Downloaded)> SyncAllAsync();
    Task<(int Uploaded, int Downloaded)> SyncWithConflictResolutionAsync();
    Task<SyncHashResult> CheckHashAsync();
    Task<int> UploadLocalAsync();
    Task<int> DownloadCloudAsync();
}

public class SyncHashResult
{
    public bool IsMatch { get; set; }
    public string LocalHash { get; set; } = "";
    public string ServerHash { get; set; } = "";
    public DateTime LocalMaxUpdatedAt { get; set; }
    public DateTime ServerMaxUpdatedAt { get; set; }
}
