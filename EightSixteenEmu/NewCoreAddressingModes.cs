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
 *  Addressing Modes adapted from MPU folder for NewCore
 */

namespace EightSixteenEmu
{
    public partial class NewCore
    {
        partial class Processor
        {
            /// <summary>
            /// Calculates a full 24-bit address from bank and 16-bit address
            /// </summary>
            private static uint MakeAddress(byte bank, ushort address) => (uint)((bank << 16) | address);

            /// <summary>
            /// Generates cycles for Immediate addressing mode
            /// </summary>
            private static List<Cycle> AM_Immediate(Processor proc, bool isByte)
            {
                var cycles = new List<Cycle>
                {
                    new Cycle(
                        proc,
                        Cycle.CycleType.Read,
                        new List<IMicroOp>
                        {
                            new MicroOpReadByteAndAdvancePC(RegisterType.RegIDL)
                        },
                        MakeAddress(proc._regPB, proc._regPC)
                    )
                };

                if (!isByte)
                {
                    cycles.Add(new Cycle(
                        proc,
                        Cycle.CycleType.Read,
                        new List<IMicroOp>
                        {
                            new MicroOpReadByteAndAdvancePC(RegisterType.RegIDH)
                        },
                        MakeAddress(proc._regPB, proc._regPC)
                    ));
                }

                return cycles;
            }

            /// <summary>
            /// Generates cycles for Accumulator addressing mode
            /// </summary>
            private static List<Cycle> AM_Accumulator(Processor proc, bool isByte)
            {
                var cycles = new List<Cycle>
                {
                    new Cycle(
                        proc,
                        Cycle.CycleType.Internal,
                        new List<IMicroOp>
                        {
                            isByte 
                                ? new MicroOpMove(RegisterType.RegAL, RegisterType.RegIDL)
                                : new MicroOpMove(RegisterType.RegA, RegisterType.RegID)
                        },
                        null
                    )
                };

                return cycles;
            }

            /// <summary>
            /// Generates cycles for Direct addressing mode
            /// </summary>
            private static List<Cycle> AM_Direct(Processor proc)
            {
                var cycles = new List<Cycle>
                {
                    // Read offset byte from program counter
                    new Cycle(
                        proc,
                        Cycle.CycleType.Read,
                        new List<IMicroOp>
                        {
                            new MicroOpReadByteAndAdvancePC(RegisterType.RegIDL),
                            new MicroOpSetRegister(RegisterType.RegIDH, 0)
                        },
                        MakeAddress(proc._regPB, proc._regPC)
                    )
                };

                // Add internal cycle if direct page low byte is not 0
                if (proc.RegDL != 0x00)
                {
                    cycles.Add(new Cycle(
                        proc,
                        Cycle.CycleType.Internal,
                        new List<IMicroOp>(),
                        null
                    ));
                }

                // Calculate the final address: DP + offset
                cycles.Add(new Cycle(
                    proc,
                    Cycle.CycleType.Internal,
                    new List<IMicroOp>
                    {
                        new MicroOpAddRegisters(RegisterType.RegDP, RegisterType.RegID, RegisterType.RegIA)
                    },
                    null
                ));

                return cycles;
            }

            /// <summary>
            /// Generates cycles for Direct Indexed with X addressing mode
            /// </summary>
            private static List<Cycle> AM_DirectIndexedX(Processor proc)
            {
                var cycles = new List<Cycle>
                {
                    // Read offset byte from program counter
                    new Cycle(
                        proc,
                        Cycle.CycleType.Read,
                        new List<IMicroOp>
                        {
                            new MicroOpReadByteAndAdvancePC(RegisterType.RegIDL),
                            new MicroOpSetRegister(RegisterType.RegIDH, 0)
                        },
                        MakeAddress(proc._regPB, proc._regPC)
                    ),
                    // Internal cycle for index calculation
                    new Cycle(
                        proc,
                        Cycle.CycleType.Internal,
                        new List<IMicroOp>(),
                        null
                    )
                };

                // Add internal cycle if direct page low byte is not 0
                if (proc.RegDL != 0x00)
                {
                    cycles.Add(new Cycle(
                        proc,
                        Cycle.CycleType.Internal,
                        new List<IMicroOp>(),
                        null
                    ));
                }

                // Calculate the final address: DP + offset + X
                cycles.Add(new Cycle(
                    proc,
                    Cycle.CycleType.Internal,
                    new List<IMicroOp>
                    {
                        new MicroOpAddRegisters(RegisterType.RegID, RegisterType.RegX, RegisterType.RegID),
                        new MicroOpAddRegisters(RegisterType.RegDP, RegisterType.RegID, RegisterType.RegIA)
                    },
                    null
                ));

                return cycles;
            }

