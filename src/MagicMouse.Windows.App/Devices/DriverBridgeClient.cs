using System.ComponentModel;
using System.Runtime.InteropServices;
using MagicMouse.Core.Drivers;
using Microsoft.Win32.SafeHandles;
using Windows.Devices.Enumeration;

namespace MagicMouse.Windows.App.Devices;

/// <summary>Opt-in development bridge. No driver installation or elevation happens here.</summary>
internal sealed class DriverBridgeClient : IDisposable
{
    private const string Selector = "System.Devices.InterfaceClassGuid:=\"{5E3F432C-47C6-4C64-9B77-8C8AA13D9EF2}\"";
    private readonly SafeFileHandle _handle;
    public DriverBridgeInfo Info { get; }
    public string DevicePath { get; }
    private DriverBridgeClient(SafeFileHandle handle, DriverBridgeInfo info, string path) { _handle = handle; Info = info; DevicePath = path; }

    public static async Task<DriverBridgeClient> OpenForDeviceAsync(DeviceInformation original)
    {
        var ancestors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var props = new[] { "System.Devices.DeviceInstanceId", "System.Devices.Parent" };
        var current = await DeviceInformation.CreateFromIdAsync(original.Id, props, DeviceInformationKind.DeviceInterface);
        var instance = current?.Properties.GetValueOrDefault(props[0]) as string;
        for (var depth = 0; depth < 12 && !string.IsNullOrEmpty(instance) && ancestors.Add(instance); depth++)
        {
            var node = await DeviceInformation.CreateFromIdAsync(instance, props, DeviceInformationKind.Device);
            instance = node?.Properties.GetValueOrDefault(props[1]) as string;
        }
        if (ancestors.Count == 0) throw new InvalidOperationException("No se pudo verificar la identidad del mouse para el puente.");
        var candidates = await DeviceInformation.FindAllAsync(Selector);
        foreach (var candidate in candidates)
        {
            var handle = CreateFileW(candidate.Id, 0x80000000, 0, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (handle.IsInvalid) { handle.Dispose(); continue; }
            try
            {
                var bytes = new byte[DriverBridgeProtocol.InfoSize];
                if (DeviceIoControl(handle, DriverBridgeProtocol.GetInfo, IntPtr.Zero, 0, bytes, bytes.Length, out var count, IntPtr.Zero) &&
                    count == bytes.Length && DriverBridgeProtocol.TryReadInfo(bytes, out var info) && info!.MatchesAncestry(ancestors))
                    return new(handle, info, candidate.Id);
            }
            catch { handle.Dispose(); throw; }
            handle.Dispose();
        }
        throw new InvalidOperationException("No hay un puente experimental accesible para este mouse. Comprueba la instalación y la conexión del dispositivo.");
    }
    public void EnableTouch()
    {
        if (!DeviceIoControl(_handle, DriverBridgeProtocol.EnableTouch, IntPtr.Zero, 0, Array.Empty<byte>(), 0, out _, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "El controlador no pudo activar el modo táctil conservando el reporte nativo del mouse.");
    }
    public DriverBridgePacket? Read()
    {
        var bytes = new byte[DriverBridgeProtocol.PacketSize];
        if (!DeviceIoControl(_handle, DriverBridgeProtocol.GetReport, IntPtr.Zero, 0, bytes, bytes.Length, out var count, IntPtr.Zero))
        {
            var error = Marshal.GetLastWin32Error(); if (error == 259) return null;
            throw new Win32Exception(error);
        }
        if (count != bytes.Length || !DriverBridgeProtocol.TryReadPacket(bytes, out var packet) ||
            packet!.VendorId != Info.VendorId || packet.ProductId != Info.ProductId) throw new InvalidDataException("El puente devolvió un reporte inválido.");
        return packet;
    }
    public void Dispose() => _handle.Dispose();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, IntPtr input, int inputLength, [Out] byte[] output, int outputLength, out int count, IntPtr overlapped);
}
