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
 *  Micro-operations
 *  
 *  NOTE: These are queued by the calling instruction/address mode, so any
 *  exceptions thrown means there's a screw-up in the queueing logic in the
 *  calling opcode... (uncomfortable laughter)
 */

namespace EightSixteenEmu.MPU
{
    internal abstract class MicroOpCode(MicroOpCode.OpCycleType type = MicroOpCode.OpCycleType.NoCycle)
    {
        internal enum RegByteLocation
        {
            Zero,
            MD, // External D/DB Buffer
            IAL, // Internal Address Low
            IAH, // Internal Address High
            IDL, // Internal Data Low
            IDH, // Internal Data High
            A, // lower 8 bits of the accumulator
            B, // upper 8 bits of the accumulator
            DBR, // Data Bank Register
            DL, // Direct Page Low
            DH, // Direct Page High
            IR, // Instruction Register
            K, // Program Bank Register
            PCL, // Program Counter Low
            PCH, // Program Counter High
            P, // Processor Status Register
            SL, // Stack Pointer Low
            SH, // Stack Pointer High
            XL, // Index Register X Low
            XH, // Index Register X High
            YL, // Index Register Y Low
            YH, // Index Register Y High
        }

        internal enum RegWordLocation
        {
            Zero,
            IA, // Internal Address (16-bit)
            ID, // Internal Data (16-bit)
            C, // Complete 16-bit accumulator (A | B << 8)
            D, // Direct Page Register
            PC, // Program Counter
            S, // Stack Pointer
            X, // Index Register X
            Y, // Index Register Y
        }

        internal enum OpCycleType
        {
            NoCycle, // Internal operation that does not advance the cycle
            Internal, // Internal cycle, no external memory access
            Read, // Read cycle, external memory access to read data
            Write, // Write cycle, external memory access to write data
        }

        internal struct RegLocation
        {
            public RegByteLocation? ByteLoc { get; }
            public RegWordLocation? WordLoc { get; }
            public bool IsByte => ByteLoc.HasValue;
            public bool IsWord => WordLoc.HasValue;

            public RegLocation(RegByteLocation loc) { ByteLoc = loc; WordLoc = null; }
            public RegLocation(RegWordLocation loc) { WordLoc = loc; ByteLoc = null; }
        }

        internal OpCycleType CycleType => type;

        internal abstract void Execute(Microprocessor mpu);

        internal static (RegByteLocation low, RegByteLocation high) ByteLocationsFromWordLocations(RegWordLocation source)
        {
            return source switch
            {
                RegWordLocation.Zero => (RegByteLocation.Zero, RegByteLocation.Zero),
                RegWordLocation.IA => (RegByteLocation.IAL, RegByteLocation.IAH),
                RegWordLocation.ID => (RegByteLocation.IDL, RegByteLocation.IDH),
                RegWordLocation.C => (RegByteLocation.A, RegByteLocation.B),
                RegWordLocation.D => (RegByteLocation.DL, RegByteLocation.DH),
                RegWordLocation.PC => (RegByteLocation.PCL, RegByteLocation.PCH),
                RegWordLocation.S => (RegByteLocation.SL, RegByteLocation.SH),
                RegWordLocation.X => (RegByteLocation.XL, RegByteLocation.XH),
                RegWordLocation.Y => (RegByteLocation.YL, RegByteLocation.YH),
                _ => throw new ArgumentOutOfRangeException(nameof(source), "Invalid source register location.")
            };
        }

        internal static void SetByte(Microprocessor mpu, byte value, RegByteLocation location)
        {
            switch (location)
            {
                case RegByteLocation.Zero:
                    // Use as a bit bucket, doesn't affect any register
                    break;
                case RegByteLocation.MD:
                    mpu.RegMD = value;
                    break;
                case RegByteLocation.IAL:
                    mpu.InternalAddress = (ushort)((mpu.InternalAddress & 0xFF00) | value);
                    break;
                case RegByteLocation.IAH:
                    mpu.InternalAddress = (ushort)((mpu.InternalAddress & 0x00FF) | ((ushort)value << 8));
                    break;
                case RegByteLocation.A:
                    mpu.RegAL = value;
                    break;
                case RegByteLocation.B:
                    mpu.RegAH = value;
                    break;
                case RegByteLocation.DBR:
                    mpu.RegDB = value;
                    break;
                case RegByteLocation.DL:
                    mpu.RegDL = value;
                    break;
                case RegByteLocation.DH:
                    mpu.RegDH = value;
                    break;
                case RegByteLocation.K:
                    mpu.RegPB = value;
                    break;
                case RegByteLocation.PCL:
                    mpu.RegPC = (ushort)((mpu.RegPC & 0xFF00) | value);
                    break;
                case RegByteLocation.PCH:
                    mpu.RegPC = (ushort)((mpu.RegPC & 0x00FF) | ((ushort)value << 8));
                    break;
                case RegByteLocation.P:
                    mpu.RegSR = (Microprocessor.StatusFlags)value;
                    break;
                case RegByteLocation.SL:
                    mpu.RegSL = value;
                    break;
                case RegByteLocation.SH:
                    mpu.RegSH = value;
                    break;
                case RegByteLocation.XL:
                    mpu.RegXL = value;
                    break;
                case RegByteLocation.XH:
                    mpu.RegXH = value;
                    break;
                case RegByteLocation.YL:
                    mpu.RegYL = value;
                    break;
                case RegByteLocation.YH:
                    mpu.RegYH = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(location), "Invalid destination register location.");
            }
        }

