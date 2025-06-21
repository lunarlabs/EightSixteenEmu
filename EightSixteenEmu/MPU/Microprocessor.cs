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
 */
using System.Collections.Immutable;
using static EightSixteenEmu.MPU.MicroOpCode;
using static EightSixteenEmu.W65C816;
using Addr = System.UInt32;
using Word = System.UInt16;

namespace EightSixteenEmu.MPU
{
    /// <summary>
    /// A class representing the W65C816 microprocessor.
    /// </summary>
    public class Microprocessor
    {
        #region Fields
        #region Registers
        private int _cycles;
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

        // non-accessible registers
        private byte _regMD; // memory data register
        private Addr _lastAddress; // last address accessed
        internal ushort InternalAddress; // temporary address register for various operations
        internal ushort InternalData; // temporary data register for various operations
        internal byte RegIR; // instruction register
        #endregion

        #region Microprocessor State
        private Queue<MicroOpCode> _microOps = new();
        private readonly ProcessorContext context;
        private bool HandlingInterrupt = false; // used to prevent re-entrant interrupts
        internal bool NMICalled;
        #endregion

        #region Threading and API Integration
        private readonly EmuCore _core;
        private bool _verbose;
        private Thread? _runThread;
        private volatile bool _threadRunning;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly Lock @lock = new();
        private static readonly AutoResetEvent _clockEvent = new(false);
        #endregion

        #region Addressing Modes and Operations
        internal readonly ImmutableDictionary<AddressingMode, AddressingModeStrategy> _addressingModes;
        internal readonly ImmutableDictionary<OpCode, OpcodeCommand> _operations;
        #endregion

        #endregion

        #region Properties
        #region Register Access
        public int Cycles
        {
            get
            {
                lock (@lock)
                {
                    return _cycles;
                }
            }

            internal set
            {
                lock (@lock)
                {
                    _cycles = value;
                }
            }
        }

        #endregion


        #endregion
        internal bool IRQ => _core.Mapper.DeviceInterrupting;
        internal int QueueCount => _microOps.Count;
        internal OpCode? CurrentOpCode { get; private set; }
        internal AddressingMode? CurrentAddressingMode { get; private set; }
        internal OpcodeCommand? Instruction => CurrentOpCode != null ? _operations.TryGetValue(CurrentOpCode.Value, out OpcodeCommand? op) ? op : null : null;
        internal AddressingModeStrategy? AddressingMode => CurrentAddressingMode != null ? _addressingModes.TryGetValue(CurrentAddressingMode.Value, out AddressingModeStrategy? mode) ? mode : null : null;

        public delegate void InstructionHandler(OpCode opCode, string operand);

        public event InstructionHandler? NewInstruction;

        protected virtual void OnNewInstruction(OpCode opCode, string operand)
        {
            NewInstruction?.Invoke(opCode, operand);
        }

        public delegate void CycleHandler(int cycles, Cycle details, MicroprocessorState state);

        public event CycleHandler? NewCycle;

