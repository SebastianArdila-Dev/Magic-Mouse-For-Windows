using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;

namespace MagicMouse.Windows.App.Devices;

public sealed class MagicMouseDiscoveryService : IDisposable
{
    private static readonly ushort[] ProductIds = [0x030D, 0x0269, 0x0323];
    private static readonly ushort[] VendorIds = [0x05AC, 0x004C];
    private DeviceWatcher? _watcher;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private int _refreshQueued;
    private bool _disposed;
    private const string AllHidInterfaces = "System.Devices.InterfaceClassGuid:=\"{4D1E55B2-F16F-11CF-88CB-001111000030}\"";
    public IReadOnlyList<DeviceInformation> Candidates { get; private set; } = [];

    public event Action<DeviceInformation?>? DeviceChanged;

    public async Task StartAsync()
    {
        if(_disposed) return;
        if (_watcher is not null) return;
        _watcher = DeviceInformation.CreateWatcher(AllHidInterfaces);
        _watcher.Added += OnAdded;
        _watcher.Updated += OnUpdated;
        _watcher.Removed += OnRemoved;
        _watcher.EnumerationCompleted += OnEnumerationCompleted;
        _watcher.Start();
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if(_disposed) return;
        await _refreshGate.WaitAsync();
        try
        {
            var candidates = new List<DeviceInformation>();
            var allCollections = await DeviceInformation.FindAllAsync(AllHidInterfaces, new[] { "System.DeviceInterface.Hid.UsagePage", "System.DeviceInterface.Hid.UsageId" });
            candidates.AddRange(allCollections.Where(IsMagicMouse));
            foreach (var vendorId in VendorIds)
            foreach (var productId in ProductIds)
            {
                var selector = HidDevice.GetDeviceSelector(0x01, 0x02, vendorId, productId);
                var matches = await DeviceInformation.FindAllAsync(selector);
                candidates.AddRange(matches);
            }
            Candidates = candidates.DistinctBy(device => device.Id).OrderByDescending(device => device.Properties.TryGetValue("System.DeviceInterface.Hid.UsagePage",out var usage) && Convert.ToUInt32(usage) >= 0xFF00).ToArray();
            if(!_disposed) DeviceChanged?.Invoke(Candidates.FirstOrDefault());
        }
        finally { _refreshGate.Release(); }
    }

    private void OnAdded(DeviceWatcher sender, DeviceInformation device)
    {
        if (IsMagicMouse(device)) QueueRefresh();
    }
    private void OnUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (IsMagicMouse(update.Id)) QueueRefresh();
    }
    private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (IsMagicMouse(update.Id)) QueueRefresh();
    }
    private void OnEnumerationCompleted(DeviceWatcher sender, object args) => QueueRefresh();

    private void QueueRefresh()
    {
        if(_disposed || Interlocked.Exchange(ref _refreshQueued, 1) != 0) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(150);
                await RefreshAsync();
            }
            catch (Exception) { /* Device changes are best-effort; manual retry remains available. */ }
            finally { Interlocked.Exchange(ref _refreshQueued, 0); }
        });
    }

    private static bool IsMagicMouse(DeviceInformation device)
        => IsMagicMouse(device.Id);

    private static bool IsMagicMouse(string deviceId)
    {
        var id = deviceId.ToUpperInvariant();
        var appleVendor = id.Contains("VID_05AC", StringComparison.Ordinal) || id.Contains("VID&0001004C", StringComparison.Ordinal) || id.Contains("VID&004C", StringComparison.Ordinal);
        var mouseProduct = ProductIds.Any(productId => id.Contains($"PID_{productId:X4}", StringComparison.Ordinal) || id.Contains($"PID&{productId:X4}", StringComparison.Ordinal));
        return appleVendor && mouseProduct;
    }

    public void Dispose()
    {
        if(_disposed) return;_disposed=true;
        if(_watcher is not null)
        {
            _watcher.Added-=OnAdded;_watcher.Updated-=OnUpdated;_watcher.Removed-=OnRemoved;_watcher.EnumerationCompleted-=OnEnumerationCompleted;
            if(_watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted) _watcher.Stop();
        }
        _watcher = null;
    }
}
