namespace EightSixteenEmu.Devices
{
    public class DevRAM(uint len) : MappableDevice(len, AccessMode.ReadWrite)
    {
        private byte[] data = new byte[len];

        internal override byte this[uint index] { get => data[index]; set => data[index] = value; }

        public void Write(uint index, byte value)
        {
            if (index >= Size)
                throw new ArgumentOutOfRangeException(nameof(index), "Index out of range");
            data[index] = value;
        }
        public byte Read(uint index)
        {
            if (index >= Size)
                throw new ArgumentOutOfRangeException(nameof(index), "Index out of range");
            return data[index];
        }

        public override string ToString()
        {
            return "RAM";
        }
        public override void Init()
        {
            data.Initialize();
        }

#if DEBUG
        public void Clear()
        {
            data.Initialize();
        }
#endif

    }
}
