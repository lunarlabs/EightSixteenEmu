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
 *  Additional Opcodes adapted from MPU folder for NewCore
 */

namespace EightSixteenEmu
{
    public partial class NewCore
    {
        partial class Processor
        {
            /// <summary>
            /// Generates cycles for LDA (Load Accumulator) operation
            /// </summary>
            private static List<Cycle> OP_LDA(Processor proc)
            {
                bool isByte = proc.IsAccumulatorByte();
                var cycles = new List<Cycle>
                {
                    // The addressing mode will have loaded the value into RegID
                    new Cycle(
                        proc,
                        Cycle.CycleType.Internal,
                        new List<IMicroOp>
                        {
                            isByte 
                                ? new MicroOpMove(RegisterType.RegIDL, RegisterType.RegAL)
                                : new MicroOpMove(RegisterType.RegID, RegisterType.RegA),
                            new MicroOpUpdateZeroAndNegativeFlags(isByte ? RegisterType.RegAL : RegisterType.RegA)
                        },
                        null
                    )
                };
                return cycles;
            }

            /// <summary>
            /// Generates cycles for LDX (Load X Register) operation
            /// </summary>
            private static List<Cycle> OP_LDX(Processor proc)
            {
                bool isByte = proc.IsIndexRegisterByte();
                var cycles = new List<Cycle>
                {
                    new Cycle(
                        proc,
                        Cycle.CycleType.Internal,
                        new List<IMicroOp>
                        {
                            isByte 
                                ? new MicroOpMove(RegisterType.RegIDL, RegisterType.RegXL)
                                : new MicroOpMove(RegisterType.RegID, RegisterType.RegX),
                            new MicroOpUpdateZeroAndNegativeFlags(isByte ? RegisterType.RegXL : RegisterType.RegX)
                        },
                        null
                    )
                };
                return cycles;
            }

            /// <summary>
            /// Generates cycles for LDY (Load Y Register) operation
            /// </summary>
            private static List<Cycle> OP_LDY(Processor proc)
            {
                bool isByte = proc.IsIndexRegisterByte();
                var cycles = new List<Cycle>
                {
                    new Cycle(
                        proc,
                        Cycle.CycleType.Internal,
                        new List<IMicroOp>
                        {
                            isByte 
                                ? new MicroOpMove(RegisterType.RegIDL, RegisterType.RegYL)
                                : new MicroOpMove(RegisterType.RegID, RegisterType.RegY),
                            new MicroOpUpdateZeroAndNegativeFlags(isByte ? RegisterType.RegYL : RegisterType.RegY)
                        },
                        null
                    )
                };
                return cycles;
            }

            /// <summary>
            /// Generates cycles for STA (Store Accumulator) operation
            /// </summary>
            private static List<Cycle> OP_STA(Processor proc)
            {
                bool isByte = proc.IsAccumulatorByte();
                var cycles = new List<Cycle>
                {
                    // Store accumulator to address in RegIA
                    new Cycle(
                        proc,
                        Cycle.CycleType.Write,
                        new List<IMicroOp>
                        {
                            isByte 
                                ? new MicroOpMove(RegisterType.RegAL, RegisterType.DataBus)
                                : new MicroOpMove(RegisterType.RegA, RegisterType.DataBus)
                        },
                        MakeAddress(proc._regDB, proc._internalAddress)
                    )
                };
                
                if (!isByte)
                {
                    // Write high byte for 16-bit mode
                    cycles.Add(new Cycle(
                        proc,
                        Cycle.CycleType.Write,
                        new List<IMicroOp>
                        {
                            new MicroOpMove(RegisterType.RegAH, RegisterType.DataBus)
                        },
                        MakeAddress(proc._regDB, (ushort)(proc._internalAddress + 1))
                    ));
                }
                
                return cycles;
            }

            /// <summary>
            /// Generates cycles for AND (Logical AND with Accumulator) operation
            /// </summary>
            private static List<Cycle> OP_AND(Processor proc)
            {
                bool isByte = proc.IsAccumulatorByte();
                var cycles = new List<Cycle>
                {
                    new Cycle(
                        proc,
                        Cycle.CycleType.Internal,
                        new List<IMicroOp>
                        {
                            isByte 
                                ? new MicroOpLogicalAndRegisters(RegisterType.RegAL, RegisterType.RegIDL, RegisterType.RegAL)
                                : new MicroOpLogicalAndRegisters(RegisterType.RegA, RegisterType.RegID, RegisterType.RegA),
                            new MicroOpUpdateZeroAndNegativeFlags(isByte ? RegisterType.RegAL : RegisterType.RegA)
                        },
                        null
                    )
                };
                return cycles;
            }

            /// <summary>
            /// Generates cycles for ORA (Logical OR with Accumulator) operation
            /// </summary>
            private static List<Cycle> OP_ORA(Processor proc)
            {
                bool isByte = proc.IsAccumulatorByte();
                var cycles = new List<Cycle>
                {
                    new Cycle(
                        proc,
                        Cycle.CycleType.Internal,
                        new List<IMicroOp>
                        {
                            isByte 
                                ? new MicroOpLogicalOrRegisters(RegisterType.RegAL, RegisterType.RegIDL, RegisterType.RegAL)
                                : new MicroOpLogicalOrRegisters(RegisterType.RegA, RegisterType.RegID, RegisterType.RegA),
                            new MicroOpUpdateZeroAndNegativeFlags(isByte ? RegisterType.RegAL : RegisterType.RegA)
                        },
                        null
                    )
                };
                return cycles;
            }

