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
        private uint baseAddress;
        private uint size;
        private byte[] data;
        private string romPath;

        uint IMappableDevice.BaseAddress { get { return baseAddress; } }
        uint IMappableDevice.Size { get { return size; } }
        public byte this[uint index] { get => data[index - baseAddress]; set => badWrites++; }

        public DevROM(string path, uint ba, long length = -1)
        {
            baseAddress = ba;
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
    }
}