            /// <summary>
            /// Generates cycles for Absolute addressing mode
            /// </summary>
            private static List<Cycle> AM_Absolute(Processor proc)
            {
                var cycles = new List<Cycle>
                {
                    // Read low byte of address
                    new Cycle(
                        proc,
                        Cycle.CycleType.Read,
                        new List<IMicroOp>
                        {
                            new MicroOpReadByteAndAdvancePC(RegisterType.RegIAL)
                        },
                        MakeAddress(proc._regPB, proc._regPC)
                    ),
                    // Read high byte of address
                    new Cycle(
                        proc,
                        Cycle.CycleType.Read,
                        new List<IMicroOp>
                        {
                            new MicroOpReadByteAndAdvancePC(RegisterType.RegIAH)
                        },
                        MakeAddress(proc._regPB, proc._regPC)
                    )
                };

                return cycles;
            }

            /// <summary>
            /// Generates cycles for Absolute Indexed with X addressing mode
            /// </summary>
            private static List<Cycle> AM_AbsoluteIndexedX(Processor proc, bool addPenaltyCycle)
            {
                var cycles = new List<Cycle>
                {
                    // Read low byte of address
                    new Cycle(
                        proc,
                        Cycle.CycleType.Read,
                        new List<IMicroOp>
                        {
                            new MicroOpReadByteAndAdvancePC(RegisterType.RegIDL)
                        },
                        MakeAddress(proc._regPB, proc._regPC)
                    ),
                    // Read high byte of address
                    new Cycle(
                        proc,
                        Cycle.CycleType.Read,
                        new List<IMicroOp>
                        {
                            new MicroOpReadByteAndAdvancePC(RegisterType.RegIDH)
                        },
                        MakeAddress(proc._regPB, proc._regPC)
                    )
                };

                // Calculate indexed address
                cycles.Add(new Cycle(
                    proc,
                    Cycle.CycleType.Internal,
                    new List<IMicroOp>
                    {
                        new MicroOpAddRegisters(RegisterType.RegID, RegisterType.RegX, RegisterType.RegIA)
                    },
                    null
                ));

                // Add penalty cycle for page boundary crossing or certain opcodes
                if (addPenaltyCycle)
                {
                    cycles.Add(new Cycle(
                        proc,
                        Cycle.CycleType.Internal,
                        new List<IMicroOp>(),
                        null
                    ));
                }

                return cycles;
            }

            /// <summary>
            /// Generates cycles for Program Counter Relative addressing mode (branches)
            /// </summary>
            private static List<Cycle> AM_ProgramCounterRelative(Processor proc)
            {
                var cycles = new List<Cycle>
                {
                    // Read signed offset
                    new Cycle(
                        proc,
                        Cycle.CycleType.Read,
                        new List<IMicroOp>
                        {
                            new MicroOpReadByteAndAdvancePC(RegisterType.RegIDL),
                            new MicroOpSetRegister(RegisterType.RegIDH, 0)
                        },
                        MakeAddress(proc._regPB, proc._regPC)
                    ),
                    // Calculate target address
                    new Cycle(
                        proc,
                        Cycle.CycleType.Internal,
                        new List<IMicroOp>
                        {
                            new MicroOpAddRegisters(RegisterType.RegPC, RegisterType.RegID, RegisterType.RegIA)
                        },
                        null
                    )
                };

                return cycles;
            }

            /// <summary>
            /// Generates cycles for Stack Relative addressing mode
            /// </summary>
            private static List<Cycle> AM_StackRelative(Processor proc)
            {
                var cycles = new List<Cycle>
                {
                    // Read offset byte
                    new Cycle(
                        proc,
                        Cycle.CycleType.Read,
                        new List<IMicroOp>
                        {
                            new MicroOpReadByteAndAdvancePC(RegisterType.RegIDL),
                            new MicroOpSetRegister(RegisterType.RegIDH, 0)
                        },
                        MakeAddress(proc._regPB, proc._regPC)
                    ),
                    // Internal cycle for address calculation
                    new Cycle(
                        proc,
                        Cycle.CycleType.Internal,
                        new List<IMicroOp>(),
                        null
                    ),
                    // Calculate address: SP + offset
                    new Cycle(
                        proc,
                        Cycle.CycleType.Internal,
                        new List<IMicroOp>
                        {
                            new MicroOpAddRegisters(RegisterType.RegSP, RegisterType.RegID, RegisterType.RegIA)
                        },
                        null
                    )
                };

                return cycles;
            }
        }
    }
}
