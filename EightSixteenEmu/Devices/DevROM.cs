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

        public DevROM(string path, long length = -1) : base((uint)(length == -1 ? new FileInfo(path).Length : length), AccessMode.Read)
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

        public void Reload()
        {
            data = System.IO.File.ReadAllBytes(romPath);
        }

        public override JsonObject ToJson()
        {
            JsonObject result = base.ToJson();
            result["params"] = new JsonObject
            {
                { "path", romPath },
                { "size", Size }
            };
            return result;
        }

        public override string ToString() => $"ROM ({romPath})";
    }
}
