namespace EightSixteenEmu.Devices
{
    public class DevRAM : IMappableDevice
    {
        private uint baseAddress;
        private uint size;
        private byte[] data;

        uint IMappableDevice.BaseAddress { get { return baseAddress; } }
        uint IMappableDevice.Size { get { return size; } }

        public byte this[uint index] { get => data[index - baseAddress]; set => data[index - baseAddress] = value; }

        public DevRAM(uint ba, uint len)
        {
            baseAddress = ba;
            size = len;
            data = new byte[len + 1];
        }
    }
}
