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
 */
using EightSixteenEmu.MPU;
using System.Runtime.CompilerServices;
using System.Text;
using Addr = System.UInt32;
using Word = System.UInt16;

namespace EightSixteenEmu
{
    /// <summary>
    /// A class representing the W65C816 microprocessor.
    /// </summary>
    public class Microprocessor
    {
        private int _cycles;
        private bool _threadRunning;
        private Thread? _runThread;
        private bool _verbose;
        private static readonly AutoResetEvent _clockEvent = new(false);
        private readonly EmuCore _core;
        private readonly StringBuilder _lastInstruction;
        internal bool IRQ => _core.Mapper.DeviceInterrupting;
        internal bool NMICalled;
        internal readonly Dictionary<W65C816.AddressingMode, AddressingModeStrategy> _addressingModes;
        internal readonly Dictionary<W65C816.OpCode, OpcodeCommand> _operations;
        private readonly ProcessorContext context;

        internal W65C816.OpCode CurrentOpCode { get; private set; }
        internal W65C816.AddressingMode CurrentAddressingMode { get; private set; }
        internal OpcodeCommand Instruction { get => _operations[CurrentOpCode]; }
        internal AddressingModeStrategy AddressingMode { get => _addressingModes[CurrentAddressingMode]; }

        public bool Verbose
        {
            get => _verbose;
#if !DEBUG
            set => _verbose = value;
#endif
        }

        public delegate void CycleHandler(int cycles, Cycle details);

        public event CycleHandler? NewCycle;

        protected virtual void OnNewCycle(int cycles, Cycle details)
        {
            NewCycle?.Invoke(cycles, details);
        }

        public int Cycles
        {
            get => _cycles;
            internal set => _cycles = value;
        }

        public string ExecutionState => context.StateName;

        [Flags]
        public enum StatusFlags : byte
        {
            None = 0,
            C = 0x01,   // carry
            Z = 0x02,   // zero
            I = 0x04,   // interrupt disable
            D = 0x08,   // decimal mode
            X = 0x10,   // index register width (native), break (emulation)
            M = 0x20,   // accumulator/memory width (native only)
            V = 0x40,   // overflow
            N = 0x80,   // negative
        }
         internal enum InterruptType : byte
        {
            Reset,
            Abort,
            NMI,
            IRQ,
            BRK,
            COP,
        }

        internal enum HWInterruptType : byte
        {
            None,
            IRQ,
            NMI,
        }

        public enum CycleType : Byte
        {
            Internal,
            Write,
            Read,
        }

        // accessible registers
        private Word _regA;  // accumulator
        private Word _regX;  // index register X
        private Word _regY;  // index register Y
        private Word _regDP; // direct page pointer
        private Word _regSP; // stack pointer
        private byte _regDB; // data bank
        private byte _regPB; // program bank
        private Word _regPC; // program counter
        private StatusFlags _regSR;  // status flags register
        private bool _flagE; // emulation flag

        public Word RegA
        {
            get => _regA;
            internal set => _regA = value;
        }

        public Word RegX
        {
            get => _regX;
            internal set => _regX = value;
        }

        public Word RegY
        {
            get => _regY;
            internal set => _regY = value;
        }

        public Word RegDP
        {
            get => _regDP;
            internal set => _regDP = value;
        }

        public Word RegSP
        {
            get => _regSP;
            internal set => _regSP = value;
        }

        public byte RegDB
        {
            get => _regDB;
            internal set => _regDB = value;
        }

        public byte RegPB
        {
            get => _regPB;
            internal set => _regPB = value;
        }

        public Word RegPC
        {
            get => _regPC;
            internal set => _regPC = value;
        }

        public StatusFlags RegSR
        {
            get => _regSR;
            internal set => _regSR = value;
        }

        public bool FlagE
        {
            get => _flagE;
            internal set => _flagE = value;
        }

