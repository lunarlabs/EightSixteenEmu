using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EightSixteenEmu
{
    internal class W65C816
    {
        enum Locations
        {
            Memory,
            RegA,
            RegX,
            RegY,
            RegDP,
            RegSP,
            RegDB,
            RegPB,
            RegPC,
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
            JML,
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
            switch (opcode)
            {
                case 0x00: return (OpCode.BRK, AddressingMode.Implied);
                case 0x01: return (OpCode.ORA, AddressingMode.DirectIndexedIndirect);
                case 0x02: return (OpCode.COP, AddressingMode.Immediate);
                case 0x03: return (OpCode.ORA, AddressingMode.StackRelative);
                case 0x04: return (OpCode.TSB, AddressingMode.Direct);
                case 0x05: return (OpCode.ORA, AddressingMode.Direct);
                case 0x06: return (OpCode.ASL, AddressingMode.Direct);
                case 0x07: return (OpCode.ORA, AddressingMode.DirectIndirectLong);
                case 0x08: return (OpCode.PHP, AddressingMode.Stack);
                case 0x09: return (OpCode.ORA, AddressingMode.Immediate);
                case 0x0a: return (OpCode.ASL, AddressingMode.Implied);
                case 0x0b: return (OpCode.PHD, AddressingMode.Stack);
                case 0x0c: return (OpCode.TSB, AddressingMode.Absolute);
                case 0x0d: return (OpCode.ORA, AddressingMode.Absolute);
                case 0x0e: return (OpCode.ASL, AddressingMode.Absolute);
                case 0x0f: return (OpCode.ORA, AddressingMode.AbsoluteLong);

                case 0x10: return (OpCode.BPL, AddressingMode.ProgramCounterRelative);
                case 0x11: return (OpCode.ORA, AddressingMode.DirectIndirectIndexed);
                case 0x12: return (OpCode.ORA, AddressingMode.DirectIndirect);
                case 0x13: return (OpCode.ORA, AddressingMode.StackRelativeIndirectIndexed);
                case 0x14: return (OpCode.TRB, AddressingMode.Direct);
                case 0x15: return (OpCode.ORA, AddressingMode.DirectIndexedWithX);
                case 0x16: return (OpCode.ASL, AddressingMode.DirectIndexedWithX);
                case 0x17: return (OpCode.ORA, AddressingMode.DirectIndirectLongIndexed);
                case 0x18: return (OpCode.CLC, AddressingMode.Implied);
                case 0x19: return (OpCode.ORA, AddressingMode.AbsoluteIndexedWithY);
                case 0x1a: return (OpCode.INC, AddressingMode.Accumulator);
                case 0x1b: return (OpCode.TCS, AddressingMode.Implied);
                case 0x1c: return (OpCode.TRB, AddressingMode.Absolute);
                case 0x1d: return (OpCode.ORA, AddressingMode.AbsoluteIndexedWithX);
                case 0x1e: return (OpCode.ASL, AddressingMode.AbsoluteIndexedWithX);
                case 0x1f: return (OpCode.ORA, AddressingMode.AbsoluteLongIndexed);

                case 0x20: return (OpCode.JSR, AddressingMode.Absolute);
                case 0x21: return (OpCode.AND, AddressingMode.DirectIndexedIndirect);
                case 0x22: return (OpCode.ORA, AddressingMode.DirectIndirect);
                case 0x23: return (OpCode.AND, AddressingMode.StackRelative);
                case 0x24: return (OpCode.BIT, AddressingMode.Direct);
                case 0x25: return (OpCode.AND, AddressingMode.Direct);
                case 0x26: return (OpCode.ROL, AddressingMode.Direct);
                case 0x27: return (OpCode.AND, AddressingMode.DirectIndirectLong);
                case 0x28: return (OpCode.PLP, AddressingMode.Stack);
                case 0x29: return (OpCode.AND, AddressingMode.Immediate);
                case 0x2a: return (OpCode.ROL, AddressingMode.Accumulator);
                case 0x2b: return (OpCode.PLD, AddressingMode.Stack);
                case 0x2c: return (OpCode.BIT, AddressingMode.Absolute);
                case 0x2d: return (OpCode.AND, AddressingMode.Absolute);
                case 0x2e: return (OpCode.ROL, AddressingMode.Absolute);
                case 0x2f: return (OpCode.AND, AddressingMode.AbsoluteLong);

                case 0x30: return (OpCode.BMI, AddressingMode.ProgramCounterRelative);
                case 0x31: return (OpCode.AND, AddressingMode.DirectIndirectIndexed);
                case 0x32: return (OpCode.AND, AddressingMode.DirectIndirect);
                case 0x33: return (OpCode.AND, AddressingMode.StackRelativeIndirectIndexed);
                case 0x34: return (OpCode.BIT, AddressingMode.DirectIndexedWithX);
                case 0x35: return (OpCode.AND, AddressingMode.DirectIndexedWithX);
                case 0x36: return (OpCode.ROL, AddressingMode.DirectIndexedWithX);
                case 0x37: return (OpCode.AND, AddressingMode.DirectIndirectLongIndexed);
                case 0x38: return (OpCode.SEC, AddressingMode.Implied);
                case 0x39: return (OpCode.AND, AddressingMode.AbsoluteIndexedWithY);
                case 0x3a: return (OpCode.DEC, AddressingMode.Accumulator);
                case 0x3b: return (OpCode.TSC, AddressingMode.Implied);
                case 0x3c: return (OpCode.BIT, AddressingMode.AbsoluteIndexedWithX);
                case 0x3d: return (OpCode.AND, AddressingMode.AbsoluteIndexedWithX);
                case 0x3e: return (OpCode.ROL, AddressingMode.AbsoluteIndexedWithX);
                case 0x3f: return (OpCode.AND, AddressingMode.AbsoluteLongIndexed);

                case 0x40: return (OpCode.RTI, AddressingMode.Implied);
                case 0x41: return (OpCode.EOR, AddressingMode.DirectIndexedIndirect);
                case 0x42: return (OpCode.WDM, AddressingMode.Implied);
                case 0x43: return (OpCode.EOR, AddressingMode.StackRelative);
                case 0x44: return (OpCode.MVP, AddressingMode.BlockMove);
                case 0x45: return (OpCode.EOR, AddressingMode.Direct);
                case 0x46: return (OpCode.LSR, AddressingMode.Direct);
                case 0x47: return (OpCode.EOR, AddressingMode.DirectIndirectLong);
                case 0x48: return (OpCode.PHA, AddressingMode.Stack);
                case 0x49: return (OpCode.EOR, AddressingMode.Immediate);
                case 0x4a: return (OpCode.LSR, AddressingMode.Accumulator);
                case 0x4b: return (OpCode.PHK, AddressingMode.Stack);
                case 0x4c: return (OpCode.JMP, AddressingMode.Absolute);
                case 0x4d: return (OpCode.EOR, AddressingMode.Absolute);
                case 0x4e: return (OpCode.LSR, AddressingMode.Absolute);
                case 0x4f: return (OpCode.EOR, AddressingMode.AbsoluteLong);

                case 0x50: return (OpCode.BVC, AddressingMode.ProgramCounterRelative);
                case 0x51: return (OpCode.EOR, AddressingMode.DirectIndirectIndexed);
                case 0x52: return (OpCode.EOR, AddressingMode.DirectIndirect);
                case 0x53: return (OpCode.EOR, AddressingMode.StackRelativeIndirectIndexed);
                case 0x54: return (OpCode.MVN, AddressingMode.BlockMove);
                case 0x55: return (OpCode.EOR, AddressingMode.DirectIndexedWithX);
                case 0x56: return (OpCode.LSR, AddressingMode.DirectIndexedWithX);
                case 0x57: return (OpCode.EOR, AddressingMode.DirectIndirectLongIndexed);
                case 0x58: return (OpCode.CLI, AddressingMode.Implied);
                case 0x59: return (OpCode.EOR, AddressingMode.AbsoluteIndexedWithY);
                case 0x5a: return (OpCode.PHY, AddressingMode.Stack);
                case 0x5b: return (OpCode.TCD, AddressingMode.Implied);
                case 0x5c: return (OpCode.JMP, AddressingMode.AbsoluteLong);
                case 0x5d: return (OpCode.EOR, AddressingMode.AbsoluteIndexedWithX);
                case 0x5e: return (OpCode.LSR, AddressingMode.AbsoluteIndexedWithX);
                case 0x5f: return (OpCode.EOR, AddressingMode.AbsoluteLongIndexed);

                case 0x60: return (OpCode.RTS, AddressingMode.Implied);
                case 0x61: return (OpCode.ADC, AddressingMode.DirectIndexedIndirect);
                case 0x62: return (OpCode.PER, AddressingMode.ProgramCounterRelativeLong);
                case 0x63: return (OpCode.ADC, AddressingMode.StackRelative);
                case 0x64: return (OpCode.STZ, AddressingMode.Direct);
                case 0x65: return (OpCode.ADC, AddressingMode.Direct);
                case 0x66: return (OpCode.ROR, AddressingMode.Direct);
                case 0x67: return (OpCode.ADC, AddressingMode.DirectIndirectLong);
                case 0x68: return (OpCode.PLA, AddressingMode.Stack);
                case 0x69: return (OpCode.ADC, AddressingMode.Immediate);
                case 0x6a: return (OpCode.ROR, AddressingMode.Accumulator);
                case 0x6b: return (OpCode.RTL, AddressingMode.Implied);
                case 0x6c: return (OpCode.JMP, AddressingMode.AbsoluteIndirect);
                case 0x6d: return (OpCode.ADC, AddressingMode.Absolute);
                case 0x6e: return (OpCode.ROR, AddressingMode.Absolute);
                case 0x6f: return (OpCode.ADC, AddressingMode.AbsoluteLong);

                case 0x70: return (OpCode.BVS, AddressingMode.ProgramCounterRelative);
                case 0x71: return (OpCode.ADC, AddressingMode.DirectIndirectIndexed);
                case 0x72: return (OpCode.ADC, AddressingMode.DirectIndirect);
                case 0x73: return (OpCode.ADC, AddressingMode.StackRelativeIndirectIndexed);
                case 0x74: return (OpCode.STZ, AddressingMode.Direct);
                case 0x75: return (OpCode.ADC, AddressingMode.DirectIndexedWithX);
                case 0x76: return (OpCode.ROR, AddressingMode.DirectIndexedWithX);
                case 0x77: return (OpCode.ADC, AddressingMode.DirectIndirectLongIndexed);
                case 0x78: return (OpCode.SEI, AddressingMode.Implied);
                case 0x79: return (OpCode.ADC, AddressingMode.AbsoluteIndexedWithY);
                case 0x7a: return (OpCode.PLY, AddressingMode.Stack);
                case 0x7b: return (OpCode.TDC, AddressingMode.Implied);
                case 0x7c: return (OpCode.JMP, AddressingMode.AbsoluteLong);
                case 0x7d: return (OpCode.ADC, AddressingMode.AbsoluteIndexedWithX);
                case 0x7e: return (OpCode.ROR, AddressingMode.AbsoluteIndexedWithX);
                case 0x7f: return (OpCode.ADC, AddressingMode.AbsoluteLongIndexed);

                case 0x80: return (OpCode.BRA, AddressingMode.ProgramCounterRelative);
                case 0x81: return (OpCode.STA, AddressingMode.DirectIndexedIndirect);
                case 0x82: return (OpCode.BRL, AddressingMode.ProgramCounterRelativeLong);
                case 0x83: return (OpCode.STA, AddressingMode.StackRelative);
                case 0x84: return (OpCode.STY, AddressingMode.Direct);
                case 0x85: return (OpCode.STA, AddressingMode.Direct);
                case 0x86: return (OpCode.STX, AddressingMode.Direct);
                case 0x87: return (OpCode.STA, AddressingMode.DirectIndirectLong);
                case 0x88: return (OpCode.DEY, AddressingMode.Implied);
                case 0x89: return (OpCode.BIT, AddressingMode.Immediate);
                case 0x8a: return (OpCode.TXA, AddressingMode.Implied);
                case 0x8b: return (OpCode.PHB, AddressingMode.Stack);
                case 0x8c: return (OpCode.STY, AddressingMode.Absolute);
                case 0x8d: return (OpCode.STA, AddressingMode.Absolute);
                case 0x8e: return (OpCode.STX, AddressingMode.Absolute);
                case 0x8f: return (OpCode.STA, AddressingMode.AbsoluteLong);

                case 0x90: return (OpCode.BCC, AddressingMode.ProgramCounterRelative);
                case 0x91: return (OpCode.STA, AddressingMode.DirectIndirectIndexed);
                case 0x92: return (OpCode.STA, AddressingMode.DirectIndirect);
                case 0x93: return (OpCode.STA, AddressingMode.StackRelativeIndirectIndexed);
                case 0x94: return (OpCode.STY, AddressingMode.DirectIndexedWithX);
                case 0x95: return (OpCode.STA, AddressingMode.DirectIndexedWithX);
                case 0x96: return (OpCode.STX, AddressingMode.DirectIndexedWithX);
                case 0x97: return (OpCode.STA, AddressingMode.DirectIndirectLongIndexed);
                case 0x98: return (OpCode.TYA, AddressingMode.Implied);
                case 0x99: return (OpCode.STY, AddressingMode.AbsoluteIndexedWithY);
                case 0x9a: return (OpCode.TXS, AddressingMode.Implied);
                case 0x9b: return (OpCode.TXY, AddressingMode.Implied);
                case 0x9c: return (OpCode.STZ, AddressingMode.Absolute);
                case 0x9d: return (OpCode.STA, AddressingMode.AbsoluteIndexedWithX);
                case 0x9e: return (OpCode.STZ, AddressingMode.AbsoluteIndexedWithX);
                case 0x9f: return (OpCode.STA, AddressingMode.AbsoluteLongIndexed);

                case 0xa0: return (OpCode.LDY, AddressingMode.Immediate);
                case 0xa1: return (OpCode.LDA, AddressingMode.DirectIndexedIndirect);
                case 0xa2: return (OpCode.LDX, AddressingMode.Immediate);
                case 0xa3: return (OpCode.LDA, AddressingMode.StackRelative);
                case 0xa4: return (OpCode.LDY, AddressingMode.Direct);
                case 0xa5: return (OpCode.LDA, AddressingMode.Direct);
                case 0xa6: return (OpCode.LDX, AddressingMode.Direct);
                case 0xa7: return (OpCode.LDA, AddressingMode.DirectIndirectLong);
                case 0xa8: return (OpCode.TAY, AddressingMode.Implied);
                case 0xa9: return (OpCode.LDA, AddressingMode.Immediate);
                case 0xaa: return (OpCode.TAX, AddressingMode.Implied);
                case 0xab: return (OpCode.PLB, AddressingMode.Stack);
                case 0xac: return (OpCode.LDY, AddressingMode.Absolute);
                case 0xad: return (OpCode.LDA, AddressingMode.Absolute);
                case 0xae: return (OpCode.LDX, AddressingMode.Absolute);
                case 0xaf: return (OpCode.LDA, AddressingMode.AbsoluteLong);

                case 0xb0: return (OpCode.BCS, AddressingMode.ProgramCounterRelative);
                case 0xb1: return (OpCode.LDA, AddressingMode.DirectIndirectIndexed);
                case 0xb2: return (OpCode.LDA, AddressingMode.DirectIndirect);
                case 0xb3: return (OpCode.LDA, AddressingMode.StackRelativeIndirectIndexed);
                case 0xb4: return (OpCode.LDY, AddressingMode.DirectIndexedWithX);
                case 0xb5: return (OpCode.LDA, AddressingMode.DirectIndexedWithX);
                case 0xb6: return (OpCode.LDX, AddressingMode.DirectIndexedWithY);
                case 0xb7: return (OpCode.LDA, AddressingMode.DirectIndirectLongIndexed);
                case 0xb8: return (OpCode.CLV, AddressingMode.Implied);
                case 0xb9: return (OpCode.LDA, AddressingMode.AbsoluteIndexedWithY);
                case 0xba: return (OpCode.TSX, AddressingMode.Implied);
                case 0xbb: return (OpCode.TYX, AddressingMode.Implied);
                case 0xbc: return (OpCode.LDY, AddressingMode.AbsoluteIndexedWithX);
                case 0xbd: return (OpCode.LDA, AddressingMode.AbsoluteIndexedWithX);
                case 0xbe: return (OpCode.LDX, AddressingMode.AbsoluteIndexedWithY);
                case 0xbf: return (OpCode.LDA, AddressingMode.AbsoluteLongIndexed);

                case 0xc0: return (OpCode.CPY, AddressingMode.Immediate);
                case 0xc1: return (OpCode.CMP, AddressingMode.DirectIndexedIndirect);
                case 0xc2: return (OpCode.REP, AddressingMode.Immediate);
                case 0xc3: return (OpCode.CMP, AddressingMode.StackRelative);
                case 0xc4: return (OpCode.CPY, AddressingMode.Direct);
                case 0xc5: return (OpCode.CMP, AddressingMode.Direct);
                case 0xc6: return (OpCode.DEC, AddressingMode.Direct);
                case 0xc7: return (OpCode.CMP, AddressingMode.DirectIndirectLongIndexed);
                case 0xc8: return (OpCode.INY, AddressingMode.Implied);
                case 0xc9: return (OpCode.CMP, AddressingMode.Immediate);
                case 0xca: return (OpCode.DEX, AddressingMode.Implied);
                case 0xcb: return (OpCode.WAI, AddressingMode.Implied);
                case 0xcc: return (OpCode.CPY, AddressingMode.Absolute);
                case 0xcd: return (OpCode.CMP, AddressingMode.Absolute);
                case 0xce: return (OpCode.DEC, AddressingMode.Absolute);
                case 0xcf: return (OpCode.CMP, AddressingMode.AbsoluteLong);

                case 0xd0: return (OpCode.BNE, AddressingMode.ProgramCounterRelative);
                case 0xd1: return (OpCode.CMP, AddressingMode.DirectIndirectIndexed);
                case 0xd2: return (OpCode.CMP, AddressingMode.DirectIndirect);
                case 0xd3: return (OpCode.CMP, AddressingMode.StackRelativeIndirectIndexed);
                case 0xd4: return (OpCode.PEI, AddressingMode.DirectIndirect);
                case 0xd5: return (OpCode.CMP, AddressingMode.DirectIndexedWithX);
                case 0xd6: return (OpCode.DEC, AddressingMode.DirectIndexedWithX);
                case 0xd7: return (OpCode.CMP, AddressingMode.DirectIndirectLongIndexed);
                case 0xd8: return (OpCode.CLD, AddressingMode.Implied);
                case 0xd9: return (OpCode.CMP, AddressingMode.AbsoluteIndexedWithY);
                case 0xda: return (OpCode.PHX, AddressingMode.Stack);
                case 0xdb: return (OpCode.STP, AddressingMode.Implied);
                case 0xdc: return (OpCode.JMP, AddressingMode.AbsoluteIndexedIndirect);
                case 0xdd: return (OpCode.CMP, AddressingMode.AbsoluteIndexedWithX);
                case 0xde: return (OpCode.DEC, AddressingMode.AbsoluteIndexedWithX);
                case 0xdf: return (OpCode.CMP, AddressingMode.AbsoluteLongIndexed);

                case 0xe0: return (OpCode.CPX, AddressingMode.Immediate);
                case 0xe1: return (OpCode.SBC, AddressingMode.DirectIndexedIndirect);
                case 0xe2: return (OpCode.SEP, AddressingMode.Immediate);
                case 0xe3: return (OpCode.SBC, AddressingMode.StackRelative);
                case 0xe4: return (OpCode.CPX, AddressingMode.Direct);
                case 0xe5: return (OpCode.SBC, AddressingMode.Direct);
                case 0xe6: return (OpCode.INC, AddressingMode.Direct);
                case 0xe7: return (OpCode.SBC, AddressingMode.DirectIndirectLong);
                case 0xe8: return (OpCode.INX, AddressingMode.Implied);
                case 0xe9: return (OpCode.SBC, AddressingMode.Immediate);
                case 0xea: return (OpCode.NOP, AddressingMode.Implied);
                case 0xeb: return (OpCode.XBA, AddressingMode.Implied);
                case 0xec: return (OpCode.CPX, AddressingMode.Absolute);
                case 0xed: return (OpCode.SBC, AddressingMode.Absolute);
                case 0xee: return (OpCode.INC, AddressingMode.Absolute);
                case 0xef: return (OpCode.SBC, AddressingMode.AbsoluteLong);

                case 0xf0: return (OpCode.BEQ, AddressingMode.ProgramCounterRelative);
                case 0xf1: return (OpCode.SBC, AddressingMode.DirectIndirectIndexed);
                case 0xf2: return (OpCode.SBC, AddressingMode.DirectIndirect);
                case 0xf3: return (OpCode.SBC, AddressingMode.StackRelativeIndirectIndexed);
                case 0xf4: return (OpCode.PEA, AddressingMode.Absolute);
                case 0xf5: return (OpCode.SBC, AddressingMode.DirectIndexedWithX);
                case 0xf6: return (OpCode.INC, AddressingMode.DirectIndexedWithX);
                case 0xf7: return (OpCode.SBC, AddressingMode.DirectIndirectLongIndexed);
                case 0xf8: return (OpCode.SED, AddressingMode.Implied);
                case 0xf9: return (OpCode.SBC, AddressingMode.AbsoluteIndexedWithY);
                case 0xfa: return (OpCode.PLX, AddressingMode.Stack);
                case 0xfb: return (OpCode.XCE, AddressingMode.Implied);
                case 0xfc: return (OpCode.JSR, AddressingMode.AbsoluteIndexedIndirect);
                case 0xfd: return (OpCode.SBC, AddressingMode.AbsoluteIndexedWithX);
                case 0xfe: return (OpCode.INC, AddressingMode.AbsoluteIndexedWithX);
                case 0xff: return (OpCode.SBC, AddressingMode.AbsoluteLongIndexed);

                default: throw new NotImplementedException();
            }
        }
    }
}
