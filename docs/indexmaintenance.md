# Index maintenance

If your solution is configured with an index (i.e. configured to limit the amount of blobs created by the caching system), that index can due to real world reasons get out of sync with what's stored in the blob storage.

To deal with that, we've provided a way to trigger what we call a _rebuild_ of the index, where a comparison is made between what's stored inside the SQL table and blob storage, and then results are resolved.

In short: this is how you get access and trigger it;
```
var cachePlugin = Config.Current.Plugins.Get<AzureBlobCachePlugin>();
var index = cachePlugin.GetConfiguredIndex() as IRebuildableCacheIndex;
await index.RebuildAsync((progress) => {});
```
There are parameters for providing a cancellation token and a required callback with progress updates for UI scenarios.

## Optimizely 

Optimizely (formerly EPiServer) has a way to trigger Scheduled tasks that are stoppable. The index rebuild method is developed with this type of functionality in mind. Here is what a stoppable scheduled job would look like.

```
[ScheduledPlugIn(DisplayName = "Rebuild ImageResizer blob cache index")]
public class RebuildIndexJob : ScheduledJobBase
{
    private CancellationTokenSource _tokenSource;
    private IRebuildProgress _progress;

    public RebuildIndexJob()
    {
        IsStoppable = true;
    }

    public override string Execute()
    {
        var config = Config.Current;
        var cachePlugin = config.Plugins.Get<AzureBlobCachePlugin>();
        var rebuildableIndex = cachePlugin.GetConfiguredIndex() as IRebuildableCacheIndex;

        if (rebuildableIndex == null)
            return "No rebuildable index configured, nothing to rebuild";

        _progress = null;
        _tokenSource = new CancellationTokenSource();

        try
        {
            // More about AsyncHelper here: https://cpratt.co/async-tips-tricks/
            AsyncHelper.RunSync(() => rebuildableIndex.RebuildAsync(_tokenSource.Token, (p) => UpdateProgress(p)));

            return $"{FormatProgress(_progress)}";
        }
        catch (OperationCanceledException)
        {
            return $"Job was stopped, {FormatProgress(_progress)}";
        }
        finally
        {
            _tokenSource.Dispose();
            _tokenSource = null;
        }
    }

    public override void Stop()
    {
        if (_tokenSource != null)
            _tokenSource.Cancel();
    }

    private void UpdateProgress(IRebuildProgress progress)
    {
        _progress = progress;

        OnStatusChanged(FormatProgress(progress));
    }

    private string FormatProgress(IRebuildProgress progress)
    {
        if (progress == null)
            return string.Empty;

        return $"{progress.CleanupPhase}, discovered: {progress.DiscoveredBlobs} of expected: {progress.ItemsInIndex}, added: {progress.AddedIndexItems}, removed: {progress.RemovedIndexItems}, errors: {progress.Errors}";
    }
}
```
