using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EightSixteenEmu;
using EightSixteenEmu.Devices;

namespace EightSixteenEmuTests
{
    [TestClass()]
    public class MicroprocessorTests
    {

        [TestMethod()]
        [Timeout(10000)]
        public void LoadStoreTest()
        {
            var emu = new EmuCore();
            var ram = new DevRAM(0x8000);
            emu.Mapper.AddDevice(ram, 0x0000);
            var rom = new DevROM("LoadStoreTest.rom", 0x8000);
            emu.Mapper.AddDevice(rom, 0x8000);
            var mp = emu.MPU;
            mp.ExecuteOperation();
            Microprocessor.Status status = mp.GetStatus();
            Assert.AreEqual(0x8000, status.PC);
            while (!mp.Stopped)
            {
                mp.ExecuteOperation();
            }
            status = mp.GetStatus();
            Console.WriteLine(status);
            Assert.AreEqual(0x12, status.A);
            Assert.AreEqual(0x34, status.X);
            Assert.AreEqual(0x56, status.Y);
        }
    }
}