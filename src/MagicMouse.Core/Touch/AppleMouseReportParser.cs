namespace MagicMouse.Core.Touch;

public sealed record AppleMouseReport(TouchFrame Frame, int Buttons, int DeltaX, int DeltaY);

/// <summary>Strict decoder of the documented 0x29/0x12 mouse wire layouts, not trackpad reports.</summary>
public static class AppleMouseReportParser
{
    public static bool TryParse(ReadOnlySpan<byte> bytes, DateTimeOffset timestamp, out AppleMouseReport? result)
    {
        result = null; if (bytes.IsEmpty) return false;
        int header, buttons, dx, dy;
        if (bytes[0] == 0x29)
        {
            header = 6; if (bytes.Length < header) return false;
            buttons = bytes[3] & 3; dx = Signed(bytes[1] | ((bytes[3] >> 2 & 3) << 8), 10); dy = Signed(bytes[2] | ((bytes[3] >> 4 & 3) << 8), 10);
        }
        else if (bytes[0] == 0x12)
        {
            if (bytes.Length < 8) return false; buttons = bytes[1] & 3;
            dx = Signed(bytes[2] | bytes[3] << 8, 16); dy = Signed(bytes[4] | bytes[5] << 8, 16);
            header = bytes.Length == 8 ? 8 : 14;
        }
        else return false;
        if (bytes.Length < header || (bytes.Length - header) % 8 != 0 || (bytes.Length - header) / 8 > 15) return false;
        var contacts = new List<TouchContact>(); var ids = new HashSet<int>();
        for (var offset = header; offset < bytes.Length; offset += 8)
        {
            var record = bytes.Slice(offset, 8); var state = record[7] & 0xF0;
            if (state == 0 || state == 0x20) continue;
            if (state is not (0x30 or 0x40)) return false;
            var id = (record[5] >> 6) | ((record[6] & 3) << 2); if (!ids.Add(id)) return false;
            var x = Signed(record[0] | (record[1] & 15) << 8, 12);
            var y = -Signed(record[1] >> 4 | record[2] << 4, 12);
            contacts.Add(new(id, Math.Clamp((x + 1100.0) / 2358, 0, 1), Math.Clamp((y + 1589.0) / 3636, 0, 1)));
        }
        result = new(new(timestamp, contacts), buttons, dx, dy); return true;
    }
    private static int Signed(int value, int bits) => (value & (1 << (bits - 1))) != 0 ? value - (1 << bits) : value;
}
