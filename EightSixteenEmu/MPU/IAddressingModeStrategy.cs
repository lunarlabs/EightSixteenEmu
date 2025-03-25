/*    _____      __   __  _____      __               ____          
 *   / __(_)__ _/ /  / /_/ __(_)_ __/ /____ ___ ___  / __/_ _  __ __
 *  / _// / _ `/ _ \/ __/\ \/ /\ \ / __/ -_) -_) _ \/ _//  ' \/ // /
 * /___/_/\_, /_//_/\__/___/_//_\_\\__/\__/\__/_//_/___/_/_/_/\_,_/ 
 *       /___/                                                      
 * 
 *  W65C816S microprocessor emulator
 *  Copyright (C) 2025 Matthias Lamers
 *  Released under GNUGPLv2, see LICENSE.txt for details.
 *  
 *  Based on the W65C816S, designed by Bill Mensch,
 *  and manufactured by Western Design Center (https://wdc65xx.com)
 *  Most opcode info courtesy of http://6502.org/tutorials/65c816opcodes.html
 *  
 *  Addressing Modes
 */

namespace EightSixteenEmu.MPU
{
    // WARN: When using one of these in an opcode command, only use GetAddress or GetOperand, not both.
    // Otherwise, you'll cause the addressing mode to run again, and you'll mess up the program counter.
    internal interface IAddressingModeStrategy
    {
        internal uint GetAddress(Microprocessor mpu) => throw new InvalidOperationException("This addressing mode does not support GetAddress.");
        internal ushort GetOperand(Microprocessor mpu, bool isByte = true) => isByte ? mpu.ReadByte(GetAddress(mpu)) : mpu.ReadWord(GetAddress(mpu));
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

        internal static sealed uint CalculateDirectAddress(Microprocessor mpu, byte offset, ushort register = 0)
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
        internal ushort GetOperand(Microprocessor mpu, bool isByte = true) => throw new InvalidOperationException("This addressing mode does not support GetOperand.");
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
        internal ushort GetOperand(Microprocessor mpu, bool isByte = true) => throw new InvalidOperationException("This addressing mode does not support GetOperand.");
    }

    class AM_Implied : IAddressingModeStrategy
    {
        // this also covers the "stack" addressing mode used by PLA, PLP, etc.
        public string Notation { get; private set; } = "";
        internal ushort GetOperand(Microprocessor mpu, bool isByte = true) => throw new InvalidOperationException("This addressing mode does not support GetOperand.");
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

