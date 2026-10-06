using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Windows.Devices.Enumeration;

namespace MagicMouse.Windows.App.Devices;

public sealed record HidCaptureMetadata(
    ushort VendorId,
    ushort ProductId,
    ushort Version,
    ushort UsagePage,
    ushort UsageId,
    ushort InputReportByteLength,
    string DeviceName,
    string DevicePath,
    ushort OutputReportByteLength,
    ushort FeatureReportByteLength);

public sealed record HidReportSnapshot(DateTimeOffset Timestamp, byte ReportId, int Length, string BytesHex, bool Discontinuity = false);

/// <summary>Opt-in local capture of raw input reports from an accessible HID collection.</summary>
public sealed class HidReportCaptureService : IDisposable
{
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileFlagOverlapped = 0x40000000;
    private const int HidpStatusSuccess = 0x00110000;
    private readonly object _sync = new();
    private readonly List<HidReportSnapshot> _reports = [];
    private FileStream? _stream;
    private DriverBridgeClient? _bridge;
    private CancellationTokenSource? _captureCancellation;
    private Task? _captureTask;
    private int _disposed;
    private int _isCapturing;

    public event Action<HidReportSnapshot>? ReportReceived;
    public event Action<Exception>? CaptureFailed;
    public event Action<bool>? CaptureStateChanged;
    public HidCaptureMetadata? Metadata { get; private set; }
    public bool IsCapturing => Volatile.Read(ref _isCapturing) != 0;
    public int ReportCount { get { lock (_sync) return _reports.Count; } }
    public double ReportsPerSecond
    {
        get
        {
            lock (_sync)
            {
                var cutoff = DateTimeOffset.UtcNow.AddSeconds(-1);
                return _reports.Count(report => report.Timestamp >= cutoff);
            }
        }
    }

    public Task<HidCaptureMetadata> StartAsync(DeviceInformation deviceInformation)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (_stream is not null) throw new InvalidOperationException("Stop the current capture before starting another.");

            var handle = CreateFile(deviceInformation.Id, GenericRead, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, FileFlagOverlapped, IntPtr.Zero);
            if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not open this HID collection for reading.");

