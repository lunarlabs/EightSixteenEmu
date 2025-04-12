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
    internal abstract class OpcodeCommand
    {
        internal abstract void Execute(Microprocessor mpu);

        internal static void BranchTo(Microprocessor mpu, uint address)
        {
            mpu.InternalCycle();
            if (mpu.FlagE && (byte)(mpu.RegPC >> 8) != (byte)(address >> 8))
            {
                mpu.InternalCycle();
            }
            mpu.RegPC = (ushort)address;
        }

        internal static void CopyMemory(Microprocessor mpu, ushort operand)
        {
            byte destination = (byte)(operand >> 8);
            mpu.RegDB = destination;
            byte source = (byte)operand;
            mpu.WriteByte(mpu.ReadByte((uint)(source << 16) | mpu.RegX),(uint)(destination <<16)| mpu.RegY);
            mpu.InternalCycle();
            mpu.InternalCycle();
            if (--mpu.RegA != 0xffff) mpu.RegPC -= 3; // jump back to the move instruction
        }
    }

    internal class OP_ADC : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            ushort addend = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            byte carry = mpu.ReadStatusFlag(StatusFlags.C) ? (byte)1 : (byte)0;
            if (mpu.AccumulatorIs8Bit)
            {
                if (mpu.ReadStatusFlag(StatusFlags.D))
                {
                    byte lo = (byte)((mpu.RegAL & 0x00ff) + (addend & 0x00ff) + carry);
                    byte hi = (byte)(((mpu.RegAL & 0xff00) + (addend & 0xff00)) >> 4);
                    if (lo > 9)
                    {
                        lo -= 10;
                        hi++;
                    }
                    if (hi > 9)
                    {
                        hi -= 10;
                        mpu.SetStatusFlag(StatusFlags.C, true);
                    }
                    else
                    {
                        mpu.SetStatusFlag(StatusFlags.C, false);
                    }
                    byte result = (byte)((hi << 4) | lo);
                    mpu.InternalCycle();
                    mpu.SetNZStatusFlagsFromValue(result);
                    mpu.RegAL = result;
                }
                else
                {
                    int result = mpu.RegAL + (byte)addend + carry;
                    mpu.SetStatusFlag(StatusFlags.C, (result & 0x100) != 0);
                    mpu.SetStatusFlag(StatusFlags.V, ((~(mpu.RegAL ^ addend)) & (mpu.RegAL ^ result) & 0x80) != 0);
                    mpu.InternalCycle();
                    mpu.SetNZStatusFlagsFromValue((byte)result);
                    mpu.RegAL = (byte)result;
                }
            }
            else
            {

                if (mpu.ReadStatusFlag(StatusFlags.D))
                {
                    ushort result = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        byte digit = (byte)(((mpu.RegA >> (4 * i)) & 0x0f) + ((addend >> (4 * i)) & 0x0f) + carry);
                        if (digit > 9)
                        {
                            digit -= 10;
                            carry = 1;
                        }
                        else
                        {
                            carry = 0;
                        }
                        result |= (ushort)(digit << (4 * i));
                    }
                    mpu.SetStatusFlag(StatusFlags.C, carry != 0);
                    mpu.InternalCycle();
                    mpu.SetNZStatusFlagsFromValue(result);
                    mpu.RegA = result;
                }
                else
                {
                    int result = mpu.RegA + addend + carry;
                    mpu.SetStatusFlag(StatusFlags.C, (result & 0x10000) != 0);
                    mpu.SetStatusFlag(StatusFlags.V, ((~(mpu.RegA ^ addend)) & (mpu.RegA ^ result) & 0x8000) != 0);
                    mpu.InternalCycle();
                    mpu.SetNZStatusFlagsFromValue((ushort)result);
                    mpu.RegA = (ushort)result;
                }
            }
        }
    }

    internal class OP_SBC : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            ushort subtrahend = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            byte carry = mpu.ReadStatusFlag(StatusFlags.C) ? (byte)1 : (byte)0;

            if (mpu.AccumulatorIs8Bit)
            {
                if (mpu.ReadStatusFlag(StatusFlags.D))
                {
                    byte lo = (byte)((mpu.RegAL & 0x00ff) - (subtrahend & 0x00ff) - (1 - carry));
                    byte hi = (byte)(((mpu.RegAL & 0xff00) - (subtrahend & 0xff00)) >> 4);

                    if ((lo & 0x80) != 0) // Borrow occurred
                    {
                        lo -= 6;
                        hi--;
                    }
                    if ((hi & 0x80) != 0) // Borrow occurred
                    {
                        hi -= 6;
                        mpu.SetStatusFlag(StatusFlags.C, false);
                    }
                    else
                    {
                        mpu.SetStatusFlag(StatusFlags.C, true);
                    }

                    byte result = (byte)((hi << 4) | (lo & 0x0f));
                    mpu.InternalCycle();
                    mpu.SetNZStatusFlagsFromValue(result);
                    mpu.RegAL = result;
                }
                else
                {
                    int result = mpu.RegAL - (byte)subtrahend - (1 - carry);
                    mpu.SetStatusFlag(StatusFlags.C, result >= 0);
                    mpu.SetStatusFlag(StatusFlags.V, ((mpu.RegAL ^ subtrahend) & (mpu.RegAL ^ result) & 0x80) != 0);
                    mpu.InternalCycle();
                    mpu.SetNZStatusFlagsFromValue((byte)result);
                    mpu.RegAL = (byte)result;
                }
            }
            else
            {
                if (mpu.ReadStatusFlag(StatusFlags.D))
                {
                    ushort result = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        byte digit = (byte)(((mpu.RegA >> (4 * i)) & 0x0f) - ((subtrahend >> (4 * i)) & 0x0f) - (1 - carry));
                        if ((digit & 0x80) != 0) // Borrow occurred
                        {
                            digit -= 10;
                            carry = 1;
                        }
                        else
                        {
                            carry = 0;
                        }
                        result |= (ushort)(digit << (4 * i));
                    }
                    mpu.SetStatusFlag(StatusFlags.C, carry == 0);
                    mpu.InternalCycle();
                    mpu.SetNZStatusFlagsFromValue(result);
                    mpu.RegA = result;
                }
                else
                {
                    int result = mpu.RegA - subtrahend - (1 - carry);
                    mpu.SetStatusFlag(StatusFlags.C, result >= 0);
                    mpu.SetStatusFlag(StatusFlags.V, ((mpu.RegA ^ subtrahend) & (mpu.RegA ^ result) & 0x8000) != 0);
                    mpu.InternalCycle();
                    mpu.SetNZStatusFlagsFromValue((ushort)result);
                    mpu.RegA = (ushort)result;
                }
            }
        }
    }

    internal class OP_CMP : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            if (mpu.AccumulatorIs8Bit)
            {
                int result = mpu.RegAL - (byte)operand;
                mpu.SetStatusFlag(StatusFlags.C, (byte)result <= mpu.RegAL);
                mpu.SetNZStatusFlagsFromValue((byte)result);
                mpu.InternalCycle();
            }
            else
            {
                int result = mpu.RegA - operand;
                mpu.SetStatusFlag(StatusFlags.C, (ushort)result <= mpu.RegA);
                mpu.SetNZStatusFlagsFromValue((ushort)result);
                mpu.InternalCycle();
            }
        }
    }

    internal class OP_CPX : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.IndexesAre8Bit);
            if (mpu.IndexesAre8Bit)
            {
                int result = mpu.RegXL - (byte)operand;
                mpu.SetStatusFlag(StatusFlags.C, (byte)result <= mpu.RegXL);
                mpu.SetNZStatusFlagsFromValue((byte)result);
                mpu.InternalCycle();
            }
            else
            {
                int result = mpu.RegX - operand;
                mpu.SetStatusFlag(StatusFlags.C, (ushort)result <= mpu.RegX);
                mpu.SetNZStatusFlagsFromValue((ushort)result);
                mpu.InternalCycle();
            }
        }
    }

    internal class OP_CPY : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu) {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.IndexesAre8Bit);
            if (mpu.IndexesAre8Bit)
            {
                int result = mpu.RegXL - (byte)operand;
                mpu.SetStatusFlag(StatusFlags.C, (byte)result <= mpu.RegYL);
                mpu.SetNZStatusFlagsFromValue((byte)result);
                mpu.InternalCycle();
            }
            else
            {
                int result = mpu.RegX - operand;
                mpu.SetStatusFlag(StatusFlags.C, (ushort)result <= mpu.RegY);
                mpu.SetNZStatusFlagsFromValue((ushort)result);
                mpu.InternalCycle();
            }
        }
    }

    internal class OP_DEC : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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
                mpu.InternalCycle();
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
                mpu.InternalCycle();
                mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            }
        }
    }

    internal class OP_DEX : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            if (mpu.IndexesAre8Bit)
            {
                mpu.SetNZStatusFlagsFromValue(--mpu.RegXL);
            }
            else
            {
                mpu.SetNZStatusFlagsFromValue(--mpu.RegX);
            }
            mpu.InternalCycle();
        }
    }

    internal class OP_DEY : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            if (mpu.IndexesAre8Bit)
            {
                mpu.SetNZStatusFlagsFromValue(--mpu.RegYL);
            }
            else
            {
                mpu.SetNZStatusFlagsFromValue(--mpu.RegY);
            }
            mpu.InternalCycle();
        }
    }

    internal class OP_INC : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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
                mpu.InternalCycle();
                mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            }
        }
    }

    internal class OP_INX : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            if (mpu.IndexesAre8Bit)
            {
                mpu.SetNZStatusFlagsFromValue(++mpu.RegXL);
            }
            else
            {
                mpu.SetNZStatusFlagsFromValue(++mpu.RegX);
            }
            mpu.InternalCycle();
        }
    }

    internal class OP_INY : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            if (mpu.IndexesAre8Bit)
            {
                mpu.SetNZStatusFlagsFromValue(++mpu.RegYL);
            }
            else
            {
                mpu.SetNZStatusFlagsFromValue(++mpu.RegY);
            }
            mpu.InternalCycle();
        }
    }

    internal class OP_AND : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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
            mpu.InternalCycle();
        }
    }

    internal class OP_EOR : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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
            mpu.InternalCycle();
        }
    }

    internal class OP_ORA : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            mpu.InternalCycle();
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
        }
    }

    internal class OP_BIT : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            if (mpu.AccumulatorIs8Bit)
            {
                if (mpu.CurrentAddressingMode != W65C816.AddressingMode.Immediate)
                {
                    mpu.SetStatusFlag(StatusFlags.V, (operand & 0x40) != 0);
                    mpu.SetStatusFlag(StatusFlags.N, (operand & 0x80) != 0);
                }
                mpu.SetStatusFlag(StatusFlags.Z, (mpu.RegAL & (byte)operand) == 0);
            }
            else
            {
                if (mpu.CurrentAddressingMode != W65C816.AddressingMode.Immediate)
                {
                    mpu.SetStatusFlag(StatusFlags.V, (operand & 0x4000) != 0);
                    mpu.SetStatusFlag(StatusFlags.N, (operand & 0x8000) != 0);
                }
                mpu.SetStatusFlag(StatusFlags.Z, (mpu.RegA & operand) == 0);
            }
        }
    }

    internal class OP_TRB : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, out uint address, mpu.AccumulatorIs8Bit);
            ushort mask = (ushort)(mpu.AccumulatorIs8Bit ? mpu.RegAL : mpu.RegA);
            operand &= (ushort)~mask;
            mpu.SetStatusFlag(StatusFlags.Z, operand == 0);
            mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            mpu.InternalCycle();
        }
    }

    internal class OP_TSB : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, out uint address, mpu.AccumulatorIs8Bit);
            ushort mask = (ushort)(mpu.AccumulatorIs8Bit ? mpu.RegAL : mpu.RegA);
            operand |= mask;
            mpu.SetStatusFlag(StatusFlags.Z, operand == 0);
            mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            mpu.InternalCycle();
        }
    }

    internal class OP_ASL : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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
                mpu.InternalCycle();
            }
            else
            {
                ushort operand = mpu.AddressingMode.GetOperand(mpu, out uint address, mpu.AccumulatorIs8Bit);
                mpu.SetStatusFlag(StatusFlags.C, (operand & (mpu.AccumulatorIs8Bit ? 0x80 : 0x8000)) != 0);
                operand <<= 1;
                mpu.SetNZStatusFlagsFromValue(operand, mpu.AccumulatorIs8Bit);
                mpu.InternalCycle();
                mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            }
        }
    }

    internal class OP_LSR : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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
                mpu.InternalCycle();
            }
            else
            {
                ushort operand = mpu.AddressingMode.GetOperand(mpu, out uint address, mpu.AccumulatorIs8Bit);
                mpu.SetStatusFlag(StatusFlags.C, (operand & 0x0001) != 0);
                operand >>>= 1;
                mpu.SetNZStatusFlagsFromValue(operand, mpu.AccumulatorIs8Bit);
                mpu.InternalCycle();
                mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            }
        }
    }

    internal class OP_ROL : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            uint operand;
            if (mpu.CurrentAddressingMode == W65C816.AddressingMode.Accumulator)
            {
                operand = mpu.AccumulatorIs8Bit ? mpu.RegAL : mpu.RegA;
                operand = operand << 1 | (mpu.ReadStatusFlag(StatusFlags.C) ? 1u : 0u);
                mpu.SetStatusFlag(StatusFlags.C, (mpu.AccumulatorIs8Bit ? (operand & 0x100) : (operand & 0x10000)) != 0);
                mpu.InternalCycle();
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
                mpu.SetNZStatusFlagsFromValue((ushort)operand, mpu.AccumulatorIs8Bit);
                mpu.InternalCycle();
                mpu.WriteValue((ushort)operand, mpu.AccumulatorIs8Bit, address);
            }
        }
    }

    internal class OP_ROR : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            ushort operand;
            bool carry;
            if (mpu.CurrentAddressingMode == W65C816.AddressingMode.Accumulator)
            {
                operand = mpu.AccumulatorIs8Bit ? mpu.RegAL : mpu.RegA;
                carry = operand % 2 == 1;
                operand = (ushort)(mpu.ReadStatusFlag(StatusFlags.C) ? (operand >> 1) | (mpu.AccumulatorIs8Bit ? 0x80 : 0x8000) : operand >> 1);
                mpu.SetStatusFlag(StatusFlags.C, carry);
                mpu.InternalCycle();
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
                mpu.InternalCycle();
                mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            }
        }
    }

    internal class OP_BCC : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            if (!mpu.ReadStatusFlag(StatusFlags.C))
            {
                BranchTo(mpu, destination);
            }
        }
    }

    internal class OP_BCS : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            if (mpu.ReadStatusFlag(StatusFlags.C))
            {
                BranchTo(mpu, destination);
            }
        }
    }

    internal class OP_BEQ : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            if (mpu.ReadStatusFlag(StatusFlags.Z))
            {
                BranchTo(mpu, destination);
            }
        }
    }

    internal class OP_BMI : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            if (mpu.ReadStatusFlag(StatusFlags.N))
            {
                BranchTo(mpu, destination);
            }
        }
    }

    internal class OP_BNE : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            if (!mpu.ReadStatusFlag(StatusFlags.Z))
            {
                BranchTo(mpu, destination);
            }
        }
    }

    internal class OP_BPL : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            if (!mpu.ReadStatusFlag(StatusFlags.N))
            {
                BranchTo(mpu, destination);
            }
        }
    }

    internal class OP_BRA : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            BranchTo(mpu, mpu.AddressingMode.GetAddress(mpu));
        }
    }

    internal class OP_BVC : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            if (!mpu.ReadStatusFlag(StatusFlags.V))
            {
                BranchTo(mpu, destination);
            }
        }
    }

    internal class OP_BVS : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            if (mpu.ReadStatusFlag(StatusFlags.V))
            {
                BranchTo(mpu, destination);
            }
        }
    }

    internal class OP_BRL : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            BranchTo(mpu, mpu.AddressingMode.GetAddress(mpu));
        }
    }

    internal class OP_JMP : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            if (mpu.CurrentAddressingMode == W65C816.AddressingMode.AbsoluteLong || mpu.CurrentAddressingMode == W65C816.AddressingMode.AbsoluteIndirectLong)
            {
                mpu.RegPB = (byte)(destination >> 16);
            }
            mpu.RegPC = (ushort)destination;
            mpu.InternalCycle();
        }
    }

    internal class OP_JSL : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            // wtf? In the SingleStepTests the order seems to be: read lower 16 bits, push PB, read bank address, push PC?!
            // functionally it doesn't matter, but the order is strange... if one-to-one is really required,
            // use the commented replacement...
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            mpu.PushByte(mpu.RegPB);
            mpu.PushWord((ushort)(mpu.RegPC - 1)); // that ugly off-by-one raised its head again in testing...
            mpu.RegPC = (ushort)destination;
            mpu.RegPB = (byte)(destination >> 16);
            mpu.InternalCycle();

            // Replacement:
            //ushort destAddr = mpu.ReadWord();
            //mpu.PushByte(mpu.RegPB);
            //byte bankAddr = mpu.ReadByte();
            //mpu.PushWord(mpu.RegPC);
            //mpu.RegPC = destAddr;
            //mpu.RegPB = bankAddr;
            //mpu.InternalCycle();
        }
    }

    internal class OP_JSR : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            mpu.PushWord((ushort)(mpu.RegPC - 1));
            mpu.RegPC = (ushort)destination;
            mpu.InternalCycle();
        }
    }

    internal class OP_RTS : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.RegPC = mpu.PullWord();
            mpu.InternalCycle();
        }
    }

    internal class OP_RTL : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.RegPC = mpu.PullWord();
            mpu.RegPB = mpu.PullByte();
            mpu.InternalCycle();
        }
    }

    internal class OP_RTI : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
            mpu.InternalCycle();
            mpu.RegSR = (StatusFlags)mpu.PullByte();
            mpu.RegPC = mpu.PullWord();
            if (!mpu.FlagE)
            {
                mpu.RegPB = mpu.PullByte();
            }
        }
    }
    
    internal class OP_BRK : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.Interrupt(InterruptType.BRK);
        }
    }

    internal class OP_COP : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.Interrupt(InterruptType.COP);
        }
    }

    internal class OP_CLC : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.C, false);
            mpu.InternalCycle();
        }
    }

    internal class OP_CLD : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.D, false);
            mpu.InternalCycle();
        }
    }

    internal class OP_CLI : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.I, false);
            mpu.InternalCycle();
        }
    }

    internal class OP_CLV : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.V, false);
            mpu.InternalCycle();
        }
    }

    internal class OP_SEC : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.C, true);
            mpu.InternalCycle();
        }
    }

    internal class OP_SED : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.D, true);
            mpu.InternalCycle();
        }
    }

    internal class OP_SEI : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.I, true);
            mpu.InternalCycle();
        }
    }

    internal class OP_REP : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            byte operand = (byte)mpu.AddressingMode.GetOperand(mpu, true);
            if (mpu.FlagE)
            {
                // M and X flags cannot be set in emulation mode
                operand &= 0xCF;
            }
            mpu.RegSR &= (StatusFlags)~operand;
            mpu.InternalCycle();
        }
    }

    internal class OP_SEP : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            byte operand = (byte)mpu.AddressingMode.GetOperand(mpu, true);
            if (mpu.FlagE)
            {
                // M and X flags cannot be set in emulation mode
                operand &= 0xCF;
            }
            mpu.RegSR |= (StatusFlags)operand;
            mpu.InternalCycle();
        }
    }

    internal class OP_LDA : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu) 
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
            mpu.InternalCycle();
        }
    }

    internal class OP_LDX : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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
            mpu.InternalCycle();
        }
    }

    internal class OP_LDY : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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
            mpu.InternalCycle();
        }
    }

    internal class OP_STA : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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

    internal class OP_STX : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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

    internal class OP_STY : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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

    internal class OP_STZ : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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

    internal class OP_MVN : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            CopyMemory(mpu, operand);
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

    internal class OP_MVP : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            CopyMemory(mpu, operand);
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

    internal class OP_NOP : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
        }
    }

    internal class OP_WDM : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.RegPC++;
            mpu.InternalCycle();
        }
    }

    internal class OP_PEA : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.PushWord(mpu.AddressingMode.GetOperand(mpu, false));
        }
    }

    internal class OP_PEI : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.PushWord(mpu.AddressingMode.GetOperand(mpu, false));
        }
    }

    internal class OP_PER : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.PushWord(mpu.AddressingMode.GetOperand(mpu, false));
        }
    }

    internal class OP_PHA : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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

    internal class OP_PHX : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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

    internal class OP_PHY : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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

    internal class OP_PLA : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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

    internal class OP_PLX : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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

    internal class OP_PLY : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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

    internal class OP_PHB : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.PushByte(mpu.RegDB);
        }
    }

    internal class OP_PHD : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.PushWord(mpu.RegDP);
        }
    }

    internal class OP_PHK : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.PushByte(mpu.RegPB);
        }
    }

    internal class OP_PHP : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.PushByte((byte)mpu.RegSR);
        }
    }

    internal class OP_PLB : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.RegDB = mpu.PullByte();
            mpu.SetNZStatusFlagsFromValue(mpu.RegDB);
        }
    }

    internal class OP_PLD : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.RegDP = mpu.PullWord();
            mpu.SetNZStatusFlagsFromValue(mpu.RegDP);
        }
    }

    internal class OP_PLP : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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
    internal class OP_STP : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
            mpu.Stop();
        }
    }

    internal class OP_WAI : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
            mpu.Wait();
        }
    }

    internal class OP_TAX : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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
            mpu.InternalCycle();
        }
    }

    internal class OP_TAY : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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
            mpu.InternalCycle();
        }
    }

    internal class OP_TSX : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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

    internal class OP_TXA : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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
            mpu.InternalCycle();
        }
    }

    internal class OP_TXS : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            if (mpu.FlagE || mpu.IndexesAre8Bit)
            {
                mpu.RegSL = mpu.RegXL;
            }
            else
            {
                mpu.RegSP = mpu.RegX;
            }
            mpu.InternalCycle();
        }
    }

    internal class OP_TXY : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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
            mpu.InternalCycle();
        }
    }

    internal class OP_TYA : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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
            mpu.InternalCycle();
        }
    }

    internal class OP_TYX : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
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
            mpu.InternalCycle();
        }
    }

    internal class OP_TCD : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.RegDP = mpu.RegA;
            mpu.SetNZStatusFlagsFromValue(mpu.RegDP);
            mpu.InternalCycle();
        }
    }

    internal class OP_TCS : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            if(mpu.FlagE)
            {
                mpu.RegSL = mpu.RegAL;
            }
            else
            {
                mpu.RegSP = mpu.RegA;
            }
            mpu.InternalCycle();
        }
    }

    internal class OP_TDC : OpcodeCommand 
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.RegA = mpu.RegDP;
            mpu.SetNZStatusFlagsFromValue(mpu.RegA);
            mpu.InternalCycle();
        }
    }

    internal class OP_TSC : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            mpu.RegA = mpu.RegSP;
            mpu.SetNZStatusFlagsFromValue(mpu.RegA);
            mpu.InternalCycle();
        }
    }

    internal class OP_XBA : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            byte temp = mpu.RegAL;
            mpu.RegAL = mpu.RegAH;
            mpu.RegAH = temp;
            mpu.SetNZStatusFlagsFromValue(mpu.RegAL);
            mpu.InternalCycle();
        }
    }

    internal class OP_XCE : OpcodeCommand
    {
        internal override void Execute(Microprocessor mpu)
        {
            bool carry = mpu.ReadStatusFlag(StatusFlags.C);
            mpu.SetStatusFlag(StatusFlags.C, mpu.FlagE);
            mpu.SetEmulationMode(carry);
            mpu.InternalCycle();
        }
    }
}
