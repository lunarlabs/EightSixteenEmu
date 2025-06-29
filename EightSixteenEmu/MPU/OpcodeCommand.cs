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
 *  Opcodes
 */

using System.Net;
using System.Text.RegularExpressions;
using static EightSixteenEmu.MPU.Microprocessor;

namespace EightSixteenEmu.MPU
{
    internal abstract class OpcodeCommand
    {
        [Obsolete("Use Enqueue instead. This method will be removed in a future version.")]
        internal abstract void Execute(Microprocessor mpu);

        internal virtual void Enqueue(Microprocessor mpu, AddressingModeStrategy addressing)
        {
            throw new NotImplementedException("Enqueue not implemented yet for " + this.GetType().Name);
        }

        internal static void BranchTo(Microprocessor mpu, uint address)
        {
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
            mpu.WriteByte(mpu.ReadByte((uint)(source << 16) | mpu.RegX), (uint)(destination << 16) | mpu.RegY);
            mpu.InternalCycle();
            mpu.InternalCycle();
            if (--mpu.RegA != 0xffff) mpu.RegPC -= 3; // jump back to the move instruction
        }
    }

    internal class OP_ADC : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            ushort addend = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            byte carry = mpu.ReadStatusFlag(StatusFlags.C) ? (byte)1 : (byte)0;
            if (mpu.AccumulatorIs8Bit)
            {
                if (mpu.ReadStatusFlag(StatusFlags.D))
                {
                    byte lo = (byte)((mpu.RegAL & 0x0f) + (addend & 0x0f) + carry);
                    byte hi = (byte)((mpu.RegAL >> 4 & 0x0f) + (addend >> 4 & 0x0f));
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
                    byte result = (byte)((hi << 4) | lo & 0x0f);
                    //mpu.InternalCycle();
                    mpu.SetStatusFlag(StatusFlags.V, ((~(mpu.RegAL ^ addend)) & (mpu.RegAL ^ result) & 0x80) != 0);
                    mpu.SetNZStatusFlagsFromValue(result);
                    mpu.RegAL = result;
                }
                else
                {
                    int result = mpu.RegAL + (byte)addend + carry;
                    mpu.SetStatusFlag(StatusFlags.C, (result & 0x100) != 0);
                    mpu.SetStatusFlag(StatusFlags.V, ((~(mpu.RegAL ^ addend)) & (mpu.RegAL ^ result) & 0x80) != 0);
                    //mpu.InternalCycle();
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
                        result |= (ushort)((digit & 0x0f) << (4 * i));
                    }
                    mpu.SetStatusFlag(StatusFlags.C, carry != 0);
                    //mpu.InternalCycle();
                    mpu.SetStatusFlag(StatusFlags.V, ((~(mpu.RegAL ^ addend)) & (mpu.RegAL ^ result) & 0x80) != 0);
                    mpu.SetNZStatusFlagsFromValue(result);
                    mpu.RegA = result;
                }
                else
                {
                    int result = mpu.RegA + addend + carry;
                    mpu.SetStatusFlag(StatusFlags.C, (result & 0x10000) != 0);
                    mpu.SetStatusFlag(StatusFlags.V, ((~(mpu.RegA ^ addend)) & (mpu.RegA ^ result) & 0x8000) != 0);
                    //mpu.InternalCycle();
                    mpu.SetNZStatusFlagsFromValue((ushort)result);
                    mpu.RegA = (ushort)result;
                }
            }
        }
    }

    internal class OP_SBC : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            ushort subtrahend = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            //mpu.InternalCycle(); // Account for operand fetch cycle

            if (mpu.AccumulatorIs8Bit)
            {
                int carry = mpu.ReadStatusFlag(StatusFlags.C) ? 1 : 0;

                if (mpu.ReadStatusFlag(StatusFlags.D)) // BCD mode
                {
                    byte a = mpu.RegAL;
                    byte b = (byte)subtrahend;
                    int diff = a - b - (1 - carry);

                    byte lo = (byte)((a & 0x0F) - (b & 0x0F) - (1 - carry));
                    byte hi = (byte)((a >> 4) - (b >> 4));

                    if ((lo & 0x10) != 0) // Borrow occurred in the low nibble
                    {
                        lo -= 0x06;
                        hi--;
                    }

                    if ((hi & 0x10) != 0) // Borrow in high nibble
                    {
                        hi -= 0x06;
                        mpu.SetStatusFlag(StatusFlags.C, false);
                    }
                    else
                    {
                        mpu.SetStatusFlag(StatusFlags.C, true);
                    }

                    byte result = (byte)((hi << 4) | (lo & 0x0F));
                    mpu.SetStatusFlag(StatusFlags.V, ((a ^ b) & (a ^ result) & 0x80) != 0);
                    mpu.SetNZStatusFlagsFromValue(result);
                    mpu.RegAL = result;
                }
                else // Binary mode
                {
                    byte a = mpu.RegAL;
                    byte b = (byte)subtrahend;
                    int result = a - b - (1 - carry);

                    mpu.SetStatusFlag(StatusFlags.C, result >= 0);
                    mpu.SetStatusFlag(StatusFlags.V, ((a ^ b) & (a ^ result) & 0x80) != 0);
                    mpu.SetNZStatusFlagsFromValue((byte)result);
                    mpu.RegAL = (byte)result;
                }
            }
            else
            {
                int carry = mpu.ReadStatusFlag(StatusFlags.C) ? 1 : 0;

                if (mpu.ReadStatusFlag(StatusFlags.D)) // BCD mode
                {
                    ushort result = 0;
                    int borrow = 1 - carry;
                    for (int i = 0; i < 4; i++)
                    {
                        byte digitA = (byte)((mpu.RegA >> (4 * i)) & 0x0F);
                        byte digitB = (byte)((subtrahend >> (4 * i)) & 0x0F);
                        int digit = (digitA - digitB - borrow);

                        if (digit < 0) // Borrow occurred
                        {
                            digit += 10;
                            borrow = 1;
                        }
                        else
                        {
                            borrow = 0;
                        }

                        result |= (ushort)((digit & 0x0f) << (4 * i));
                    }

                    mpu.SetStatusFlag(StatusFlags.C, borrow == 0);
                    mpu.SetStatusFlag(StatusFlags.V, ((mpu.RegA ^ subtrahend) & (mpu.RegA ^ result) & 0x8000) != 0);
                    mpu.SetNZStatusFlagsFromValue(result);
                    mpu.RegA = result;
                }
                else // Binary mode
                {
                    ushort a = mpu.RegA;
                    ushort b = subtrahend;
                    int result = a - b - (1 - carry);

                    mpu.SetStatusFlag(StatusFlags.C, result >= 0);
                    mpu.SetStatusFlag(StatusFlags.V, ((a ^ b) & (a ^ result) & 0x8000) != 0);
                    mpu.SetNZStatusFlagsFromValue((ushort)result);
                    mpu.RegA = (ushort)result;
                }
            }
        }
    }

    internal class OP_CMP : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.AccumulatorIs8Bit);
            if (mpu.AccumulatorIs8Bit)
            {
                int result = mpu.RegAL - (byte)operand;
                mpu.SetStatusFlag(StatusFlags.C, (byte)result <= mpu.RegAL);
                mpu.SetNZStatusFlagsFromValue((byte)result);
                //mpu.InternalCycle();
            }
            else
            {
                int result = mpu.RegA - operand;
                mpu.SetStatusFlag(StatusFlags.C, (ushort)result <= mpu.RegA);
                mpu.SetNZStatusFlagsFromValue((ushort)result);
                //mpu.InternalCycle();
            }
        }
    }

    internal class OP_CPX : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.IndexesAre8Bit);
            if (mpu.IndexesAre8Bit)
            {
                int result = mpu.RegXL - (byte)operand;
                mpu.SetStatusFlag(StatusFlags.C, (byte)result <= mpu.RegXL);
                mpu.SetNZStatusFlagsFromValue((byte)result);
                //mpu.InternalCycle();
            }
            else
            {
                int result = mpu.RegX - operand;
                mpu.SetStatusFlag(StatusFlags.C, (ushort)result <= mpu.RegX);
                mpu.SetNZStatusFlagsFromValue((ushort)result);
                //mpu.InternalCycle();
            }
        }
    }

    internal class OP_CPY : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, mpu.IndexesAre8Bit, true);
            if (mpu.IndexesAre8Bit)
            {
                int result = mpu.RegYL - (byte)operand;
                mpu.SetStatusFlag(StatusFlags.C, (byte)result <= mpu.RegYL);
                mpu.SetNZStatusFlagsFromValue((byte)result);
                //mpu.InternalCycle();
            }
            else
            {
                int result = mpu.RegY - operand;
                mpu.SetStatusFlag(StatusFlags.C, (ushort)result <= mpu.RegY);
                mpu.SetNZStatusFlagsFromValue((ushort)result);
                //mpu.InternalCycle();
            }
        }
    }

    internal class OP_DEC : OpcodeCommand
    {
        [Obsolete]
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
                if (mpu.FlagE)
                {
                    mpu.WriteByte((byte)operand, address);
                }
                else
                {
                    mpu.InternalCycle();
                }
                operand--;
                if (mpu.AccumulatorIs8Bit)
                {
                    mpu.SetNZStatusFlagsFromValue((byte)operand);
                }
                else
                {
                    mpu.SetNZStatusFlagsFromValue((ushort)operand);
                }
                //mpu.InternalCycle();
                mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            }
        }
    }

    internal class OP_DEX : OpcodeCommand
    {
        [Obsolete]
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
        [Obsolete]
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
        [Obsolete]
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
                mpu.InternalCycle();
            }
            else
            {
                ushort operand = mpu.AddressingMode.GetOperand(mpu, out uint address, mpu.AccumulatorIs8Bit);
                if (mpu.FlagE)
                {
                    mpu.WriteByte((byte)operand, address);
                }
                else
                {
                    mpu.InternalCycle();
                }
                operand++;
                if (mpu.AccumulatorIs8Bit)
                {
                    mpu.SetNZStatusFlagsFromValue((byte)operand);
                }
                else
                {
                    mpu.SetNZStatusFlagsFromValue((ushort)operand);
                }
                //mpu.InternalCycle();
                mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            }
        }
    }

    internal class OP_INX : OpcodeCommand
    {
        [Obsolete]
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
        [Obsolete]
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
        [Obsolete]
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
            //mpu.InternalCycle();
        }
    }

    internal class OP_EOR : OpcodeCommand
    {
        [Obsolete]
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
        }
    }

    internal class OP_ORA : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
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
        }
    }

    internal class OP_BIT : OpcodeCommand
    {
        [Obsolete]
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
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, out uint address, mpu.AccumulatorIs8Bit);
            ushort mask = (ushort)(mpu.AccumulatorIs8Bit ? mpu.RegAL : mpu.RegA);
            if (mpu.FlagE)
            {
                mpu.WriteByte((byte)operand, address);
            }
            else
            {
                mpu.InternalCycle();
            }
            mpu.SetStatusFlag(StatusFlags.Z, ((mpu.AccumulatorIs8Bit ? (byte)operand : operand) & mask) == 0);
            operand &= (ushort)~mask;
            mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
        }
    }

    internal class OP_TSB : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            ushort operand = mpu.AddressingMode.GetOperand(mpu, out uint address, mpu.AccumulatorIs8Bit);
            ushort mask = (ushort)(mpu.AccumulatorIs8Bit ? mpu.RegAL : mpu.RegA);
            if (mpu.FlagE) mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            else mpu.InternalCycle();
            mpu.SetStatusFlag(StatusFlags.Z, ((mpu.AccumulatorIs8Bit ? (byte)operand : operand) & mask) == 0);
            operand |= mask;
            mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            //mpu.InternalCycle();
        }
    }

    internal class OP_ASL : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            if (mpu.CurrentAddressingMode == W65C816.AddressingMode.Accumulator)
            {
                mpu.InternalCycle();
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
            }
            else
            {
                //mpu.InternalCycle();
                ushort operand = mpu.AddressingMode.GetOperand(mpu, out uint address, mpu.AccumulatorIs8Bit);
                mpu.SetStatusFlag(StatusFlags.C, (operand & (mpu.AccumulatorIs8Bit ? 0x80 : 0x8000)) != 0);
                if (mpu.FlagE)
                {
                    mpu.WriteByte((byte)operand, address);
                }
                else
                {
                    mpu.InternalCycle();
                }
                operand <<= 1;
                mpu.SetNZStatusFlagsFromValue(operand, mpu.AccumulatorIs8Bit);
                mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            }
        }
    }

    internal class OP_LSR : OpcodeCommand
    {
        [Obsolete]
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
                if (mpu.FlagE)
                {
                    mpu.WriteByte((byte)operand, address);
                }
                else
                {
                    mpu.InternalCycle();
                }
                operand >>>= 1;
                mpu.SetNZStatusFlagsFromValue(operand, mpu.AccumulatorIs8Bit);
                //mpu.InternalCycle();
                mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            }
        }
    }

    internal class OP_ROL : OpcodeCommand
    {
        [Obsolete]
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
                if (mpu.FlagE)
                {
                    mpu.WriteByte((byte)operand, address);
                }
                else
                {
                    mpu.InternalCycle();
                }
                operand = operand << 1 | (mpu.ReadStatusFlag(StatusFlags.C) ? 1u : 0u);
                mpu.SetStatusFlag(StatusFlags.C, (mpu.AccumulatorIs8Bit ? (operand & 0x100) : (operand & 0x10000)) != 0);
                mpu.SetNZStatusFlagsFromValue((ushort)operand, mpu.AccumulatorIs8Bit);
                //mpu.InternalCycle();
                mpu.WriteValue((ushort)operand, mpu.AccumulatorIs8Bit, address);
            }
        }
    }

    internal class OP_ROR : OpcodeCommand
    {
        [Obsolete]
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
                if (mpu.FlagE)
                {
                    mpu.WriteByte((byte)operand, address);
                }
                else
                {
                    mpu.InternalCycle();
                }
                carry = operand % 2 == 1;
                operand = (ushort)(mpu.ReadStatusFlag(StatusFlags.C) ? (operand >> 1) | (mpu.AccumulatorIs8Bit ? 0x80 : 0x8000) : operand >> 1);
                mpu.SetStatusFlag(StatusFlags.C, carry);
                //mpu.InternalCycle();
                mpu.SetNZStatusFlagsFromValue(operand, mpu.AccumulatorIs8Bit);
                mpu.WriteValue(operand, mpu.AccumulatorIs8Bit, address);
            }
        }
    }

    internal class OP_BCC : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            if (!mpu.ReadStatusFlag(StatusFlags.C))
            {
                mpu.InternalCycle();
                BranchTo(mpu, destination);
            }
        }
    }

    internal class OP_BCS : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            if (mpu.ReadStatusFlag(StatusFlags.C))
            {
                mpu.InternalCycle();
                BranchTo(mpu, destination);
            }
        }
    }

    internal class OP_BEQ : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            if (mpu.ReadStatusFlag(StatusFlags.Z))
            {
                mpu.InternalCycle();
                BranchTo(mpu, destination);
            }
        }
    }

    internal class OP_BMI : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            if (mpu.ReadStatusFlag(StatusFlags.N))
            {
                mpu.InternalCycle();
                BranchTo(mpu, destination);
            }
        }
    }

    internal class OP_BNE : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            if (!mpu.ReadStatusFlag(StatusFlags.Z))
            {
                mpu.InternalCycle();
                BranchTo(mpu, destination);
            }
        }
    }

    internal class OP_BPL : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            if (!mpu.ReadStatusFlag(StatusFlags.N))
            {
                mpu.InternalCycle();
                BranchTo(mpu, destination);
            }
        }
    }

    internal class OP_BRA : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            BranchTo(mpu, mpu.AddressingMode.GetAddress(mpu));
            mpu.InternalCycle();
        }
    }

    internal class OP_BVC : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            if (!mpu.ReadStatusFlag(StatusFlags.V))
            {
                mpu.InternalCycle();
                BranchTo(mpu, destination);
            }
        }
    }

    internal class OP_BVS : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            if (mpu.ReadStatusFlag(StatusFlags.V))
            {
                mpu.InternalCycle();
                BranchTo(mpu, destination);
            }
        }
    }

    internal class OP_BRL : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.RegPC = (ushort)mpu.AddressingMode.GetAddress(mpu);
            mpu.InternalCycle();
        }
    }

    internal class OP_JMP : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            uint destination = mpu.AddressingMode.GetAddress(mpu);
            if (mpu.CurrentAddressingMode == W65C816.AddressingMode.AbsoluteLong || mpu.CurrentAddressingMode == W65C816.AddressingMode.AbsoluteIndirectLong)
            {
                mpu.RegPB = (byte)(destination >> 16);
            }
            mpu.RegPC = (ushort)destination;
            if (mpu.CurrentAddressingMode == W65C816.AddressingMode.AbsoluteIndexedIndirect) mpu.InternalCycle();
        }
    }

    internal class OP_JSL : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            // wtf? In the SingleStepTests the order seems to be: read lower 16 bits, push PB, read bank address, push PC?!
            // functionally it doesn't matter, but the order is strange... if one-to-one is really required,
            // use the commented replacement...
            //uint destination = mpu.AddressingMode.GetAddress(mpu);
            //mpu.PushByte(mpu.RegPB);
            //mpu.PushWord((ushort)(mpu.RegPC - 1)); // that ugly off-by-one raised its head again in testing...
            //mpu.RegPC = (ushort)destination;
            //mpu.RegPB = (byte)(destination >> 16);
            //mpu.InternalCycle();

            // Replacement:
            ushort destAddr = mpu.ReadWord();
            mpu.PushByte(mpu.RegPB);
            mpu.InternalCycle();
            byte bankAddr = mpu.ReadByte();
            mpu.PushWord((ushort)(mpu.RegPC - 1));
            mpu.RegPC = destAddr;
            mpu.RegPB = bankAddr;
        }
    }

    internal class OP_JSR : OpcodeCommand
    {
        [Obsolete]
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
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
            mpu.InternalCycle();
            mpu.RegPC = (ushort)(mpu.PullWord() + 1);
            mpu.InternalCycle();
        }
    }

    internal class OP_RTL : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
            mpu.InternalCycle();
            mpu.RegPC = (ushort)(mpu.PullWord(true) + 1);
            mpu.RegPB = mpu.PullByte(true);
            //mpu.InternalCycle();
            if (mpu.FlagE) mpu.RegSH = 0x01;
        }
    }

    internal class OP_RTI : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            bool bFlag = mpu.ReadStatusFlag(StatusFlags.X);
            // TODO: are we meant to keep the B flag as is in emulation? Or is it an always set thing??
            mpu.InternalCycle();
            mpu.InternalCycle();
            mpu.RegSR = (StatusFlags)mpu.PullByte();
            mpu.RegPC = mpu.PullWord();
            if (mpu.FlagE)
            {
                mpu.SetStatusFlag(StatusFlags.X, bFlag);
                mpu.SetStatusFlag(StatusFlags.M, true);
            }
            else
            {
                mpu.RegPB = mpu.PullByte();
            }
            if (mpu.IndexesAre8Bit)
            {
                mpu.RegXH = 0;
                mpu.RegYH = 0;
            }
        }
    }

    internal class OP_BRK : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.ReadByte(); // Read the next byte to skip over the "signature" byte
            //mpu.Interrupt(InterruptType.BRK);
        }
    }

    internal class OP_COP : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.ReadByte(); // Read the next byte to skip over the "signature" byte
            //mpu.Interrupt(InterruptType.COP);
        }
    }

    internal class OP_CLC : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.C, false);
            mpu.InternalCycle();
        }
    }

    internal class OP_CLD : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.D, false);
            mpu.InternalCycle();
        }
    }

    internal class OP_CLI : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.I, false);
            mpu.InternalCycle();
        }
    }

    internal class OP_CLV : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.V, false);
            mpu.InternalCycle();
        }
    }

    internal class OP_SEC : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.C, true);
            mpu.InternalCycle();
        }
    }

    internal class OP_SED : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.D, true);
            mpu.InternalCycle();
        }
    }

    internal class OP_SEI : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.SetStatusFlag(StatusFlags.I, true);
            mpu.InternalCycle();
        }
    }

    internal class OP_REP : OpcodeCommand
    {
        [Obsolete]
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
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            byte operand = (byte)mpu.AddressingMode.GetOperand(mpu, true);
            if (mpu.FlagE)
            {
                // M and X flags cannot be set in emulation mode
                operand &= 0xCF;
            }
            mpu.RegSR |= (StatusFlags)operand;
            if (mpu.IndexesAre8Bit)
            {
                mpu.RegXH = 0;
                mpu.RegYH = 0;
            }
            mpu.InternalCycle();
        }
    }

    internal class OP_LDA : OpcodeCommand
    {
        [Obsolete]
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
            //mpu.InternalCycle();
        }
    }

    internal class OP_LDX : OpcodeCommand
    {
        [Obsolete]
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
            //mpu.InternalCycle();
        }
    }

    internal class OP_LDY : OpcodeCommand
    {
        [Obsolete]
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
            //mpu.InternalCycle();
        }
    }

    internal class OP_STA : OpcodeCommand
    {
        [Obsolete]
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
        [Obsolete]
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
        [Obsolete]
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
        [Obsolete]
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
        [Obsolete]
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
        [Obsolete]
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
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
        }
    }

    internal class OP_WDM : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.RegPC++;
            mpu.InternalCycle();
        }
    }

    internal class OP_PEA : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.PushWord(mpu.AddressingMode.GetOperand(mpu, false));
        }
    }

    internal class OP_PEI : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.PushWord(mpu.AddressingMode.GetOperand(mpu, false));
        }
    }

    internal class OP_PER : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            ushort operand = (ushort)mpu.AddressingMode.GetAddress(mpu);
            mpu.InternalCycle();
            mpu.PushWord(operand);
        }
    }

    internal class OP_PHA : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
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
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
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
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
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
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
            mpu.InternalCycle();
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
            if (mpu.FlagE) mpu.RegSH = 0x01;
        }
    }

    internal class OP_PLX : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
            mpu.InternalCycle();
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
            if (mpu.FlagE) mpu.RegSH = 0x01;
        }
    }

    internal class OP_PLY : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
            mpu.InternalCycle();
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
            if (mpu.FlagE) mpu.RegSH = 0x01;
        }
    }

    internal class OP_PHB : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
            mpu.PushByte(mpu.RegDB);
        }
    }

    internal class OP_PHD : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
            mpu.PushWord(mpu.RegDP);
        }
    }

    internal class OP_PHK : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
            mpu.PushByte(mpu.RegPB);
        }
    }

    internal class OP_PHP : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
            mpu.PushByte((byte)mpu.RegSR);
        }
    }

    internal class OP_PLB : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
            mpu.InternalCycle();
            mpu.RegDB = mpu.PullByte(true);
            mpu.SetNZStatusFlagsFromValue(mpu.RegDB);
            if (mpu.FlagE) mpu.RegSH = 0x01;
        }
    }

    internal class OP_PLD : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
            mpu.InternalCycle();
            mpu.RegDP = mpu.PullWord(true);
            mpu.SetNZStatusFlagsFromValue(mpu.RegDP);
            if (mpu.FlagE) mpu.RegSH = 0x01;
        }
    }

    internal class OP_PLP : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
            mpu.InternalCycle();
            byte operand = mpu.PullByte();
            if (mpu.FlagE)
            {
                // M and X flags cannot be reset in emulation mode
                operand |= 0x30;
            }
            mpu.RegSR = (StatusFlags)operand;
            if (mpu.IndexesAre8Bit)
            {
                mpu.RegXH = 0;
                mpu.RegYH = 0;
            }
            if (mpu.FlagE) mpu.RegSH = 0x01;
        }
    }

    // TODO: We need to figure out how to handle the microprocessor state in Microprocessor.cs before we implement these
    internal class OP_STP : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
            mpu.InternalCycle();
            mpu.Stop();
        }
    }

    internal class OP_WAI : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.InternalCycle();
            mpu.InternalCycle();
            mpu.Wait();
        }
    }

    internal class OP_TAX : OpcodeCommand
    {
        [Obsolete]
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
        [Obsolete]
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
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            if (mpu.FlagE || mpu.IndexesAre8Bit)
            {
                mpu.RegXL = mpu.RegSL;
                mpu.SetNZStatusFlagsFromValue(mpu.RegXL);
            }
            else
            {
                mpu.RegX = mpu.RegSP;
                mpu.SetNZStatusFlagsFromValue(mpu.RegX);
            }
            mpu.InternalCycle();
        }
    }

    internal class OP_TXA : OpcodeCommand
    {
        [Obsolete]
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
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            if (mpu.FlagE)
            {
                mpu.RegSL = mpu.RegXL;
            }
            else
            {
                if (mpu.IndexesAre8Bit) mpu.RegXH = 0;
                mpu.RegSP = mpu.RegX;
            }
            mpu.InternalCycle();
        }
    }

    internal class OP_TXY : OpcodeCommand
    {
        [Obsolete]
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
        [Obsolete]
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
        [Obsolete]
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
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.RegDP = mpu.RegA;
            mpu.SetNZStatusFlagsFromValue(mpu.RegDP);
            mpu.InternalCycle();
        }
    }

    internal class OP_TCS : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            if (mpu.FlagE)
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
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.RegA = mpu.RegDP;
            mpu.SetNZStatusFlagsFromValue(mpu.RegA);
            mpu.InternalCycle();
        }
    }

    internal class OP_TSC : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            mpu.RegA = mpu.RegSP;
            mpu.SetNZStatusFlagsFromValue(mpu.RegA);
            mpu.InternalCycle();
        }
    }

    internal class OP_XBA : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            byte temp = mpu.RegAL;
            mpu.RegAL = mpu.RegAH;
            mpu.RegAH = temp;
            mpu.SetNZStatusFlagsFromValue(mpu.RegAL);
            mpu.InternalCycle();
            mpu.InternalCycle();
        }
    }

    internal class OP_XCE : OpcodeCommand
    {
        [Obsolete]
        internal override void Execute(Microprocessor mpu)
        {
            bool carry = mpu.ReadStatusFlag(StatusFlags.C);
            mpu.SetStatusFlag(StatusFlags.C, mpu.FlagE);
            mpu.SetEmulationMode(carry);
            mpu.InternalCycle();
        }
    }
}
