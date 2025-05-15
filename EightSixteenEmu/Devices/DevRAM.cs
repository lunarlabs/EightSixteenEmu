using System.Text.Json.Nodes;

namespace EightSixteenEmu.Devices
{
    /// <summary>
    /// A basic RAM device that encapsulates a byte array.
    /// </summary>
    public class DevRAM : MappableDevice
    {

        private byte[] data;
        /// <param name="len">The size of the RAM.</param>
        /// <param name="guid">The GUID of the device, used in serialization.</param>
        public DevRAM(uint len, Guid? guid = null) : base(len, AccessMode.ReadWrite, guid)
        {
            if (len == 0)
                throw new ArgumentOutOfRangeException(nameof(len), "Size must be greater than 0.");
            data = new byte[len];
        }

        public DevRAM(JsonObject? paramsObj, Guid? guid = null) : base(paramsObj, AccessMode.ReadWrite, guid)
        {
            // base constructor will throw if paramsObj is null or size is 0
            data = new byte[Size];
        }

        internal override byte this[uint index] { get => data[index]; set => data[index] = value; }

        public void Write(uint index, byte value)
        {
            if (index >= Size)
                throw new ArgumentOutOfRangeException(nameof(index), "Index out of range");
            data[index] = value;
        }
        public void Write(uint index, byte[] value)
        {
            if (index + value.Length > Size)
                throw new ArgumentOutOfRangeException(nameof(index), "Index out of range");
            Array.Copy(value, 0, data, index, value.Length);
        }

        public byte Read(uint index)
        {
            if (index >= Size)
                throw new ArgumentOutOfRangeException(nameof(index), "Index out of range");
            return data[index];
        }

        public byte[] bytes
        {
            get
            {
                byte[] result = new byte[Size];
                Array.Copy(data, result, Size);
                return result;
            }
        }

        public override JsonObject? GetState()
        {
            string dumpFile = $"{guid}.ramdump";
            System.IO.File.WriteAllBytes(dumpFile, data);
            JsonObject result = new()
            {
                { "dumpFile", dumpFile }
            };
            return result;
        }

        public override void SetState(JsonObject? state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state), "State object cannot be null.");
            if (state["dumpFile"] == null)
                throw new ArgumentNullException(nameof(state), "Dump file parameter is required.");
            string dumpFile = state["dumpFile"].GetValue<string>();
            if (!System.IO.File.Exists(dumpFile))
                throw new FileNotFoundException($"Dump file '{dumpFile}' not found.", dumpFile);
            data = System.IO.File.ReadAllBytes(dumpFile);
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
