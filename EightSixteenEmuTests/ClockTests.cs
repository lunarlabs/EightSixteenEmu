using Microsoft.VisualStudio.TestTools.UnitTesting;
using EightSixteenEmu;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EightSixteenEmu.Tests
{
    [TestClass()]
    public class ClockTests
    {
        [TestMethod()]
        public void ClockTest()
        {
            var clock = new Clock(TimeSpan.FromSeconds(1), true);
            Assert.IsNotNull(clock);
        }
    }
}