        public byte RegAH // high byte of accumulator
        {
            get => HighByte(_regA);
            internal set => _regA = Join(LowByte(_regA), value);
        }
        public byte RegAL // low byte of accumulator
        {
            get => LowByte(_regA);
            internal set => _regA = Join(value, HighByte(_regA));
        }
        public byte RegXH // high byte of X register
        {
            get => HighByte(_regX);
            internal set => _regX = Join(LowByte(_regX), value);
        }
        public byte RegXL // low byte of X register
        {
            get => LowByte(_regX);
            internal set => _regX = Join(value, HighByte(_regX));
        }
        public byte RegYH // high byte of Y register
        {
            get => HighByte(_regY);
            internal set => _regY = Join(LowByte(_regY), value);
        }
        public byte RegYL // low byte of Y register
        {
            get => LowByte(_regY);
            internal set => _regY = Join(value, HighByte(_regY));
        }
        public byte RegSH // high byte of stack pointer
        {
            get => HighByte(_regSP);
            internal set => _regSP = Join(LowByte(_regSP), value);
        }
        public byte RegSL // low byte of stack pointer
        {
            get => LowByte(_regSP);
            internal set => _regSP = Join(value, HighByte(_regSP));
        }

        public byte RegDH //high byte of direct pointer
        {
            get => HighByte(_regDP);
            internal set => _regDP = Join(LowByte(_regDP), value);
        }

        public byte RegDL //low byte of direct pointer
        {
            get => LowByte(_regDP);
            internal set => _regDP = Join(value, HighByte(_regDP));
        }

        public bool FlagM { get => ReadStatusFlag(StatusFlags.M); }
        public bool FlagX { get => ReadStatusFlag(StatusFlags.X); }

        // non-accessible registers
        internal byte _regIR; // instruction register
        private byte _regMD; // memory data register
        private Addr _lastAddress; // last address accessed

        internal byte RegMD
        {
            get => _regMD;
            set => _regMD = value;
        }

