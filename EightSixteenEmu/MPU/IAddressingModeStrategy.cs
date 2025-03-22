using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EightSixteenEmu.MPU
{
    internal interface IAddressingModeStrategy
    {
        internal abstract uint GetAddress(Microprocessor mpu);
        internal abstract ushort GetOperand(Microprocessor mpu, bool isByte = true);
        public string Notation
        {
            get;
            protected set;
        }
    }
}
