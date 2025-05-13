using System.Text.Json.Nodes;

namespace EightSixteenEmu.Devices
{
    public abstract class MappableDevice : Device
    {

        public uint Size { get; }
        public AccessMode Access { get; }
        public enum AccessMode
        {
            Read,
            Write,
            ReadWrite
        }

        internal MappableDevice(uint size, AccessMode access)
        {
            if (size == 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Size must be greater than 0.");
            Size = size;
            Access = access;
        }

        internal virtual byte this[uint index]
        {
            get
            {
                if (Access == AccessMode.Write)
                    throw new InvalidOperationException("Cannot read from a write-only device.");
                else
                    throw new NotImplementedException("Read operation not implemented.");
            }
            set
            {
                if (Access == AccessMode.Read)
                    throw new InvalidOperationException("Cannot write to a read-only device.");
                else
                    throw new NotImplementedException("Write operation not implemented.");
            }
        }

        public override JsonObject ToJson()
        {
            JsonObject result = base.ToJson();
            result["params"] = new JsonObject
            {
                { "size", Size },
            };
            return result;
        }
    }
}