        /// <summary>
        /// Creates a new instance of the W65C816 microprocessor.
        /// </summary>
        public Microprocessor(EmuCore core)
        {
            _core = core;
            ColdReset();

            _threadRunning = false;
#if DEBUG
            _verbose = true;
#endif
            _core.ClockTick += OnClockTick;
            _core.Reset += OnReset;
            _core.NMI += OnNMI;
            _lastInstruction = new StringBuilder();
            var implied = new AM_Implied(); // this one pulls double duty

            // the house of pain
            #region Dictionary Initialization
            _addressingModes = new Dictionary<W65C816.AddressingMode, AddressingModeStrategy>
            {
                { W65C816.AddressingMode.Immediate, new AM_Immediate() },
                { W65C816.AddressingMode.Accumulator, new AM_Accumulator() },
                { W65C816.AddressingMode.ProgramCounterRelative, new AM_ProgramCounterRelative() },
                { W65C816.AddressingMode.ProgramCounterRelativeLong, new AM_ProgramCounterRelativeLong() },
                { W65C816.AddressingMode.Implied, implied },
                { W65C816.AddressingMode.Stack, implied },
                { W65C816.AddressingMode.Direct, new AM_Direct() },
                { W65C816.AddressingMode.DirectIndexedWithX, new AM_DirectIndexedX() },
                { W65C816.AddressingMode.DirectIndexedWithY, new AM_DirectIndexedY() },
                { W65C816.AddressingMode.DirectIndirect, new AM_DirectIndirect() },
                { W65C816.AddressingMode.DirectIndexedIndirect, new AM_DirectIndexedIndirect() },
                { W65C816.AddressingMode.DirectIndirectIndexed, new AM_DirectIndirectIndexed() },
                { W65C816.AddressingMode.DirectIndirectLong, new AM_DirectIndirectLong() },
                { W65C816.AddressingMode.DirectIndirectLongIndexed, new AM_DirectIndirectLongIndexedY() },
                { W65C816.AddressingMode.Absolute, new AM_Absolute() },
                { W65C816.AddressingMode.AbsoluteIndexedWithX, new AM_AbsoluteIndexedX() },
                { W65C816.AddressingMode.AbsoluteIndexedWithY, new AM_AbsoluteIndexedY() },
                { W65C816.AddressingMode.AbsoluteLong, new AM_AbsoluteLong() },
                { W65C816.AddressingMode.AbsoluteLongIndexed, new AM_AbsoluteLongIndexedX() },
                { W65C816.AddressingMode.StackRelative, new AM_StackRelative() },
                { W65C816.AddressingMode.StackRelativeIndirectIndexed, new AM_StackRelativeIndirectIndexedY() },
                { W65C816.AddressingMode.AbsoluteIndirect, new AM_AbsoluteIndirect() },
                { W65C816.AddressingMode.AbsoluteIndirectLong, new AM_AbsoluteIndirectLong() },
                { W65C816.AddressingMode.AbsoluteIndexedIndirect, new AM_AbsoluteIndexedIndirect() },
                { W65C816.AddressingMode.BlockMove, new AM_BlockMove() },
            };
            _operations = new Dictionary<W65C816.OpCode, OpcodeCommand>
            {
                { W65C816.OpCode.ADC, new OP_ADC() },
                { W65C816.OpCode.AND, new OP_AND() },
                { W65C816.OpCode.ASL, new OP_ASL() },
                { W65C816.OpCode.BCC, new OP_BCC() },
                { W65C816.OpCode.BCS, new OP_BCS() },
                { W65C816.OpCode.BEQ, new OP_BEQ() },
                { W65C816.OpCode.BIT, new OP_BIT() },
                { W65C816.OpCode.BMI, new OP_BMI() },
                { W65C816.OpCode.BNE, new OP_BNE() },
                { W65C816.OpCode.BPL, new OP_BPL() },
                { W65C816.OpCode.BRK, new OP_BRK() },
                { W65C816.OpCode.CLC, new OP_CLC() },
                { W65C816.OpCode.CLD, new OP_CLD() },
                { W65C816.OpCode.CLI, new OP_CLI() },
                { W65C816.OpCode.CLV, new OP_CLV() },
                { W65C816.OpCode.CPX, new OP_CPX() },
                { W65C816.OpCode.CPY, new OP_CPY() },
                { W65C816.OpCode.CMP, new OP_CMP() },
                { W65C816.OpCode.COP, new OP_COP() },
                { W65C816.OpCode.DEC, new OP_DEC() },
                { W65C816.OpCode.DEX, new OP_DEX() },
                { W65C816.OpCode.DEY, new OP_DEY() },
                { W65C816.OpCode.EOR, new OP_EOR() },
                { W65C816.OpCode.INC, new OP_INC() },
                { W65C816.OpCode.INX, new OP_INX() },
                { W65C816.OpCode.INY, new OP_INY() },
                { W65C816.OpCode.JMP, new OP_JMP() },
                { W65C816.OpCode.JSR, new OP_JSR() },
                { W65C816.OpCode.LDA, new OP_LDA() },
                { W65C816.OpCode.LDX, new OP_LDX() },
                { W65C816.OpCode.LDY, new OP_LDY() },
                { W65C816.OpCode.LSR, new OP_LSR() },
                { W65C816.OpCode.NOP, new OP_NOP() },
                { W65C816.OpCode.ORA, new OP_ORA() },
                { W65C816.OpCode.PHA, new OP_PHA() },
                { W65C816.OpCode.PHP, new OP_PHP() },
                { W65C816.OpCode.PLA, new OP_PLA() },
                { W65C816.OpCode.PLP, new OP_PLP() },
                { W65C816.OpCode.PHD, new OP_PHD() },
                { W65C816.OpCode.PHX, new OP_PHX() },
                { W65C816.OpCode.PHY, new OP_PHY() },
                { W65C816.OpCode.REP, new OP_REP() },
                { W65C816.OpCode.ROL, new OP_ROL() },
                { W65C816.OpCode.ROR, new OP_ROR() },
                { W65C816.OpCode.RTI, new OP_RTI() },
                { W65C816.OpCode.RTS, new OP_RTS() },
                { W65C816.OpCode.SBC, new OP_SBC() },
                { W65C816.OpCode.SEC, new OP_SEC() },
                { W65C816.OpCode.SED, new OP_SED() },
                { W65C816.OpCode.SEI, new OP_SEI() },
                { W65C816.OpCode.STA, new OP_STA() },
                { W65C816.OpCode.STX, new OP_STX() },
                { W65C816.OpCode.STY, new OP_STY() },
                { W65C816.OpCode.STP, new OP_STP() },
                { W65C816.OpCode.TAX, new OP_TAX() },
                { W65C816.OpCode.TAY, new OP_TAY() },
                { W65C816.OpCode.TRB, new OP_TRB() },
                { W65C816.OpCode.TSB, new OP_TSB() },
                { W65C816.OpCode.TSX, new OP_TSX() },
                { W65C816.OpCode.TXA, new OP_TXA() },
                { W65C816.OpCode.TXS, new OP_TXS() },
                { W65C816.OpCode.TYA, new OP_TYA() },
                { W65C816.OpCode.TYX, new OP_TYX() },
                { W65C816.OpCode.WAI, new OP_WAI() },
                { W65C816.OpCode.WDM, new OP_WDM() },
                { W65C816.OpCode.XBA, new OP_XBA() },
                { W65C816.OpCode.XCE, new OP_XCE() },
                { W65C816.OpCode.TCS, new OP_TCS() },
                { W65C816.OpCode.PLD, new OP_PLD() },
                { W65C816.OpCode.MVP, new OP_MVP() },
                { W65C816.OpCode.PHK, new OP_PHK() },
                { W65C816.OpCode.BVC, new OP_BVC() },
                { W65C816.OpCode.MVN, new OP_MVN() },
                { W65C816.OpCode.PER, new OP_PER() },
                { W65C816.OpCode.STZ, new OP_STZ() },
                { W65C816.OpCode.RTL, new OP_RTL() },
                { W65C816.OpCode.BVS, new OP_BVS() },
                { W65C816.OpCode.PLY, new OP_PLY() },
                { W65C816.OpCode.BRA, new OP_BRA() },
                { W65C816.OpCode.BRL, new OP_BRL() },
                { W65C816.OpCode.PHB, new OP_PHB() },
                { W65C816.OpCode.TXY, new OP_TXY() },
                { W65C816.OpCode.PLB, new OP_PLB() },
                { W65C816.OpCode.PEI, new OP_PEI() },
                { W65C816.OpCode.SEP, new OP_SEP() },
                { W65C816.OpCode.PEA, new OP_PEA() },
                { W65C816.OpCode.PLX, new OP_PLX() },
                { W65C816.OpCode.TSC, new OP_TSC() },
                { W65C816.OpCode.TCD, new OP_TCD() },
                { W65C816.OpCode.TDC, new OP_TDC() },
                { W65C816.OpCode.JSL, new OP_JSL() },
            };
            #endregion
            context = new ProcessorContext(this);
        }

