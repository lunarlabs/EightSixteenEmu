using EightSixteenEmu;
using EightSixteenEmu.Devices;


namespace EmuXTesting
{
    public class MapperTests
    {
        [Fact]
        public void AddDevice_ShouldMapCorrectly()
        {
            var device = new DevRAM(0x1000);
            EmuCore.Instance.Mapper.Clear();
            EmuCore.Instance.Mapper.AddDevice(device, 0x0000, 0x0000, 0x1000);

            // Check if the device is mapped correctly
            Assert.NotNull(EmuCore.Instance.Mapper[0x0000]);
            Assert.NotNull(EmuCore.Instance.Mapper[0x0FFF]);
            Assert.Null(EmuCore.Instance.Mapper[0x1000]); // end value should be exclusive
        }

        [Fact]
        public void AddDevice_ShouldNotAllowZeroLength()
        {
            var device = new DevRAM(0x1000);
            EmuCore.Instance.Mapper.Clear();
            Assert.Throws<ArgumentOutOfRangeException>(() => EmuCore.Instance.Mapper.AddDevice(device, 0x0000, 0x0000, 0));
        }
    }
}
