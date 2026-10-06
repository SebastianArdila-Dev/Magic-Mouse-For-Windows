using System.Buffers.Binary;
using System.Text;
using MagicMouse.Core.Drivers;
using Xunit;

namespace MagicMouse.Core.Tests;

public sealed class DriverBridgeTests
{
    private static byte[] Packet()
    {
        var data = new byte[200];
        BinaryPrimitives.WriteUInt32LittleEndian(data, DriverBridgeProtocol.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4),1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(12),14);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(16),5);
        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(24),DateTimeOffset.UtcNow.ToFileTime());
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(32),0x004C);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(34),0x0269);
        data[40]=0x12; return data;
    }
    [Fact]
    public void Kernel_packet_offsets_match_the_managed_reader()
    {
        Assert.True(DriverBridgeProtocol.TryReadPacket(Packet(),out var packet));
        Assert.Equal(5UL,packet!.Sequence); Assert.Equal(0x004C,packet.VendorId); Assert.Equal(0x0269,packet.ProductId);
        Assert.Equal(14,packet.Report.Length); Assert.False(packet.Discontinuity);
    }
    [Theory]
    [InlineData(0,0)] [InlineData(4,2)] [InlineData(8,2)] [InlineData(12,0)] [InlineData(12,161)] [InlineData(32,0)] [InlineData(34,0)] [InlineData(36,1)] [InlineData(40,0x28)]
    public void Invalid_headers_ids_lengths_and_reserved_bits_are_rejected(int offset,int value)
    {
        var data=Packet(); data[offset]=(byte)value;
        Assert.False(DriverBridgeProtocol.TryReadPacket(data,out _));
    }
    [Fact]
    public void Truncated_packets_and_invalid_filetimes_are_rejected()
    {
        var data=Packet(); Assert.False(DriverBridgeProtocol.TryReadPacket(data.AsSpan(0,199),out _));
        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(24),-1);
        Assert.False(DriverBridgeProtocol.TryReadPacket(data,out _));
    }
    [Fact]
    public void Overflow_flag_is_exposed_to_reset_gesture_state()
    {
        var data=Packet(); data[8]=1;
        Assert.True(DriverBridgeProtocol.TryReadPacket(data,out var packet)); Assert.True(packet!.Discontinuity);
    }
    [Fact]
    public void Driver_identity_matches_a_parent_exactly_not_another_mouse_of_the_same_model()
    {
        var data=new byte[528]; BinaryPrimitives.WriteUInt32LittleEndian(data,DriverBridgeProtocol.Magic); data[4]=1;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(8),0x004C);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(10),0x0269);
        Encoding.Unicode.GetBytes("BTHENUM\\MOUSE_A").CopyTo(data,16);
        Assert.True(DriverBridgeProtocol.TryReadInfo(data,out var info));
        Assert.True(info!.MatchesAncestry(["HID\\CHILD", "bthenum\\mouse_a"]));
        Assert.False(info.MatchesAncestry(["BTHENUM\\MOUSE_AB", "BTHENUM\\MOUSE_B"]));
    }
    [Fact]
    public void Unterminated_and_empty_device_identity_are_rejected()
    {
        var data=new byte[528]; BinaryPrimitives.WriteUInt32LittleEndian(data,DriverBridgeProtocol.Magic); data[4]=1;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(8),0x004C); BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(10),0x0269);
        Assert.False(DriverBridgeProtocol.TryReadInfo(data,out _));
        for(var i=16;i<data.Length;i+=2)data[i]=65;
        Assert.False(DriverBridgeProtocol.TryReadInfo(data,out _));
    }
    [Fact]
    public void Random_untrusted_packets_never_throw()
    {
        var random=new Random(41);
        for(var i=0;i<5000;i++) {var data=new byte[random.Next(0,600)]; random.NextBytes(data); DriverBridgeProtocol.TryReadPacket(data,out _); DriverBridgeProtocol.TryReadInfo(data,out _);}
    }
}
