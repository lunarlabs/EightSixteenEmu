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
            byte carry = mpu.ReadStatusFlag(Microprocessor.StatusFlags.C) ? (byte)1 : (byte)0;
            if (mpu.AccumulatorIs8Bit)
            {
                int result = mpu.RegAL + addend + carry;
                if (mpu.ReadStatusFlag(Microprocessor.StatusFlags.D))
                {
                    {
                        if (((result) & 0x0f) > 0x09) result += 0x06;
                        if (((result) & 0xf0) > 0x90) result += 0x60;
                    }
                }
                mpu.SetStatusFlag(Microprocessor.StatusFlags.C, (result & 0x100) != 0);
                mpu.SetStatusFlag(Microprocessor.StatusFlags.V, ((~(mpu.RegAL ^ addend)) & (mpu.RegAL ^ result) & 0x80) != 0);
                mpu.NextCycle();
                mpu.SetNZStatusFlagsFromValue((byte)result);
                mpu.RegAL = (byte)result;
            }
            else
            {
                int result = mpu.RegA + addend + carry;
                if (mpu.ReadStatusFlag(Microprocessor.StatusFlags.D))
                {
                    {
                        if (((result) & 0x000f) > 0x0009) result += 0x0006;
                        if (((result) & 0x00f0) > 0x0090) result += 0x0060;
                        if (((result) & 0x0f00) > 0x0900) result += 0x0600;
                        if (((result) & 0xf000) > 0x9000) result += 0x6000;
                    }
                }
                mpu.SetStatusFlag(Microprocessor.StatusFlags.C, (result & 0x10000) != 0);
                mpu.SetStatusFlag(Microprocessor.StatusFlags.V, ((~(mpu.RegA ^ addend)) & (mpu.RegA ^ result) & 0x8000) != 0);
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
            byte carry = mpu.ReadStatusFlag(Microprocessor.StatusFlags.C) ? (byte)1 : (byte)0;
            if (mpu.AccumulatorIs8Bit)
            {
                int result = mpu.RegAL + ~(byte)subtrahend - (1 - carry);
                if (mpu.ReadStatusFlag(Microprocessor.StatusFlags.D))
                {
                    {
                        if (((result) & 0x0f) > 0x09) result += 0x06;
                        if (((result) & 0xf0) > 0x90) result += 0x60;
                    }
                }
                mpu.SetStatusFlag(Microprocessor.StatusFlags.C, (byte)result >= (byte)subtrahend);
                mpu.SetStatusFlag(Microprocessor.StatusFlags.V, ((mpu.RegAL ^ subtrahend) & (mpu.RegAL ^ result) & 0x80) != 0);
                mpu.NextCycle();
                mpu.SetNZStatusFlagsFromValue((byte)result);
                mpu.RegAL = (byte)result;
            }
            else
            {
                int result = mpu.RegA - subtrahend - (1 - carry);
                if (mpu.ReadStatusFlag(Microprocessor.StatusFlags.D))
                {
                    {
                        if (((result) & 0x000f) > 0x0009) result += 0x0006;
                        if (((result) & 0x00f0) > 0x0090) result += 0x0060;
                        if (((result) & 0x0f00) > 0x0900) result += 0x0600;
                        if (((result) & 0xf000) > 0x9000) result += 0x6000;
                    }
                }
                mpu.SetStatusFlag(Microprocessor.StatusFlags.C, (ushort)result >= subtrahend);
                mpu.SetStatusFlag(Microprocessor.StatusFlags.V, ((mpu.RegA ^ subtrahend) & (mpu.RegA ^ result) & 0x8000) != 0);
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
                mpu.SetStatusFlag(Microprocessor.StatusFlags.C, (byte)result <= mpu.RegAL);
                mpu.SetNZStatusFlagsFromValue((byte)result);
                mpu.NextCycle();
            }
            else
            {
                int result = mpu.RegA - operand;
                mpu.SetStatusFlag(Microprocessor.StatusFlags.C, (ushort)result <= mpu.RegA);
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
                mpu.SetStatusFlag(Microprocessor.StatusFlags.C, (byte)result <= mpu.RegXL);
                mpu.SetNZStatusFlagsFromValue((byte)result);
                mpu.NextCycle();
            }
            else
            {
                int result = mpu.RegX - operand;
                mpu.SetStatusFlag(Microprocessor.StatusFlags.C, (ushort)result <= mpu.RegX);
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
                mpu.SetStatusFlag(Microprocessor.StatusFlags.C, (byte)result <= mpu.RegYL);
                mpu.SetNZStatusFlagsFromValue((byte)result);
                mpu.NextCycle();
            }
            else
            {
                int result = mpu.RegX - operand;
                mpu.SetStatusFlag(Microprocessor.StatusFlags.C, (ushort)result <= mpu.RegY);
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
                    mpu.SetStatusFlag(Microprocessor.StatusFlags.V, (operand & 0x40) != 0);
                    mpu.SetStatusFlag(Microprocessor.StatusFlags.N, (operand & 0x80) != 0);
                }
                mpu.SetStatusFlag(Microprocessor.StatusFlags.Z, (mpu.RegAL & (byte)operand) == 0);
            }
            else
            {
                if (mpu.CurrentAddressingMode == W65C816.AddressingMode.Immediate)
                {
                    mpu.SetStatusFlag(Microprocessor.StatusFlags.V, (operand & 0x4000) != 0);
                    mpu.SetStatusFlag(Microprocessor.StatusFlags.N, (operand & 0x8000) != 0);
                }
                mpu.SetStatusFlag(Microprocessor.StatusFlags.Z, (mpu.RegA & operand) == 0);
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
            mpu.SetStatusFlag(Microprocessor.StatusFlags.Z, operand == 0);
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
            mpu.SetStatusFlag(Microprocessor.StatusFlags.Z, operand == 0);
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
                    mpu.SetStatusFlag(Microprocessor.StatusFlags.C, (mpu.RegAL & 0x80) != 0);
                    mpu.RegAL <<= 1;
                    mpu.SetNZStatusFlagsFromValue(mpu.RegAL);
                }
                else
                {
                    mpu.SetStatusFlag(Microprocessor.StatusFlags.C, (mpu.RegA & 0x8000) != 0);
                    mpu.RegA <<= 1;
                    mpu.SetNZStatusFlagsFromValue(mpu.RegA);
                }
                mpu.NextCycle();
            }
            else
            {
                ushort operand = mpu.AddressingMode.GetOperand(mpu, out uint address, mpu.AccumulatorIs8Bit);
                mpu.SetStatusFlag(Microprocessor.StatusFlags.C, (operand & 0x8000) != 0);
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
                    mpu.SetStatusFlag(Microprocessor.StatusFlags.C, (mpu.RegAL & 0x01) != 0);
                    mpu.RegAL >>>= 1;
                    mpu.SetNZStatusFlagsFromValue(mpu.RegAL);
                }
                else
                {
                    mpu.SetStatusFlag(Microprocessor.StatusFlags.C, (mpu.RegA & 0x0001) != 0);
                    mpu.RegA >>>= 1;
                    mpu.SetNZStatusFlagsFromValue(mpu.RegA);
                }
                mpu.NextCycle();
            }
            else
            {
                ushort operand = mpu.AddressingMode.GetOperand(mpu, out uint address, mpu.AccumulatorIs8Bit);
                mpu.SetStatusFlag(Microprocessor.StatusFlags.C, (operand & 0x0001) != 0);
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
                operand = operand << 1 | (mpu.ReadStatusFlag(Microprocessor.StatusFlags.C) ? 1u : 0u);
                mpu.SetStatusFlag(Microprocessor.StatusFlags.C, (mpu.AccumulatorIs8Bit ? (operand & 0x100) : (operand & 0x10000)) != 0);
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
                operand = operand << 1 | (mpu.ReadStatusFlag(Microprocessor.StatusFlags.C) ? 1u : 0u);
                mpu.SetStatusFlag(Microprocessor.StatusFlags.C, (mpu.AccumulatorIs8Bit ? (operand & 0x100) : (operand & 0x10000)) != 0);
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
                operand = (ushort)(mpu.ReadStatusFlag(Microprocessor.StatusFlags.C) ? (operand >> 1) | (mpu.AccumulatorIs8Bit ? 0x80 : 0x8000) : operand >> 1);
                mpu.SetStatusFlag(Microprocessor.StatusFlags.C, carry);
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
                operand = (ushort)(mpu.ReadStatusFlag(Microprocessor.StatusFlags.C) ? (operand >> 1) | (mpu.AccumulatorIs8Bit ? 0x80 : 0x8000) : operand >> 1);
                mpu.SetStatusFlag(Microprocessor.StatusFlags.C, carry);
                mpu.NextCycle();
                mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            }
        }
    }

    internal class OP_BCC
    {
        internal void Execute(Microprocessor mpu)
        {
            if (!mpu.ReadStatusFlag(Microprocessor.StatusFlags.C))
            {
                IOpcodeCommand.BranchTo(mpu, mpu.AddressingMode.GetAddress(mpu));
            }
        }
    }

    internal class OP_BCS
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.ReadStatusFlag(Microprocessor.StatusFlags.C))
            {
                IOpcodeCommand.BranchTo(mpu, mpu.AddressingMode.GetAddress(mpu));
            }
        }
    }

    internal class OP_BEQ
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.ReadStatusFlag(Microprocessor.StatusFlags.Z))
            {
                IOpcodeCommand.BranchTo(mpu, mpu.AddressingMode.GetAddress(mpu));
            }
        }
    }

    internal class OP_BMI
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.ReadStatusFlag(Microprocessor.StatusFlags.N))
            {
                IOpcodeCommand.BranchTo(mpu, mpu.AddressingMode.GetAddress(mpu));
            }
        }
    }

    internal class OP_BNE
    {
        internal void Execute(Microprocessor mpu)
        {
            if (!mpu.ReadStatusFlag(Microprocessor.StatusFlags.Z))
            {
                IOpcodeCommand.BranchTo(mpu, mpu.AddressingMode.GetAddress(mpu));
            }
        }
    }

    internal class OP_BPL
    {
        internal void Execute(Microprocessor mpu)
        {
            if (!mpu.ReadStatusFlag(Microprocessor.StatusFlags.N))
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
            if (!mpu.ReadStatusFlag(Microprocessor.StatusFlags.V))
            {
                IOpcodeCommand.BranchTo(mpu, mpu.AddressingMode.GetAddress(mpu));
            }
        }
    }

    internal class OP_BVS
    {
        internal void Execute(Microprocessor mpu)
        {
            if (mpu.ReadStatusFlag(Microprocessor.StatusFlags.V))
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
}
