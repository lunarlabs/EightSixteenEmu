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
 *  Opcodes
 */

using static EightSixteenEmu.Microprocessor;

namespace EightSixteenEmu.MPU
{
    internal interface IOpcodeCommand
    {
        internal abstract void Execute(Microprocessor mpu);

        internal static void BranchTo(Microprocessor mpu, uint address)
        {
            mpu.NextCycle();
            if (mpu.FlagE && (byte)(mpu.RegPC >> 8) != (byte)(address >> 8))
            {
                mpu.NextCycle();
            }
            mpu.RegPC = (ushort)address;
        }

        internal static void CopyMemory(Microprocessor mpu, ushort operand)
        {
            byte destination = (byte)(operand >> 8);
            mpu.RegDB = destination;
            byte source = (byte)operand;
            mpu.WriteByte(mpu.ReadByte((uint)(source << 16) | mpu.RegX),(uint)(destination <<16)| mpu.RegY);
            mpu.NextCycle();
            mpu.NextCycle();
            if (--mpu.RegA != 0xffff) mpu.RegPC -= 3; // jump back to the move instruction
        }
    }

    internal class OP_ADC
    {
        internal void Execute(Microprocessor mpu)
        {
            ushort addend = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            byte carry = mpu.ReadStatusFlag(StatusFlags.C) ? (byte)1 : (byte)0;
            if (mpu.AccumulatorIs8Bit)
            {
                int result = mpu.RegAL + addend + carry;
                if (mpu.ReadStatusFlag(StatusFlags.D))
                {
                    {
                        if (((result) & 0x0f) > 0x09) result += 0x06;
                        if (((result) & 0xf0) > 0x90) result += 0x60;
                    }
                }
                mpu.SetStatusFlag(StatusFlags.C, (result & 0x100) != 0);
                mpu.SetStatusFlag(StatusFlags.V, ((~(mpu.RegAL ^ addend)) & (mpu.RegAL ^ result) & 0x80) != 0);
                mpu.NextCycle();
                mpu.SetNZStatusFlagsFromValue((byte)result);
                mpu.RegAL = (byte)result;
            }
            else
            {
                int result = mpu.RegA + addend + carry;
                if (mpu.ReadStatusFlag(StatusFlags.D))
                {
                    {
                        if (((result) & 0x000f) > 0x0009) result += 0x0006;
                        if (((result) & 0x00f0) > 0x0090) result += 0x0060;
                        if (((result) & 0x0f00) > 0x0900) result += 0x0600;
                        if (((result) & 0xf000) > 0x9000) result += 0x6000;
                    }
                }
                mpu.SetStatusFlag(StatusFlags.C, (result & 0x10000) != 0);
                mpu.SetStatusFlag(StatusFlags.V, ((~(mpu.RegA ^ addend)) & (mpu.RegA ^ result) & 0x8000) != 0);
                mpu.NextCycle();
                mpu.SetNZStatusFlagsFromValue((ushort)result);
                mpu.RegA = (ushort)result;
            }
        }
    }

    internal class OP_SBC
    {
        internal void Execute(Microprocessor mpu)
        {
            ushort subtrahend = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            byte carry = mpu.ReadStatusFlag(StatusFlags.C) ? (byte)1 : (byte)0;
            if (mpu.AccumulatorIs8Bit)
            {
                int result = mpu.RegAL + ~(byte)subtrahend - (1 - carry);
                if (mpu.ReadStatusFlag(StatusFlags.D))
                {
                    {
                        if (((result) & 0x0f) > 0x09) result += 0x06;
                        if (((result) & 0xf0) > 0x90) result += 0x60;
                    }
                }
                mpu.SetStatusFlag(StatusFlags.C, (byte)result >= (byte)subtrahend);
                mpu.SetStatusFlag(StatusFlags.V, ((mpu.RegAL ^ subtrahend) & (mpu.RegAL ^ result) & 0x80) != 0);
                mpu.NextCycle();
                mpu.SetNZStatusFlagsFromValue((byte)result);
                mpu.RegAL = (byte)result;
            }
            else
            {
                int result = mpu.RegA - subtrahend - (1 - carry);
                if (mpu.ReadStatusFlag(StatusFlags.D))
                {
                    {
                        if (((result) & 0x000f) > 0x0009) result += 0x0006;
                        if (((result) & 0x00f0) > 0x0090) result += 0x0060;
                        if (((result) & 0x0f00) > 0x0900) result += 0x0600;
                        if (((result) & 0xf000) > 0x9000) result += 0x6000;
                    }
                }
                mpu.SetStatusFlag(StatusFlags.C, (ushort)result >= subtrahend);
                mpu.SetStatusFlag(StatusFlags.V, ((mpu.RegA ^ subtrahend) & (mpu.RegA ^ result) & 0x8000) != 0);
                mpu.NextCycle();
                mpu.SetNZStatusFlagsFromValue((ushort)result);
                mpu.RegA = (ushort)result;

            }
        }
    }

