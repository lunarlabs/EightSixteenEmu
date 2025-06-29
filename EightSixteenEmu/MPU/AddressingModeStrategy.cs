﻿/*    _____      __   __  _____      __               ____          
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

    // INFO: In most cases, using QueueAddress will put the calculated address in InternalAddress.
    internal abstract class AddressingModeStrategy
    {
        [Obsolete("Use QueueAddress instead. This method will be removed in a future version.")]
        internal virtual uint GetAddress(Microprocessor mpu) => throw new InvalidOperationException("This addressing mode does not support GetAddress.");
        [Obsolete("Use QueueOperand instead. This method will be removed in a future version.")]
        internal virtual ushort GetOperand(Microprocessor mpu, bool isByte = true, bool wrap = false) => isByte ? mpu.ReadByte(GetAddress(mpu)) : mpu.ReadWord(GetAddress(mpu), wrap);
        internal virtual void QueueAddress(Microprocessor mpu) => throw new InvalidOperationException("This addressing mode does not support QueueAddress.");
        internal virtual void QueueOperand(Microprocessor mpu, bool isByte = true, bool wrap = false) => throw new InvalidOperationException("This addressing mode does not support QueueOperand.");
        internal ushort GetOperand(Microprocessor mpu, out uint fetchedAddress, bool isByte = true, bool wrap = false)
        {
            fetchedAddress = GetAddress(mpu);
            return isByte ? mpu.ReadByte(fetchedAddress) : mpu.ReadWord(fetchedAddress, wrap);
        }

        internal static uint FullAddress(byte bank, int address) => (uint)((bank << 16) | (ushort)address);

        internal static uint FullAddress(byte bank, byte page, byte address) => (uint)((bank << 16) | (page << 8) | address);

        internal static uint GetFullPC(Microprocessor mpu)
        {
            return (uint)((mpu.RegPB << 16) | mpu.RegPC);
        }

        internal static uint CalculateDirectAddress(Microprocessor mpu, byte offset, ushort register = 0)
        {
            if (mpu.RegDL != 0x00) mpu.InternalCycle();
            if (mpu.FlagE && mpu.RegDL == 0x00)
            {
                return FullAddress(0, mpu.RegDH, (byte)(offset + (byte)register));
            }
            else
            {
                return FullAddress(0, mpu.RegDP + offset + register);
            }
        }

        internal static bool CrossesPageBoundaries(uint a, uint b) => (byte)(a >> 8) != (byte)(b >> 8);
    }

    internal class AM_Immediate : AddressingModeStrategy
    {
        [Obsolete]
        internal override ushort GetOperand(Microprocessor mpu, bool isByte = true, bool wrap = false)
        {
            ushort result = isByte ? mpu.ReadByte() : mpu.ReadWord();
            return result;
        }
        internal override void QueueOperand(Microprocessor mpu, bool isByte = true, bool wrap = false)
        {
            mpu.EnqueueMicroOp(new MicroOpReadToAndAdvancePC(MicroOpCode.RegByteLocation.IDL));
            if (!isByte)
            {
                mpu.EnqueueMicroOp(new MicroOpReadToAndAdvancePC(MicroOpCode.RegByteLocation.IDH));
            }
        }
    }

    internal class AM_Accumulator : AddressingModeStrategy
    {

        [Obsolete]
        internal override ushort GetOperand(Microprocessor mpu, bool isByte = true, bool wrap = false)
        {
            mpu.InternalCycle();
            return isByte ? mpu.RegAL : mpu.RegA;
        }
    }

    internal class AM_ProgramCounterRelative : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            sbyte offset = (sbyte)mpu.ReadByte();
            return FullAddress(mpu.RegPB, mpu.RegPC + offset);
        }

        [Obsolete]
        internal override ushort GetOperand(Microprocessor mpu, bool isByte = true, bool wrap = false) => throw new InvalidOperationException("This addressing mode does not support GetOperand.");
    }

    internal class AM_ProgramCounterRelativeLong : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            ushort offset = mpu.ReadWord();
            return FullAddress(mpu.RegPB, mpu.RegPC + offset);
        }

        [Obsolete]
        internal override ushort GetOperand(Microprocessor mpu, bool isByte = true, bool wrap = false) => throw new InvalidOperationException("This addressing mode does not support GetOperand.");
    }

    internal class AM_Implied : AddressingModeStrategy
    {
        // this also covers the "stack" addressing mode used by PLA, PLP, etc.
        [Obsolete]
        internal override ushort GetOperand(Microprocessor mpu, bool isByte = true, bool wrap = false) => throw new InvalidOperationException("This addressing mode does not support GetOperand.");
    }

    internal class AM_Direct : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            if (mpu.RegDL != 0x00) mpu.InternalCycle();
            return FullAddress(0, mpu.RegDP + offset);
        }
    }

    internal class AM_DirectIndexedX : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            mpu.InternalCycle();
            return CalculateDirectAddress(mpu, offset, mpu.RegX);
        }
    }

    internal class AM_DirectIndexedY : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            if (mpu.RegDL != 0x00
                || mpu.CurrentOpCode == W65C816.OpCode.STX
                || mpu.CurrentOpCode == W65C816.OpCode.LDX) mpu.InternalCycle();
            return CalculateDirectAddress(mpu, offset, mpu.RegY);
        }
    }

    internal class AM_DirectIndirect : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            uint pointer = CalculateDirectAddress(mpu, offset);
            return FullAddress(mpu.RegDB, mpu.ReadWord(pointer, true));
        }
    }

    internal class AM_DirectIndexedIndirect : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            mpu.InternalCycle();
            uint pointer = CalculateDirectAddress(mpu, offset, mpu.RegX);
            return FullAddress(mpu.RegDB, mpu.ReadWord(pointer, true));
        }
    }

    internal class AM_DirectIndirectIndexed : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            uint pointer = CalculateDirectAddress(mpu, offset);
            uint destination = (uint)((mpu.RegDB << 16) + mpu.ReadWord(pointer, true) + mpu.RegY);
            if (!mpu.FlagX 
                || mpu.CurrentOpCode == W65C816.OpCode.STA 
                || CrossesPageBoundaries(destination, destination - mpu.RegY)) mpu.InternalCycle();
            return destination;
        }
    }
    internal class AM_DirectIndirectLong : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            if (mpu.RegDL != 0x00) mpu.InternalCycle();
            return mpu.ReadAddr(FullAddress(0, mpu.RegDP + offset), true);
        }
    }
    internal class AM_DirectIndirectLongIndexedY : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            if (mpu.RegDL != 0x00) mpu.InternalCycle();
            return mpu.ReadAddr(FullAddress(0, mpu.RegDP + offset), true) + mpu.RegY;
        }
    }
    internal class AM_Absolute : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            ushort address = mpu.ReadWord();
            return FullAddress(mpu.RegDB, address);
        }
    }
    internal class AM_AbsoluteIndexedX : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            ushort address = mpu.ReadWord();
            // return FullAddress(mpu.RegDB, address + mpu.RegX);
            uint destination = FullAddress(mpu.RegDB, address) + mpu.RegX;
            if (!mpu.FlagX
                || mpu.CurrentOpCode == W65C816.OpCode.ASL
                || mpu.CurrentOpCode == W65C816.OpCode.DEC
                || mpu.CurrentOpCode == W65C816.OpCode.INC
                || mpu.CurrentOpCode == W65C816.OpCode.LSR
                || mpu.CurrentOpCode == W65C816.OpCode.ROL
                || mpu.CurrentOpCode == W65C816.OpCode.ROR
                || mpu.CurrentOpCode == W65C816.OpCode.STZ
                || mpu.CurrentOpCode == W65C816.OpCode.STA
                || CrossesPageBoundaries(address, destination)) mpu.InternalCycle();
            return destination;
        }
    }

    internal class AM_AbsoluteIndexedY : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            ushort address = mpu.ReadWord();
            uint destination = FullAddress(mpu.RegDB, address) + mpu.RegY;
            if (!mpu.FlagX
                || mpu.CurrentOpCode == W65C816.OpCode.ASL
                || mpu.CurrentOpCode == W65C816.OpCode.DEC
                || mpu.CurrentOpCode == W65C816.OpCode.INC
                || mpu.CurrentOpCode == W65C816.OpCode.LSR
                || mpu.CurrentOpCode == W65C816.OpCode.ROL
                || mpu.CurrentOpCode == W65C816.OpCode.ROR
                || mpu.CurrentOpCode == W65C816.OpCode.STA
                || CrossesPageBoundaries(address, destination)) mpu.InternalCycle();
            return destination;
        }
    }

    internal class AM_AbsoluteLong : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            uint address = mpu.ReadAddr();
            return address;
        }
    }
    internal class AM_AbsoluteLongIndexedX : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            uint address = mpu.ReadAddr();
            return address + mpu.RegX;
        }
    }
    internal class AM_StackRelative : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            mpu.InternalCycle();
            return FullAddress(0, mpu.RegSP + offset);
        }
    }
    internal class AM_StackRelativeIndirectIndexedY : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            byte offset = mpu.ReadByte();
            mpu.InternalCycle();
            uint pointer = FullAddress(0, mpu.RegSP + offset);
            uint destination = FullAddress(mpu.RegDB, mpu.ReadWord(pointer, true)) + mpu.RegY;
            mpu.InternalCycle();
            return destination;
        }
    }
    internal class AM_AbsoluteIndirect : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            ushort address = mpu.ReadWord();
            return FullAddress(mpu.RegPB, mpu.ReadWord(FullAddress(0, address), true));
        }

        [Obsolete]
        internal override ushort GetOperand(Microprocessor mpu, bool isByte = true, bool wrap = false) => throw new InvalidOperationException("This addressing mode does not support GetOperand.");
    }
    internal class AM_AbsoluteIndirectLong : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            ushort address = mpu.ReadWord();
            return mpu.ReadAddr(FullAddress(0, address), true);
        }

        [Obsolete]
        internal override ushort GetOperand(Microprocessor mpu, bool isByte = true, bool wrap = false) => throw new InvalidOperationException("This addressing mode does not support GetOperand.");
    }
    internal class AM_AbsoluteIndexedIndirect : AddressingModeStrategy
    {
        [Obsolete]
        internal override uint GetAddress(Microprocessor mpu)
        {
            ushort address = mpu.ReadWord();
            return FullAddress(mpu.RegPB, mpu.ReadWord(FullAddress(mpu.RegPB, address + mpu.RegX), true));
        }

        [Obsolete]
        internal override ushort GetOperand(Microprocessor mpu, bool isByte = true, bool wrap = false) => throw new InvalidOperationException("This addressing mode does not support GetOperand.");
    }
    internal class AM_BlockMove : AddressingModeStrategy
    {
        [Obsolete]
        internal override ushort GetOperand(Microprocessor mpu, bool isByte = true, bool wrap = false)
        {
            byte source = mpu.ReadByte();
            byte dest = mpu.ReadByte();
            return (ushort)((source << 8) | dest);
            // remember to split this into two bytes when using it
        }
    }
}
