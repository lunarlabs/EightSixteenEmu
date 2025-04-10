﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EightSixteenEmu.Devices
{
    public class DevROM : IMappedReadDevice
    {
        private uint size;
        private byte[] data;
        private string romPath;

        uint IMappableDevice.Size => size;
        public byte this[uint index] { get => data[index]; }

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

        void IMappableDevice.Init() { }

        public void Reload()
        {
            data = System.IO.File.ReadAllBytes(romPath);
        }

        public override string ToString() => $"ROM ({romPath})";
    }
}
