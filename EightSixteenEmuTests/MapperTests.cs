using EightSixteenEmu;
using EightSixteenEmu.Devices;
using EightSixteenEmu.MemoryMapping;

namespace EightSixteenEmuTests;

[TestClass]
public class MapperTests
{
    [TestMethod]
    public void AddDevice_ShouldMap_Correctly()
    {
        var emu = new EmuCore();
        var device = new DevRAM(0x1000);
        emu.Mapper.AddDevice(device, 0x0000, 0x0, 0x1000);

        Assert.IsNotNull(emu.Mapper[0x0000]);
        Assert.IsNotNull(emu.Mapper[0x0fff]);
        Assert.IsNull(emu.Mapper[0x1000]);
    }

    [TestMethod]
    public void AddDevice_ShouldThrow_WhenOverlapping()
    {
        var emu = new EmuCore();
        var device1 = new DevRAM(0x1000);
        var device2 = new DevRAM(0x1000);
        emu.Mapper.AddDevice(device1, 0x0000, 0x0, 0x1000);
        Assert.ThrowsException<InvalidOperationException>(() => emu.Mapper.AddDevice(device2, 0x0fff, 0x0, 0x1000));
    }

    [TestMethod]
    public void AddDevice_ShouldAllow_Adjacent()
    {
        var emu = new EmuCore();
        var device1 = new DevRAM(0x1000);
        var device2 = new DevRAM(0x1000);
        emu.Mapper.AddDevice(device1, 0x0000, 0x0, 0x1000);
        emu.Mapper.AddDevice(device2, 0x1000, 0x0, 0x1000);
        Assert.IsNotNull(emu.Mapper[0x0000]);
        Assert.IsNotNull(emu.Mapper[0x1000]);
    }

    [TestMethod]
    public void AddDevice_ShouldThrow_WithZeroLength()
    {
        var emu = new EmuCore();
        var device = new DevRAM(0x1000);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => emu.Mapper.AddDevice(device, 0x0000, 0x0, 0x0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => emu.Mapper.AddDevice(device, 0x0000, 0x1000, -1));
    }
}
