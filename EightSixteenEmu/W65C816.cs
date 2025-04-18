using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EightSixteenEmu
{
    public class W65C816
    {
        public enum Vector : uint
        {
            NativeCOP = 0x00ffe4,
            NativeBRK = 0x00ffe6,
            NativeAbort = 0x00ffe8,
            NativeNMI = 0x00ffea, 
            NativeIRQ = 0x00ffee,
            EmulationCOP = 0x00fff4,
            EmulationAbort = 0x00fff8,
            EmulationNMI = 0x00fffa,
            Reset = 0x00fffc,
            EmulationIRQ = 0x00fffe,
        }
        /// <summary>
        /// Represents the different addressing modes of the 65C816
        /// </summary>
        public enum AddressingMode
        {
            /// <summary>
            /// The operands immediately follow the instruction.
            /// </summary>
            /// <remarks>
            /// Referenced by "#" in the datasheet.
            /// </remarks>
            Immediate,

            /// <summary>
            /// The operand is the accumulator.
            /// </summary>
            /// <remarks>
            /// Referenced by "A" in the datasheet.
            /// </remarks>
            Accumulator,

            /// <summary>
            /// A signed 8-bit value representing an offset to branch to.
            /// Only used in branch instructions.
            /// </summary>
            /// <remarks>
            /// Referenced by "r" in the datasheet. Only used in branch instructions.
            /// </remarks>
            ProgramCounterRelative,

            /// <summary>
            /// A signed 16-bit value representing an offset to branch to.
            /// </summary>
            /// <remarks>
            /// Referenced by "rl" in the datasheet. Only used by the BRL and PER instructions.
            /// </remarks>
            ProgramCounterRelativeLong,

            /// <summary>
            /// This signifies a single byte instruction, with the operand defined by the instruction.
            /// </summary>
            /// <remarks>
            /// Represented by "i" in the datasheet.
            /// </remarks>
            Implied,

            /// <summary>
            /// Used by all instructions that push or pop data from the stack.
            /// </summary>
            /// <remarks>
            /// Represented by "s" in the datasheet.
            /// Functionally the same as Implied.
            /// </remarks>
            Stack,

            /// <summary>
            /// The argument is added to <c>RegDP</c> to form the effective address in Bank 0.
            /// </summary>
            /// <remarks>
            /// Represented by "d" in the datasheet.
            /// An extra cycle is added when the Direct Register is not page aligned (low byte of <c>RegDP</c> is not 0)
            /// </remarks>
            Direct,

            /// <summary>
            /// The argument is added to the sum of <c>RegDP</c> and <c>RegX</c> to form the pointer to the effective address in Bank 0.
            /// </summary>
            /// <remarks>
            /// Represented by "d,x" in the datasheet.
            /// </remarks>
            DirectIndexedWithX,

            /// <summary>
            /// The argument is added to the sum of <c>RegDP</c> and <c>RegY</c> to form the pointer to the effective address in Bank 0.
            /// </summary>
            /// <remarks>
            /// Represented by "d,y" in the datasheet.
            /// </remarks>
            DirectIndexedWithY,

            /// <summary>
            /// The argument is added to <c>RegDP</c> to form the pointer to the effective address in the <c>RegDB</c> bank.
            /// </summary>
            /// <remarks>
            /// Represented by "(d)" in the datasheet.
            /// </remarks>
            DirectIndirect,

            /// <summary>
            /// The argument is added to the sum of <c>RegDP</c> and <c>RegX</c> to form the effective address in the <c>RegDB</c> bank.
            /// </summary>
            /// <remarks>
            /// Represented by "(d,x)" in the datasheet.
            /// </remarks>
            DirectIndexedIndirect,

            /// <summary>
            /// The argument is added to <c>RegDP</c> to form a base address in in the <c>RegDB</c> bank, which is then added to <c>RegY</c>
            /// to form the effective address.
            /// </summary>
            /// <remarks>
            /// Represented by "(d),y" in the datasheet.
            /// </remarks>
            DirectIndirectIndexed,


            DirectIndirectLong,
            DirectIndirectLongIndexed,
            Absolute,
            AbsoluteIndexedWithX,
            AbsoluteIndexedWithY,
            AbsoluteLong,
            AbsoluteLongIndexed,
            StackRelative,
            StackRelativeIndirectIndexed,
            AbsoluteIndirect,
            AbsoluteIndirectLong,
            AbsoluteIndexedIndirect,
            BlockMove,
        }
        /// <summary>
        /// Represents the instruction set of the 65C816.
        /// </summary>
        public enum OpCode
        {
            ADC,
            AND,
            ASL,
            BCC,
            BCS,
            BEQ,
            BIT,
            BMI,
            BNE,
            BPL,
            BRA,
            BRK,
            BRL,
            BVC,
            BVS,
            CLC,
            CLD,
            CLI,
            CLV,
            CMP,
            COP,
            CPX,
            CPY,
            DEC,
            DEX,
            DEY,
            EOR,
            INC,
            INX,
            INY,
            JMP,
            JSL,
            JSR,
            LDA,
            LDX,
            LDY,
            LSR,
            MVN,
            MVP,
            NOP,
            ORA,
            PEA,
            PEI,
            PER,
            PHA,
            PHB,
            PHD,
            PHK,
            PHP,
            PHX,
            PHY,
            PLA,
            PLB,
            PLD,
            PLP,
            PLX,
            PLY,
            REP,
            ROL,
            ROR,
            RTI,
            RTL,
            RTS,
            SBC,
            SEP,
            SEC,
            SED,
            SEI,
            STA,
            STP,
            STX,
            STY,
            STZ,
            TAX,
            TAY,
            TCD,
            TCS,
            TDC,
            TRB,
            TSB,
            TSC,
            TSX,
            TXA,
            TXS,
            TXY,
            TYA,
            TYX,
            WAI,
            WDM,
            XBA,
            XCE,
        }

        public static (OpCode, AddressingMode) OpCodeLookup(byte opcode)
        {
            return opcode switch
            {
                0x00 => (OpCode.BRK, AddressingMode.Implied),
                0x01 => (OpCode.ORA, AddressingMode.DirectIndexedIndirect),
                0x02 => (OpCode.COP, AddressingMode.Immediate),
                0x03 => (OpCode.ORA, AddressingMode.StackRelative),
                0x04 => (OpCode.TSB, AddressingMode.Direct),
                0x05 => (OpCode.ORA, AddressingMode.Direct),
                0x06 => (OpCode.ASL, AddressingMode.Direct),
                0x07 => (OpCode.ORA, AddressingMode.DirectIndirectLong),
                0x08 => (OpCode.PHP, AddressingMode.Stack),
                0x09 => (OpCode.ORA, AddressingMode.Immediate),
                0x0a => (OpCode.ASL, AddressingMode.Accumulator),
                0x0b => (OpCode.PHD, AddressingMode.Stack),
                0x0c => (OpCode.TSB, AddressingMode.Absolute),
                0x0d => (OpCode.ORA, AddressingMode.Absolute),
                0x0e => (OpCode.ASL, AddressingMode.Absolute),
                0x0f => (OpCode.ORA, AddressingMode.AbsoluteLong),

                0x10 => (OpCode.BPL, AddressingMode.ProgramCounterRelative),
                0x11 => (OpCode.ORA, AddressingMode.DirectIndirectIndexed),
                0x12 => (OpCode.ORA, AddressingMode.DirectIndirect),
                0x13 => (OpCode.ORA, AddressingMode.StackRelativeIndirectIndexed),
                0x14 => (OpCode.TRB, AddressingMode.Direct),
                0x15 => (OpCode.ORA, AddressingMode.DirectIndexedWithX),
                0x16 => (OpCode.ASL, AddressingMode.DirectIndexedWithX),
                0x17 => (OpCode.ORA, AddressingMode.DirectIndirectLongIndexed),
                0x18 => (OpCode.CLC, AddressingMode.Implied),
                0x19 => (OpCode.ORA, AddressingMode.AbsoluteIndexedWithY),
                0x1a => (OpCode.INC, AddressingMode.Accumulator),
                0x1b => (OpCode.TCS, AddressingMode.Implied),
                0x1c => (OpCode.TRB, AddressingMode.Absolute),
                0x1d => (OpCode.ORA, AddressingMode.AbsoluteIndexedWithX),
                0x1e => (OpCode.ASL, AddressingMode.AbsoluteIndexedWithX),
                0x1f => (OpCode.ORA, AddressingMode.AbsoluteLongIndexed),

                0x20 => (OpCode.JSR, AddressingMode.Absolute),
                0x21 => (OpCode.AND, AddressingMode.DirectIndexedIndirect),
                0x22 => (OpCode.JSL, AddressingMode.AbsoluteLong),
                0x23 => (OpCode.AND, AddressingMode.StackRelative),
                0x24 => (OpCode.BIT, AddressingMode.Direct),
                0x25 => (OpCode.AND, AddressingMode.Direct),
                0x26 => (OpCode.ROL, AddressingMode.Direct),
                0x27 => (OpCode.AND, AddressingMode.DirectIndirectLong),
                0x28 => (OpCode.PLP, AddressingMode.Stack),
                0x29 => (OpCode.AND, AddressingMode.Immediate),
                0x2a => (OpCode.ROL, AddressingMode.Accumulator),
                0x2b => (OpCode.PLD, AddressingMode.Stack),
                0x2c => (OpCode.BIT, AddressingMode.Absolute),
                0x2d => (OpCode.AND, AddressingMode.Absolute),
                0x2e => (OpCode.ROL, AddressingMode.Absolute),
                0x2f => (OpCode.AND, AddressingMode.AbsoluteLong),

                0x30 => (OpCode.BMI, AddressingMode.ProgramCounterRelative),
                0x31 => (OpCode.AND, AddressingMode.DirectIndirectIndexed),
                0x32 => (OpCode.AND, AddressingMode.DirectIndirect),
                0x33 => (OpCode.AND, AddressingMode.StackRelativeIndirectIndexed),
                0x34 => (OpCode.BIT, AddressingMode.DirectIndexedWithX),
                0x35 => (OpCode.AND, AddressingMode.DirectIndexedWithX),
                0x36 => (OpCode.ROL, AddressingMode.DirectIndexedWithX),
                0x37 => (OpCode.AND, AddressingMode.DirectIndirectLongIndexed),
                0x38 => (OpCode.SEC, AddressingMode.Implied),
                0x39 => (OpCode.AND, AddressingMode.AbsoluteIndexedWithY),
                0x3a => (OpCode.DEC, AddressingMode.Accumulator),
                0x3b => (OpCode.TSC, AddressingMode.Implied),
                0x3c => (OpCode.BIT, AddressingMode.AbsoluteIndexedWithX),
                0x3d => (OpCode.AND, AddressingMode.AbsoluteIndexedWithX),
                0x3e => (OpCode.ROL, AddressingMode.AbsoluteIndexedWithX),
                0x3f => (OpCode.AND, AddressingMode.AbsoluteLongIndexed),

                0x40 => (OpCode.RTI, AddressingMode.Implied),
                0x41 => (OpCode.EOR, AddressingMode.DirectIndexedIndirect),
                0x42 => (OpCode.WDM, AddressingMode.Implied),
                0x43 => (OpCode.EOR, AddressingMode.StackRelative),
                0x44 => (OpCode.MVP, AddressingMode.BlockMove),
                0x45 => (OpCode.EOR, AddressingMode.Direct),
                0x46 => (OpCode.LSR, AddressingMode.Direct),
                0x47 => (OpCode.EOR, AddressingMode.DirectIndirectLong),
                0x48 => (OpCode.PHA, AddressingMode.Stack),
                0x49 => (OpCode.EOR, AddressingMode.Immediate),
                0x4a => (OpCode.LSR, AddressingMode.Accumulator),
                0x4b => (OpCode.PHK, AddressingMode.Stack),
                0x4c => (OpCode.JMP, AddressingMode.Absolute),
                0x4d => (OpCode.EOR, AddressingMode.Absolute),
                0x4e => (OpCode.LSR, AddressingMode.Absolute),
                0x4f => (OpCode.EOR, AddressingMode.AbsoluteLong),

                0x50 => (OpCode.BVC, AddressingMode.ProgramCounterRelative),
                0x51 => (OpCode.EOR, AddressingMode.DirectIndirectIndexed),
                0x52 => (OpCode.EOR, AddressingMode.DirectIndirect),
                0x53 => (OpCode.EOR, AddressingMode.StackRelativeIndirectIndexed),
                0x54 => (OpCode.MVN, AddressingMode.BlockMove),
                0x55 => (OpCode.EOR, AddressingMode.DirectIndexedWithX),
                0x56 => (OpCode.LSR, AddressingMode.DirectIndexedWithX),
                0x57 => (OpCode.EOR, AddressingMode.DirectIndirectLongIndexed),
                0x58 => (OpCode.CLI, AddressingMode.Implied),
                0x59 => (OpCode.EOR, AddressingMode.AbsoluteIndexedWithY),
                0x5a => (OpCode.PHY, AddressingMode.Stack),
                0x5b => (OpCode.TCD, AddressingMode.Implied),
                0x5c => (OpCode.JMP, AddressingMode.AbsoluteLong),
                0x5d => (OpCode.EOR, AddressingMode.AbsoluteIndexedWithX),
                0x5e => (OpCode.LSR, AddressingMode.AbsoluteIndexedWithX),
                0x5f => (OpCode.EOR, AddressingMode.AbsoluteLongIndexed),

                0x60 => (OpCode.RTS, AddressingMode.Implied),
                0x61 => (OpCode.ADC, AddressingMode.DirectIndexedIndirect),
                0x62 => (OpCode.PER, AddressingMode.ProgramCounterRelativeLong),
                0x63 => (OpCode.ADC, AddressingMode.StackRelative),
                0x64 => (OpCode.STZ, AddressingMode.Direct),
                0x65 => (OpCode.ADC, AddressingMode.Direct),
                0x66 => (OpCode.ROR, AddressingMode.Direct),
                0x67 => (OpCode.ADC, AddressingMode.DirectIndirectLong),
                0x68 => (OpCode.PLA, AddressingMode.Stack),
                0x69 => (OpCode.ADC, AddressingMode.Immediate),
                0x6a => (OpCode.ROR, AddressingMode.Accumulator),
                0x6b => (OpCode.RTL, AddressingMode.Implied),
                0x6c => (OpCode.JMP, AddressingMode.AbsoluteIndirect),
                0x6d => (OpCode.ADC, AddressingMode.Absolute),
                0x6e => (OpCode.ROR, AddressingMode.Absolute),
                0x6f => (OpCode.ADC, AddressingMode.AbsoluteLong),

                0x70 => (OpCode.BVS, AddressingMode.ProgramCounterRelative),
                0x71 => (OpCode.ADC, AddressingMode.DirectIndirectIndexed),
                0x72 => (OpCode.ADC, AddressingMode.DirectIndirect),
                0x73 => (OpCode.ADC, AddressingMode.StackRelativeIndirectIndexed),
                0x74 => (OpCode.STZ, AddressingMode.DirectIndexedWithX),
                0x75 => (OpCode.ADC, AddressingMode.DirectIndexedWithX),
                0x76 => (OpCode.ROR, AddressingMode.DirectIndexedWithX),
                0x77 => (OpCode.ADC, AddressingMode.DirectIndirectLongIndexed),
                0x78 => (OpCode.SEI, AddressingMode.Implied),
                0x79 => (OpCode.ADC, AddressingMode.AbsoluteIndexedWithY),
                0x7a => (OpCode.PLY, AddressingMode.Stack),
                0x7b => (OpCode.TDC, AddressingMode.Implied),
                0x7c => (OpCode.JMP, AddressingMode.AbsoluteIndexedIndirect),
                0x7d => (OpCode.ADC, AddressingMode.AbsoluteIndexedWithX),
                0x7e => (OpCode.ROR, AddressingMode.AbsoluteIndexedWithX),
                0x7f => (OpCode.ADC, AddressingMode.AbsoluteLongIndexed),

                0x80 => (OpCode.BRA, AddressingMode.ProgramCounterRelative),
                0x81 => (OpCode.STA, AddressingMode.DirectIndexedIndirect),
                0x82 => (OpCode.BRL, AddressingMode.ProgramCounterRelativeLong),
                0x83 => (OpCode.STA, AddressingMode.StackRelative),
                0x84 => (OpCode.STY, AddressingMode.Direct),
                0x85 => (OpCode.STA, AddressingMode.Direct),
                0x86 => (OpCode.STX, AddressingMode.Direct),
                0x87 => (OpCode.STA, AddressingMode.DirectIndirectLong),
                0x88 => (OpCode.DEY, AddressingMode.Implied),
                0x89 => (OpCode.BIT, AddressingMode.Immediate),
                0x8a => (OpCode.TXA, AddressingMode.Implied),
                0x8b => (OpCode.PHB, AddressingMode.Stack),
                0x8c => (OpCode.STY, AddressingMode.Absolute),
                0x8d => (OpCode.STA, AddressingMode.Absolute),
                0x8e => (OpCode.STX, AddressingMode.Absolute),
                0x8f => (OpCode.STA, AddressingMode.AbsoluteLong),

                0x90 => (OpCode.BCC, AddressingMode.ProgramCounterRelative),
                0x91 => (OpCode.STA, AddressingMode.DirectIndirectIndexed),
                0x92 => (OpCode.STA, AddressingMode.DirectIndirect),
                0x93 => (OpCode.STA, AddressingMode.StackRelativeIndirectIndexed),
                0x94 => (OpCode.STY, AddressingMode.DirectIndexedWithX),
                0x95 => (OpCode.STA, AddressingMode.DirectIndexedWithX),
                0x96 => (OpCode.STX, AddressingMode.DirectIndexedWithY),
                0x97 => (OpCode.STA, AddressingMode.DirectIndirectLongIndexed),
                0x98 => (OpCode.TYA, AddressingMode.Implied),
                0x99 => (OpCode.STA, AddressingMode.AbsoluteIndexedWithY),
                0x9a => (OpCode.TXS, AddressingMode.Implied),
                0x9b => (OpCode.TXY, AddressingMode.Implied),
                0x9c => (OpCode.STZ, AddressingMode.Absolute),
                0x9d => (OpCode.STA, AddressingMode.AbsoluteIndexedWithX),
                0x9e => (OpCode.STZ, AddressingMode.AbsoluteIndexedWithX),
                0x9f => (OpCode.STA, AddressingMode.AbsoluteLongIndexed),

                0xa0 => (OpCode.LDY, AddressingMode.Immediate),
                0xa1 => (OpCode.LDA, AddressingMode.DirectIndexedIndirect),
                0xa2 => (OpCode.LDX, AddressingMode.Immediate),
                0xa3 => (OpCode.LDA, AddressingMode.StackRelative),
                0xa4 => (OpCode.LDY, AddressingMode.Direct),
                0xa5 => (OpCode.LDA, AddressingMode.Direct),
                0xa6 => (OpCode.LDX, AddressingMode.Direct),
                0xa7 => (OpCode.LDA, AddressingMode.DirectIndirectLong),
                0xa8 => (OpCode.TAY, AddressingMode.Implied),
                0xa9 => (OpCode.LDA, AddressingMode.Immediate),
                0xaa => (OpCode.TAX, AddressingMode.Implied),
                0xab => (OpCode.PLB, AddressingMode.Stack),
                0xac => (OpCode.LDY, AddressingMode.Absolute),
                0xad => (OpCode.LDA, AddressingMode.Absolute),
                0xae => (OpCode.LDX, AddressingMode.Absolute),
                0xaf => (OpCode.LDA, AddressingMode.AbsoluteLong),

                0xb0 => (OpCode.BCS, AddressingMode.ProgramCounterRelative),
                0xb1 => (OpCode.LDA, AddressingMode.DirectIndirectIndexed),
                0xb2 => (OpCode.LDA, AddressingMode.DirectIndirect),
                0xb3 => (OpCode.LDA, AddressingMode.StackRelativeIndirectIndexed),
                0xb4 => (OpCode.LDY, AddressingMode.DirectIndexedWithX),
                0xb5 => (OpCode.LDA, AddressingMode.DirectIndexedWithX),
                0xb6 => (OpCode.LDX, AddressingMode.DirectIndexedWithY),
                0xb7 => (OpCode.LDA, AddressingMode.DirectIndirectLongIndexed),
                0xb8 => (OpCode.CLV, AddressingMode.Implied),
                0xb9 => (OpCode.LDA, AddressingMode.AbsoluteIndexedWithY),
                0xba => (OpCode.TSX, AddressingMode.Implied),
                0xbb => (OpCode.TYX, AddressingMode.Implied),
                0xbc => (OpCode.LDY, AddressingMode.AbsoluteIndexedWithX),
                0xbd => (OpCode.LDA, AddressingMode.AbsoluteIndexedWithX),
                0xbe => (OpCode.LDX, AddressingMode.AbsoluteIndexedWithY),
                0xbf => (OpCode.LDA, AddressingMode.AbsoluteLongIndexed),

                0xc0 => (OpCode.CPY, AddressingMode.Immediate),
                0xc1 => (OpCode.CMP, AddressingMode.DirectIndexedIndirect),
                0xc2 => (OpCode.REP, AddressingMode.Immediate),
                0xc3 => (OpCode.CMP, AddressingMode.StackRelative),
                0xc4 => (OpCode.CPY, AddressingMode.Direct),
                0xc5 => (OpCode.CMP, AddressingMode.Direct),
                0xc6 => (OpCode.DEC, AddressingMode.Direct),
                0xc7 => (OpCode.CMP, AddressingMode.DirectIndirectLong),
                0xc8 => (OpCode.INY, AddressingMode.Implied),
                0xc9 => (OpCode.CMP, AddressingMode.Immediate),
                0xca => (OpCode.DEX, AddressingMode.Implied),
                0xcb => (OpCode.WAI, AddressingMode.Implied),
                0xcc => (OpCode.CPY, AddressingMode.Absolute),
                0xcd => (OpCode.CMP, AddressingMode.Absolute),
                0xce => (OpCode.DEC, AddressingMode.Absolute),
                0xcf => (OpCode.CMP, AddressingMode.AbsoluteLong),

                0xd0 => (OpCode.BNE, AddressingMode.ProgramCounterRelative),
                0xd1 => (OpCode.CMP, AddressingMode.DirectIndirectIndexed),
                0xd2 => (OpCode.CMP, AddressingMode.DirectIndirect),
                0xd3 => (OpCode.CMP, AddressingMode.StackRelativeIndirectIndexed),
                0xd4 => (OpCode.PEI, AddressingMode.Direct),
                0xd5 => (OpCode.CMP, AddressingMode.DirectIndexedWithX),
                0xd6 => (OpCode.DEC, AddressingMode.DirectIndexedWithX),
                0xd7 => (OpCode.CMP, AddressingMode.DirectIndirectLongIndexed),
                0xd8 => (OpCode.CLD, AddressingMode.Implied),
                0xd9 => (OpCode.CMP, AddressingMode.AbsoluteIndexedWithY),
                0xda => (OpCode.PHX, AddressingMode.Stack),
                0xdb => (OpCode.STP, AddressingMode.Implied),
                0xdc => (OpCode.JMP, AddressingMode.AbsoluteIndirectLong),
                0xdd => (OpCode.CMP, AddressingMode.AbsoluteIndexedWithX),
                0xde => (OpCode.DEC, AddressingMode.AbsoluteIndexedWithX),
                0xdf => (OpCode.CMP, AddressingMode.AbsoluteLongIndexed),

                0xe0 => (OpCode.CPX, AddressingMode.Immediate),
                0xe1 => (OpCode.SBC, AddressingMode.DirectIndexedIndirect),
                0xe2 => (OpCode.SEP, AddressingMode.Immediate),
                0xe3 => (OpCode.SBC, AddressingMode.StackRelative),
                0xe4 => (OpCode.CPX, AddressingMode.Direct),
                0xe5 => (OpCode.SBC, AddressingMode.Direct),
                0xe6 => (OpCode.INC, AddressingMode.Direct),
                0xe7 => (OpCode.SBC, AddressingMode.DirectIndirectLong),
                0xe8 => (OpCode.INX, AddressingMode.Implied),
                0xe9 => (OpCode.SBC, AddressingMode.Immediate),
                0xea => (OpCode.NOP, AddressingMode.Implied),
                0xeb => (OpCode.XBA, AddressingMode.Implied),
                0xec => (OpCode.CPX, AddressingMode.Absolute),
                0xed => (OpCode.SBC, AddressingMode.Absolute),
                0xee => (OpCode.INC, AddressingMode.Absolute),
                0xef => (OpCode.SBC, AddressingMode.AbsoluteLong),

                0xf0 => (OpCode.BEQ, AddressingMode.ProgramCounterRelative),
                0xf1 => (OpCode.SBC, AddressingMode.DirectIndirectIndexed),
                0xf2 => (OpCode.SBC, AddressingMode.DirectIndirect),
                0xf3 => (OpCode.SBC, AddressingMode.StackRelativeIndirectIndexed),
                0xf4 => (OpCode.PEA, AddressingMode.Immediate),
                0xf5 => (OpCode.SBC, AddressingMode.DirectIndexedWithX),
                0xf6 => (OpCode.INC, AddressingMode.DirectIndexedWithX),
                0xf7 => (OpCode.SBC, AddressingMode.DirectIndirectLongIndexed),
                0xf8 => (OpCode.SED, AddressingMode.Implied),
                0xf9 => (OpCode.SBC, AddressingMode.AbsoluteIndexedWithY),
                0xfa => (OpCode.PLX, AddressingMode.Stack),
                0xfb => (OpCode.XCE, AddressingMode.Implied),
                0xfc => (OpCode.JSR, AddressingMode.AbsoluteIndexedIndirect),
                0xfd => (OpCode.SBC, AddressingMode.AbsoluteIndexedWithX),
                0xfe => (OpCode.INC, AddressingMode.AbsoluteIndexedWithX),
                0xff => (OpCode.SBC, AddressingMode.AbsoluteLongIndexed),
            };
        }
    }
}
