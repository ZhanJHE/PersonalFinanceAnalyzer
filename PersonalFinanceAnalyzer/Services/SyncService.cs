using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PersonalFinanceAnalyzer.Models;

namespace PersonalFinanceAnalyzer.Services;

public class SyncService : ISyncService
{
    private readonly IDatabaseService _local;
    private readonly ICloudDataService _cloud;
    private readonly IAuthService _auth;
    private readonly HttpClient _http;
    private readonly string _serverUrl;

    public SyncService(IDatabaseService local, ICloudDataService cloud, IAuthService auth, IConfiguration config)
    {
        _local = local;
        _cloud = cloud;
        _auth = auth;
        _serverUrl = config["ServerUrl"] ?? "http://localhost:5000";
        _http = new HttpClient();
    }

    public async Task<SyncHashResult> CheckHashAsync()
    {
        var result = new SyncHashResult();

        // Compute local hash
        result.LocalHash = await _local.ComputeHashAsync();

        // Get local max UpdatedAt
        var localTransactions = await _local.GetTransactionsAsync();
        result.LocalMaxUpdatedAt = localTransactions.Count > 0
            ? localTransactions.Max(t => t.UpdatedAt)
            : DateTime.MinValue;

        // Fetch server hash
        if (!_auth.IsLoggedIn || _auth.Token == null)
        {
            result.IsMatch = false;
            return result;
        }

        try
        {
            _http.DefaultRequestHeaders.Remove("Authorization");
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_auth.Token}");

            var response = await _http.GetAsync($"{_serverUrl.TrimEnd('/')}/api/sync/hash");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                result.ServerHash = json.GetProperty("hash").GetString() ?? "";
                result.ServerMaxUpdatedAt = json.GetProperty("maxUpdatedAt").GetDateTime();
                result.IsMatch = result.LocalHash == result.ServerHash;
            }
        }
        catch
        {
            // Server unreachable
            result.IsMatch = false;
        }

        return result;
    }

    // 全量同步（无冲突处理，Last-Write-Wins）
    public async Task<(int Uploaded, int Downloaded)> SyncAllAsync()
    {
        var localTransactions = await _local.GetTransactionsAsync();
        var uploaded = await _cloud.UploadAllAsync(localTransactions);
        var cloudTransactions = await _cloud.DownloadAllAsync();
        await _local.CacheTransactionsAsync(cloudTransactions);
        return (uploaded, cloudTransactions.Count);
    }

    // 上传本地覆盖云端
    public async Task<int> UploadLocalAsync()
    {
        var localTransactions = await _local.GetTransactionsAsync();
        return await _cloud.UploadAllAsync(localTransactions);
    }

    // 下载云端覆盖本地
    public async Task<int> DownloadCloudAsync()
    {
        var cloudTransactions = await _cloud.DownloadAllAsync();
        await _local.CacheTransactionsAsync(cloudTransactions);
        return cloudTransactions.Count;
    }

    // 带冲突解决的同步
    public async Task<(int Uploaded, int Downloaded)> SyncWithConflictResolutionAsync()
    {
        var localDict = (await _local.GetTransactionsAsync()).ToDictionary(t => t.Id);
        var cloudList = await _cloud.DownloadAllAsync();

        var conflicts = new List<(Transaction Local, Transaction Cloud)>();

        foreach (var cloud in cloudList)
        {
            if (localDict.TryGetValue(cloud.Id, out var local))
            {
                var diff = Math.Abs((local.UpdatedAt - cloud.UpdatedAt).TotalSeconds);
                if (diff > 1)
                    conflicts.Add((local, cloud));
                localDict.Remove(cloud.Id);
            }
        }

        var onlyLocal = localDict.Values.ToList();

        if (conflicts.Count > 0)
        {
            var (keepLocal, keepCloud) = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var win = new Views.SyncConflictWindow(conflicts);
                win.Owner = System.Windows.Application.Current.MainWindow;
                win.ShowDialog();
                return (win.KeepLocalIds, win.KeepCloudIds);
            });

            var localToUpload = onlyLocal.Concat(
                conflicts.Where(c => keepLocal.Contains(c.Local.Id)).Select(c => c.Local)).ToList();
            await _cloud.UploadAllAsync(localToUpload);

            var cloudToDownload = conflicts
                .Where(c => keepCloud.Contains(c.Cloud.Id))
                .Select(c => c.Cloud).ToList();

            var allCloud = await _cloud.DownloadAllAsync();
            await _local.CacheTransactionsAsync(allCloud);

            var uploaded = localToUpload.Count;
            var downloaded = cloudToDownload.Count;
            return (uploaded, downloaded);
        }

        return await SyncAllAsync();
    }
}
