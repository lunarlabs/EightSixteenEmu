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
 *  
 *  Micro-operations adapted from MPU folder for NewCore
 */

namespace EightSixteenEmu
{
    public partial class NewCore
    {
        partial class Processor
        {
            /// <summary>
            /// Marks a cycle as an internal cycle (no memory access)
            /// </summary>
            private struct MicroOpInternalCycle : IMicroOp
            {
                public void Execute(Processor proc)
                {
                    // No operation, just marks an internal cycle
                }
                public override string ToString() => "INTERNAL";
            }

            /// <summary>
            /// Reads a byte from Program Counter and advances PC
            /// </summary>
            private struct MicroOpReadByteAndAdvancePC : IMicroOp
            {
                private readonly RegisterType _destination;
                
                public MicroOpReadByteAndAdvancePC(RegisterType destination)
                {
                    _destination = destination;
                }
                
                public void Execute(Processor proc)
                {
                    // DataBus should already contain the value from the Read cycle
                    // Just move it to destination and increment PC
                    ushort value = proc._dataBus;
                    proc.SetRegisterValue(_destination, value);
                    proc._regPC = (ushort)((proc._regPC + 1) & 0xFFFF);
                }
                
                public override string ToString() => $"READPC {RegisterNames[_destination]}";
            }

            /// <summary>
            /// Pushes a byte to the stack
            /// </summary>
            private struct MicroOpPushByte : IMicroOp
            {
                private readonly RegisterType _source;
                
                public MicroOpPushByte(RegisterType source)
                {
                    _source = source;
                }
                
                public void Execute(Processor proc)
                {
                    byte value = (byte)proc.GetRegisterValue(_source);
                    proc._dataBus = value;
                    // Note: The actual write happens in the Cycle, after this executes
                    // Stack pointer decrements after the write
                    proc._regSP = (ushort)((proc._regSP - 1) & 0xFFFF);
                }
                
                public override string ToString() => $"PUSH {RegisterNames[_source]}";
            }

            /// <summary>
            /// Pulls a byte from the stack
            /// </summary>
            private struct MicroOpPullByte : IMicroOp
            {
                private readonly RegisterType _destination;
                
                public MicroOpPullByte(RegisterType destination)
                {
                    _destination = destination;
                }
                
                public void Execute(Processor proc)
                {
                    // Stack pointer increments before the read
                    proc._regSP = (ushort)((proc._regSP + 1) & 0xFFFF);
                    // DataBus should contain the value from the Read cycle
                    proc.SetRegisterValue(_destination, proc._dataBus);
                }
                
                public override string ToString() => $"PULL {RegisterNames[_destination]}";
            }

            /// <summary>
            /// Compares two registers and sets flags
            /// </summary>
            private struct MicroOpCompareRegisters : IMicroOp
            {
                private readonly RegisterType _register1;
                private readonly RegisterType _register2;
                private readonly bool _isByte;
                
                public MicroOpCompareRegisters(RegisterType register1, RegisterType register2, bool isByte)
                {
                    _register1 = register1;
                    _register2 = register2;
                    _isByte = isByte;
                }
                
                public void Execute(Processor proc)
                {
                    ushort value1 = proc.GetRegisterValue(_register1);
                    ushort value2 = proc.GetRegisterValue(_register2);
                    
                    if (_isByte)
                    {
                        value1 &= 0x00FF;
                        value2 &= 0x00FF;
                        int result = (value1 - value2) & 0xFF;
                        proc.SetFlag(StatusFlags.C, value1 >= value2);
                        proc.SetFlag(StatusFlags.Z, result == 0);
                        proc.SetFlag(StatusFlags.N, (result & 0x80) != 0);
                    }
                    else
                    {
                        int result = (value1 - value2) & 0xFFFF;
                        proc.SetFlag(StatusFlags.C, value1 >= value2);
                        proc.SetFlag(StatusFlags.Z, result == 0);
                        proc.SetFlag(StatusFlags.N, (result & 0x8000) != 0);
                    }
                }
                
                public override string ToString() => $"CMP {RegisterNames[_register1]}, {RegisterNames[_register2]}";
            }

            /// <summary>
            /// Performs a BIT test operation
            /// </summary>
            private struct MicroOpBitTest : IMicroOp
            {
                private readonly RegisterType _accumulator;
                private readonly RegisterType _operand;
                private readonly bool _isByte;
                private readonly bool _skipNVFlags;
                
                public MicroOpBitTest(RegisterType accumulator, RegisterType operand, bool isByte, bool skipNVFlags)
                {
                    _accumulator = accumulator;
                    _operand = operand;
                    _isByte = isByte;
                    _skipNVFlags = skipNVFlags;
                }
                
