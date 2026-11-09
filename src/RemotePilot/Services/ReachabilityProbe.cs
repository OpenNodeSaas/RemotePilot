using System.Net.Sockets;
using System.Diagnostics;
using RemotePilot.Models;

namespace RemotePilot.Services;

public sealed class ReachabilityProbe
{
    private Timer? _probeTimer;
    private readonly SemaphoreSlim _batchSemaphore;
    public event EventHandler<List<ServerAsset>>? ProbeCompleted;

    public ReachabilityProbe(int maxConcurrency = 10)
    {
        _batchSemaphore = new SemaphoreSlim(maxConcurrency > 0 ? maxConcurrency : 10);
    }

    public async Task<int?> ProbeSingleAsync(string host, int port, int timeoutMs = 3000, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host) || port <= 0 || port > 65535)
        {
            return null;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(host, port);
            var delayTask = Task.Delay(timeoutMs, cancellationToken);

            var completedTask = await Task.WhenAny(connectTask, delayTask).ConfigureAwait(false);
            if (completedTask == delayTask)
            {
                // Timeout
                return null;
            }

            // Await connectTask to throw if connection failed
            await connectTask.ConfigureAwait(false);
            stopwatch.Stop();
            return (int)stopwatch.ElapsedMilliseconds;
        }
        catch
        {
            return null;
        }
    }

    public async Task<Dictionary<Guid, int?>> ProbeBatchAsync(IEnumerable<ServerAsset> assets, int timeoutMs = 3000, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<Guid, int?>();
        var assetList = assets?.ToList() ?? [];
        if (assetList.Count == 0)
        {
            return results;
        }

        var tasks = assetList.Select(async asset =>
        {
            await _batchSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var latency = await ProbeSingleAsync(asset.Host, asset.Port, timeoutMs, cancellationToken).ConfigureAwait(false);
                lock (results)
                {
                    results[asset.Id] = latency;
                }
            }
            finally
            {
                _batchSemaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return results;
    }

    public void StartPeriodicProbe(int intervalMinutes, IEnumerable<ServerAsset> assets, AssetStore? assetStore = null, int timeoutMs = 3000)
    {
        StopPeriodicProbe();

        if (intervalMinutes < 1)
        {
            return;
        }

        var assetList = assets?.ToList() ?? [];
        if (assetList.Count == 0)
        {
            return;
        }

        var intervalTimeSpan = TimeSpan.FromMinutes(intervalMinutes);
        _probeTimer = new Timer(async _ =>
        {
            try
            {
                var results = await ProbeBatchAsync(assetList, timeoutMs).ConfigureAwait(false);
                var now = DateTimeOffset.UtcNow;
                
                var updatedAssets = new List<ServerAsset>();
                foreach (var asset in assetList)
                {
                    if (results.TryGetValue(asset.Id, out var latency))
                    {
                        var updated = asset with
                        {
                            LastProbeAt = now,
                            LastProbeReachable = latency.HasValue
                        };
                        updatedAssets.Add(updated);
                    }
                    else
                    {
                        updatedAssets.Add(asset);
                    }
                }

                if (assetStore != null)
                {
                    try
                    {
                        var storeData = assetStore.Load();
                        // Update existing assets matching IDs
                        var updatedDict = updatedAssets.ToDictionary(a => a.Id);
                        var newAssetList = storeData.Assets.Select(a => updatedDict.TryGetValue(a.Id, out var up) ? up : a).ToList();
                        
                        // Also handle case where storeData might have assets not in the periodic probe list
                        assetStore.Save(storeData with { Assets = newAssetList });
                    }
                    catch
                    {
                        // Ignore persistence errors during background check
                    }
                }

                ProbeCompleted?.Invoke(this, updatedAssets);
            }
            catch
            {
                // Suppress background exceptions
            }
        }, null, TimeSpan.Zero, intervalTimeSpan);
    }

    public void StopPeriodicProbe()
    {
        _probeTimer?.Dispose();
        _probeTimer = null;
    }
}