        protected virtual void OnNewCycle(int cycles, Cycle details, MicroprocessorState state)
        {
            NewCycle?.Invoke(cycles, details, state);
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

        [Flags]
        public enum HWInterrupts : byte
        {
            None = 0x00,
            Reset = 0x01,
            Abort = 0x02,
            NMI = 0x04,
            IRQ = 0x08,
        }

        public enum CycleType : byte
        {
            Internal,
            Write,
            Read,
        }


        public Word RegA
        {
            get
            {
                lock (@lock)
                {
                    return _regA;
                }
            }
            internal set
            {
                lock (@lock)
                {
                    _regA = value;
                }
            }
        }

        public Word RegX
        {
            get
            {
                lock (@lock)
                {
                    return _regX;
                }
            }
            internal set
            {
                lock (@lock)
                {
                    _regX = value;
                }
            }
        }

        public Word RegY
        {
            get
            {
                lock (@lock)
                {
                    return _regY;
                }
            }
            internal set
            {
                lock (@lock)
                {
                    _regY = value;
                }
            }
        }

        public Word RegDP
        {
            get
            {
                lock (@lock)
                {
                    return _regDP;
                }
            }
            internal set
            {
                lock (@lock)
                {
                    _regDP = value;
                }
            }
        }

        public Word RegSP
        {
            get
            {
                lock (@lock)
                {
                    return _regSP;
                }
            }
            internal set
            {
                lock (@lock)
                {
                    _regSP = value;
                }
            }
        }

        public byte RegDB
        {
            get
            {
                lock (@lock)
                {
                    return _regDB;
                }
            }
            internal set
            {
                lock (@lock)
                {
                    _regDB = value;
                }
            }
        }

        public byte RegPB
        {
            get
            {
                lock (@lock)
                {
                    return _regPB;
                }
            }
            internal set
            {
                lock (@lock)
                {
                    _regPB = value;
                }
            }
        }

        public Word RegPC
        {
            get
            {
                lock (@lock)
                {
                    return _regPC;
                }
            }
            internal set
            {
                lock (@lock)
                {
                    _regPC = value;
                }
            }
        }

        public StatusFlags RegSR
        {
            get
            {
                lock (@lock)
                {
                    return _regSR;
                }
            }
            internal set
            {
                lock (@lock)
                {
                    _regSR = value;
                }
            }
        }

        public bool FlagE
        {
            get
            {
                lock (@lock)
                {
                    return _flagE;
                }
            }
            internal set
            {
                lock (@lock)
                {
                    _flagE = value;
                }
            }
        }

        public byte RegAH // high byte of accumulator
        {
            get => HighByte(RegA);
            internal set => RegA = Join(LowByte(RegA), value);
        }
        public byte RegAL // low byte of accumulator
        {
            get => LowByte(RegA);
            internal set => RegA = Join(value, HighByte(RegA));
        }
        public byte RegXH // high byte of X register
        {
            get => HighByte(RegX);
            internal set => RegX = Join(LowByte(RegX), value);
        }
        public byte RegXL // low byte of X register
        {
            get => LowByte(RegX);
            internal set => RegX = Join(value, HighByte(RegX));
        }
        public byte RegYH // high byte of Y register
        {
            get => HighByte(RegY);
            internal set => RegY = Join(LowByte(RegY), value);
        }
        public byte RegYL // low byte of Y register
        {
            get => LowByte(RegY);
            internal set => RegY = Join(value, HighByte(RegY));
        }
        public byte RegSH // high byte of stack pointer
        {
            get => HighByte(RegSP);
            internal set => RegSP = Join(LowByte(RegSP), value);
        }
        public byte RegSL // low byte of stack pointer
        {
            get => LowByte(RegSP);
            internal set => RegSP = Join(value, HighByte(RegSP));
        }

        public byte RegDH //high byte of direct pointer
        {
            get => HighByte(RegDP);
            internal set => RegDP = Join(LowByte(RegDP), value);
        }

        public byte RegDL //low byte of direct pointer
        {
            get => LowByte(RegDP);
            internal set => RegDP = Join(value, HighByte(RegDP));
        }

        public bool FlagM { get => ReadStatusFlag(StatusFlags.M); }
        public bool FlagX { get => ReadStatusFlag(StatusFlags.X); }


        internal byte RegMD
        {
            get
            {
                lock (@lock)
                {
                    return _regMD;
                }
            }

            set
            {
                lock (@lock)
                {
                    _regMD = value;
                }
            }
        }

        /// <summary>
        /// Creates a new instance of the W65C816 microprocessor.
        /// </summary>
        public Microprocessor(EmuCore core)
        {
            _core = core;

            _threadRunning = false;
#if DEBUG
            _verbose = true;
#endif
            _core.ClockTick += OnClockTick;
            var implied = new AM_Implied(); // this one pulls double duty

            // the house of pain
            #region Dictionary Initialization
            var tempAddressingModes = new Dictionary<AddressingMode, AddressingModeStrategy>
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
            var tempOperations = new Dictionary<OpCode, OpcodeCommand>
            {
                { OpCode.ADC, new OP_ADC() },
                { OpCode.AND, new OP_AND() },
                { OpCode.ASL, new OP_ASL() },
                { OpCode.BCC, new OP_BCC() },
                { OpCode.BCS, new OP_BCS() },
                { OpCode.BEQ, new OP_BEQ() },
                { OpCode.BIT, new OP_BIT() },
                { OpCode.BMI, new OP_BMI() },
                { OpCode.BNE, new OP_BNE() },
                { OpCode.BPL, new OP_BPL() },
                { OpCode.BRK, new OP_BRK() },
                { OpCode.CLC, new OP_CLC() },
                { OpCode.CLD, new OP_CLD() },
                { OpCode.CLI, new OP_CLI() },
                { OpCode.CLV, new OP_CLV() },
                { OpCode.CPX, new OP_CPX() },
                { OpCode.CPY, new OP_CPY() },
                { OpCode.CMP, new OP_CMP() },
                { OpCode.COP, new OP_COP() },
                { OpCode.DEC, new OP_DEC() },
                { OpCode.DEX, new OP_DEX() },
                { OpCode.DEY, new OP_DEY() },
                { OpCode.EOR, new OP_EOR() },
                { OpCode.INC, new OP_INC() },
                { OpCode.INX, new OP_INX() },
                { OpCode.INY, new OP_INY() },
                { OpCode.JMP, new OP_JMP() },
                { OpCode.JSR, new OP_JSR() },
                { OpCode.LDA, new OP_LDA() },
                { OpCode.LDX, new OP_LDX() },
                { OpCode.LDY, new OP_LDY() },
                { OpCode.LSR, new OP_LSR() },
                { OpCode.NOP, new OP_NOP() },
                { OpCode.ORA, new OP_ORA() },
                { OpCode.PHA, new OP_PHA() },
                { OpCode.PHP, new OP_PHP() },
                { OpCode.PLA, new OP_PLA() },
                { OpCode.PLP, new OP_PLP() },
                { OpCode.PHD, new OP_PHD() },
                { OpCode.PHX, new OP_PHX() },
                { OpCode.PHY, new OP_PHY() },
                { OpCode.REP, new OP_REP() },
                { OpCode.ROL, new OP_ROL() },
                { OpCode.ROR, new OP_ROR() },
                { OpCode.RTI, new OP_RTI() },
                { OpCode.RTS, new OP_RTS() },
                { OpCode.SBC, new OP_SBC() },
                { OpCode.SEC, new OP_SEC() },
                { OpCode.SED, new OP_SED() },
                { OpCode.SEI, new OP_SEI() },
                { OpCode.STA, new OP_STA() },
                { OpCode.STX, new OP_STX() },
                { OpCode.STY, new OP_STY() },
                { OpCode.STP, new OP_STP() },
                { OpCode.TAX, new OP_TAX() },
                { OpCode.TAY, new OP_TAY() },
                { OpCode.TRB, new OP_TRB() },
                { OpCode.TSB, new OP_TSB() },
                { OpCode.TSX, new OP_TSX() },
                { OpCode.TXA, new OP_TXA() },
                { OpCode.TXS, new OP_TXS() },
                { OpCode.TYA, new OP_TYA() },
                { OpCode.TYX, new OP_TYX() },
                { OpCode.WAI, new OP_WAI() },
                { OpCode.WDM, new OP_WDM() },
                { OpCode.XBA, new OP_XBA() },
                { OpCode.XCE, new OP_XCE() },
                { OpCode.TCS, new OP_TCS() },
                { OpCode.PLD, new OP_PLD() },
                { OpCode.MVP, new OP_MVP() },
                { OpCode.PHK, new OP_PHK() },
                { OpCode.BVC, new OP_BVC() },
                { OpCode.MVN, new OP_MVN() },
                { OpCode.PER, new OP_PER() },
                { OpCode.STZ, new OP_STZ() },
                { OpCode.RTL, new OP_RTL() },
                { OpCode.BVS, new OP_BVS() },
                { OpCode.PLY, new OP_PLY() },
                { OpCode.BRA, new OP_BRA() },
                { OpCode.BRL, new OP_BRL() },
                { OpCode.PHB, new OP_PHB() },
                { OpCode.TXY, new OP_TXY() },
                { OpCode.PLB, new OP_PLB() },
                { OpCode.PEI, new OP_PEI() },
                { OpCode.SEP, new OP_SEP() },
                { OpCode.PEA, new OP_PEA() },
                { OpCode.PLX, new OP_PLX() },
                { OpCode.TSC, new OP_TSC() },
                { OpCode.TCD, new OP_TCD() },
                { OpCode.TDC, new OP_TDC() },
                { OpCode.JSL, new OP_JSL() },
            };
            _addressingModes = tempAddressingModes.ToImmutableDictionary();
            _operations = tempOperations.ToImmutableDictionary();
            #endregion
            context = new ProcessorContext(this);
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

        public void SetNMI()
        {
            NMICalled = true;
        }

        internal void OnNMI(object? sender, EventArgs e)
        {
            NMICalled = true;
        }

        internal void EnqueueMicroOp(MicroOpCode microOp)
        {
            lock (@lock)
            {
                _microOps.Enqueue(microOp);
            }
        }
        internal void StartThread()
        {
            if (!_threadRunning)
            {
                _runThread = new Thread(Run);
                _runThread.Start();
            }
        }

        internal void StopThread()
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
                if (_verbose) Console.WriteLine(ex.Message);
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

        [Obsolete("Will not be used in new queue-based system, which will handle cycles automatically.")]
        internal void InternalCycle()
        {
            OnNewCycle(_cycles, new Cycle(CycleType.Internal, _lastAddress), Status);
            HandleClockCycle();
        }

        private void DoCycle()
        {
            if (_microOps.Count == 0)
            {
                // no current operation, fetch the next
                _microOps.Enqueue(new MicroOpFetchAndDecode()); 
            }
            MicroOpCode microOp = _microOps.Dequeue(); // get the first micro-operation of the cycle
            OpCycleType fullCycleType = microOp.CycleType; // for debugging purposes
            _cycles++;
        }

        private void EnqueueInterrupt(InterruptType type)
        {
            if (!HandlingInterrupt)
            {
                if (!_flagE) _microOps.Enqueue(new MicroOpPushByteFrom(RegByteLocation.K)); // push PB if not in emulation mode
                _microOps.Enqueue(new MicroOpPushByteFrom(RegByteLocation.PCL)); // push PC low byte
                _microOps.Enqueue(new MicroOpPushByteFrom(RegByteLocation.PCH)); // push PC high byte
                if (_flagE)
                {
                    if (type == InterruptType.BRK) _microOps.Enqueue(new MicroOpChangeFlags(StatusFlags.X, StatusFlags.None));
                    else _microOps.Enqueue(new MicroOpChangeFlags(StatusFlags.None, StatusFlags.X));
                }
                _microOps.Enqueue(new MicroOpPushByteFrom(RegByteLocation.P)); // push status register
                _microOps.Enqueue(new MicroOpChangeFlags(StatusFlags.I, StatusFlags.D)); // Mask interrupt on, clear decimal mode
                Vector vector;
                if (_flagE)
                {
                    vector = type switch
                    {
                        InterruptType.BRK => Vector.EmulationIRQ,
                        InterruptType.COP => Vector.EmulationCOP,
                        InterruptType.IRQ => Vector.EmulationIRQ,
                        InterruptType.NMI => Vector.EmulationNMI,
                        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
                    };
                }
                else
                {
                    vector = type switch
                    {
                        InterruptType.BRK => Vector.NativeBRK,
                        InterruptType.COP => Vector.NativeCOP,
                        InterruptType.IRQ => Vector.NativeIRQ,
                        InterruptType.NMI => Vector.NativeNMI,
                        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
                    };
                }
            }
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
            return (byte)word;
        }
        static byte HighByte(Word word)
        {
            return (byte)(word >> 8);
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
            return (Word)(l | h << 8);
        }
        static Addr Address(byte bank, Word address)
        {
            return Bank(bank) | address;
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
            return (Word)(w >> 8 | w << 8);
        }
        private Addr _longPC { get => Address(_regPB, _regPC); }
        #endregion

        #region Memory Access

        internal void ByteRead(Addr address)
        {
            _lastAddress = address;
            byte? result = _core.Mapper[address];
            if (result != null)
            {
                _regMD = (byte)result;
            }
        }

        internal void ByteWrite(Addr address)
        {
            _lastAddress = address;
            _core.Mapper[address] = _regMD;
        }

        internal void EnqueueWordRead(RegWordLocation destination, Addr address)
        {
            (RegByteLocation high, RegByteLocation low ) = ByteLocationsFromWordLocations(destination);
            EnqueueMicroOp(new MicroOpReadTo(address, low));
            EnqueueMicroOp(new MicroOpReadTo(address + 1, high));
        }

        internal void EnqueueWordRead(RegWordLocation destination)
        {
            (RegByteLocation high, RegByteLocation low) = ByteLocationsFromWordLocations(destination);
            EnqueueMicroOp(new MicroOpReadToAndAdvancePC(low));
            EnqueueMicroOp(new MicroOpReadToAndAdvancePC(high));
        }

        internal void EnqueueWordWrite(RegWordLocation source, Addr address)
        {
            (RegByteLocation high, RegByteLocation low) = ByteLocationsFromWordLocations(source);
            EnqueueMicroOp(new MicroOpWriteFrom(address, low));
            EnqueueMicroOp(new MicroOpWriteFrom(address + 1, high));
        }

        internal byte ReadByte(Addr address)
        {
            _lastAddress = address;
            byte? result = _core.Mapper[address];
            if (result != null)
            {
                _regMD = (byte)result;
            }

            OnNewCycle(_cycles, new Cycle(CycleType.Read, address, _regMD), Status);
            HandleClockCycle();
            return _regMD;
        }

        internal void WriteByte(byte value, Addr address)
        {
            _lastAddress = address;
            _regMD = value;
            _core.Mapper[address] = value;
            OnNewCycle(_cycles, new Cycle(CycleType.Write, address, _regMD), Status);
            HandleClockCycle();
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

        internal void WriteWord(Word value, Addr address)
        {
            WriteByte(HighByte(value), address + 1);
            WriteByte(LowByte(value), address);
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

        [Obsolete("This is covered by MicroOpCodeReadToAndAdvancePC, which handles the cycles and PC increment automatically.")]
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

        internal Word ReadValue(bool isByte, Addr address)
        {
            return isByte switch
            {
                false => ReadWord(address),
                true => ReadByte(address),
            };
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

        internal byte PullByte(bool ignoreBounds = false)
        {
            byte result;
            if (_flagE && ignoreBounds)
            {
                result = ReadByte(++_regSP);
            }
            else
            {
                result = _flagE ? ReadByte((uint)(0x0100 | ++RegSL)) : ReadByte(++_regSP);
            }
            return result;
        }

        internal Word PullWord(bool ignoreBounds = false)
        {
            byte l = PullByte(ignoreBounds);
            byte h = PullByte(ignoreBounds);
            if (_flagE && !ignoreBounds) RegSH = 0x01;
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
            SetStatusFlag(StatusFlags.Z, (isByte ? (byte)value : value) == 0);
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

        internal void LoadInterruptVector(Vector vector)
        {
            _regPC = ReadWord((Addr)vector);
            _regPB = 0x00;
        }

        [Obsolete("This is not used in the new queue-based system. Use the version without parameters.")]
        internal void DecodeInstruction(byte inst = 0xEA) // NOP by default
        {
            // peek ahead for the event
            (CurrentOpCode, CurrentAddressingMode) = OpCodeLookup(inst);
            //OnNewInstruction(CurrentOpCode, AddressModeNotation(CurrentAddressingMode));

            // now have the actual read to fire off the cycle event
            RegIR = ReadByte();
        }

        internal void DecodeInstruction()
        {
            // the instruction byte should already be in RegIR
            (CurrentOpCode, CurrentAddressingMode) = OpCodeLookup(RegIR);
        }

        internal void ClearInstruction()
        {
            CurrentOpCode = null;
            CurrentAddressingMode = null;
        }

        private string AddressModeNotation(AddressingMode mode)
        {
            switch (mode)
            {
                case W65C816.AddressingMode.Immediate:
                    if (AccumulatorIs8Bit)
                    {
                        return $"#${(byte)ReadOperand():X2}";
                    }
                    else
                    {
                        return $"#${(ushort)ReadOperand(2):X4}";
                    }
                case W65C816.AddressingMode.Accumulator:
                    return "A";
                case W65C816.AddressingMode.ProgramCounterRelative:
                    return $"{(sbyte)ReadOperand():+0;-0}";
                case W65C816.AddressingMode.ProgramCounterRelativeLong:
                    return $"+{(short)ReadOperand(2)}";
                case W65C816.AddressingMode.Implied:
                    return "";
                case W65C816.AddressingMode.Stack:
                    return "";
                case W65C816.AddressingMode.Direct:
                    return $"${(byte)ReadOperand():X2}";
                case W65C816.AddressingMode.DirectIndexedWithX:
                    return $"${(byte)ReadOperand():X2}, X";
                case W65C816.AddressingMode.DirectIndexedWithY:
                    return $"${(byte)ReadOperand():X2}, Y";
                case W65C816.AddressingMode.DirectIndirect:
                    return $"(${(byte)ReadOperand():X2})";
                case W65C816.AddressingMode.DirectIndexedIndirect:
                    return $"(${(byte)ReadOperand():X2}, X)";
                case W65C816.AddressingMode.DirectIndirectIndexed:
                    return $"(${(byte)ReadOperand():X2}), Y";
                case W65C816.AddressingMode.DirectIndirectLong:
                    return $"[${(byte)ReadOperand():X2}]";
                case W65C816.AddressingMode.DirectIndirectLongIndexed:
                    return $"[${(byte)ReadOperand():X2}], Y";
                case W65C816.AddressingMode.Absolute:
                    return $"${(ushort)ReadOperand(2):X4}";
                case W65C816.AddressingMode.AbsoluteIndexedWithX:
                    return $"${(ushort)ReadOperand(2):X4}, X";
                case W65C816.AddressingMode.AbsoluteIndexedWithY:
                    return $"${(ushort)ReadOperand(2):X4}, Y";
                case W65C816.AddressingMode.AbsoluteLong:
                    return $"${ReadOperand(3):X6}";
                case W65C816.AddressingMode.AbsoluteLongIndexed:
                    return $"${ReadOperand(3):X6}, X";
                case W65C816.AddressingMode.StackRelative:
                    return $"${(byte)ReadOperand():X2}, S";
                case W65C816.AddressingMode.StackRelativeIndirectIndexed:
                    return $"$({(byte)ReadOperand():X2}, S), Y";
                case W65C816.AddressingMode.AbsoluteIndirect:
                    return $"(${(ushort)ReadOperand(2):X4})";
                case W65C816.AddressingMode.AbsoluteIndirectLong:
                    return $"[${(ushort)ReadOperand(2):X4}]";
                case W65C816.AddressingMode.AbsoluteIndexedIndirect:
                    return $"(${(ushort)ReadOperand(2):X4}, X)";
                case W65C816.AddressingMode.BlockMove:
                    byte source = _core.Mapper[_longPC + 1] ?? _regMD;
                    byte dest = _core.Mapper[_longPC + 2] ?? _regMD;
                    return $"${source:X2}, ${dest:X2}";
                default:
                    throw new ArgumentException("invalid addressing mode", nameof(mode));
            }

            int ReadOperand(int bytes = 1)
            {
                int result = 0;
                for (byte i = 0; i < bytes; i++)
                {
                    result |= (_core.Mapper[_longPC + i + 1] ?? _regMD) << i * 8;
                }
                return result;
            }
        }

        public void ExecuteInstruction()
        {
            if (_runThread == null || !_threadRunning)
            {
                NextInstruction();
            }
            else throw new InvalidOperationException("Cannot advance operation manually while thread is running.");
        }


        public MicroprocessorState Status
        {
            get
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

            internal set
            {
                _cycles = value.Cycles;
                _regA = value.A;
                _regX = value.X;
                _regY = value.Y;
                _regDP = value.DP;
                _regSP = value.SP;
                _regPC = value.PC;
                _regDB = value.DB;
                _regPB = value.PB;
                SetStatusFlag(StatusFlags.N, value.FlagN);
                SetStatusFlag(StatusFlags.V, value.FlagV);
                SetStatusFlag(StatusFlags.M, value.FlagM);
                SetStatusFlag(StatusFlags.X, value.FlagX);
                SetStatusFlag(StatusFlags.D, value.FlagD);
                SetStatusFlag(StatusFlags.I, value.FlagI);
                SetStatusFlag(StatusFlags.Z, value.FlagZ);
                SetStatusFlag(StatusFlags.C, value.FlagC);
                SetEmulationMode(value.FlagE);
            }
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
            public Word A, X, Y, DP, SP, PC;
            public byte DB, PB;
            public bool FlagN, FlagV, FlagM, FlagX, FlagD, FlagI, FlagZ, FlagC, FlagE;

            public override string ToString()
            {
                string flags = $"{(FlagN ? "N" : "-")}{(FlagV ? "V" : "-")}{(FlagE ? "." : FlagM ? "M" : "-")}{(FlagX ? FlagE ? "B" : "X" : "-")}{(FlagD ? "D" : "-")}{(FlagI ? "I" : "-")}{(FlagZ ? "Z" : "-")}{(FlagC ? "C" : "-")} {(FlagE ? "E" : "-")}";
                return $"Cycles: {Cycles}, A: 0x{A:x4}, X: 0x{X:x4}, Y: 0x{Y:x4}, DP: 0x{DP:x4}, SP: 0x{SP:x4}, DB: 0x{DB:x2}, PB: 0x{PB:x2} PC: 0x{PC:x4}, Flags: {flags}";
            }

            public string Flags()
            {
                return $"{(FlagN ? "N" : "-")}{(FlagV ? "V" : "-")}{(FlagE ? FlagM ? "." : "!" : FlagM ? "M" : "-")}{(FlagX ? FlagE ? "B" : "X" : "-")}{(FlagD ? "D" : "-")}{(FlagI ? "I" : "-")}{(FlagZ ? "Z" : "-")}{(FlagC ? "C" : "-")} {(FlagE ? "E" : "-")}";
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
            public Dictionary<string, bool> FlagsAsDictionary
            {
                get
                {
                    var result = new Dictionary<string, bool>
                {
                    { "N", FlagN },
                    { "V", FlagV },
                    { "M", FlagM },
                    { "X", FlagX },
                    { "D", FlagD },
                    { "I", FlagI },
                    { "Z", FlagZ },
                    { "C", FlagC },
                    { "E", FlagE }
                };
                    return result;
                }
            }
        }
    }
}