    internal class OP_CMP
    {
        internal void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            if (mpu.AccumulatorIs8Bit)
            {
                int result = mpu.RegAL - (byte)operand;
                mpu.SetStatusFlag(StatusFlags.C, (byte)result <= mpu.RegAL);
                mpu.SetNZStatusFlagsFromValue((byte)result);
                mpu.NextCycle();
            }
            else
            {
                int result = mpu.RegA - operand;
                mpu.SetStatusFlag(StatusFlags.C, (ushort)result <= mpu.RegA);
                mpu.SetNZStatusFlagsFromValue((ushort)result);
                mpu.NextCycle();
            }
        }
    }

    internal class OP_CPX
    {
        internal void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.IndexesAre8Bit);
            if (mpu.IndexesAre8Bit)
            {
                int result = mpu.RegXL - (byte)operand;
                mpu.SetStatusFlag(StatusFlags.C, (byte)result <= mpu.RegXL);
                mpu.SetNZStatusFlagsFromValue((byte)result);
                mpu.NextCycle();
            }
            else
            {
                int result = mpu.RegX - operand;
                mpu.SetStatusFlag(StatusFlags.C, (ushort)result <= mpu.RegX);
                mpu.SetNZStatusFlagsFromValue((ushort)result);
                mpu.NextCycle();
            }
        }
    }

    internal class OP_CPY
    {
        internal void Execute(Microprocessor mpu) {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.IndexesAre8Bit);
            if (mpu.IndexesAre8Bit)
            {
                int result = mpu.RegXL - (byte)operand;
                mpu.SetStatusFlag(StatusFlags.C, (byte)result <= mpu.RegYL);
                mpu.SetNZStatusFlagsFromValue((byte)result);
                mpu.NextCycle();
            }
            else
            {
                int result = mpu.RegX - operand;
                mpu.SetStatusFlag(StatusFlags.C, (ushort)result <= mpu.RegY);
                mpu.SetNZStatusFlagsFromValue((ushort)result);
                mpu.NextCycle();
            }
        }
    }

    internal class OP_DEC
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.CurrentAddressingMode == W65C816.AddressingMode.Accumulator)
            {
                if (mpu.AccumulatorIs8Bit)
                {
                    mpu.SetNZStatusFlagsFromValue(--mpu.RegAL);
                }
                else
                {
                    mpu.SetNZStatusFlagsFromValue(--mpu.RegA);
                }
                mpu.NextCycle();
            }
            else
            {
                ushort operand = mpu.AddressingMode.GetOperand(mpu, out uint address, mpu.AccumulatorIs8Bit);
                operand--;
                if (mpu.AccumulatorIs8Bit)
                {
                    mpu.SetNZStatusFlagsFromValue((byte)operand);
                }
                else
                {
                    mpu.SetNZStatusFlagsFromValue((ushort)operand);
                }
                mpu.NextCycle();
                mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            }
        }
    }

    internal class OP_DEX
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.IndexesAre8Bit)
            {
                mpu.SetNZStatusFlagsFromValue(--mpu.RegXL);
            }
            else
            {
                mpu.SetNZStatusFlagsFromValue(--mpu.RegX);
            }
            mpu.NextCycle();
        }
    }

    internal class OP_DEY
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.IndexesAre8Bit)
            {
                mpu.SetNZStatusFlagsFromValue(--mpu.RegYL);
            }
            else
            {
                mpu.SetNZStatusFlagsFromValue(--mpu.RegY);
            }
            mpu.NextCycle();
        }
    }

    internal class OP_INC
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.CurrentAddressingMode == W65C816.AddressingMode.Accumulator)
            {
                if (mpu.AccumulatorIs8Bit)
                {
                    mpu.SetNZStatusFlagsFromValue(++mpu.RegAL);
                }
                else
                {
                    mpu.SetNZStatusFlagsFromValue(++mpu.RegA);
                }
                mpu.NextCycle();
            }
            else
            {
                ushort operand = mpu.AddressingMode.GetOperand(mpu, out uint address, mpu.AccumulatorIs8Bit);
                operand++;
                if (mpu.AccumulatorIs8Bit)
                {
                    mpu.SetNZStatusFlagsFromValue((byte)operand);
                }
                else
                {
                    mpu.SetNZStatusFlagsFromValue((ushort)operand);
                }
                mpu.NextCycle();
                mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            }
        }
    }

    internal class OP_INX
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.IndexesAre8Bit)
            {
                mpu.SetNZStatusFlagsFromValue(++mpu.RegXL);
            }
            else
            {
                mpu.SetNZStatusFlagsFromValue(++mpu.RegX);
            }
            mpu.NextCycle();
        }
    }

    internal class OP_INY
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.IndexesAre8Bit)
            {
                mpu.SetNZStatusFlagsFromValue(++mpu.RegYL);
            }
            else
            {
                mpu.SetNZStatusFlagsFromValue(++mpu.RegY);
            }
            mpu.NextCycle();
        }
    }

    internal class OP_AND
    {
        internal void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            if (mpu.AccumulatorIs8Bit)
            {
                mpu.RegAL &= (byte)operand;
                mpu.SetNZStatusFlagsFromValue(mpu.RegAL);
            }
            else
            {
                mpu.RegA &= operand;
                mpu.SetNZStatusFlagsFromValue(mpu.RegA);
            }
            mpu.NextCycle();
        }
    }

    internal class OP_EOR
    {
        internal void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            if (mpu.AccumulatorIs8Bit)
            {
                mpu.RegAL ^= (byte)operand;
                mpu.SetNZStatusFlagsFromValue(mpu.RegAL);
            }
            else
            {
                mpu.RegA ^= operand;
                mpu.SetNZStatusFlagsFromValue(mpu.RegA);
            }
            mpu.NextCycle();
        }
    }

    internal class OP_ORA
    {
        internal void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            if (mpu.AccumulatorIs8Bit)
            {
                mpu.RegAL |= (byte)operand;
                mpu.SetNZStatusFlagsFromValue(mpu.RegAL);
            }
            else
            {
                mpu.RegA |= operand;
                mpu.SetNZStatusFlagsFromValue(mpu.RegA);
            }
            mpu.NextCycle();
        }
    }

    internal class OP_BIT
    {
        internal void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            if (mpu.AccumulatorIs8Bit)
            {
                if (mpu.CurrentAddressingMode == W65C816.AddressingMode.Immediate)
                {
                    mpu.SetStatusFlag(StatusFlags.V, (operand & 0x40) != 0);
                    mpu.SetStatusFlag(StatusFlags.N, (operand & 0x80) != 0);
                }
                mpu.SetStatusFlag(StatusFlags.Z, (mpu.RegAL & (byte)operand) == 0);
            }
            else
            {
                if (mpu.CurrentAddressingMode == W65C816.AddressingMode.Immediate)
                {
                    mpu.SetStatusFlag(StatusFlags.V, (operand & 0x4000) != 0);
                    mpu.SetStatusFlag(StatusFlags.N, (operand & 0x8000) != 0);
                }
                mpu.SetStatusFlag(StatusFlags.Z, (mpu.RegA & operand) == 0);
            }
            mpu.NextCycle();
        }
    }

    internal class OP_TRB
    {
        internal void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, out uint address, mpu.AccumulatorIs8Bit);
            ushort mask = (ushort)(mpu.AccumulatorIs8Bit ? mpu.RegAL : mpu.RegA);
            operand &= (ushort)~mask;
            mpu.SetStatusFlag(StatusFlags.Z, operand == 0);
            mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            mpu.NextCycle();
        }
    }

    internal class OP_TSB
    {
        internal void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, out uint address, mpu.AccumulatorIs8Bit);
            ushort mask = (ushort)(mpu.AccumulatorIs8Bit ? mpu.RegAL : mpu.RegA);
            operand |= mask;
            mpu.SetStatusFlag(StatusFlags.Z, operand == 0);
            mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            mpu.NextCycle();
        }
    }

    internal class OP_ASL
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.CurrentAddressingMode == W65C816.AddressingMode.Accumulator)
            {
                if (mpu.AccumulatorIs8Bit)
                {
                    mpu.SetStatusFlag(StatusFlags.C, (mpu.RegAL & 0x80) != 0);
                    mpu.RegAL <<= 1;
                    mpu.SetNZStatusFlagsFromValue(mpu.RegAL);
                }
                else
                {
                    mpu.SetStatusFlag(StatusFlags.C, (mpu.RegA & 0x8000) != 0);
                    mpu.RegA <<= 1;
                    mpu.SetNZStatusFlagsFromValue(mpu.RegA);
                }
                mpu.NextCycle();
            }
            else
            {
                ushort operand = mpu.AddressingMode.GetOperand(mpu, out uint address, mpu.AccumulatorIs8Bit);
                mpu.SetStatusFlag(StatusFlags.C, (operand & 0x8000) != 0);
                operand <<= 1;
                mpu.SetNZStatusFlagsFromValue(operand);
                mpu.NextCycle();
                mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            }
        }
    }

    internal class OP_LSR
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.CurrentAddressingMode == W65C816.AddressingMode.Accumulator)
            {
                if (mpu.AccumulatorIs8Bit)
                {
                    mpu.SetStatusFlag(StatusFlags.C, (mpu.RegAL & 0x01) != 0);
                    mpu.RegAL >>>= 1;
                    mpu.SetNZStatusFlagsFromValue(mpu.RegAL);
                }
                else
                {
                    mpu.SetStatusFlag(StatusFlags.C, (mpu.RegA & 0x0001) != 0);
                    mpu.RegA >>>= 1;
                    mpu.SetNZStatusFlagsFromValue(mpu.RegA);
                }
                mpu.NextCycle();
            }
            else
            {
                ushort operand = mpu.AddressingMode.GetOperand(mpu, out uint address, mpu.AccumulatorIs8Bit);
                mpu.SetStatusFlag(StatusFlags.C, (operand & 0x0001) != 0);
                operand >>>= 1;
                mpu.SetNZStatusFlagsFromValue(operand);
                mpu.NextCycle();
                mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            }
        }
    }

    internal class OP_ROL
    {
        internal void Execute(Microprocessor mpu)
        {
            uint operand;
            if (mpu.CurrentAddressingMode == W65C816.AddressingMode.Accumulator)
            {
                operand = mpu.AccumulatorIs8Bit ? mpu.RegAL : mpu.RegA;
                operand = operand << 1 | (mpu.ReadStatusFlag(StatusFlags.C) ? 1u : 0u);
                mpu.SetStatusFlag(StatusFlags.C, (mpu.AccumulatorIs8Bit ? (operand & 0x100) : (operand & 0x10000)) != 0);
                mpu.NextCycle();
                if (mpu.AccumulatorIs8Bit)
                {
                    mpu.RegAL = (byte)operand;
                    mpu.SetNZStatusFlagsFromValue(mpu.RegAL);
                }
                else
                {
                    mpu.RegA = (ushort)operand;
                    mpu.SetNZStatusFlagsFromValue(mpu.RegA);
                }
            }
            else
            {
                operand = mpu.AddressingMode.GetOperand(mpu, out uint address, mpu.AccumulatorIs8Bit);
                operand = operand << 1 | (mpu.ReadStatusFlag(StatusFlags.C) ? 1u : 0u);
                mpu.SetStatusFlag(StatusFlags.C, (mpu.AccumulatorIs8Bit ? (operand & 0x100) : (operand & 0x10000)) != 0);
                mpu.NextCycle();
                mpu.WriteValue((ushort)operand, mpu.AccumulatorIs8Bit, address);
            }
        }
    }

    internal class OP_ROR
    {
        internal void Execute(Microprocessor mpu)
        {
            ushort operand;
            bool carry;
            if (mpu.CurrentAddressingMode == W65C816.AddressingMode.Accumulator)
            {
                operand = mpu.AccumulatorIs8Bit ? mpu.RegAL : mpu.RegA;
                carry = operand % 2 == 1;
                operand = (ushort)(mpu.ReadStatusFlag(StatusFlags.C) ? (operand >> 1) | (mpu.AccumulatorIs8Bit ? 0x80 : 0x8000) : operand >> 1);
                mpu.SetStatusFlag(StatusFlags.C, carry);
                mpu.NextCycle();
                if (mpu.AccumulatorIs8Bit)
                {
                    mpu.RegAL = (byte)operand;
                    mpu.SetNZStatusFlagsFromValue(mpu.RegAL);
                }
                else
                {
                    mpu.RegA = (ushort)operand;
                    mpu.SetNZStatusFlagsFromValue(mpu.RegA);
                }
            }
            else
            {
                operand = mpu.AddressingMode.GetOperand(mpu, out uint address, mpu.AccumulatorIs8Bit);
                carry = operand % 2 == 1;
                operand = (ushort)(mpu.ReadStatusFlag(StatusFlags.C) ? (operand >> 1) | (mpu.AccumulatorIs8Bit ? 0x80 : 0x8000) : operand >> 1);
                mpu.SetStatusFlag(StatusFlags.C, carry);
                mpu.NextCycle();
                mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            }
        }
    }

    internal class OP_BCC
    {
        internal void Execute(Microprocessor mpu)
        {
            if (!mpu.ReadStatusFlag(StatusFlags.C))
            {
                IOpcodeCommand.BranchTo(mpu, mpu.AddressingMode.GetAddress(mpu));
            }
        }
    }

    internal class OP_BCS
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.ReadStatusFlag(StatusFlags.C))
            {
                IOpcodeCommand.BranchTo(mpu, mpu.AddressingMode.GetAddress(mpu));
            }
        }
    }

    internal class OP_BEQ
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.ReadStatusFlag(StatusFlags.Z))
            {
                IOpcodeCommand.BranchTo(mpu, mpu.AddressingMode.GetAddress(mpu));
            }
        }
    }

    internal class OP_BMI
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.ReadStatusFlag(StatusFlags.N))
            {
                IOpcodeCommand.BranchTo(mpu, mpu.AddressingMode.GetAddress(mpu));
            }
        }
    }

    internal class OP_BNE
    {
        internal void Execute(Microprocessor mpu)
        {
            if (!mpu.ReadStatusFlag(StatusFlags.Z))
            {
                IOpcodeCommand.BranchTo(mpu, mpu.AddressingMode.GetAddress(mpu));
            }
        }
    }

    internal class OP_BPL
    {
        internal void Execute(Microprocessor mpu)
        {
            if (!mpu.ReadStatusFlag(StatusFlags.N))
            {
                IOpcodeCommand.BranchTo(mpu, mpu.AddressingMode.GetAddress(mpu));
            }
        }
    }

    internal class OP_BRA
    {
        internal void Execture(Microprocessor mpu)
        {
            IOpcodeCommand.BranchTo(mpu, mpu.AddressingMode.GetAddress(mpu));
        }
    }

    internal class OP_BVC
    {
        internal void Execute(Microprocessor mpu)
        {
            if (!mpu.ReadStatusFlag(StatusFlags.V))
            {
                IOpcodeCommand.BranchTo(mpu, mpu.AddressingMode.GetAddress(mpu));
            }
        }
    }

    internal class OP_BVS
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.ReadStatusFlag(StatusFlags.V))
            {
                IOpcodeCommand.BranchTo(mpu, mpu.AddressingMode.GetAddress(mpu));
            }
        }
    }

    internal class OP_BRL
    {
        internal void Execute(Microprocessor mpu)
        {
            IOpcodeCommand.BranchTo(mpu, mpu.AddressingMode.GetAddress(mpu));
        }
    }

    internal class OP_JMP
    {
        internal void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            if (mpu.CurrentAddressingMode == W65C816.AddressingMode.AbsoluteLong || mpu.CurrentAddressingMode == W65C816.AddressingMode.AbsoluteIndirectLong)
            {
                mpu.RegPB = (byte)(destination >> 16);
            }
            mpu.RegPC = (ushort)destination;
            mpu.NextCycle();
        }
    }

    internal class OP_JSL
    {
        internal void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            mpu.PushWord(mpu.RegPB);
            mpu.RegPB = (byte)(destination >> 16);
            mpu.PushWord(mpu.RegPC);
            mpu.RegPC = (ushort)destination;
            mpu.NextCycle();
        }
    }

    internal class OP_JSR 
    {
        internal void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            mpu.PushWord(mpu.RegPC);
            mpu.RegPC = (ushort)destination;
            mpu.NextCycle();
        }
    }

    internal class OP_RTS
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.RegPC = mpu.PullWord();
            mpu.NextCycle();
        }
    }

    internal class OP_RTL
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.RegPC = mpu.PullWord();
            mpu.RegPB = mpu.PullByte();
            mpu.NextCycle();
        }
    }

    internal class OP_RTI
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.NextCycle();
            mpu.NextCycle();
            mpu.RegSR = (StatusFlags)mpu.PullByte();
            mpu.RegPC = mpu.PullWord();
            if (mpu.FlagE)
            {
                mpu.RegPC = mpu.PullByte();
            }
        }
    }
    
    internal class OP_BRK
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.Interrupt(InterruptType.BRK);
        }
    }

    internal class OP_COP
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.Interrupt(InterruptType.COP);
        }
    }

    internal class OP_CLC
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.C, false);
            mpu.NextCycle();
        }
    }

    internal class OP_CLD
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.D, false);
            mpu.NextCycle();
        }
    }

    internal class OP_CLI
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.I, false);
            mpu.NextCycle();
        }
    }

    internal class OP_CLV
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.V, false);
            mpu.NextCycle();
        }
    }

    internal class OP_SEC
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.C, true);
            mpu.NextCycle();
        }
    }

    internal class OP_SED
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.D, true);
            mpu.NextCycle();
        }
    }

    internal class OP_SEI
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.I, true);
            mpu.NextCycle();
        }
    }

    internal class OP_REP
    {
        internal void Execute(Microprocessor mpu)
        {
            byte operand = (byte)mpu.AddressingMode.GetOperand(mpu, true);
            if (mpu.FlagE)
            {
                // M and X flags cannot be set in emulation mode
                operand &= 0xCF;
            }
            mpu.RegSR &= (StatusFlags)~operand;
            mpu.NextCycle();
        }
    }

    internal class OP_SEP
    {
        internal void Execute(Microprocessor mpu)
        {
            byte operand = (byte)mpu.AddressingMode.GetOperand(mpu, true);
            if (mpu.FlagE)
            {
                // M and X flags cannot be set in emulation mode
                operand &= 0xCF;
            }
            mpu.RegSR |= (StatusFlags)operand;
            mpu.NextCycle();
        }
    }

    internal class OP_LDA
    {
        internal void Execute(Microprocessor mpu) 
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            if (mpu.AccumulatorIs8Bit)
            {
                mpu.RegAL = (byte)operand;
                mpu.SetNZStatusFlagsFromValue(mpu.RegAL);
            }
            else
            {
                mpu.RegA = operand;
                mpu.SetNZStatusFlagsFromValue(mpu.RegA);
            }
            mpu.NextCycle();
        }
    }

    internal class OP_LDX
    {
        internal void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.IndexesAre8Bit);
            if (mpu.IndexesAre8Bit)
            {
                mpu.RegXL = (byte)operand;
                mpu.SetNZStatusFlagsFromValue(mpu.RegXL);
            }
            else
            {
                mpu.RegX = operand;
                mpu.SetNZStatusFlagsFromValue(mpu.RegX);
            }
            mpu.NextCycle();
        }
    }

    internal class OP_LDY
    {
        internal void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.IndexesAre8Bit);
            if (mpu.IndexesAre8Bit)
            {
                mpu.RegYL = (byte)operand;
                mpu.SetNZStatusFlagsFromValue(mpu.RegYL);
            }
            else
            {
                mpu.RegY = operand;
                mpu.SetNZStatusFlagsFromValue(mpu.RegY);
            }
            mpu.NextCycle();
        }
    }

    internal class OP_STA
    {
        internal void Execute(Microprocessor mpu)
        {
            uint address = mpu.AddressingMode.GetAddress(mpu);
            if (mpu.AccumulatorIs8Bit)
            {
                mpu.WriteByte(mpu.RegAL, address);
            }
            else
            {
                mpu.WriteWord(mpu.RegA, address);
            }
        }
    }

    internal class OP_STX 
    {
        internal void Execute(Microprocessor mpu)
        {
            uint address = mpu.AddressingMode.GetAddress(mpu);
            if (mpu.IndexesAre8Bit)
            {
                mpu.WriteByte(mpu.RegXL, address);
            }
            else
            {
                mpu.WriteWord(mpu.RegX, address);
            }
        }
    }

    internal class OP_STY
    {
        internal void Execute(Microprocessor mpu)
        {
            uint address = mpu.AddressingMode.GetAddress(mpu);
            if (mpu.IndexesAre8Bit)
            {
                mpu.WriteByte(mpu.RegYL, address);
            }
            else
            {
                mpu.WriteWord(mpu.RegY, address);
            }
        }
    }

    internal class OP_STZ
    {
        internal void Execute(Microprocessor mpu)
        {
            uint address = mpu.AddressingMode.GetAddress(mpu);
            if (mpu.AccumulatorIs8Bit)
            {
                mpu.WriteByte(0, address);
            }
            else
            {
                mpu.WriteWord(0, address);
            }
        }
    }

    internal class OP_MVN
    {
        internal void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            IOpcodeCommand.CopyMemory(mpu, operand);
            if (mpu.IndexesAre8Bit)
            {
                mpu.RegXL++;
                mpu.RegYL++;
            }
            else
            {
                mpu.RegX++;
                mpu.RegY++;
            }
        }
    }

    internal class OP_MVP
    {
        internal void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            IOpcodeCommand.CopyMemory(mpu, operand);
            if (mpu.IndexesAre8Bit)
            {
                mpu.RegXL--;
                mpu.RegYL--;
            }
            else
            {
                mpu.RegX--;
                mpu.RegY--;
            }
        }
    }

    internal class OP_NOP
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.NextCycle();
        }
    }

    internal class OP_WDM
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.RegPC++;
            mpu.NextCycle();
        }
    }

    internal class OP_PEA
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.PushWord(mpu.AddressingMode.GetOperand(mpu, false));
        }
    }

    internal class OP_PEI
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.PushWord(mpu.AddressingMode.GetOperand(mpu, false));
        }
    }

    internal class OP_PER
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.PushWord(mpu.AddressingMode.GetOperand(mpu, false));
        }
    }

    internal class OP_PHA
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.AccumulatorIs8Bit)
            {
                mpu.PushByte(mpu.RegAL);
            }
            else
            {
                mpu.PushWord(mpu.RegA);
            }
        }
    }

    internal class OP_PHX
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.IndexesAre8Bit)
            {
                mpu.PushByte(mpu.RegXL);
            }
            else
            {
                mpu.PushWord(mpu.RegX);
            }
        }
    }

    internal class OP_PHY
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.IndexesAre8Bit)
            {
                mpu.PushByte(mpu.RegYL);
            }
            else
            {
                mpu.PushWord(mpu.RegY);
            }
        }
    }

    internal class OP_PLA
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.AccumulatorIs8Bit)
            {
                mpu.RegAL = mpu.PullByte();
                mpu.SetNZStatusFlagsFromValue(mpu.RegAL);
            }
            else
            {
                mpu.RegA = mpu.PullWord();
                mpu.SetNZStatusFlagsFromValue(mpu.RegA);
            }
        }
    }

    internal class OP_PLX
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.IndexesAre8Bit)
            {
                mpu.RegXL = mpu.PullByte();
                mpu.SetNZStatusFlagsFromValue(mpu.RegXL);
            }
            else
            {
                mpu.RegX = mpu.PullWord();
                mpu.SetNZStatusFlagsFromValue(mpu.RegX);
            }
        }
    }

    internal class OP_PLY
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.IndexesAre8Bit)
            {
                mpu.RegYL = mpu.PullByte();
                mpu.SetNZStatusFlagsFromValue(mpu.RegYL);
            }
            else
            {
                mpu.RegY = mpu.PullWord();
                mpu.SetNZStatusFlagsFromValue(mpu.RegY);
            }
        }
    }

    internal class OP_PHB
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.PushByte(mpu.RegPB);
        }
    }

    internal class OP_PHD
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.PushWord(mpu.RegDP);
        }
    }

    internal class OP_PHK
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.PushByte(mpu.RegPB);
        }
    }

    internal class OP_PHP
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.PushByte((byte)mpu.RegSR);
        }
    }

    internal class OP_PLB
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.RegPB = mpu.PullByte();
            mpu.SetNZStatusFlagsFromValue(mpu.RegPB);
        }
    }

    internal class OP_PLD
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.RegDP = mpu.PullWord();
            mpu.SetNZStatusFlagsFromValue(mpu.RegDP);
        }
    }

    internal class OP_PLP
    {
        internal void Execute(Microprocessor mpu)
        {
            byte operand = mpu.PullByte();
            if (mpu.FlagE)
            {
                // M and X flags cannot be reset in emulation mode
                operand |= 0x30;
            }
            mpu.RegSR = (StatusFlags)operand;
        }
    }

    // TODO: We need to figure out how to handle the microprocessor state in Microprocessor.cs before we implement these
    internal class OP_STP
    {
        internal void Execute(Microprocessor mpu) => throw new NotImplementedException();
    }

    internal class OP_WAI
    {
        internal void Execute(Microprocessor mpu) => throw new NotImplementedException();
    }

    internal class OP_TAX
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.IndexesAre8Bit)
            {
                mpu.RegXL = mpu.RegAL;
                mpu.SetNZStatusFlagsFromValue(mpu.RegXL);
            }
            else
            {
                mpu.RegX = mpu.RegA;
                mpu.SetNZStatusFlagsFromValue(mpu.RegX);
            }
            mpu.NextCycle();
        }
    }

    internal class OP_TAY
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.IndexesAre8Bit)
            {
                mpu.RegYL = mpu.RegAL;
                mpu.SetNZStatusFlagsFromValue(mpu.RegYL);
            }
            else
            {
                mpu.RegY = mpu.RegA;
                mpu.SetNZStatusFlagsFromValue(mpu.RegY);
            }
            mpu.NextCycle();
        }
    }

    internal class OP_TSX
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.FlagE || mpu.IndexesAre8Bit)
            {
                mpu.RegXL = mpu.RegSL;
                mpu.SetNZStatusFlagsFromValue(mpu.RegX);
            }
            else
            {
                mpu.RegX = mpu.RegSP;
                mpu.SetNZStatusFlagsFromValue(mpu.RegX);
            }
        }
    }

    internal class OP_TXA
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.AccumulatorIs8Bit)
            {
                mpu.RegAL = mpu.RegXL;
                mpu.SetNZStatusFlagsFromValue(mpu.RegAL);
            }
            else
            {
                mpu.RegA = mpu.RegX;
                mpu.SetNZStatusFlagsFromValue(mpu.RegA);
            }
            mpu.NextCycle();
        }
    }

    internal class OP_TXS
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.FlagE || mpu.IndexesAre8Bit)
            {
                mpu.RegSL = mpu.RegXL;
            }
            else
            {
                mpu.RegSP = mpu.RegX;
            }
            mpu.NextCycle();
        }
    }

    internal class OP_TXY
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.IndexesAre8Bit)
            {
                mpu.RegYL = mpu.RegXL;
                mpu.SetNZStatusFlagsFromValue(mpu.RegYL);
            }
            else
            {
                mpu.RegY = mpu.RegX;
                mpu.SetNZStatusFlagsFromValue(mpu.RegY);
            }
            mpu.NextCycle();
        }
    }

    internal class OP_TYA
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.AccumulatorIs8Bit)
            {
                mpu.RegAL = mpu.RegYL;
                mpu.SetNZStatusFlagsFromValue(mpu.RegAL);
            }
            else
            {
                mpu.RegA = mpu.RegY;
                mpu.SetNZStatusFlagsFromValue(mpu.RegA);
            }
            mpu.NextCycle();
        }
    }

    internal class OP_TYX
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.IndexesAre8Bit)
            {
                mpu.RegXL = mpu.RegYL;
                mpu.SetNZStatusFlagsFromValue(mpu.RegXL);
            }
            else
            {
                mpu.RegX = mpu.RegY;
                mpu.SetNZStatusFlagsFromValue(mpu.RegX);
            }
            mpu.NextCycle();
        }
    }

    internal class OP_TCD
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.RegDP = mpu.RegA;
            mpu.SetNZStatusFlagsFromValue(mpu.RegDP);
            mpu.NextCycle();
        }
    }

    internal class OP_TCS
    {
        internal void Execute(Microprocessor mpu)
        {
            if(mpu.FlagE)
            {
                mpu.RegSL = mpu.RegAL;
            }
            else
            {
                mpu.RegSP = mpu.RegA;
            }
            mpu.NextCycle();
        }
    }

    internal class OP_TDC 
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.RegA = mpu.RegDP;
            mpu.SetNZStatusFlagsFromValue(mpu.RegA);
            mpu.NextCycle();
        }
    }

    internal class OP_TSC
    {
        internal void Execute(Microprocessor mpu)
        {
            mpu.RegA = mpu.RegSP;
            mpu.SetNZStatusFlagsFromValue(mpu.RegA);
            mpu.NextCycle();
        }
    }

    internal class OP_XBA
    {
        internal void Execute(Microprocessor mpu)
        {
            byte temp = mpu.RegAL;
            mpu.RegAL = mpu.RegAH;
            mpu.RegAH = temp;
            mpu.SetNZStatusFlagsFromValue(mpu.RegA);
            mpu.NextCycle();
        }
    }

    internal class OP_XCE
    {
        internal void Execute(Microprocessor mpu)
        {
            bool carry = mpu.ReadStatusFlag(StatusFlags.C);
            mpu.SetStatusFlag(StatusFlags.C, mpu.FlagE);
            mpu.SetEmulationMode(carry);
            mpu.NextCycle();
        }
    }
}