            /// <summary>
            /// Generates cycles for EOR (Logical Exclusive OR with Accumulator) operation
            /// </summary>
            private static List<Cycle> OP_EOR(Processor proc)
            {
                bool isByte = proc.IsAccumulatorByte();
                var cycles = new List<Cycle>
                {
                    new Cycle(
                        proc,
                        Cycle.CycleType.Internal,
                        new List<IMicroOp>
                        {
                            isByte 
                                ? new MicroOpLogicalEorRegisters(RegisterType.RegAL, RegisterType.RegIDL, RegisterType.RegAL)
                                : new MicroOpLogicalEorRegisters(RegisterType.RegA, RegisterType.RegID, RegisterType.RegA),
                            new MicroOpUpdateZeroAndNegativeFlags(isByte ? RegisterType.RegAL : RegisterType.RegA)
                        },
                        null
                    )
                };
                return cycles;
            }

            /// <summary>
            /// Generates cycles for ADC (Add with Carry) operation
            /// </summary>
            private static List<Cycle> OP_ADC(Processor proc)
            {
                bool isByte = proc.IsAccumulatorByte();
                bool isDecimal = proc.GetFlag(StatusFlags.D);
                var cycles = new List<Cycle>
                {
                    new Cycle(
                        proc,
                        Cycle.CycleType.Internal,
                        new List<IMicroOp>
                        {
                            isDecimal
                                ? (isByte 
                                    ? (IMicroOp)new MicroOpAddBCDRegisters(RegisterType.RegAL, RegisterType.RegIDL, RegisterType.RegAL, true, true)
                                    : new MicroOpAddBCDRegisters(RegisterType.RegA, RegisterType.RegID, RegisterType.RegA, true, true))
                                : (isByte 
                                    ? (IMicroOp)new MicroOpAddRegisters(RegisterType.RegAL, RegisterType.RegIDL, RegisterType.RegAL, true, true)
                                    : new MicroOpAddRegisters(RegisterType.RegA, RegisterType.RegID, RegisterType.RegA, true, true)),
                            new MicroOpUpdateZeroAndNegativeFlags(isByte ? RegisterType.RegAL : RegisterType.RegA)
                        },
                        null
                    )
                };
                return cycles;
            }

            /// <summary>
            /// Generates cycles for SBC (Subtract with Carry) operation
            /// </summary>
            private static List<Cycle> OP_SBC(Processor proc)
            {
                bool isByte = proc.IsAccumulatorByte();
                bool isDecimal = proc.GetFlag(StatusFlags.D);
                var cycles = new List<Cycle>
                {
                    new Cycle(
                        proc,
                        Cycle.CycleType.Internal,
                        new List<IMicroOp>
                        {
                            isDecimal
                                ? (isByte 
                                    ? (IMicroOp)new MicroOpSubtractBCDRegisters(RegisterType.RegAL, RegisterType.RegIDL, RegisterType.RegAL, true, true)
                                    : new MicroOpSubtractBCDRegisters(RegisterType.RegA, RegisterType.RegID, RegisterType.RegA, true, true))
                                : (isByte 
                                    ? (IMicroOp)new MicroOpSubtractRegisters(RegisterType.RegAL, RegisterType.RegIDL, RegisterType.RegAL, true, true)
                                    : new MicroOpSubtractRegisters(RegisterType.RegA, RegisterType.RegID, RegisterType.RegA, true, true)),
                            new MicroOpUpdateZeroAndNegativeFlags(isByte ? RegisterType.RegAL : RegisterType.RegA)
                        },
                        null
                    )
                };
                return cycles;
            }

            /// <summary>
            /// Generates cycles for CMP (Compare Accumulator) operation
            /// </summary>
            private static List<Cycle> OP_CMP(Processor proc)
            {
                bool isByte = proc.IsAccumulatorByte();
                var cycles = new List<Cycle>
                {
                    new Cycle(
                        proc,
                        Cycle.CycleType.Internal,
                        new List<IMicroOp>
                        {
                            isByte 
                                ? new MicroOpCompareRegisters(RegisterType.RegAL, RegisterType.RegIDL, true)
                                : new MicroOpCompareRegisters(RegisterType.RegA, RegisterType.RegID, false)
                        },
                        null
                    )
                };
                return cycles;
            }

            /// <summary>
            /// Generates cycles for CPX (Compare X Register) operation
            /// </summary>
            private static List<Cycle> OP_CPX(Processor proc)
            {
                bool isByte = proc.IsIndexRegisterByte();
                var cycles = new List<Cycle>
                {
                    new Cycle(
                        proc,
                        Cycle.CycleType.Internal,
                        new List<IMicroOp>
                        {
                            isByte 
                                ? new MicroOpCompareRegisters(RegisterType.RegXL, RegisterType.RegIDL, true)
                                : new MicroOpCompareRegisters(RegisterType.RegX, RegisterType.RegID, false)
                        },
                        null
                    )
                };
                return cycles;
            }

            /// <summary>
            /// Generates cycles for CPY (Compare Y Register) operation
            /// </summary>
            private static List<Cycle> OP_CPY(Processor proc)
            {
                bool isByte = proc.IsIndexRegisterByte();
                var cycles = new List<Cycle>
                {
                    new Cycle(
                        proc,
                        Cycle.CycleType.Internal,
                        new List<IMicroOp>
                        {
                            isByte 
                                ? new MicroOpCompareRegisters(RegisterType.RegYL, RegisterType.RegIDL, true)
                                : new MicroOpCompareRegisters(RegisterType.RegY, RegisterType.RegID, false)
                        },
                        null
                    )
                };
                return cycles;
            }
        }
    }
}