            try
            {
                var attributes = new HidAttributes { Size = (uint)Marshal.SizeOf<HidAttributes>() };
                if (!HidD_GetAttributes(handle, ref attributes)) throw new Win32Exception(Marshal.GetLastWin32Error(), "HID attributes were not available.");
                if (!HidD_GetPreparsedData(handle, out var preparsed)) throw new Win32Exception(Marshal.GetLastWin32Error(), "The HID report descriptor could not be read.");

                HidpCaps caps;
                try
                {
                    var status = HidP_GetCaps(preparsed, out caps);
                    if (status != HidpStatusSuccess) throw new InvalidOperationException($"Windows HID parser returned status 0x{status:X8}.");
                }
                finally { HidD_FreePreparsedData(preparsed); }

                if (caps.InputReportByteLength == 0) throw new InvalidOperationException("This HID collection has no input reports.");
                var metadata = new HidCaptureMetadata(attributes.VendorId, attributes.ProductId, attributes.VersionNumber, caps.UsagePage,
                    caps.Usage, caps.InputReportByteLength, deviceInformation.Name, deviceInformation.Id,
                    caps.OutputReportByteLength, caps.FeatureReportByteLength);
                Metadata = metadata;
                lock (_sync) _reports.Clear();
                _captureCancellation = new CancellationTokenSource();
                _stream = new FileStream(handle, FileAccess.Read, Math.Max((int)caps.InputReportByteLength, 64), isAsync: true);
                Volatile.Write(ref _isCapturing, 1);
                _captureTask = CaptureLoopAsync(_stream, caps.InputReportByteLength, _captureCancellation.Token);
                CaptureStateChanged?.Invoke(true);
                return Task.FromResult(metadata);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }
    }

    public async Task<HidCaptureMetadata> StartBridgeAsync(DeviceInformation deviceInformation)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var bridge = await DriverBridgeClient.OpenForDeviceAsync(deviceInformation);
        lock (_sync)
        {
            if (Volatile.Read(ref _disposed) != 0 || _stream is not null || _bridge is not null) { bridge.Dispose(); throw new InvalidOperationException("La captura no está disponible."); }
            _bridge = bridge; _reports.Clear(); _captureCancellation = new CancellationTokenSource();
            Metadata = new(bridge.Info.VendorId, bridge.Info.ProductId, bridge.Info.Firmware, 0xFF00, 1, 160,
                deviceInformation.Name, bridge.DevicePath, 0, 3);
            Volatile.Write(ref _isCapturing, 1);
            _captureTask = CaptureBridgeLoopAsync(bridge, _captureCancellation.Token);
            CaptureStateChanged?.Invoke(true);
            return Metadata;
        }
    }

    private async Task CaptureBridgeLoopAsync(DriverBridgeClient bridge, CancellationToken cancellation)
    {
        var sequence = new MagicMouse.Core.Drivers.DriverBridgeSequence();
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                var packet = bridge.Read();
                if (packet is null) { await Task.Delay(8, cancellation); continue; }
                if (!sequence.Accept(packet.Sequence, packet.Discontinuity, out var discontinuity))
                    throw new InvalidDataException("El puente devolvió una secuencia de reportes repetida o fuera de orden.");
                var snapshot = new HidReportSnapshot(packet.Timestamp, packet.Report[0], packet.Report.Length, Convert.ToHexString(packet.Report),
                    discontinuity);
                lock (_sync) { _reports.Add(snapshot); if (_reports.Count > 5000) _reports.RemoveRange(0, _reports.Count - 5000); }
                ReportReceived?.Invoke(snapshot);
            }
        }
        catch (Exception) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception) { CaptureFailed?.Invoke(exception); }
        finally
        {
            if (ReferenceEquals(Interlocked.CompareExchange(ref _bridge, null, bridge), bridge))
            { bridge.Dispose(); Volatile.Write(ref _isCapturing, 0); CaptureStateChanged?.Invoke(false); }
        }
    }

    public IReadOnlyList<HidReportSnapshot> Snapshot()
    {
        lock (_sync) return _reports.ToArray();
    }

    public void RequestTouchReports()
    {
        if (_bridge is { } bridge) { bridge.EnableTouch(); return; }
        var metadata = Metadata ?? throw new InvalidOperationException("Primero inicia la lectura HID.");
        if (metadata.VendorId is not (0x05AC or 0x004C) || metadata.ProductId is not (0x030D or 0x0269 or 0x0323))
            throw new InvalidOperationException("No se enviarán comandos a un dispositivo no reconocido.");
        byte[] command = metadata.ProductId == 0x030D ? [0xD7, 0x01] : [0xF1, 0x02, 0x01];
        if (metadata.FeatureReportByteLength < command.Length) throw new InvalidOperationException("Esta colección no expone el reporte de función requerido. Windows puede reservar la superficie táctil para el controlador.");
        using var handle = CreateFile(metadata.DevicePath, 0, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        var payload = new byte[metadata.FeatureReportByteLength]; command.CopyTo(payload, 0);
        if (!HidD_SetFeature(handle, payload, payload.Length)) throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo solicitar el modo táctil. No se instalaron controladores alternativos.");
    }

    public void Clear()
    {
        lock (_sync) _reports.Clear();
    }

    public async Task ExportAsync(string path, CancellationToken cancellationToken = default)
    {
        var metadata = Metadata ?? throw new InvalidOperationException("No HID capture has been started.");
        var payload = new { CapturedAt = DateTimeOffset.UtcNow, Device = metadata, ReportCount, ReportsPerSecond, Reports = Snapshot() };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
    }

    private async Task CaptureLoopAsync(FileStream stream, int reportLength, CancellationToken cancellationToken)
    {
        var buffer = new byte[reportLength];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (read == 0) throw new EndOfStreamException("Windows closed the HID input stream.");
                var bytes = buffer.AsSpan(0, read).ToArray();
                var snapshot = new HidReportSnapshot(DateTimeOffset.UtcNow, bytes[0], bytes.Length, Convert.ToHexString(bytes));
                lock (_sync)
                {
                    _reports.Add(snapshot);
                    if (_reports.Count > 5000) _reports.RemoveRange(0, _reports.Count - 5000);
                }
                ReportReceived?.Invoke(snapshot);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
        catch (IOException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) { CaptureFailed?.Invoke(exception); }
        finally
        {
            if (ReferenceEquals(Interlocked.CompareExchange(ref _stream, null, stream), stream))
            {
                stream.Dispose();
                Volatile.Write(ref _isCapturing, 0);
                CaptureStateChanged?.Invoke(false);
            }
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            var cancellation = _captureCancellation;
            _captureCancellation = null;
            cancellation?.Cancel();
            _stream?.Dispose();
            _stream = null; _bridge?.Dispose(); _bridge = null;
            Volatile.Write(ref _isCapturing, 0);
            cancellation?.Dispose();
            _captureTask = null;
            CaptureStateChanged?.Invoke(false);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        Stop();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidAttributes
    {
        public uint Size;
        public ushort VendorId;
        public ushort ProductId;
        public ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidpCaps
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        public ushort Reserved0, Reserved1, Reserved2, Reserved3, Reserved4, Reserved5, Reserved6, Reserved7, Reserved8;
        public ushort Reserved9, Reserved10, Reserved11, Reserved12, Reserved13, Reserved14, Reserved15, Reserved16;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetAttributes(SafeFileHandle hidDeviceObject, ref HidAttributes attributes);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_SetFeature(SafeFileHandle handle, byte[] report, int length);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle hidDeviceObject, out IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll", EntryPoint = "HidP_GetCaps")]
    private static extern int HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);
}