    class AM_DirectIndexedX : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "${0:x2},X";
        internal uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            Notation = $"${offset:x2},X";
            if (mpu.RegDL != 0x00) mpu.NextCycle();
            return IAddressingModeStrategy.CalculateDirectAddress(mpu, offset, mpu.RegX);
        }
    }

    class AM_DirectIndexedY : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "${0:x2},Y";
        internal uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            Notation = $"${offset:x2},X";
            if (mpu.RegDL != 0x00) mpu.NextCycle();
            return IAddressingModeStrategy.CalculateDirectAddress(mpu, offset, mpu.RegY);
        }
    }

    class AM_DirectIndirect : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "(${0:x2})";
        internal uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            Notation = $"(${offset:x2})";
            uint pointer = IAddressingModeStrategy.CalculateDirectAddress(mpu, offset);
            return IAddressingModeStrategy.FullAddress(mpu.RegDB, mpu.ReadWord(pointer));
        }
    }

    class AM_DirectIndexedIndirect : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "(${0:x2},X)";
        internal uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            Notation = $"(${offset:x2},X)";
            uint pointer = IAddressingModeStrategy.CalculateDirectAddress(mpu, offset, mpu.RegX);
            return IAddressingModeStrategy.FullAddress(mpu.RegDB, mpu.ReadWord(pointer));
        }
    }

    class AM_DirectIndirectIndexed : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "(${0:x2}),Y";
        internal uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            Notation = $"(${offset:x2}),Y";
            uint pointer = IAddressingModeStrategy.CalculateDirectAddress(mpu, offset);
            return IAddressingModeStrategy.FullAddress(mpu.RegDB, mpu.ReadWord(pointer) + mpu.RegY);
        }
    }
    class AM_DirectIndirectLong : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "[${0:x2}]";
        internal uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            Notation = $"[${offset:x2}]";
            return mpu.ReadAddr(IAddressingModeStrategy.FullAddress(0, mpu.RegDP + offset), true);
        }
    }
    class AM_DirectIndirectLongIndexedY : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "[${0:x2}],Y";
        internal uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            Notation = $"[${offset:x2}],Y";
            return mpu.ReadAddr(IAddressingModeStrategy.FullAddress(0, mpu.RegDP + offset), true) + mpu.RegY;
        }
    }
    class AM_Absolute : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "${0:x4}";
        internal uint GetAddress(Microprocessor mpu)
        {
            ushort address = mpu.ReadWord();
            Notation = $"${address:x4}";
            return IAddressingModeStrategy.FullAddress(mpu.RegDB, address);
        }
    }
    class AM_AbsoluteIndexedX : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "${0:x4}, X";
        internal uint GetAddress(Microprocessor mpu)
        {
            ushort address = mpu.ReadWord();
            Notation = $"${address:x4}, X";
            return IAddressingModeStrategy.FullAddress(mpu.RegDB, address + mpu.RegX);
        }
    }

    class AM_AbsoluteIndexedY : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "${0:x4}, Y";
        internal uint GetAddress(Microprocessor mpu)
        {
            ushort address = mpu.ReadWord();
            Notation = $"${address:x4}, Y";
            return IAddressingModeStrategy.FullAddress(mpu.RegDB, address + mpu.RegY);
        }
    }

    class AM_AbsoluteLong : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "${0:x6}";
        internal uint GetAddress(Microprocessor mpu)
        {
            uint address = mpu.ReadAddr();
            Notation = $"${address:x6}";
            return address;
        }
    }
    class AM_AbsoluteLongIndexedX : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "${0:x6}, X";
        internal uint GetAddress(Microprocessor mpu)
        {
            uint address = mpu.ReadAddr();
            Notation = $"${address:x6}, X";
            return address + mpu.RegX;
        }
    }
    class AM_StackRelative : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "${0:x2}, S";
        internal uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            Notation = $"${offset:x2}, S";
            return IAddressingModeStrategy.FullAddress(0, mpu.RegSP + offset);
        }
    }
    class AM_StackRelativeIndirectIndexedY : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "(${0:x2}, S), Y";
        internal uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            Notation = $"(${offset:x2}, S), Y";
            uint pointer = IAddressingModeStrategy.FullAddress(0, mpu.RegSP + offset);
            return IAddressingModeStrategy.FullAddress(mpu.RegDB, mpu.ReadWord(pointer) + mpu.RegY);
        }
    }
    class AM_AbsoluteIndirect : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "(${0:x4})";
        internal uint GetAddress(Microprocessor mpu)
        {
            ushort address = mpu.ReadWord();
            Notation = $"(${address:x4})";
            return IAddressingModeStrategy.FullAddress(mpu.RegPB, mpu.ReadWord(IAddressingModeStrategy.FullAddress(0, address)));
        }
        internal ushort GetOperand(Microprocessor mpu, bool isByte = true) => throw new InvalidOperationException("This addressing mode does not support GetOperand.");
    }
    class AM_AbsoluteIndirectLong : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "[${0:x4}]";
        internal uint GetAddress(Microprocessor mpu)
        {
            ushort address = mpu.ReadWord();
            Notation = $"[${address:x4}]";
            return mpu.ReadAddr(IAddressingModeStrategy.FullAddress(0, address), true);
        }
        internal ushort GetOperand(Microprocessor mpu, bool isByte = true) => throw new InvalidOperationException("This addressing mode does not support GetOperand.");
    }
    class AM_AbsoluteIndexedIndirect : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "(${0:x4}, X)";
        internal uint GetAddress(Microprocessor mpu)
        {
            ushort address = mpu.ReadWord();
            Notation = $"(${address:x4}, X)";
            return IAddressingModeStrategy.FullAddress(mpu.RegPB, mpu.ReadWord(IAddressingModeStrategy.FullAddress(0, address + mpu.RegX)));
        }
        internal ushort GetOperand(Microprocessor mpu, bool isByte = true) => throw new InvalidOperationException("This addressing mode does not support GetOperand.");
    }
    class AM_BlockMove : IAddressingModeStrategy
    {
        public string Notation { get; private set; } = "${0:x4},${1:x4}";
        internal ushort GetOperand(Microprocessor mpu, bool isByte = true)
        {
            ushort source = mpu.ReadWord();
            ushort dest = mpu.ReadWord();
            Notation = $"${source:x4},${dest:x4}";
            return (ushort)((source << 8) | dest);
            // remember to split this into two bytes when using it
        }
    }
}
