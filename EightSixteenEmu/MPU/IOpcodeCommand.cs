using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EightSixteenEmu.MPU
{
    internal interface IOpcodeCommand
    {
        internal abstract void Execute(Microprocessor mpu, IAddressingModeStrategy addressingMode);
    }
}
