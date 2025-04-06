using EightSixteenEmu;
using EightSixteenEmu.Devices;


namespace EmuXTesting
{
    public class MapperTests
    {
        [Fact]
        public void AddDevice_ShouldMapCorrectly()
        {
            EmuCore emu = new EmuCore();
            var device = new DevRAM(0x1000);
            emu.Mapper.AddDevice(device, 0x0000, 0x0000, 0x1000);

            // Check if the device is mapped correctly
            Assert.NotNull(emu.Mapper[0x0000]);
            Assert.NotNull(emu.Mapper[0x0FFF]);
            Assert.Null(emu.Mapper[0x1000]); // end value should be exclusive
        }

        [Fact]
        public void AddDevice_ShouldNotAllowZeroLength()
        {
            EmuCore emu = new EmuCore();
            var device = new DevRAM(0x1000);
            Assert.Throws<ArgumentOutOfRangeException>(() => emu.Mapper.AddDevice(device, 0x0000, 0x0000, 0));
        }
    }
}
