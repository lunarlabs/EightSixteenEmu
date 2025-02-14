namespace EightSixteenEmu.Devices
{
    public class DevRAM : IMappableDevice
    {
        private UInt32 baseAddress;
        private UInt32 size;
        private byte[] data;

        UInt32 IMappableDevice.BaseAddress { get { return baseAddress; } }
        UInt32 IMappableDevice.Size { get { return size; } }

        public byte this[uint index] { get => data[index - baseAddress]; set => data[index - baseAddress] = value; }

        public DevRAM(uint ba, uint len)
        {
            baseAddress = ba;
            size = len;
            data = new byte[len + 1];
        }
    }
}
