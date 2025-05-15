using System.Text.Json.Nodes;

namespace EightSixteenEmu.Devices
{
    /// <summary>
    /// Base class for all devices that can be mapped to memory.
    /// </summary>
    /// <remarks>
    /// If a device is mappable, it means that it can be accessed directly in memory.
    /// For devices that don't have address lines, use a length of 1.
    /// </remarks>
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

        internal MappableDevice(uint size, AccessMode access, Guid? guid = null) : base(guid)
        {
            if (size == 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Size must be greater than 0.");
            Size = size;
            Access = access;
        }

        internal MappableDevice(JsonObject paramsObj, AccessMode access, Guid? guid = null) : base(guid)
        {
            if (paramsObj == null)
                throw new ArgumentNullException(nameof(paramsObj), "Params object cannot be null.");
            else
            {
                if (paramsObj["size"] == null)
                    throw new ArgumentNullException(nameof(paramsObj), "Size parameter is required.");
                else if (paramsObj["size"].GetValue<uint>() == 0)
                    throw new ArgumentOutOfRangeException(nameof(paramsObj), "Size must be greater than 0.");
                else Size = paramsObj["size"].GetValue<uint>();
            }

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

        public override JsonObject? GetParams()
        {
            JsonObject result = new()
            {
                { "size", Size },
            };
            return result;
        } 
    }
}
