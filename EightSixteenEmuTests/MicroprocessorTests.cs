using Microsoft.VisualStudio.TestTools.UnitTesting;
using EightSixteenEmu;
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
            var deviceList = new List<IMappableDevice>();
            var ram = new DevRAM(0, 0x10000);
            deviceList.Add(ram);
            var mp = new Microprocessor(deviceList);
            Assert.IsNotNull(mp);
        }

        [TestMethod()]
        [Timeout(10000)]
        public void InitializeTest()
        {
            var deviceList = new List<IMappableDevice>();
            var ram = new DevRAM(0, 0x10000);
            deviceList.Add(ram);
            var mp = new Microprocessor(deviceList);
            mp.ExecuteOperation();
            Microprocessor.Status status = mp.GetStatus();
            Console.WriteLine(status);
            Assert.AreEqual(status.Cycles, 2);
            Assert.AreEqual(status.SP, 0x0100);

        }
    }
}