namespace MagicMouse.Core.Appearance;

/// <summary>Produces a Windows .cur with an alpha bitmap and a matching transparency mask.</summary>
public static class CursorFile
{
    public static byte[] Create(string style, double percent, uint accent = 0x007AFF)
    {
        if (!double.IsFinite(percent) || percent is < 70 or > 180) throw new ArgumentOutOfRangeException(nameof(percent));
        var size = (int)Math.Round(32 * percent / 100 * (style == "Grande" ? 1.25 : 1));
        var points = style == "macOS"
            ? new (double X, double Y)[] { (.09,.06),(.09,.86),(.31,.66),(.46,.97),(.60,.90),(.45,.61),(.77,.61) }
            : new (double X, double Y)[] { (.09,.06),(.09,.88),(.30,.66),(.46,.98),(.60,.90),(.45,.61),(.77,.61) };
        var stride = ((size + 31) / 32) * 4;
        var pixels = new byte[size * size * 4]; var mask = new byte[stride * size];
        for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
        {
            double a = 0, red = 0, green = 0, blue = 0;
            for (var sy = 0; sy < 4; sy++) for (var sx = 0; sx < 4; sx++)
            {
                var px = (x + (sx + .5) / 4) / size; var py = (y + (sy + .5) / 4) / size;
                var inside = Contains(points, px, py); var edge = Distance(points, px, py) < .035;
                if (!inside && !edge) continue;
                var color = edge ? (style == "Minimal" ? 0x18181Bu : 0xFFFFFFu) : style switch { "Minimal" => 0xFFFFFFu, "Personalizado" => accent, _ => 0x18181Bu };
                a++; red += (color >> 16) & 255; green += (color >> 8) & 255; blue += color & 255;
            }
            var offset = ((size - 1 - y) * size + x) * 4;
            if (a > 0) { pixels[offset] = (byte)(blue / a); pixels[offset + 1] = (byte)(green / a); pixels[offset + 2] = (byte)(red / a); pixels[offset + 3] = (byte)Math.Round(255 * a / 16); }
            else mask[(size - 1 - y) * stride + x / 8] |= (byte)(0x80 >> (x % 8));
        }
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0); writer.Write((ushort)2); writer.Write((ushort)1);
        writer.Write((byte)size); writer.Write((byte)size); writer.Write((byte)0); writer.Write((byte)0);
        writer.Write((ushort)Math.Round(size * .09)); writer.Write((ushort)Math.Round(size * .06));
        writer.Write((uint)(40 + pixels.Length + mask.Length)); writer.Write(22u);
        writer.Write(40u); writer.Write(size); writer.Write(size * 2); writer.Write((ushort)1); writer.Write((ushort)32);
        writer.Write(0u); writer.Write((uint)(pixels.Length + mask.Length)); writer.Write(0); writer.Write(0); writer.Write(0u); writer.Write(0u);
        writer.Write(pixels); writer.Write(mask); return stream.ToArray();
    }
    private static bool Contains((double X, double Y)[] polygon, double x, double y)
    {
        var inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            if ((polygon[i].Y > y) != (polygon[j].Y > y) && x < (polygon[j].X - polygon[i].X) * (y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X) inside = !inside;
        return inside;
    }
    private static double Distance((double X, double Y)[] p, double x, double y)
    {
        var minimum = double.MaxValue;
        for (var i = 0; i < p.Length; i++) { var b = p[(i + 1) % p.Length]; var dx = b.X - p[i].X; var dy = b.Y - p[i].Y; var t = Math.Clamp(((x - p[i].X) * dx + (y - p[i].Y) * dy) / (dx * dx + dy * dy), 0, 1); minimum = Math.Min(minimum, Math.Sqrt(Math.Pow(x - p[i].X - t * dx, 2) + Math.Pow(y - p[i].Y - t * dy, 2))); }
        return minimum;
    }
}
