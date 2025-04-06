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
    internal abstract class AddressingModeStrategy
    {
        internal virtual uint GetAddress(Microprocessor mpu) => throw new InvalidOperationException("This addressing mode does not support GetAddress.");
        internal virtual ushort GetOperand(Microprocessor mpu, bool isByte = true) => isByte ? mpu.ReadByte(GetAddress(mpu)) : mpu.ReadWord(GetAddress(mpu));
        internal ushort GetOperand(Microprocessor mpu, out uint fetchedAddress, bool isByte = true)
        {
            fetchedAddress = GetAddress(mpu);
            return isByte ? mpu.ReadByte(fetchedAddress) : mpu.ReadWord(fetchedAddress);
        }
        internal string _notation = "";
        
        public virtual string Notation
        {
            get => _notation;
        }

        internal static uint FullAddress(byte bank, int address) => (uint)((bank << 16) | (ushort)address);

        internal static uint FullAddress(byte bank, byte page, byte address) => (uint)((bank << 16) | (page << 8) | address);

        internal static uint GetFullPC(Microprocessor mpu)
        {
            return (uint)((mpu.RegPB << 16) | mpu.RegPC);
        }

        internal static uint CalculateDirectAddress(Microprocessor mpu, byte offset, ushort register = 0)
        {
            if (mpu.FlagE && mpu.RegDL == 0x00)
            {
                return FullAddress(0, mpu.RegDH, (byte)(offset + (byte)register));
            }
            else
            {
                //mpu.NextCycle();
                return FullAddress(0, mpu.RegDP + offset);
            }
        }
    }

    internal class AM_Immediate : AddressingModeStrategy
    {
        internal override ushort GetOperand(Microprocessor mpu, bool isByte = true)
        {
            ushort result = isByte ? mpu.ReadByte() : mpu.ReadWord();
            _notation = isByte ? $"#${result:x2}" : $"#${result:x4}";
            return result;
        }
    }

    internal class AM_Accumulator : AddressingModeStrategy
    {
        public override string Notation => "A";
        internal override ushort GetOperand(Microprocessor mpu, bool isByte = true) => isByte ? mpu.RegAL : mpu.RegA;
    }

    internal class AM_ProgramCounterRelative : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu) 
        {
            sbyte offset = (sbyte)mpu.ReadByte();
            _notation = $"{offset:+0,-#}";
            return AddressingModeStrategy.FullAddress(mpu.RegPB, mpu.RegPC + offset);
        }
        internal override ushort GetOperand(Microprocessor mpu, bool isByte = true) => throw new InvalidOperationException("This addressing mode does not support GetOperand.");
    }

    internal class AM_ProgramCounterRelativeLong : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu)
        {
            ushort offset = mpu.ReadWord();
            _notation = $"{offset:+0,-#}";
            return AddressingModeStrategy.FullAddress(mpu.RegPB, mpu.RegPC + offset);
        }
        internal override ushort GetOperand(Microprocessor mpu, bool isByte = true) => throw new InvalidOperationException("This addressing mode does not support GetOperand.");
    }

    internal class AM_Implied : AddressingModeStrategy
    {
        // this also covers the "stack" addressing mode used by PLA, PLP, etc.
        internal override ushort GetOperand(Microprocessor mpu, bool isByte = true) => throw new InvalidOperationException("This addressing mode does not support GetOperand.");
    }

    internal class AM_Direct : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            _notation = $"${offset:x2}";
            //if (mpu.RegDL != 0x00) mpu.NextCycle();
            return AddressingModeStrategy.FullAddress(0, mpu.RegDP + offset);
        }
    }

    internal class AM_DirectIndexedX : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            _notation = $"${offset:x2},X";
            //if (mpu.RegDL != 0x00) mpu.NextCycle();
            return AddressingModeStrategy.CalculateDirectAddress(mpu, offset, mpu.RegX);
        }
    }

    internal class AM_DirectIndexedY : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            _notation = $"${offset:x2},Y";
            //if (mpu.RegDL != 0x00) mpu.NextCycle();
            return AddressingModeStrategy.CalculateDirectAddress(mpu, offset, mpu.RegY);
        }
    }

    internal class AM_DirectIndirect : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            _notation = $"(${offset:x2})";
            uint pointer = AddressingModeStrategy.CalculateDirectAddress(mpu, offset);
            return AddressingModeStrategy.FullAddress(mpu.RegDB, mpu.ReadWord(pointer));
        }
    }

    internal class AM_DirectIndexedIndirect : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            _notation = $"(${offset:x2},X)";
            uint pointer = AddressingModeStrategy.CalculateDirectAddress(mpu, offset, mpu.RegX);
            return AddressingModeStrategy.FullAddress(mpu.RegDB, mpu.ReadWord(pointer));
        }
    }

    internal class AM_DirectIndirectIndexed : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            _notation = $"(${offset:x2}),Y";
            uint pointer = AddressingModeStrategy.CalculateDirectAddress(mpu, offset);
            return AddressingModeStrategy.FullAddress(mpu.RegDB, mpu.ReadWord(pointer) + mpu.RegY);
        }
    }
    internal class AM_DirectIndirectLong : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            _notation = $"[${offset:x2}]";
            return mpu.ReadAddr(AddressingModeStrategy.FullAddress(0, mpu.RegDP + offset), true);
        }
    }
    internal class AM_DirectIndirectLongIndexedY : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            _notation = $"[${offset:x2}],Y";
            return mpu.ReadAddr(AddressingModeStrategy.FullAddress(0, mpu.RegDP + offset), true) + mpu.RegY;
        }
    }
    internal class AM_Absolute : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu)
        {
            ushort address = mpu.ReadWord();
            _notation = $"${address:x4}";
            return AddressingModeStrategy.FullAddress(mpu.RegDB, address);
        }
    }
    internal class AM_AbsoluteIndexedX : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu)
        {
            ushort address = mpu.ReadWord();
            _notation = $"${address:x4}, X";
            return AddressingModeStrategy.FullAddress(mpu.RegDB, address + mpu.RegX);
        }
    }

    internal class AM_AbsoluteIndexedY : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu)
        {
            ushort address = mpu.ReadWord();
            _notation = $"${address:x4}, Y";
            return AddressingModeStrategy.FullAddress(mpu.RegDB, address + mpu.RegY);
        }
    }

    internal class AM_AbsoluteLong : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu)
        {
            uint address = mpu.ReadAddr();
            _notation = $"${address:x6}";
            return address;
        }
    }
    internal class AM_AbsoluteLongIndexedX : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu)
        {
            uint address = mpu.ReadAddr();
            _notation = $"${address:x6}, X";
            return address + mpu.RegX;
        }
    }
    internal class AM_StackRelative : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            _notation = $"${offset:x2}, S";
            return AddressingModeStrategy.FullAddress(0, mpu.RegSP + offset);
        }
    }
    internal class AM_StackRelativeIndirectIndexedY : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            _notation = $"(${offset:x2}, S), Y";
            uint pointer = AddressingModeStrategy.FullAddress(0, mpu.RegSP + offset);
            return AddressingModeStrategy.FullAddress(mpu.RegDB, mpu.ReadWord(pointer) + mpu.RegY);
        }
    }
    internal class AM_AbsoluteIndirect : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu)
        {
            ushort address = mpu.ReadWord();
            _notation = $"(${address:x4})";
            return AddressingModeStrategy.FullAddress(mpu.RegPB, mpu.ReadWord(AddressingModeStrategy.FullAddress(0, address)));
        }
        internal override ushort GetOperand(Microprocessor mpu, bool isByte = true) => throw new InvalidOperationException("This addressing mode does not support GetOperand.");
    }
    internal class AM_AbsoluteIndirectLong : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu)
        {
            ushort address = mpu.ReadWord();
            _notation = $"[${address:x4}]";
            return mpu.ReadAddr(AddressingModeStrategy.FullAddress(0, address), true);
        }
        internal override ushort GetOperand(Microprocessor mpu, bool isByte = true) => throw new InvalidOperationException("This addressing mode does not support GetOperand.");
    }
    internal class AM_AbsoluteIndexedIndirect : AddressingModeStrategy
    {
        internal override uint GetAddress(Microprocessor mpu)
        {
            ushort address = mpu.ReadWord();
            _notation = $"(${address:x4}, X)";
            return AddressingModeStrategy.FullAddress(mpu.RegPB, mpu.ReadWord(AddressingModeStrategy.FullAddress(0, address + mpu.RegX)));
        }
        internal override ushort GetOperand(Microprocessor mpu, bool isByte = true) => throw new InvalidOperationException("This addressing mode does not support GetOperand.");
    }
    internal class AM_BlockMove : AddressingModeStrategy
    {
        internal override ushort GetOperand(Microprocessor mpu, bool isByte = true)
        {
            ushort source = mpu.ReadWord();
            ushort dest = mpu.ReadWord();
            _notation = $"${source:x4},${dest:x4}";
            return (ushort)((source << 8) | dest);
            // remember to split this into two bytes when using it
        }
    }
}
