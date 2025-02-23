namespace EightSixteenEmu.Devices
{
    public class DevRAM(uint ba, uint len) : IMappableDevice
    {
        private uint baseAddress = ba;
        private uint size = len;
        private byte[] data = new byte[len];

        uint IMappableDevice.BaseAddress { get { return baseAddress; } }
        uint IMappableDevice.Size { get { return size; } }

        public byte this[uint index] { get => data[index - baseAddress]; set => data[index - baseAddress] = value; }

        public override string ToString()
        {
            return "RAM";
        }
    }
}
