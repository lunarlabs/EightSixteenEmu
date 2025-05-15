using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace EightSixteenEmu.Devices
{
    public class DevROM : MappableDevice
    {
        private byte[] data;
        private string romPath;

        internal override byte this[uint index] { get => data[index]; }

        public DevROM(string path, long length = -1, Guid? guid = null) : base((uint)(length == -1 ? new FileInfo(path).Length : length), AccessMode.Read, guid)
        {
            if (length == -1)
            {
                length = new FileInfo(path).Length;
            }
            if (length <= 0 || length > new FileInfo(path).Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than 0 and less than the file size.");
            }
            romPath = path;
            data = System.IO.File.ReadAllBytes(path);
        }

        public DevROM(JsonObject paramsObj, Guid? guid = null) : base(paramsObj, AccessMode.Read, guid)
        {
            if (paramsObj == null)
                throw new ArgumentNullException(nameof(paramsObj), "Params object cannot be null.");
            else
            {
                if (paramsObj["romPath"] == null)
                    throw new ArgumentNullException(nameof(paramsObj), "ROM path parameter is required.");
                else
                    romPath = paramsObj["romPath"].GetValue<string>();
            }
            data = System.IO.File.ReadAllBytes(romPath);
        }

        public void Reload()
        {
            data = System.IO.File.ReadAllBytes(romPath);
        }

        public override JsonObject? GetParams()
        {
            JsonObject result = base.GetParams() ?? new() { { "size", Size } }; // redundancy because MappableDevice.GetParams() never returns null
            result["romPath"] = romPath;
            return result;
        }

        public override string ToString() => $"ROM ({romPath})";
    }
}
