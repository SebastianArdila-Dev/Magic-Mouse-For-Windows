using System.Buffers.Binary;
using System.Text;
using MagicMouse.Core.Touch;

namespace MagicMouse.Core.Drivers;

public sealed record DriverBridgeInfo(ushort VendorId, ushort ProductId, ushort Firmware, string InstanceId)
{
    public bool MatchesAncestry(IEnumerable<string> instanceIds) => instanceIds.Any(id => string.Equals(id, InstanceId, StringComparison.OrdinalIgnoreCase));
}
public sealed record DriverBridgePacket(ulong Sequence, DateTimeOffset Timestamp, ushort VendorId, ushort ProductId, bool Discontinuity, byte[] Report);

/// <summary>Versioned, bounded ABI shared with the experimental KMDF bridge.</summary>
public static class DriverBridgeProtocol
{
    public const uint Magic = 0x424D4D53, Version = 1;
    public const int PacketSize = 200, InfoSize = 528;
    public const uint GetInfo = 0x226000, GetReport = 0x226004, EnableTouch = 0x226008;
    private static bool Known(ushort vendor, ushort product) => vendor is 0x05AC or 0x004C && product is 0x030D or 0x0269 or 0x0323;
    private static bool Header(ReadOnlySpan<byte> data) => data.Length >= 8 && BinaryPrimitives.ReadUInt32LittleEndian(data) == Magic && BinaryPrimitives.ReadUInt32LittleEndian(data[4..]) == Version;

    public static bool TryReadInfo(ReadOnlySpan<byte> data, out DriverBridgeInfo? info)
    {
        info = null;
        if (data.Length != InfoSize || !Header(data)) return false;
        var vendor = BinaryPrimitives.ReadUInt16LittleEndian(data[8..]);
        var product = BinaryPrimitives.ReadUInt16LittleEndian(data[10..]);
        if (!Known(vendor, product) || BinaryPrimitives.ReadUInt16LittleEndian(data[14..]) != 0) return false;
        var text = Encoding.Unicode.GetString(data[16..]); var end = text.IndexOf('\0');
        if (end <= 0) return false;
        info = new(vendor, product, BinaryPrimitives.ReadUInt16LittleEndian(data[12..]), text[..end]); return true;
    }
    public static bool TryReadPacket(ReadOnlySpan<byte> data, out DriverBridgePacket? packet)
    {
        packet = null;
        if (data.Length != PacketSize || !Header(data)) return false;
        var flags = BinaryPrimitives.ReadUInt32LittleEndian(data[8..]);
        var length = BinaryPrimitives.ReadUInt32LittleEndian(data[12..]);
        var vendor = BinaryPrimitives.ReadUInt16LittleEndian(data[32..]);
        var product = BinaryPrimitives.ReadUInt16LittleEndian(data[34..]);
        if ((flags & ~1u) != 0 || length is 0 or > 160 || !Known(vendor, product) || BinaryPrimitives.ReadUInt32LittleEndian(data[36..]) != 0) return false;
        DateTimeOffset timestamp;
        try { timestamp = DateTimeOffset.FromFileTime(BinaryPrimitives.ReadInt64LittleEndian(data[24..])); }
        catch (ArgumentOutOfRangeException) { return false; }
        var report = data.Slice(40, (int)length);
        if (report[0] != (product == 0x030D ? 0x29 : 0x12) || !AppleMouseReportParser.TryParse(report, timestamp, out _)) return false;
        packet = new(BinaryPrimitives.ReadUInt64LittleEndian(data[16..]), timestamp, vendor, product, (flags & 1) != 0, report.ToArray()); return true;
    }
}
