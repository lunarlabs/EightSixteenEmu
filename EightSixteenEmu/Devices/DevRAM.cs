namespace EightSixteenEmu.Devices
{
    public class DevRAM(uint len) : IMappedReadDevice, IMappedWriteDevice
    {
        private uint size = len;
        private byte[] data = new byte[len];

        uint IMappableDevice.Size { get { return size; } }

        public byte this[uint index] { get => data[index]; set => data[index] = value; }

        public override string ToString()
        {
            return "RAM";
        }
        void IMappableDevice.Init()
        {
            data.Initialize();
        }
    }
}