        private void ColdReset()
        {
            _regA = 0x0000;
            _regX = 0x0000;
            _regY = 0x0000;
            _regDP = 0x0000;
            _regSP = 0x0100;
            _regDB = 0x00;
            _regPB = 0x00;
            _regPC = 0x0000;
            _regSR = (StatusFlags)0x34;
            _flagE = true;
            _regMD = 0x00;
            _cycles = 0;
        }

        public void Reset() => context.Reset();
        private void NextInstruction() => context.NextInstruction();
        internal void Interrupt(InterruptType source) => context.Interrupt(source);
        internal void Stop() => context.Stop();
        internal void Wait() => context.Wait();
        public void BusRequest() => context.BusRequest();
        public void BusRelease() => context.BusRelease();
        internal void Disable() => context.Disable();
        internal void Enable() => context.Enable();
        public void SetProcessorState(MicroprocessorState state) => context.SetProcessorState(state);


        internal void OnClockTick(object? sender, EventArgs e)
        {
            if (_threadRunning)
            {
                _clockEvent.Set();
            }
        }

        internal void OnReset(object? sender, EventArgs e)
        {
            // TODO: Right now, if the Reset event is fired, the current operation will complete
            // which means memory and registers will be altered before the reset starts.
            // The '816 treats the reset signal as immediate, don't change anything
            // Use a cancellation token or Thread.Interrupt?
            Reset();
        }

