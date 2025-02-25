using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EightSixteenEmu.Devices
{
    public class DevROM : IMappableDevice
    {
        private int badWrites = 0;
        private uint size;
        private byte[] data;
        private string romPath;

        uint IMappableDevice.Size { get { return size; } }
        public byte this[uint index] { get => data[index]; set => badWrites++; }

        public DevROM(string path, long length = -1)
        {
            romPath = path;
            if (length == -1)
            {
                length = new System.IO.FileInfo(path).Length;
            }
            size = (uint)length;
            data = System.IO.File.ReadAllBytes(path);
        }

        public void Reload()
        {
            data = System.IO.File.ReadAllBytes(romPath);
        }

        public override string ToString()
        {
            return $"ROM ({romPath})";
        }
    }
}
