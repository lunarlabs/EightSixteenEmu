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

        internal static void BranchTo(Microprocessor mpu, ushort address)
        {
            mpu.NextCycle();
            if (mpu.FlagE && (byte)(mpu.RegPC >> 8) != (byte)(address >> 8))
            {
                mpu.NextCycle();
            }
            mpu.RegPC = address;
        }

        internal static void CopyMemory(Microprocessor mpu, ushort operand)
        {
            byte destination = (byte)(operand >> 8);
            mpu.RegDB = destination;
            byte source = (byte)operand;
            mpu.WriteByte(mpu.ReadByte((uint)(source << 16) | mpu.RegX),(uint)(destination <<16)| mpu.RegY);
            mpu.NextCycle();
            mpu.NextCycle();
        }
    }
}