        internal void OnNMI(object? sender, EventArgs e)
        {
            NMICalled = true;
        }

        public void StartThread()
        {
            if (!_threadRunning)
            {
                _runThread = new Thread(Run);
                _runThread.Start();
            }
        }

        public void StopThread()
        {
            if (_runThread != null && _threadRunning)
            {
                _threadRunning = false;
                _clockEvent.Set();
                _runThread.Join();
            }
        }

        private void Run()
        {
            if (_verbose)
            {
                Console.WriteLine("Starting W65C816 microprocessor thread.");
            }
            _threadRunning = true;
            try
            {
                while (_threadRunning)
                {
                    NextInstruction();
                }
            }
            catch (ThreadInterruptedException ex)
            {
                if(_verbose) Console.WriteLine(ex.Message);
                _threadRunning = false;
                _runThread?.Join();
            }
            finally
            {
                if (_verbose)
                {
                    Console.WriteLine("Stopping W65C816 microprocessor thread.");
                }
            }
        }

        internal void InternalCycle()
        {
            OnNewCycle(_cycles, new Cycle(CycleType.Internal, _lastAddress));
            HandleClockCycle();
        }

        private void HandleClockCycle()
        {
            if (_threadRunning)
            {
                _clockEvent.WaitOne();
            }
            _cycles++;
        }

        #region Data Manipulation
        static byte LowByte(Word word)
        {
            return (byte)(word);
        }
        static byte HighByte(Word word)
        {
            return ((byte)(word >> 8));
        }
        static Addr Bank(byte b)
        {
            return (Addr)(b << 16);
        }
        static byte BankOf(Addr addr)
        {
            return (byte)(addr >> 16);
        }
        static Word Join(byte l, byte h)
        {
            return (Word)(l | (h << 8));
        }
        static Addr Address(byte bank, Word address)
        {
            return (Bank(bank) | address);
        }
        static Addr Address(byte bank, byte page, byte loc)
        {
            return Bank(bank) | Join(loc, page);
        }

        // to avoid headaches when adding offsets to Words
        static Addr Address(byte bank, int address) 
        { 
            return Address(bank, (Word)address);
        }
        static Word Swap(Word w)
        {
            return (Word)((w >> 8) | (w << 8));
        }
        private Addr _longPC { get => Address(_regPB, _regPC); }
        #endregion

        #region Memory Access

        internal Word ReadValue(bool isByte, Addr address)
        {
            return isByte switch
            {
                false => ReadWord(address),
                true => ReadByte(address),
            };
        }

        internal Word ReadImmediate(bool isByte)
        {
            Word result = isByte switch
            {
                false => ReadWord(),
                true => ReadByte(),
            };
            string arg = isByte ? $"#${result:x2}" : $"#${result:x4}";
            _lastInstruction.Append(arg);
            return result;
        }

        internal void WriteValue(Word value, bool isByte, Addr address)
        {
            if (!isByte)
            {
                WriteWord(value, address);
            } 
            else
            {
                WriteByte((byte)value, address);
            }
        }

        internal byte ReadByte(Addr address)
        {
            _lastAddress = address;
            byte? result = _core.Mapper[address];
            if (result != null)
            {
                _regMD = (byte)result;
            }
            
            OnNewCycle(_cycles, new Cycle(CycleType.Read, address, _regMD));
            HandleClockCycle();
            return _regMD;
        }

        internal Word ReadWord(Addr address, bool wrapping = false)
        {
            if (!wrapping) return Join(ReadByte(address), ReadByte(address + 1));
            else
            {
                byte b = BankOf(address);
                Word a = (Word)address;
                return Join(ReadByte(Address(b, a)), ReadByte(Address(b, (Word)(a + 1))));
            }
        }

        internal Addr ReadAddr(Addr address, bool wrapping = false)
        {
            if (!wrapping) return Address(ReadByte(address + 2), ReadWord(address));
            else
            {
                byte b = BankOf(address);
                Word a = (Word)address;
                return Address(ReadByte(Address(b, (Word)(a + 2))), ReadWord(address, wrapping));
            }
        }

