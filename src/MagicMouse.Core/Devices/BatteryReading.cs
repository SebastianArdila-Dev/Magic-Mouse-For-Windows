namespace MagicMouse.Core.Devices;

public sealed record BatteryReading(int? Percent, bool? IsCharging)
{
    public static BatteryReading Unknown { get; } = new(null,null);
    public double FillFraction => Percent is int value ? Math.Clamp(value,0,100)/100.0 : 0;
    public static BatteryReading FromWindowsProperties(object? life, object? combined)
    {
        static int? Value(object? value) => value switch { byte b=>b, ushort s=>s, uint i when i<=255=>(int)i, int i when i is >=0 and <=255=>i, _=>null };
        var level=Value(life); var state=Value(combined);
        if (state is >=101 and <=200) return new(state-100,true);
        if (state is >=0 and <=100) return new(level is >=0 and <=100 ? level : state,false);
        return new(level is >=0 and <=100 ? level : null,null);
    }
}