                public void Execute(Processor proc)
                {
                    ushort accValue = proc.GetRegisterValue(_accumulator);
                    ushort operandValue = proc.GetRegisterValue(_operand);
                    
                    if (_isByte)
                    {
                        accValue &= 0x00FF;
                        operandValue &= 0x00FF;
                        byte result = (byte)(accValue & operandValue);
                        
                        if (!_skipNVFlags)
                        {
                            proc.SetFlag(StatusFlags.N, (operandValue & 0x80) != 0);
                            proc.SetFlag(StatusFlags.V, (operandValue & 0x40) != 0);
                        }
                        proc.SetFlag(StatusFlags.Z, result == 0);
                    }
                    else
                    {
                        ushort result = (ushort)(accValue & operandValue);
                        
                        if (!_skipNVFlags)
                        {
                            proc.SetFlag(StatusFlags.N, (operandValue & 0x8000) != 0);
                            proc.SetFlag(StatusFlags.V, (operandValue & 0x4000) != 0);
                        }
                        proc.SetFlag(StatusFlags.Z, result == 0);
                    }
                }
                
                public override string ToString() => $"BIT {RegisterNames[_accumulator]}, {RegisterNames[_operand]}";
            }

            /// <summary>
            /// Test and Reset Bits
            /// </summary>
            private struct MicroOpTestResetBits : IMicroOp
            {
                private readonly RegisterType _accumulator;
                private readonly RegisterType _memory;
                private readonly bool _isByte;
                
                public MicroOpTestResetBits(RegisterType accumulator, RegisterType memory, bool isByte)
                {
                    _accumulator = accumulator;
                    _memory = memory;
                    _isByte = isByte;
                }
                
                public void Execute(Processor proc)
                {
                    ushort accValue = proc.GetRegisterValue(_accumulator);
                    ushort memValue = proc.GetRegisterValue(_memory);
                    
                    if (_isByte)
                    {
                        accValue &= 0x00FF;
                        memValue &= 0x00FF;
                        proc.SetFlag(StatusFlags.Z, (accValue & memValue) == 0);
                        memValue = (ushort)(memValue & ~accValue);
                    }
                    else
                    {
                        proc.SetFlag(StatusFlags.Z, (accValue & memValue) == 0);
                        memValue = (ushort)(memValue & ~accValue);
                    }
                    
                    proc.SetRegisterValue(_memory, memValue);
                }
                
                public override string ToString() => $"TRB {RegisterNames[_accumulator]}, {RegisterNames[_memory]}";
            }

            /// <summary>
            /// Test and Set Bits
            /// </summary>
            private struct MicroOpTestSetBits : IMicroOp
            {
                private readonly RegisterType _accumulator;
                private readonly RegisterType _memory;
                private readonly bool _isByte;
                
                public MicroOpTestSetBits(RegisterType accumulator, RegisterType memory, bool isByte)
                {
                    _accumulator = accumulator;
                    _memory = memory;
                    _isByte = isByte;
                }
                
                public void Execute(Processor proc)
                {
                    ushort accValue = proc.GetRegisterValue(_accumulator);
                    ushort memValue = proc.GetRegisterValue(_memory);
                    
                    if (_isByte)
                    {
                        accValue &= 0x00FF;
                        memValue &= 0x00FF;
                        proc.SetFlag(StatusFlags.Z, (accValue & memValue) == 0);
                        memValue = (ushort)(memValue | accValue);
                    }
                    else
                    {
                        proc.SetFlag(StatusFlags.Z, (accValue & memValue) == 0);
                        memValue = (ushort)(memValue | accValue);
                    }
                    
                    proc.SetRegisterValue(_memory, memValue);
                }
                
                public override string ToString() => $"TSB {RegisterNames[_accumulator]}, {RegisterNames[_memory]}";
            }

            /// <summary>
            /// Exchange B and A (XBA instruction)
            /// </summary>
            private struct MicroOpExchangeBA : IMicroOp
            {
                public void Execute(Processor proc)
                {
                    byte temp = proc.RegAL;
                    proc.RegAL = proc.RegAH;
                    proc.RegAH = temp;
                    proc.UpdateZeroAndNegativeFlags(proc.RegAL, true);
                }
                
                public override string ToString() => "XBA";
            }

            /// <summary>
            /// Exchange Carry and Emulation flags (XCE instruction)
            /// </summary>
            private struct MicroOpExchangeCE : IMicroOp
            {
                public void Execute(Processor proc)
                {
                    bool carry = proc.GetFlag(StatusFlags.C);
                    proc.SetFlag(StatusFlags.C, proc._flagE);
                    proc.SetEmulationFlag(carry);
                }
                
                public override string ToString() => "XCE";
            }
        }
    }
}
