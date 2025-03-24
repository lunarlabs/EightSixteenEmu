using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace EightSixteenEmu.MPU
{
    // WARN: When using one of these in an opcode command, only use GetAddress or GetOperand, not both.
    // Otherwise, you'll cause the addressing mode to run again, and you'll mess up the program counter.
    internal interface IAddressingModeStrategy
    {
        internal uint GetAddress(Microprocessor mpu) => throw new InvalidOperationException("This addressing mode does not support GetAddress.");
        internal ushort GetOperand(Microprocessor mpu, bool isByte = true) => throw new InvalidOperationException("This addressing mode does not support GetOperand.");
        public string Notation
        {
            get;
        }

        internal static sealed uint FullAddress(byte bank, int address) => (uint)((bank << 16) | (ushort)address);

        internal static sealed uint FullAddress(byte bank, byte page, byte address) => (uint)((bank << 16) | (page << 8) | address);

        internal static sealed uint GetFullPC(Microprocessor mpu)
        {
            return (uint)((mpu.RegPB << 16) | mpu.RegPC);
        }

        internal static sealed uint CalculateDirectAddress(Microprocessor mpu, byte offset, ushort register)
        {
            if (mpu.FlagE && mpu.RegDL == 0x00)
            {
                return FullAddress(0, mpu.RegDH, (byte)(offset + (byte)register));
            }
            else
            {
                mpu.NextCycle();
                return FullAddress(0, mpu.RegDP + offset);
            }
        }
    }

    class AM_Immediate : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "#${0:x2}";
        internal ushort GetOperand(Microprocessor mpu, bool isByte = true)
        {
            ushort result = isByte ? mpu.ReadByte() : mpu.ReadWord();
            Notation = isByte ? $"#${result:x2}" : $"#${result:x4}";
            return result;
        }
    }

    class AM_Accumulator : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "A";
        internal ushort GetOperand(Microprocessor mpu, bool isByte = true) => isByte ? mpu.RegAL : mpu.RegA;
    }

    class AM_ProgramCounterRelative : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "${0:x4}";
        internal uint GetAddress(Microprocessor mpu) 
        {
            sbyte offset = (sbyte)mpu.ReadByte();
            Notation = $"{offset:+0,-#}";
            return IAddressingModeStrategy.FullAddress(mpu.RegPB, mpu.RegPC + offset);
        }
    }

    class AM_ProgramCounterRelativeLong : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "${0:x6}";
        internal uint GetAddress(Microprocessor mpu)
        {
            ushort offset = mpu.ReadWord();
            Notation = $"{offset:+0,-#}";
            return IAddressingModeStrategy.FullAddress(mpu.RegPB, mpu.RegPC + offset);
        }
    }

    class AM_Implied : IAddressingModeStrategy
    {
        // this also covers the "stack" addressing mode used by PLA, PLP, etc.
        public string Notation { get; private set; } = "";
    }

    class AM_Direct : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "${0:x2}";
        internal uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            Notation = $"${offset:x2}";
            if (mpu.RegDL != 0x00) mpu.NextCycle();
            return IAddressingModeStrategy.FullAddress(0, mpu.RegDP + offset);
        }
    }
}