        internal byte ReadByte()
        {
            byte result = ReadByte(_longPC);
            _regPC += 1;
            return result;
        }

        internal Word ReadWord()
        {
            Word result = ReadWord(_longPC, true);
            _regPC += 2;
            return result;
        }

        internal Addr ReadAddr()
        {
            Addr result = ReadAddr(_longPC, true);
            _regPC += 3;
            return result;
        }

        internal void WriteByte(byte value, Addr address)
        {
            _lastAddress = address;
            _regMD = value;
            _core.Mapper[address] = value;
            OnNewCycle(_cycles, new Cycle(CycleType.Write, address, _regMD));
            HandleClockCycle();
        }

        internal void WriteWord(Word value, Addr address)
        {
            WriteByte(HighByte(value), address + 1);
            WriteByte(LowByte(value), address);
        }

        internal void PushByte(byte value)
        {
            WriteByte(value, _regSP--);
            if (_flagE)
            {
                RegSH = 0x01;
            }
        }

        internal void PushWord(Word value)
        {
            PushByte(HighByte(value));
            PushByte(LowByte(value));
        }

        internal byte PullByte()
        {
            byte result = _flagE ? ReadByte((uint)(0x0100 | ++RegSL)) : ReadByte(++_regSP);
            return result;
        }

        internal Word PullWord()
        {
            byte l = PullByte();
            byte h = PullByte();
            return Join(l, h);
        }

        #endregion

        #region Status Register Manipulation
        internal void SetStatusFlag(StatusFlags flag, bool value)
        {
                if (value)
                {
                    _regSR |= flag;
                }
                else
                {
                    _regSR &= ~flag;
                }
        }
        internal bool AccumulatorIs8Bit { get { return _flagE || ReadStatusFlag(StatusFlags.M); } }
        // in emulation, flag M should always be set, but we'll check both just in case
        internal bool IndexesAre8Bit { get { return _flagE || ReadStatusFlag(StatusFlags.X); } }
        // in emulation, flag X becomes flag B, for determining BRK vs IRQ behavior, while the indexes remain bytes.

        internal bool ReadStatusFlag(StatusFlags flag)
        {
            return (_regSR & flag) != 0;
        }

        internal void SetNZStatusFlagsFromValue(byte value)
        {
            SetStatusFlag(StatusFlags.N, (value & 0x80) != 0);
            SetStatusFlag(StatusFlags.Z, value == 0);
        }

        internal void SetNZStatusFlagsFromValue(Word value, bool isByte = false)
        {
            SetStatusFlag(StatusFlags.N, (value & (isByte ? 0x80 : 0x8000)) != 0);
            SetStatusFlag(StatusFlags.Z, value == 0);
        }

        internal void SetEmulationMode(bool value)
        {
            if (value)
            {
                SetStatusFlag(StatusFlags.M | StatusFlags.X, true);
                RegXH = 0;
                RegYH = 0;
                RegSH = 0x01;
                _flagE = true;
            }
            else { _flagE = false; }
        }
        #endregion

        internal void LoadInterruptVector(W65C816.Vector vector)
        {
            _regPC = ReadWord((Addr)vector);
            _regPB = 0x00;
        }

        internal void DecodeInstruction()
        {
            _regIR = ReadByte();
            (CurrentOpCode, CurrentAddressingMode) = W65C816.OpCodeLookup(_regIR);
            // TODO: Maybe an event here that peeks ahead and gives out the instruction?
            // I already have the notation templates in AddressingModeStrategy, probably would be
            // trivial to make it its own method
        }

        public void ExecuteInstruction()
        {
            if (_runThread == null || !_threadRunning) 
            {
                NextInstruction(); 
            }
            else throw new InvalidOperationException("Cannot advance operation manually while thread is running.");
        }
        