        internal static void SetWord(Microprocessor mpu, ushort value, RegWordLocation location)
        {
            switch (location)
            {
                case RegWordLocation.Zero:
                    // Use as a bit bucket, doesn't affect any register
                    break;
                case RegWordLocation.IA:
                    mpu.InternalAddress = value;
                    break;
                case RegWordLocation.ID:
                    mpu.InternalData = value;
                    break;
                case RegWordLocation.C:
                    mpu.RegA = value;
                    break;
                case RegWordLocation.D:
                    mpu.RegDP = value;
                    break;
                case RegWordLocation.PC:
                    mpu.RegPC = value;
                    break;
                case RegWordLocation.S:
                    mpu.RegSP = value;
                    break;
                case RegWordLocation.X:
                    mpu.RegX = value;
                    break;
                case RegWordLocation.Y:
                    mpu.RegY = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(location), "Invalid destination register location.");
            }
        }

        internal static byte GetByte(Microprocessor mpu, RegByteLocation location)
        {
            return location switch
            {
                RegByteLocation.Zero => 0, // Special zero pseudo-register, always returns 0 (for STZ etc)
                RegByteLocation.MD => mpu.RegMD,
                RegByteLocation.IAL => (byte)mpu.InternalAddress,
                RegByteLocation.IAH => (byte)(mpu.InternalAddress >> 8),
                RegByteLocation.A => mpu.RegAL,
                RegByteLocation.B => mpu.RegAH,
                RegByteLocation.DBR => mpu.RegDB,
                RegByteLocation.DL => mpu.RegDL,
                RegByteLocation.DH => mpu.RegDH,
                RegByteLocation.K => mpu.RegPB,
                RegByteLocation.PCL => (byte)mpu.RegPC,
                RegByteLocation.PCH => (byte)(mpu.RegPC >> 8),
                RegByteLocation.P => (byte)mpu.RegSR,
                RegByteLocation.SL => mpu.RegSL,
                RegByteLocation.SH => mpu.RegSH,
                RegByteLocation.XL => mpu.RegXL,
                RegByteLocation.XH => mpu.RegXH,
                RegByteLocation.YL => mpu.RegYL,
                RegByteLocation.YH => mpu.RegYH,
                _ => throw new ArgumentOutOfRangeException(nameof(location), "Invalid source register location.")
            };
        }

        internal static ushort GetWord(Microprocessor mpu, RegWordLocation location)
        {
            return location switch
            {
                RegWordLocation.Zero => 0, // Special zero pseudo-register, always returns 0 (for STZ etc)
                RegWordLocation.IA => mpu.InternalAddress,
                RegWordLocation.ID => mpu.InternalData,
                RegWordLocation.C => mpu.RegA,
                RegWordLocation.D => mpu.RegDP,
                RegWordLocation.PC => mpu.RegPC,
                RegWordLocation.S => mpu.RegSP,
                RegWordLocation.X => mpu.RegX,
                RegWordLocation.Y => mpu.RegY,
                _ => throw new ArgumentOutOfRangeException(nameof(location), "Invalid source register location.")
            };
        }

        internal static void ReadByteAndAdvancePC(Microprocessor mpu, RegByteLocation destination)
        {
            mpu.ByteRead((uint)((mpu.RegPB << 16) | mpu.RegPC));
            SetByte(mpu, mpu.RegMD, destination);
            mpu.RegPC = (ushort)((mpu.RegPC + 1) & 0xFFFF); // Increment PC after reading
        }
    }

    internal class MicroOpNop : MicroOpCode
    {
        internal override void Execute(Microprocessor mpu)
        {
            // No operation, just a placeholder
        }
    }
    
    internal class MicroOpInternalCycle : MicroOpCode
    {
        internal MicroOpInternalCycle() : base(OpCycleType.Internal)
        {
            // This operation is used for internal cycles that do not require external memory access.
            // It does not require any parameters as it simply represents an internal operation.
        }
        internal override void Execute(Microprocessor mpu)
        {
            // No operation, just a placeholder for internal cycles (VDA and VPA low)
        }
    }

    // TODO: Change ReadByte and WriteByte in Microprocessor to only affect _regMD...
    internal class MicroOpMoveByte : MicroOpCode
    {
        private readonly RegByteLocation _source;
        private readonly RegByteLocation _destination;
        private readonly bool _setNZFlags; // If true, set the N and Z flags based on the value moved
        internal MicroOpMoveByte(RegByteLocation source, RegByteLocation destination, bool setNZFlags = false)
        {
            _source = source;
            _destination = destination;
            _setNZFlags = setNZFlags;
        }
        internal override void Execute(Microprocessor mpu)
        {
            byte value = GetByte(mpu, _source);
            SetByte(mpu, value, _destination);
            if (_setNZFlags) // If set, update the N and Z flags based on the value moved
            {
                mpu.RegSR = (Microprocessor.StatusFlags)((mpu.RegSR & ~(Microprocessor.StatusFlags.N | Microprocessor.StatusFlags.Z)) |
                    (value == 0 ? Microprocessor.StatusFlags.Z : 0) |
                    (value >= 0x80 ? Microprocessor.StatusFlags.N : 0));
            }
        }
    }
    internal class MicroOpMoveWord : MicroOpCode
    {
        private readonly RegWordLocation _source;
        private readonly RegWordLocation _destination;
        private readonly bool _setNZFlags;
        internal MicroOpMoveWord(RegWordLocation source, RegWordLocation destination, bool setNZFlags = false)
        {
            _source = source;
            _destination = destination;
            _setNZFlags = setNZFlags;
        }
        internal override void Execute(Microprocessor mpu)
        {
            ushort value = GetWord(mpu, _source);
            SetWord(mpu, value, _destination);
        }
    }

    internal class MicroOpSetByte : MicroOpCode
    {
        private readonly byte _value;
        private readonly RegByteLocation _destination;
        internal MicroOpSetByte(byte value, RegByteLocation destination)
        {
            _value = value;
            _destination = destination;
        }
        internal override void Execute(Microprocessor mpu)
        {
            SetByte(mpu, _value, _destination);
        }
    }

    internal class MicroOpSetWord : MicroOpCode
    {
        private readonly ushort _value;
        private readonly RegWordLocation _destination;
        internal MicroOpSetWord(ushort value, RegWordLocation destination)
        {
            _value = value;
            _destination = destination;
        }
        internal override void Execute(Microprocessor mpu)
        {
            SetWord(mpu, _value, _destination);
        }
    }

    internal class MicroOpReadTo : MicroOpCode
    {
        private readonly uint _address;
        private readonly RegByteLocation _destination;
        internal MicroOpReadTo(uint address, RegByteLocation destination) : base(OpCycleType.Read)
        {
            if (destination == RegByteLocation.MD)
                throw new ArgumentException("Cannot move MD to itself (bad enqueue?)", nameof(destination));
            _address = address;
            _destination = destination;
        }
        internal override void Execute(Microprocessor mpu)
        {
            mpu.ByteRead(_address);
            SetByte(mpu, mpu.RegMD, _destination);
        }
    }

    internal class MicroOpReadToAndAdvancePC : MicroOpCode
    {
        private readonly RegByteLocation _destination;
        internal MicroOpReadToAndAdvancePC(RegByteLocation destination) : base(OpCycleType.Read)
        {
            if (destination == RegByteLocation.MD)
                throw new ArgumentException("Cannot move MD to itself (bad enqueue?)", nameof(destination));
            _destination = destination;
        }
        internal override void Execute(Microprocessor mpu)
        {
            ReadByteAndAdvancePC(mpu, _destination);
        }
    }

    internal class MicroOpWriteFrom : MicroOpCode
    {
        private readonly uint _address;
        private readonly RegByteLocation _source;
        internal MicroOpWriteFrom(uint address, RegByteLocation source) : base(OpCycleType.Write)
        {
            if (source == RegByteLocation.MD)
                throw new ArgumentException("Cannot move MD to itself (bad enqueue?)", nameof(source));
            _address = address;
            _source = source;
        }
        internal override void Execute(Microprocessor mpu)
        {
            byte value = GetByte(mpu, _source);
            SetByte(mpu, value, RegByteLocation.MD); // Set MD to the value to write
            mpu.ByteWrite(_address);
        }
    }

    internal class MicroOpChangeFlags : MicroOpCode
    {
        private readonly Microprocessor.StatusFlags _flagsToSet;
        private readonly Microprocessor.StatusFlags _flagsToClear;
        internal MicroOpChangeFlags(Microprocessor.StatusFlags flagsToSet, Microprocessor.StatusFlags flagsToClear) : base(OpCycleType.NoCycle)
        {
            if ((flagsToSet & flagsToClear) != 0)
                throw new ArgumentException("Flags to set and clear cannot overlap.", nameof(flagsToSet));
            _flagsToSet = flagsToSet;
            _flagsToClear = flagsToClear;
        }
        internal override void Execute(Microprocessor mpu)
        {
            // Clear specified flags
            mpu.RegSR &= ~_flagsToClear;
            // Set specified flags
            mpu.RegSR |= _flagsToSet;
        }
    }

    internal class MicroOpFetchAndDecode : MicroOpCode
    {
        internal MicroOpFetchAndDecode() : base(OpCycleType.Read)
        {
            // This operation is used to fetch the next instruction byte and decode it.
            // It does not require any parameters as it operates on the current program counter.
        }
        internal override void Execute(Microprocessor mpu)
        {
            ReadByteAndAdvancePC(mpu, RegByteLocation.IR);
            mpu.DecodeInstruction();
            if ((mpu.Instruction != null) && (mpu.AddressingMode != null))
            {
                mpu.Instruction.Enqueue(mpu, mpu.AddressingMode);
            }
            else
            {
                // DecodeInstruction failed. If this pops, it's a bug.
                throw new InvalidOperationException("Failed to decode instruction or addressing mode.");
            }
        }
    }

    internal class MicroOpInstructionFinished : MicroOpCode
    {
        // This operation is used to signal that the current instruction has finished execution.
        // It does not require any parameters as it simply indicates the end of the instruction cycle.        
        internal override void Execute(Microprocessor mpu)
        {
            mpu.ClearInstruction();
        }
    }

    internal class MicroOpLogicAndByte : MicroOpCode
    {
        private readonly RegByteLocation _src1;
        private readonly RegByteLocation _src2;
        private readonly RegByteLocation _destination;
        private readonly bool _setNZFlags; // If true, set the N and Z flags based on the result
        internal MicroOpLogicAndByte(RegByteLocation src1, RegByteLocation src2, RegByteLocation destination, bool setNZFlags = false)
        {
            _src1 = src1;
            _src2 = src2;
            _destination = destination;
            _setNZFlags = setNZFlags;
        }
        internal override void Execute(Microprocessor mpu)
        {
            byte value1 = GetByte(mpu, _src1);
            byte value2 = GetByte(mpu, _src2);
            byte result = (byte)(value1 & value2);
            SetByte(mpu, result, _destination);
            if (_setNZFlags) // If set, update the N and Z flags based on the result
            {
                mpu.RegSR = (Microprocessor.StatusFlags)((mpu.RegSR & ~(Microprocessor.StatusFlags.N | Microprocessor.StatusFlags.Z)) |
                    (result == 0 ? Microprocessor.StatusFlags.Z : 0) |
                    (result >= 0x80 ? Microprocessor.StatusFlags.N : 0));
            }
        }
    }

    internal class MicroOpLogicAndWord : MicroOpCode
    {
        private readonly RegWordLocation _src1;
        private readonly RegWordLocation _src2;
        private readonly RegWordLocation _destination;
        private readonly bool _setNZFlags; // If true, set the N and Z flags based on the result
        internal MicroOpLogicAndWord(RegWordLocation src1, RegWordLocation src2, RegWordLocation destination, bool setNZFlags = false)
        {
            _src1 = src1;
            _src2 = src2;
            _destination = destination;
            _setNZFlags = setNZFlags;
        }
        internal override void Execute(Microprocessor mpu)
        {
            ushort value1 = GetWord(mpu, _src1);
            ushort value2 = GetWord(mpu, _src2);
            ushort result = (ushort)(value1 & value2);
            SetWord(mpu, result, _destination);
            if (_setNZFlags) // If set, update the N and Z flags based on the result
            {
                mpu.RegSR = (Microprocessor.StatusFlags)((mpu.RegSR & ~(Microprocessor.StatusFlags.N | Microprocessor.StatusFlags.Z)) |
                    (result == 0 ? Microprocessor.StatusFlags.Z : 0) |
                    (result >= 0x8000 ? Microprocessor.StatusFlags.N : 0));
            }
        }
    }

    internal class MicroOpLogicOrByte : MicroOpCode
    {
        private readonly RegByteLocation _src1;
        private readonly RegByteLocation _src2;
        private readonly RegByteLocation _destination;
        private readonly bool _setNZFlags; // If true, set the N and Z flags based on the result
        internal MicroOpLogicOrByte(RegByteLocation src1, RegByteLocation src2, RegByteLocation destination, bool setNZFlags = false)
        {
            _src1 = src1;
            _src2 = src2;
            _destination = destination;
            _setNZFlags = setNZFlags;
        }
        internal override void Execute(Microprocessor mpu)
        {
            byte value1 = GetByte(mpu, _src1);
            byte value2 = GetByte(mpu, _src2);
            byte result = (byte)(value1 | value2);
            SetByte(mpu, result, _destination);
            if (_setNZFlags) // If set, update the N and Z flags based on the result
            {
                mpu.RegSR = (Microprocessor.StatusFlags)((mpu.RegSR & ~(Microprocessor.StatusFlags.N | Microprocessor.StatusFlags.Z)) |
                    (result == 0 ? Microprocessor.StatusFlags.Z : 0) |
                    (result >= 0x80 ? Microprocessor.StatusFlags.N : 0));
            }
        }
    }

    internal class MicroOpLogicOrWord : MicroOpCode
    {
        private readonly RegWordLocation _src1;
        private readonly RegWordLocation _src2;
        private readonly RegWordLocation _destination;
        private readonly bool _setNZFlags; // If true, set the N and Z flags based on the result
        internal MicroOpLogicOrWord(RegWordLocation src1, RegWordLocation src2, RegWordLocation destination, bool setNZFlags = false)
        {
            _src1 = src1;
            _src2 = src2;
            _destination = destination;
            _setNZFlags = setNZFlags;
        }
        internal override void Execute(Microprocessor mpu)
        {
            ushort value1 = GetWord(mpu, _src1);
            ushort value2 = GetWord(mpu, _src2);
            ushort result = (ushort)(value1 | value2);
            SetWord(mpu, result, _destination);
            if (_setNZFlags) // If set, update the N and Z flags based on the result
            {
                mpu.RegSR = (Microprocessor.StatusFlags)((mpu.RegSR & ~(Microprocessor.StatusFlags.N | Microprocessor.StatusFlags.Z)) |
                    (result == 0 ? Microprocessor.StatusFlags.Z : 0) |
                    (result >= 0x8000 ? Microprocessor.StatusFlags.N : 0));
            }
        }
    }

    internal class MicroOpLogicXorByte : MicroOpCode
    {
        private readonly RegByteLocation _src1;
        private readonly RegByteLocation _src2;
        private readonly RegByteLocation _destination;
        private readonly bool _setNZFlags; // If true, set the N and Z flags based on the result
        internal MicroOpLogicXorByte(RegByteLocation src1, RegByteLocation src2, RegByteLocation destination, bool setNZFlags = false)
        {
            _src1 = src1;
            _src2 = src2;
            _destination = destination;
            _setNZFlags = setNZFlags;
        }
        internal override void Execute(Microprocessor mpu)
        {
            byte value1 = GetByte(mpu, _src1);
            byte value2 = GetByte(mpu, _src2);
            byte result = (byte)(value1 ^ value2);
            SetByte(mpu, result, _destination);
            if (_setNZFlags) // If set, update the N and Z flags based on the result
            {
                mpu.RegSR = (Microprocessor.StatusFlags)((mpu.RegSR & ~(Microprocessor.StatusFlags.N | Microprocessor.StatusFlags.Z)) |
                    (result == 0 ? Microprocessor.StatusFlags.Z : 0) |
                    (result >= 0x80 ? Microprocessor.StatusFlags.N : 0));
            }
        }
    }

    internal class MicroOpLogicXorWord : MicroOpCode
    {
        private readonly RegWordLocation _src1;
        private readonly RegWordLocation _src2;
        private readonly RegWordLocation _destination;
        private readonly bool _setNZFlags; // If true, set the N and Z flags based on the result
        internal MicroOpLogicXorWord(RegWordLocation src1, RegWordLocation src2, RegWordLocation destination, bool setNZFlags = false)
        {
            _src1 = src1;
            _src2 = src2;
            _destination = destination;
            _setNZFlags = setNZFlags;
        }
        internal override void Execute(Microprocessor mpu)
        {
            ushort value1 = GetWord(mpu, _src1);
            ushort value2 = GetWord(mpu, _src2);
            ushort result = (ushort)(value1 ^ value2);
            SetWord(mpu, result, _destination);
            if (_setNZFlags) // If set, update the N and Z flags based on the result
            {
                mpu.RegSR = (Microprocessor.StatusFlags)((mpu.RegSR & ~(Microprocessor.StatusFlags.N | Microprocessor.StatusFlags.Z)) |
                    (result == 0 ? Microprocessor.StatusFlags.Z : 0) |
                    (result >= 0x8000 ? Microprocessor.StatusFlags.N : 0));
            }
        }
    }

    internal class MicroOpAddByte : MicroOpCode
    {
        private readonly RegByteLocation _src1;
        private readonly RegByteLocation _src2;
        private readonly RegByteLocation _destination;
        private readonly bool _useCarry; // If true, use the carry flag in the addition
        private readonly bool _useDecimal; // If true, use decimal mode for addition
        private readonly bool _setOverflow; // If true, set the overflow flag based on the result
        private readonly bool _setNZFlags; // If true, set the N and Z flags based on the result
        internal MicroOpAddByte(RegByteLocation src1, RegByteLocation src2, RegByteLocation destination, bool useCarry = false, bool useDecimal = false, bool setOverflow = false, bool setNZFlags = false)
        {
            _src1 = src1;
            _src2 = src2;
            _destination = destination;
            _useCarry = useCarry;
            _useDecimal = useDecimal;
            _setOverflow = setOverflow;
            _setNZFlags = setNZFlags;
        }
        internal override void Execute(Microprocessor mpu)
        {
            byte value1 = GetByte(mpu, _src1);
            byte value2 = GetByte(mpu, _src2);
            byte carry = (byte)(_useCarry ? (mpu.RegSR & Microprocessor.StatusFlags.C) : 0);
            bool carryResult = false;
            int result;
            if (_useDecimal)
            {
                byte lo = (byte)((value1 & 0x0F) + (value2 & 0x0F) + carry);
                byte hi = (byte)(((value1 >> 4) & 0x0F) + ((value2 >> 4) & 0x0F));
                if (lo > 9)
                {
                    lo -= 10; // Adjust for BCD
                    hi++;
                }
                if (hi > 9)
                {
                    hi -= 10; // Adjust for BCD
                    carryResult = true; // Set carry if high nibble exceeds 9
                }
                result = (lo & 0x0F) | ((hi & 0x0F) << 4);
            }
            else
            {
                result = value1 + value2 + carry;
                carryResult = result > 0xFF; // Check for carry in normal addition
            }
            SetByte(mpu, (byte)result, _destination);
            if (_useCarry)
            {
                // Set the carry flag based on the addition
                mpu.RegSR = carryResult ? (mpu.RegSR | Microprocessor.StatusFlags.C) : (mpu.RegSR & ~Microprocessor.StatusFlags.C);
            }
            if (_setOverflow)
            {
                // Set the overflow flag based on the addition
                bool overflow = ((~(value1 ^ value2) & (value1 ^ (byte)result)) & 0x80) != 0;
                mpu.RegSR = overflow ? (mpu.RegSR | Microprocessor.StatusFlags.V) : (mpu.RegSR & ~Microprocessor.StatusFlags.V);
            }
            if (_setNZFlags)
            {
                mpu.RegSR = (Microprocessor.StatusFlags)((mpu.RegSR & ~(Microprocessor.StatusFlags.N | Microprocessor.StatusFlags.Z)) |
                    (result == 0 ? Microprocessor.StatusFlags.Z : 0) |
                    (result >= 0x80 ? Microprocessor.StatusFlags.N : 0));
            }
        }
    }

    internal class MicroOpAddWord : MicroOpCode
    {
        private readonly RegWordLocation _src1;
        private readonly RegWordLocation _src2;
        private readonly RegWordLocation _destination;
        private readonly bool _useCarry; // If true, use the carry flag in the addition
        private readonly bool _useDecimal; // If true, use decimal mode for addition
        private readonly bool _setOverflow; // If true, set the overflow flag based on the result
        private readonly bool _setNZFlags; // If true, set the N and Z flags based on the result

        internal MicroOpAddWord(RegWordLocation src1,
                                RegWordLocation src2,
                                RegWordLocation destination,
                                bool useCarry = false,
                                bool useDecimal = false,
                                bool setOverflow = false,
                                bool setNZFlags = false)
        {
            _src1 = src1;
            _src2 = src2;
            _destination = destination;
            _useCarry = useCarry;
            _useDecimal = useDecimal;
            _setOverflow = setOverflow;
            _setNZFlags = setNZFlags;
        }
        internal override void Execute(Microprocessor mpu)
        {
            ushort value1 = GetWord(mpu, _src1);
            ushort value2 = GetWord(mpu, _src2);
            ushort carry = (ushort)(_useCarry ? (mpu.RegSR & Microprocessor.StatusFlags.C) : 0);
            bool carryResult = carry != 0;
            int result = 0;
            if (_useDecimal)
            {
                for (int i = 0; i < 4; i++) // process each nibble
                {
                    byte digit = (byte)(((value1 >> (i * 4)) & 0x0F) + ((value2 >> (i * 4)) & 0x0F) + (carryResult ? 1 : 0));
                    carryResult = digit > 9; // Check if the digit exceeds BCD limit
                    if (carryResult) digit -= 10; // Adjust for BCD
                    result |= (digit & 0x0F) << (i * 4); // Set the result nibble
                }
            }
            else
            {
                result = value1 + value2 + carry;
                carryResult = result > 0xFFFF; // Check for carry in normal addition
            }
            SetWord(mpu, (ushort)result, _destination);
            if (_useCarry)
            {
                // Set the carry flag based on the addition
                mpu.RegSR = carryResult ? (mpu.RegSR | Microprocessor.StatusFlags.C) : (mpu.RegSR & ~Microprocessor.StatusFlags.C);
            }
            if (_setOverflow)
            {
                // Set the overflow flag based on the addition
                bool overflow = ((~(value1 ^ value2) & (value1 ^ (ushort)result)) & 0x8000) != 0;
                mpu.RegSR = overflow ? (mpu.RegSR | Microprocessor.StatusFlags.V) : (mpu.RegSR & ~Microprocessor.StatusFlags.V);
            }
            if (_setNZFlags)
            {
                mpu.RegSR = (Microprocessor.StatusFlags)((mpu.RegSR & ~(Microprocessor.StatusFlags.N | Microprocessor.StatusFlags.Z)) |
                    (result == 0 ? Microprocessor.StatusFlags.Z : 0) |
                    (result >= 0x8000 ? Microprocessor.StatusFlags.N : 0));
            }
        }
    }

    internal class MicroOpSubtractByte : MicroOpCode
    {
        private readonly RegByteLocation _src1;
        private readonly RegByteLocation _src2;
        private readonly RegByteLocation _destination;
        private readonly bool _useCarry;     // If true, use the carry flag in the subtraction
        private readonly bool _useDecimal;   // If true, use decimal mode for subtraction
        private readonly bool _setOverflow;  // If true, set the overflow flag based on the result
        private readonly bool _setNZFlags;   // If true, set the N and Z flags based on the result

        internal MicroOpSubtractByte(RegByteLocation src1,
                                     RegByteLocation src2,
                                     RegByteLocation destination,
                                     bool useCarry = false,
                                     bool useDecimal = false,
                                     bool setOverflow = false,
                                     bool setNZFlags = false)
        {
            _src1 = src1;
            _src2 = src2;
            _destination = destination;
            _useCarry = useCarry;
            _useDecimal = useDecimal;
            _setOverflow = setOverflow;
            _setNZFlags = setNZFlags;
        }

        internal override void Execute(Microprocessor mpu)
        {
            byte value1 = GetByte(mpu, _src1);
            byte value2 = GetByte(mpu, _src2);
            int carry = _useCarry ? ((mpu.RegSR & Microprocessor.StatusFlags.C) != 0 ? 1 : 0) : 1;
            int result;
            bool carryResult = false;

            if (_useDecimal)
            {
                // BCD subtraction
                byte a = value1;
                byte b = value2;
                int diff = a - b - (1 - carry);

                byte lo = (byte)((a & 0x0F) - (b & 0x0F) - (1 - carry));
                byte hi = (byte)((a >> 4) - (b >> 4));

                if ((lo & 0x10) != 0)
                {
                    lo -= 0x06;
                    hi--;
                }

                if ((hi & 0x10) != 0)
                {
                    hi -= 0x06;
                    carryResult = false;
                }
                else
                {
                    carryResult = true;
                }

                result = (hi << 4) | (lo & 0x0F);
            }
            else
            {
                // Binary subtraction
                int diff = value1 - value2 - (1 - carry);
                result = diff & 0xFF;
                carryResult = diff >= 0;
            }

            SetByte(mpu, (byte)result, _destination);

            if (_useCarry)
            {
                mpu.RegSR = carryResult
                    ? (mpu.RegSR | Microprocessor.StatusFlags.C)
                    : (mpu.RegSR & ~Microprocessor.StatusFlags.C);
            }
            if (_setOverflow)
            {
                // Overflow: ((A ^ B) & (A ^ result) & 0x80) != 0
                bool overflow = ((value1 ^ value2) & (value1 ^ (byte)result) & 0x80) != 0;
                mpu.RegSR = overflow
                    ? (mpu.RegSR | Microprocessor.StatusFlags.V)
                    : (mpu.RegSR & ~Microprocessor.StatusFlags.V);
            }
            if (_setNZFlags)
            {
                mpu.RegSR = (Microprocessor.StatusFlags)((mpu.RegSR & ~(Microprocessor.StatusFlags.N | Microprocessor.StatusFlags.Z)) |
                    (result == 0 ? Microprocessor.StatusFlags.Z : 0) |
                    (result >= 0x80 ? Microprocessor.StatusFlags.N : 0));
            }
        }
    }

    internal class MicroOpSubtractWord : MicroOpCode
    {
        private readonly RegWordLocation _src1;
        private readonly RegWordLocation _src2;
        private readonly RegWordLocation _destination;
        private readonly bool _useCarry;     // If true, use the carry flag in the subtraction
        private readonly bool _useDecimal;   // If true, use decimal mode for subtraction
        private readonly bool _setOverflow;  // If true, set the overflow flag based on the result
        private readonly bool _setNZFlags;   // If true, set the N and Z flags based on the result

        internal MicroOpSubtractWord(RegWordLocation src1,
                                     RegWordLocation src2,
                                     RegWordLocation destination,
                                     bool useCarry = false,
                                     bool useDecimal = false,
                                     bool setOverflow = false,
                                     bool setNZFlags = false)
        {
            _src1 = src1;
            _src2 = src2;
            _destination = destination;
            _useCarry = useCarry;
            _useDecimal = useDecimal;
            _setOverflow = setOverflow;
            _setNZFlags = setNZFlags;
        }

        internal override void Execute(Microprocessor mpu)
        {
            ushort value1 = GetWord(mpu, _src1);
            ushort value2 = GetWord(mpu, _src2);
            int carry = _useCarry ? ((mpu.RegSR & Microprocessor.StatusFlags.C) != 0 ? 1 : 0) : 1;
            int result;
            bool carryResult = false;

            if (_useDecimal)
            {
                // BCD subtraction, digit by digit
                ushort res = 0;
                int borrow = 1 - carry;
                for (int i = 0; i < 4; i++)
                {
                    byte digitA = (byte)((value1 >> (4 * i)) & 0x0F);
                    byte digitB = (byte)((value2 >> (4 * i)) & 0x0F);
                    int digit = digitA - digitB - borrow;

                    if (digit < 0)
                    {
                        digit += 10;
                        borrow = 1;
                    }
                    else
                    {
                        borrow = 0;
                    }

                    res |= (ushort)((digit & 0x0F) << (4 * i));
                }
                result = res;
                carryResult = borrow == 0;
            }
            else
            {
                // Binary subtraction
                int diff = value1 - value2 - (1 - carry);
                result = diff & 0xFFFF;
                carryResult = diff >= 0;
            }

            SetWord(mpu, (ushort)result, _destination);

            if (_useCarry)
            {
                mpu.RegSR = carryResult
                    ? (mpu.RegSR | Microprocessor.StatusFlags.C)
                    : (mpu.RegSR & ~Microprocessor.StatusFlags.C);
            }
            if (_setOverflow)
            {
                // Overflow: ((A ^ B) & (A ^ result) & 0x8000) != 0
                bool overflow = ((value1 ^ value2) & (value1 ^ (ushort)result) & 0x8000) != 0;
                mpu.RegSR = overflow
                    ? (mpu.RegSR | Microprocessor.StatusFlags.V)
                    : (mpu.RegSR & ~Microprocessor.StatusFlags.V);
            }
            if (_setNZFlags)
            {
                mpu.RegSR = (Microprocessor.StatusFlags)((mpu.RegSR & ~(Microprocessor.StatusFlags.N | Microprocessor.StatusFlags.Z)) |
                    (result == 0 ? Microprocessor.StatusFlags.Z : 0) |
                    (result >= 0x8000 ? Microprocessor.StatusFlags.N : 0));
            }
        }
    }

    internal class MicroOpIncrementByte : MicroOpCode
    {
        private readonly RegByteLocation _location;
        private readonly bool _setNZFlags; // If true, set the N and Z flags based on the result
        internal MicroOpIncrementByte(RegByteLocation location, bool setNZFlags = false)
        {
            _location = location;
            _setNZFlags = setNZFlags;
        }
        internal override void Execute(Microprocessor mpu)
        {
            byte value = GetByte(mpu, _location);
            value++;
            SetByte(mpu, value, _location);
            if (_setNZFlags) // If set, update the N and Z flags based on the incremented value
            {
                mpu.RegSR = (Microprocessor.StatusFlags)((mpu.RegSR & ~(Microprocessor.StatusFlags.N | Microprocessor.StatusFlags.Z)) |
                    (value == 0 ? Microprocessor.StatusFlags.Z : 0) |
                    (value >= 0x80 ? Microprocessor.StatusFlags.N : 0));
            }
        }
    }

    internal class MicroOpIncrementWord : MicroOpCode
    {
        private readonly RegWordLocation _location;
        private readonly bool _setNZFlags; // If true, set the N and Z flags based on the result
        internal MicroOpIncrementWord(RegWordLocation location, bool setNZFlags = false)
        {
            _location = location;
            _setNZFlags = setNZFlags;
        }
        internal override void Execute(Microprocessor mpu)
        {
            ushort value = GetWord(mpu, _location);
            value++;
            SetWord(mpu, value, _location);
            if (_setNZFlags) // If set, update the N and Z flags based on the incremented value
            {
                mpu.RegSR = (Microprocessor.StatusFlags)((mpu.RegSR & ~(Microprocessor.StatusFlags.N | Microprocessor.StatusFlags.Z)) |
                    (value == 0 ? Microprocessor.StatusFlags.Z : 0) |
                    (value >= 0x8000 ? Microprocessor.StatusFlags.N : 0));
            }
        }
    }

    internal class MicroOpDecrementByte : MicroOpCode
    {
        private readonly RegByteLocation _location;
        private readonly bool _setNZFlags; // If true, set the N and Z flags based on the result
        internal MicroOpDecrementByte(RegByteLocation location, bool setNZFlags = false)
        {
            _location = location;
            _setNZFlags = setNZFlags;
        }
        internal override void Execute(Microprocessor mpu)
        {
            byte value = GetByte(mpu, _location);
            value--;
            SetByte(mpu, value, _location);
            if (_setNZFlags) // If set, update the N and Z flags based on the incremented value
            {
                mpu.RegSR = (Microprocessor.StatusFlags)((mpu.RegSR & ~(Microprocessor.StatusFlags.N | Microprocessor.StatusFlags.Z)) |
                    (value == 0 ? Microprocessor.StatusFlags.Z : 0) |
                    (value >= 0x80 ? Microprocessor.StatusFlags.N : 0));
            }
        }
    }

    internal class MicroOpDecrementWord : MicroOpCode
    {
        private readonly RegWordLocation _location;
        private readonly bool _setNZFlags; // If true, set the N and Z flags based on the result
        internal MicroOpDecrementWord(RegWordLocation location, bool setNZFlags = false)
        {
            _location = location;
            _setNZFlags = setNZFlags;
        }
        internal override void Execute(Microprocessor mpu)
        {
            ushort value = GetWord(mpu, _location);
            value--;
            SetWord(mpu, value, _location);
            if (_setNZFlags) // If set, update the N and Z flags based on the incremented value
            {
                mpu.RegSR = (Microprocessor.StatusFlags)((mpu.RegSR & ~(Microprocessor.StatusFlags.N | Microprocessor.StatusFlags.Z)) |
                    (value == 0 ? Microprocessor.StatusFlags.Z : 0) |
                    (value >= 0x8000 ? Microprocessor.StatusFlags.N : 0));
            }
        }
    }

    internal class MicroOpPushByteFrom : MicroOpCode
    {
        private readonly RegByteLocation _source;
        internal MicroOpPushByteFrom(RegByteLocation source) : base(OpCycleType.Write)
        {
            // Might double check this if, because it seems like you can push MD to stack in some cases
            // (PEA, PEI, PER?)
            if (source == RegByteLocation.MD)
                throw new ArgumentException("Cannot push MD to stack (bad enqueue?)", nameof(source));
            _source = source;
        }
        internal override void Execute(Microprocessor mpu)
        {
            SetByte(mpu, GetByte(mpu, _source), RegByteLocation.MD); // Set MD to the value to push
            mpu.ByteWrite(mpu.RegSP); // Write the value to the stack
            mpu.RegSP = (ushort)((mpu.RegSP - 1) & 0xFFFF); // Decrement SP after writing
        }
    }

    internal class MicroOpPullByteTo : MicroOpCode
    {
        private readonly RegByteLocation _destination;
        internal MicroOpPullByteTo(RegByteLocation destination) : base(OpCycleType.Read)
        {
            if (destination == RegByteLocation.MD)
                throw new ArgumentException("Cannot pull MD from stack (bad enqueue?)", nameof(destination));
            _destination = destination;
        }
        internal override void Execute(Microprocessor mpu)
        {
            mpu.RegSP = (ushort)((mpu.RegSP + 1) & 0xFFFF); // Increment SP before reading
            mpu.ByteRead(mpu.RegSP); // Read the value from the stack
            SetByte(mpu, mpu.RegMD, _destination); // Move the value to the destination register
        }
    }

    internal class MicroOpResetRegistersForEmulation : MicroOpCode
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.RegXH = 0x00;
            mpu.RegYH = 0x00;
            mpu.RegSH = 0x01;
        }
    }
}
