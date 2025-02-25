using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EightSixteenEmu.Devices;

namespace EightSixteenEmu.Tests
{
    [TestClass()]
    public class MicroprocessorTests
    {
        [TestMethod()]
        public void MicroprocessorTest()
        {
            var emu = new EmuCore();
            var ram = new DevRAM(0x10000);
            emu.AddDevice(ram, 0x0000);
            Assert.IsNotNull(emu.MPU);
        }

        [TestMethod()]
        public void InitializeTest()
        {
            var emu = new EmuCore();
            var ram = new DevRAM(0x10000);
            emu.AddDevice(ram, 0x000000);
            emu.MPU.ExecuteOperation();
            Microprocessor.Status status = emu.MPU.GetStatus();
            Console.WriteLine(status);
            Assert.AreEqual(status.Cycles, 2);
            Assert.AreEqual(status.SP, 0x0100);
        }

        [TestMethod()]
        public void DeviceListTest()
        {
            var emu = new EmuCore();
            var ram = new DevRAM(0x8000);
            Assert.ThrowsException<IndexOutOfRangeException>(() => Console.WriteLine(ram[0x10000]));
            emu.AddDevice(ram, 0x000000);
            var rom = new DevROM("LoadStoreTest.rom", 0x8000);
            emu.AddDevice(rom, 0x8000);
            Console.WriteLine(emu.DeviceList());
            Assert.IsNotNull(emu.DeviceList());
        }

        [TestMethod()]
        [Timeout(10000)]
        public void LoadStoreTest()
        {
            var emu = new EmuCore();
            var ram = new DevRAM(0x8000);
            emu.AddDevice(ram, 0x0000);
            var rom = new DevROM("LoadStoreTest.rom", 0x8000);
            emu.AddDevice(rom, 0x8000);
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
        [TestMethod()]
        public void AddNonOverlappingDevicesTest()
        {
            var emu = new EmuCore();
            var ram = new DevRAM(0x8000); // RAM ends at 0x7FFF
            emu.AddDevice(ram, 0x0000);
            var rom = new DevROM("LoadStoreTest.rom", 0x8000); // ROM starts at 0x8000
            emu.AddDevice(rom, 0x8000);
            Assert.IsNotNull(emu.DeviceList());
        }
        [TestMethod()]
        public void AddOverlappingDevicesTest()
        {
            var emu = new EmuCore();
            var ram = new DevRAM(0x8000); // RAM ends at 0x7FFF
            emu.AddDevice(ram, 0x0000);
            var overlappingRom = new DevROM("LoadStoreTest.rom", 0x4000); // ROM starts at 0x4000, overlaps with RAM
            Assert.ThrowsException<InvalidOperationException>(() => emu.AddDevice(overlappingRom, 0x4000));
        }
    }
}