        public MicroprocessorState GetStatus()
        {
            MicroprocessorState result = new()
            {
                Cycles = _cycles,
                A = _regA,
                X = _regX,
                Y = _regY,
                DP = _regDP,
                SP = _regSP,
                PC = _regPC,
                DB = _regDB,
                PB = _regPB,
                FlagN = (_regSR & StatusFlags.N) == StatusFlags.N,
                FlagV = (_regSR & StatusFlags.V) == StatusFlags.V,
                FlagM = (_regSR & StatusFlags.M) == StatusFlags.M,
                FlagX = (_regSR & StatusFlags.X) == StatusFlags.X,
                FlagD = (_regSR & StatusFlags.D) == StatusFlags.D,
                FlagI = (_regSR & StatusFlags.I) == StatusFlags.I,
                FlagZ = (_regSR & StatusFlags.Z) == StatusFlags.Z,
                FlagC = (_regSR & StatusFlags.C) == StatusFlags.C,
                FlagE = _flagE
            };
            return result;
        }

        public void SetStatus(MicroprocessorState state)
        {
            _cycles = state.Cycles;
            _regA = state.A;
            _regX = state.X;
            _regY = state.Y;
            _regDP = state.DP;
            _regSP = state.SP;
            _regPC = state.PC;
            _regDB = state.DB;
            _regPB = state.PB;
            SetStatusFlag(StatusFlags.N, state.FlagN);
            SetStatusFlag(StatusFlags.V, state.FlagV);
            SetStatusFlag(StatusFlags.M, state.FlagM);
            SetStatusFlag(StatusFlags.X, state.FlagX);
            SetStatusFlag(StatusFlags.D, state.FlagD);
            SetStatusFlag(StatusFlags.I, state.FlagI);
            SetStatusFlag(StatusFlags.Z, state.FlagZ);
            SetStatusFlag(StatusFlags.C, state.FlagC);
            SetEmulationMode(state.FlagE);
        }

        public struct Cycle
        {
            public CycleType Type;
            public Addr Address;
            public byte Value;
            public override string ToString()
            {
                return $"Address: 0x{Address:X6}, Value: 0x{Value:X2}, Type: {Type}";
            }

            public Cycle(CycleType type, Addr address, byte value = 0x00)
            {
                Type = type;
                Address = address;
                Value = value;
            }
        }

        public class MicroprocessorState
        {
            public int Cycles;
            public UInt16 A, X, Y, DP, SP, PC;
            public Byte DB, PB;
            public bool FlagN, FlagV, FlagM, FlagX, FlagD, FlagI, FlagZ, FlagC, FlagE;

            public override string ToString()
            {
                string flags = $"{(FlagN ? "N" : "-")}{(FlagV ? "V" : "-")}{(FlagE ? "." : (FlagM ? "M" : "-"))}{(FlagX ? (FlagE ? "B" : "X") : "-")}{(FlagD ? "D" : "-")}{(FlagI ? "I" : "-")}{(FlagZ ? "Z" : "-")}{(FlagC ? "C" : "-")} {(FlagE ? "E" : "-")}";
                return $"Cycles: {Cycles}, A: 0x{A:x4}, X: 0x{X:x4}, Y: 0x{Y:x4}, DP: 0x{DP:x4}, SP: 0x{SP:x4}, DB: 0x{DB:x2}, PB: 0x{PB:x2} PC: 0x{PC:x4}, Flags: {flags}";
            }

            public string Flags()
            {
                return $"{(FlagN ? "N" : "-")}{(FlagV ? "V" : "-")}{(FlagE ? (FlagM ? "." : "!") : (FlagM ? "M" : "-"))}{(FlagX ? (FlagE ? "B" : "X") : "-")}{(FlagD ? "D" : "-")}{(FlagI ? "I" : "-")}{(FlagZ ? "Z" : "-")}{(FlagC ? "C" : "-")} {(FlagE ? "E" : "-")}";
            }

            public Dictionary<string, ushort> RegistersAsDictionary
            {
                get
                {
                    var result = new Dictionary<string, ushort>
                {
                    { "A", A },
                    { "X", X },
                    { "Y", Y },
                    { "DP", DP },
                    { "SP", SP },
                    { "PC", PC },
                    { "DB", DB },
                    { "PB", PB }
                };
                    return result;
                }
            }
        }
    }
}
