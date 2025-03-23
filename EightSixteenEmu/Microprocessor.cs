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
        private bool _resetting;
        private bool _interruptingMaskable;
        private bool _interruptingNonMaskable;
        private bool _aborting;
        private bool _stopped;
        private bool _waiting;
        private bool _verbose;
        private static readonly AutoResetEvent _clockEvent = new(false);
        private readonly EmuCore _core;
        private readonly StringBuilder _lastInstruction;

        public bool Verbose
        {
            get => _verbose;
#if !DEBUG
            set => _verbose = value;
#endif
        }

        public int Cycles
        {
            get => _cycles;
        }
        public bool Stopped { get => _stopped; }
        public bool Waiting { get => _waiting; }

        private delegate void DoOperation(W65C816.AddressingMode mode);

        [Flags]
        public enum StatusFlags : byte
        {
            None = 0,
            C = 0x01,   // carry
            Z = 0x02,   // zero
            I = 0x04,   // interrupt disable
            D = 0x08,   // decimal mode
            X = 0x10,   // index register width (native), break (emulation)
            M = 0x20,   // accumulator/memory width
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
        private byte _regIR; // instruction register
        private byte _regMD; // memory data register

        /// <summary>
        /// Creates a new instance of the W65C816 microprocessor.
        /// </summary>
        public Microprocessor(EmuCore core)
        {
            _core = core;
            _regA = 0x0000;
            _regX = 0x0000;
            _regY = 0x0000;
            _regDP = 0x0000;
            _regSP = 0x0100;
            _regDB = 0x00;
            _regPB = 0x00;
            _regPC = 0x0000;
            _regSR = (StatusFlags)0x34;
            _flagE = false;
            _regMD = 0x00;

            _cycles = 0;
            _threadRunning = false;
            _resetting = true;
            _interruptingMaskable = false;
            _interruptingNonMaskable = false;
            _stopped = false;
#if DEBUG
            _verbose = true;
#endif
            _core.ClockTick += OnClockTick;
            _core.Reset += OnReset;
            _core.IRQ += OnInterrupt;
            _core.NMI += OnNMI;
            _lastInstruction = new StringBuilder();
        }

        internal void OnClockTick(object? sender, EventArgs e)
        {
            if (_threadRunning)
            {
                _clockEvent.Set();
            }
        }

        internal void OnInterrupt(object? sender, EventArgs e)
        {
            _interruptingMaskable = true;
        }

        internal void OnReset(object? sender, EventArgs e)
        {
            // TODO: Right now, if the Reset event is fired, the current operation will complete
            // which means memory and registers will be altered before the reset starts.
            // This is probably not how the real '816 handles resets...
            // Use a cancellation token?
            _resetting = true;
        }

        internal void OnNMI(object? sender, EventArgs e)
        {
            _interruptingNonMaskable = true;
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
            while (_threadRunning)
            {
                NextOperation();
            }
            if (_verbose)
            {
                Console.WriteLine("Stopping W65C816 microprocessor thread.");
            }
        }

        internal void NextCycle()
        {
            if (_threadRunning)
            {
                _clockEvent.WaitOne();
            }
            _cycles++;
        }

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
            NextCycle();
            byte? result = _core.Mapper[address];
            if (result != null)
            {
                _regMD = (byte)result;
            }
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
            NextCycle();
            
            _regMD = value;
            _core.Mapper[address] = value;
        }

        internal void WriteWord(Word value, Addr address)
        {
            WriteByte(LowByte(value), address);
            WriteByte(HighByte(value), address + 1);
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
            byte result = ReadByte(++_regSP);
            if (_flagE)
            {
                RegSH = 0x01;
            }
            return result;
        }

        internal Word PullWord()
        {
            byte l = PullByte();
            byte h = PullByte();
            return Join(l, h);
        }

        #endregion

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
        internal bool AccumulatorIs8Bit { get { return ReadStatusFlag(StatusFlags.M); } }
        internal bool IndexesAre8Bit { get { return _flagE || ReadStatusFlag(StatusFlags.X); } }

        internal bool ReadStatusFlag(StatusFlags flag)
        {
            return (_regSR & flag) != 0;
        }

        internal void SetNZStatusFlagsFromValue(byte value)
        {
            SetStatusFlag(StatusFlags.N, (value & 0x80) != 0);
            SetStatusFlag(StatusFlags.Z, value == 0);
        }

        internal void SetNZStatusFlagsFromValue(Word value)
        {
            SetStatusFlag(StatusFlags.N, (value & 0x8000) != 0);
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

        private Addr GetEffectiveAddress(W65C816.AddressingMode addressingMode)
        {
            Addr OffsetBySignedValue(bool isEightBit)
            {
                short offset;
                if (isEightBit)
                {
                    offset = (sbyte)ReadByte();
                }
                else
                {
                    offset = (short)ReadWord();
                }
                if (_verbose) Console.Write($"{offset:+0,-#}");
                Addr result = Address(_regPB, (Word)(_regPC + offset));
                return result;
            }

            Addr CalculateDirectAddress(byte offset, Word register = 0)
            {
                if (_flagE && RegDL == 0)
                {
                    return Address(0, RegDH, (byte)(offset + LowByte(register)));
                }
                else
                {
                    return Address(0, _regDP + offset + register);
                }
            }

            Addr pointer;
            byte offset;
            Word location;
            switch (addressingMode)
            {
                case W65C816.AddressingMode.Immediate:
                    // WARN: Do the reads (and subsequent RegPC advances) in the operation
                    _lastInstruction.Append("#");
                    return _longPC;
                case W65C816.AddressingMode.Accumulator:
                    _lastInstruction.Append("A");
                    return 0;
                case W65C816.AddressingMode.ProgramCounterRelative:
                    return OffsetBySignedValue(true);
                case W65C816.AddressingMode.ProgramCounterRelativeLong:
                    return OffsetBySignedValue(false);
                case W65C816.AddressingMode.Implied:
                    return 0;
                case W65C816.AddressingMode.Stack:
                    return 0;
                case W65C816.AddressingMode.Direct:
                    offset = ReadByte();
                    _lastInstruction.Append($"${offset:x2}");
                    if (RegDL != 0x00) NextCycle();
                    return Address(0, _regDP + offset);
                case W65C816.AddressingMode.DirectIndexedWithX:
                    offset = ReadByte();
                    _lastInstruction.Append($"${offset:x2}, X");
                    if (RegDL != 0x00) NextCycle();
                    return CalculateDirectAddress(offset, _regX);
                case W65C816.AddressingMode.DirectIndexedWithY:
                    offset = ReadByte();
                    _lastInstruction.Append($"${offset:x2}, Y");
                    if (RegDL != 0x00) NextCycle();
                    return CalculateDirectAddress(offset, _regY);
                case W65C816.AddressingMode.DirectIndirect:
                    offset = ReadByte();
                    _lastInstruction.Append($"(${offset:x2})");
                    if (RegDL != 0x00) NextCycle();
                    pointer = CalculateDirectAddress(offset);
                    return Address(_regDB, ReadWord(pointer));
                case W65C816.AddressingMode.DirectIndexedIndirect:
                    offset = ReadByte();
                    _lastInstruction.Append($"(${offset:x2}, X)");
                    if (RegDL != 0x00) NextCycle();
                    pointer = CalculateDirectAddress(offset, _regX);
                    return Address(_regDB, ReadWord(pointer));
                case W65C816.AddressingMode.DirectIndirectIndexed:
                    offset = ReadByte();
                    _lastInstruction.Append($"(${offset:x2}), Y");
                    if (RegDL != 0x00) NextCycle();
                    pointer = CalculateDirectAddress(offset);
                    return Address(_regDB, ReadWord(pointer + _regY));
                case W65C816.AddressingMode.DirectIndirectLong:
                    offset = ReadByte();
                    _lastInstruction.Append($"[${offset:x2}]");
                    if (RegDL != 0x00) NextCycle();
                    return ReadAddr(Address(0, _regDP + offset), true);
                case W65C816.AddressingMode.DirectIndirectLongIndexed:
                    offset = ReadByte();
                    _lastInstruction.Append($"[${offset:x2}], Y");
                    if (RegDL != 0x00) NextCycle();
                    return ReadAddr(Address(0, _regDP + offset), true) + _regY;
                case W65C816.AddressingMode.Absolute:
                    // WARN: Special case for JMP and JSR -- replace RegDB with RegPB
                    location = ReadWord();
                    _lastInstruction.Append($"${location:x4}");
                    return Address(_regDB, location);
                case W65C816.AddressingMode.AbsoluteIndexedWithX:
                    location = ReadWord();
                    _lastInstruction.Append($"${location:x4}, X");
                    return Address(_regDB, location + _regX);
                case W65C816.AddressingMode.AbsoluteIndexedWithY:
                    location = ReadWord();
                    _lastInstruction.Append($"${location:x4}, Y");
                    return Address(_regDB, location + _regY);
                case W65C816.AddressingMode.AbsoluteLong:
                    pointer = ReadAddr();
                    _lastInstruction.Append($"${pointer:x6}");
                    return pointer;
                case W65C816.AddressingMode.AbsoluteLongIndexed:
                    pointer = ReadAddr();
                    _lastInstruction.Append($"${pointer:x6}, X");
                    return pointer + _regX;
                case W65C816.AddressingMode.StackRelative:
                    offset = ReadByte();
                    _lastInstruction.Append($"${offset:x2}, S");
                    return Address(0, offset + _regSP);
                case W65C816.AddressingMode.StackRelativeIndirectIndexed:
                    offset = ReadByte();
                    _lastInstruction.Append($"(${offset:x2}, S), Y");
                    pointer = Address(0, offset + _regSP);
                    return Address(_regDB, ReadWord(pointer + _regY));
                case W65C816.AddressingMode.AbsoluteIndirect:
                    location = ReadWord();
                    _lastInstruction.Append($"(${location:x4})");
                    pointer = Address(0, location);
                    return Address(_regPB, ReadWord(pointer));
                case W65C816.AddressingMode.AbsoluteIndirectLong:
                    location = ReadWord();
                    _lastInstruction.Append($"[${location:x4}]");
                    pointer = Address(0, location);
                    return ReadAddr(pointer);
                case W65C816.AddressingMode.AbsoluteIndexedIndirect:
                    location = ReadWord();
                    _lastInstruction.Append($"(${location:x4}, X)");
                    pointer = Address(_regPB, location);
                    return Address(_regPB, ReadWord(pointer) + _regX);
                case W65C816.AddressingMode.BlockMove:
                    return 0; // handled in the operation function
                default:
                    return 0;
            }
        }

        private void LoadInterruptVector(W65C816.Vector vector)
        {
            _regPC = ReadWord((Addr)vector);
            _regPB = 0x00;
        }

        #region Opcodes

        #region ADC SBC
        private void OpAdc(W65C816.AddressingMode addressingMode)
        {
            Word addend;
            if (addressingMode == W65C816.AddressingMode.Immediate)
            {
                addend = ReadImmediate(AccumulatorIs8Bit);
            }
            else
            {
                Addr address = GetEffectiveAddress(addressingMode);
                addend = ReadValue(AccumulatorIs8Bit, address);
            }
            byte carry = (byte)(ReadStatusFlag(StatusFlags.C) ? 1 : 0);
            if (AccumulatorIs8Bit)
            {
                int al = LowByte(_regA) + addend + carry;
                if (ReadStatusFlag(StatusFlags.D))
                {
                    if (((al) & 0x0f) > 0x09) al += 0x06;
                    if (((al) & 0xf0) > 0x90) al += 0x60;
                }
                SetStatusFlag(StatusFlags.C, (al & 0x100u) != 0);
                SetStatusFlag(StatusFlags.V, ((Word)((~(_regA ^ addend)) & (_regA ^ al) & 0x80) != 0));
                SetNZStatusFlagsFromValue((byte)al);
                RegAL = (byte)al;
            }
            else
            {
                int al = _regA + addend + carry;
                if (ReadStatusFlag(StatusFlags.D))
                {
                    if (((al) & 0x000f) > 0x0009) al += 0x0006;
                    if (((al) & 0x00f0) > 0x0090) al += 0x0060;
                    if (((al) & 0x0f00) > 0x0900) al += 0x0600;
                    if (((al) & 0xf000) > 0x9000) al += 0x6000;
                }
                SetStatusFlag(StatusFlags.C, (al & 0x10000u) != 0);
                SetStatusFlag(StatusFlags.V, ((Word)((~(_regA ^ addend)) & (_regA ^ al) & 0x8000) != 0));
                SetNZStatusFlagsFromValue((Word)al);
                _regA = (Word)al;
            }
            NextCycle();
        }

        private void OpSbc(W65C816.AddressingMode addressingMode)
        {
            Word subtrahend;
            if (addressingMode == W65C816.AddressingMode.Immediate)
            {
                subtrahend = ReadImmediate(AccumulatorIs8Bit);
            }
            else
            {
                Addr address = GetEffectiveAddress(addressingMode);
                subtrahend = ReadValue(AccumulatorIs8Bit, address);
            }
            if (AccumulatorIs8Bit)
            {
                int al = RegAL + ~(byte)subtrahend + (byte)(ReadStatusFlag(StatusFlags.C) ? 0 : 1);
                if (ReadStatusFlag(StatusFlags.D))
                {
                    if (((al) & 0x0f) > 0x09) al += 0x06;
                    if (((al) & 0xf0) > 0x90) al += 0x60;
                }
                SetStatusFlag(StatusFlags.C, (al & 0x100u) != 0);
                SetStatusFlag(StatusFlags.V, (Word)((~(_regA ^ subtrahend)) & (_regA ^ al) & 0x80) != 0);
                SetNZStatusFlagsFromValue((byte)al);
                RegAL = (byte)al;
            }
            else
            {
                int al = _regA + ~subtrahend + (byte)(ReadStatusFlag(StatusFlags.C) ? 0 : 1);
                if (ReadStatusFlag(StatusFlags.D))
                {
                    if (((al) & 0x000f) > 0x0009) al += 0x0006;
                    if (((al) & 0x00f0) > 0x0090) al += 0x0060;
                    if (((al) & 0x0f00) > 0x0900) al += 0x0600;
                    if (((al) & 0xf000) > 0x9000) al += 0x6000;
                }
                SetStatusFlag(StatusFlags.C, (al & 0x10000u) != 0);
                SetStatusFlag(StatusFlags.V, (Word)((~(_regA ^ subtrahend)) & (_regA ^ al) & 0x8000) != 0);
                SetNZStatusFlagsFromValue((Word)al);
                _regA = (Word)al;
            }
        }
        #endregion
        #region CMP CPX CPY
        private void OpCmp(W65C816.AddressingMode addressingMode)
        {
            Word data;
            if (addressingMode == W65C816.AddressingMode.Immediate)
            {
                data = ReadImmediate(AccumulatorIs8Bit);
            }
            else 
            { 
                data = ReadValue(AccumulatorIs8Bit, GetEffectiveAddress(addressingMode)); 
            }
            if (AccumulatorIs8Bit)
            {
                data = (byte)(RegAL - (byte)data);
                SetNZStatusFlagsFromValue((byte)data);
                SetStatusFlag(StatusFlags.C, RegAL >= data);
            }
            else
            {
                data = (Word)(_regA - data);
                SetNZStatusFlagsFromValue(data);
                SetStatusFlag(StatusFlags.C, _regA >= data);
            }
        }

        private void OpCpx(W65C816.AddressingMode addressingMode)
        {
            Word data;
            if (addressingMode == W65C816.AddressingMode.Immediate)
            {
                data = ReadImmediate(IndexesAre8Bit);
            }
            else
            {
                data = ReadValue(IndexesAre8Bit, GetEffectiveAddress(addressingMode));
            }
            if (IndexesAre8Bit)
            {
                data = (byte)(RegXL - (byte)data);
                SetNZStatusFlagsFromValue((byte)data);
                SetStatusFlag(StatusFlags.C, RegXL >= data);
            }
            else
            {
                data = (Word)(_regX - data);
                SetNZStatusFlagsFromValue(data);
                SetStatusFlag(StatusFlags.C, _regX >= data);
            }
        }

        private void OpCpy(W65C816.AddressingMode addressingMode)
        {
            Word data;
            if (addressingMode == W65C816.AddressingMode.Immediate)
            {
                data = ReadImmediate(IndexesAre8Bit);
            }
            else
            {
                data = ReadValue(IndexesAre8Bit, GetEffectiveAddress(addressingMode));
            }
            if (IndexesAre8Bit)
            {
                data = (byte)(RegYL - (byte)data);
                SetNZStatusFlagsFromValue((byte)data);
                SetStatusFlag(StatusFlags.C, RegYL >= data);
            }
            else
            {
                data = (Word)(_regY - data);
                SetNZStatusFlagsFromValue(data);
                SetStatusFlag(StatusFlags.C, _regY >= data);
            }
        }
        #endregion
        #region DEA DEC DEX DEY INA INC INX INY
        private void OpDec(W65C816.AddressingMode addressingMode)
        {
            if (addressingMode == W65C816.AddressingMode.Accumulator)
            {
                if (AccumulatorIs8Bit)
                {
                    byte al = RegAL;
                    SetNZStatusFlagsFromValue(--al);
                    RegAL = al;
                }
                else
                {
                    Word a = _regA;
                    SetNZStatusFlagsFromValue(--a);
                    _regA = a;
                }
                NextCycle();
            }
            else
            {
                Addr address = GetEffectiveAddress(addressingMode);
                Word value = ReadValue(AccumulatorIs8Bit, address);
                value -= 1;
                NextCycle();
                if (AccumulatorIs8Bit)
                {
                    SetNZStatusFlagsFromValue((byte)value);
                }
                else
                {
                    SetNZStatusFlagsFromValue(value);
                }
                WriteValue(value, AccumulatorIs8Bit, address);
            }
        }

        private void OpDex(W65C816.AddressingMode addressingMode)
        {
            if (IndexesAre8Bit)
            {
                SetNZStatusFlagsFromValue(--RegXL);
            }
            else
            {
                SetNZStatusFlagsFromValue(--_regX);
            }
            NextCycle();
        }

        private void OpDey(W65C816.AddressingMode addressingMode)
        {
            if (IndexesAre8Bit)
            {
                SetNZStatusFlagsFromValue(--RegYL);
            }
            else
            {
                SetNZStatusFlagsFromValue(--_regY);
            }
            NextCycle();
        }

        private void OpInc(W65C816.AddressingMode addressingMode)
        {
            if (addressingMode == W65C816.AddressingMode.Accumulator)
            {
                if (AccumulatorIs8Bit)
                {
                    byte al = RegAL;
                    SetNZStatusFlagsFromValue(++al);
                    RegAL = al;
                }
                else
                {
                    Word a = _regA;
                    SetNZStatusFlagsFromValue(++a);
                    _regA = a;
                }
                NextCycle();
            }
            else
            {
                Addr address = GetEffectiveAddress(addressingMode);
                Word value = ReadValue(AccumulatorIs8Bit, address);
                value += 1;
                NextCycle();
                if (AccumulatorIs8Bit)
                {
                    SetNZStatusFlagsFromValue((byte)value);
                }
                else
                {
                    SetNZStatusFlagsFromValue(value);
                }
                WriteValue(value, AccumulatorIs8Bit, address);
            }
        }

        private void OpInx(W65C816.AddressingMode addressingMode)
        {
            if (IndexesAre8Bit)
            {
                SetNZStatusFlagsFromValue(++RegXL);
            }
            else
            {
                SetNZStatusFlagsFromValue(++_regX);
            }
            NextCycle();
        }

        private void OpIny(W65C816.AddressingMode addressingMode)
        {
            if (IndexesAre8Bit)
            {
                SetNZStatusFlagsFromValue(++RegYL);
            }
            else
            {
                SetNZStatusFlagsFromValue(++_regY);
            }
            NextCycle();
        }
        #endregion
        #region AND EOR ORA
        private void OpAnd(W65C816.AddressingMode addressingMode)
        {
            Word operand = ReadValue(AccumulatorIs8Bit, GetEffectiveAddress(addressingMode));
            if (AccumulatorIs8Bit)
            {
                RegAL = (byte)((byte)operand & RegAL);
                SetNZStatusFlagsFromValue(RegAL);
            }
            else
            {
                _regA = (ushort)(operand & _regA);
                SetNZStatusFlagsFromValue(_regA);
            }
            NextCycle();
        }

        private void OpEor(W65C816.AddressingMode addressingMode)
        {
            Word operand = ReadValue(AccumulatorIs8Bit, GetEffectiveAddress(addressingMode));
            if (AccumulatorIs8Bit)
            {
                RegAL = (byte)((byte)operand ^ RegAL);
                SetNZStatusFlagsFromValue(RegAL);
            }
            else
            {
                _regA = (ushort)(operand ^ _regA);
                SetNZStatusFlagsFromValue(_regA);
            }
            NextCycle();
        }

        private void OpOra(W65C816.AddressingMode addressingMode)
        {
            Word operand = ReadValue(AccumulatorIs8Bit, GetEffectiveAddress(addressingMode));
            if (AccumulatorIs8Bit)
            {
                RegAL = (byte)((byte)operand | RegAL);
                SetNZStatusFlagsFromValue(RegAL);
            }
            else
            {
                _regA = (ushort)(operand | _regA);
                SetNZStatusFlagsFromValue(_regA);
            }
            NextCycle();
        }
        #endregion

        private void OpBit(W65C816.AddressingMode addressingMode)
        {
            Word operand;
            if (addressingMode == W65C816.AddressingMode.Immediate)
            {
                operand = ReadImmediate(AccumulatorIs8Bit);
                if (AccumulatorIs8Bit)
                { 
                    SetStatusFlag(StatusFlags.Z, (operand & RegAL) == 0); 
                }
                else
                {
                    SetStatusFlag(StatusFlags.Z, (operand & _regA) == 0);
                }
            }
            else
            {
                operand = ReadValue(AccumulatorIs8Bit, GetEffectiveAddress(addressingMode));
                if (AccumulatorIs8Bit)
                {
                    SetStatusFlag(StatusFlags.Z, (operand & RegAL) == 0);
                    SetStatusFlag(StatusFlags.N, (operand & 0x80) != 0);
                    SetStatusFlag(StatusFlags.V, (operand & 0x40) != 0);
                }
                else
                {
                    SetStatusFlag(StatusFlags.Z, (operand & _regA) == 0);
                    SetStatusFlag(StatusFlags.N, (operand & 0x8000) != 0);
                    SetStatusFlag(StatusFlags.V, (operand & 0x4000) != 0);
                }
            }
            NextCycle();
        }

        #region TRB TSB
        private void OpTrb(W65C816.AddressingMode addressingMode)
        {
            Addr address = GetEffectiveAddress(addressingMode);
            Word value = ReadValue(AccumulatorIs8Bit, address);
            Word mask = (ushort)((AccumulatorIs8Bit ? RegAL : _regA) & value);

            WriteValue((ushort)(value & ~mask), AccumulatorIs8Bit, address);
            NextCycle();
        }

        private void OpTsb(W65C816.AddressingMode addressingMode)
        {
            Addr address = GetEffectiveAddress(addressingMode);
            Word value = ReadValue(AccumulatorIs8Bit, address);
            Word mask = (ushort)((AccumulatorIs8Bit ? RegAL : _regA) & value);

            WriteValue((ushort)(value | mask), AccumulatorIs8Bit, address);
            NextCycle();
        }
        #endregion
        #region ASL LSR ROL ROR
        private void OpAsl(W65C816.AddressingMode addressingMode)
        {
            if (addressingMode == W65C816.AddressingMode.Accumulator)
            {
                if (AccumulatorIs8Bit)
                {
                    SetStatusFlag(StatusFlags.C, (RegAL & 0x80) != 0);
                    RegAL <<= 1;
                    SetNZStatusFlagsFromValue(RegAL);
                }
                else
                {
                    SetStatusFlag(StatusFlags.C, (_regA & 0x8000) != 0);
                    _regA <<= 1;
                    SetNZStatusFlagsFromValue(_regA);
                }
            }
            else
            {
                Addr address = GetEffectiveAddress(addressingMode);
                Word operand;
                if (AccumulatorIs8Bit)
                {
                    operand = ReadByte(address);
                    SetStatusFlag(StatusFlags.C, (operand & 0x80) != 0);
                    operand = (byte)(operand << 1);
                    WriteByte((byte)(operand), address);
                    SetNZStatusFlagsFromValue((byte)operand);
                }
                else
                {
                    operand = ReadWord(address);
                    SetStatusFlag(StatusFlags.C, (operand & 0x8000) != 0);
                    operand <<= 1;
                    WriteWord(operand, address);
                    SetNZStatusFlagsFromValue(operand);
                }
            }
            NextCycle();
        }

        private void OpLsr(W65C816.AddressingMode addressingMode)
        {
            if (addressingMode == W65C816.AddressingMode.Accumulator)
            {
                if (AccumulatorIs8Bit)
                {
                    SetStatusFlag(StatusFlags.C, (RegAL & 0x01) != 0);
                    RegAL >>>= 1;
                    SetNZStatusFlagsFromValue(RegAL);
                }
                else
                {
                    SetStatusFlag(StatusFlags.C, (_regA & 0x01) != 0);
                    _regA >>>= 1;
                    SetNZStatusFlagsFromValue(_regA);
                }
            }
            else
            {
                Addr address = GetEffectiveAddress(addressingMode);
                Word operand;
                if (AccumulatorIs8Bit)
                {
                    operand = ReadByte(address);
                    SetStatusFlag(StatusFlags.C, (operand & 0x01) != 0);
                    operand = (byte)(operand >>> 1);
                    WriteByte((byte)(operand), address);
                    SetNZStatusFlagsFromValue((byte)operand);
                }
                else
                {
                    operand = ReadWord(address);
                    SetStatusFlag(StatusFlags.C, (operand & 0x01) != 0);
                    operand >>>= 1;
                    WriteWord(operand, address);
                    SetNZStatusFlagsFromValue(operand);
                }
            }
            NextCycle();
        }

        private void OpRol(W65C816.AddressingMode addressingMode)
        {
            bool shiftedOut;
            if (addressingMode == W65C816.AddressingMode.Accumulator)
            {
                if (AccumulatorIs8Bit)
                {
                    shiftedOut = (RegAL & 0x80) != 0;
                    RegAL <<= 1;
                    if (ReadStatusFlag(StatusFlags.C)) RegAL |= 0x01;
                    SetNZStatusFlagsFromValue(RegAL);
                    SetStatusFlag(StatusFlags.C, shiftedOut);
                }
                else
                {
                    shiftedOut = (_regA & 0x8000) != 0;
                    _regA <<= 1;
                    if (ReadStatusFlag(StatusFlags.C)) _regA |= 0x01;
                    SetNZStatusFlagsFromValue(_regA);
                    SetStatusFlag(StatusFlags.C, shiftedOut);
                }
            }
            else
            {
                Addr address = GetEffectiveAddress(addressingMode);
                Word operand = ReadValue(AccumulatorIs8Bit, address);
                if (AccumulatorIs8Bit)
                {
                    shiftedOut = (operand & 0x80) != 0;
                    operand = operand <<= 1;
                    if (ReadStatusFlag(StatusFlags.C)) operand |= 0x01;
                    WriteByte((byte)operand, address);
                    SetNZStatusFlagsFromValue((byte)operand);
                }
                else
                {
                    shiftedOut = (operand & 0x8000) != 0;
                    operand = operand <<= 1;
                    if (ReadStatusFlag(StatusFlags.C)) operand |= 0x01;
                    WriteWord(operand, address);
                    SetNZStatusFlagsFromValue(operand);
                }
            }
            NextCycle();
        }

        private void OpRor(W65C816.AddressingMode addressingMode)
        {
            bool shiftedOut;
            if (addressingMode == W65C816.AddressingMode.Accumulator)
            {
                if (AccumulatorIs8Bit)
                {
                    shiftedOut = (RegAL & 0x01) != 0;
                    RegAL >>>= 1;
                    if (ReadStatusFlag(StatusFlags.C)) RegAL |= 0x80;
                    SetNZStatusFlagsFromValue(RegAL);
                    SetStatusFlag(StatusFlags.C, shiftedOut);
                }
                else
                {
                    shiftedOut = (_regA & 0x0001) != 0;
                    _regA >>>= 1;
                    if (ReadStatusFlag(StatusFlags.C)) _regA |= 0x8000;
                    SetNZStatusFlagsFromValue(_regA);
                    SetStatusFlag(StatusFlags.C, shiftedOut);
                }
            }
            else
            {
                Addr address = GetEffectiveAddress(addressingMode);
                Word operand = ReadValue(AccumulatorIs8Bit, address);
                shiftedOut = (operand & 0x01) != 0;
                operand >>>= 1;
                if (AccumulatorIs8Bit)
                {
                    if (ReadStatusFlag(StatusFlags.C)) operand |= 0x80;
                    WriteByte((byte)operand, address);
                    SetNZStatusFlagsFromValue((byte)operand);
                }
                else
                {
                    if (ReadStatusFlag(StatusFlags.C)) operand |= 0x8000;
                    WriteWord(operand, address);
                    SetNZStatusFlagsFromValue(operand);
                }
            }
            NextCycle();
        }
        #endregion
        #region BCC BCS BEQ BMI BNE BPL BRA BVC BVS
        private void OpBcc(W65C816.AddressingMode addressingMode)
        {
            if (!ReadStatusFlag(StatusFlags.C))
            {
                BranchTo(GetEffectiveAddress(addressingMode));
            }
            NextCycle();
        }

        private void OpBcs(W65C816.AddressingMode addressingMode)
        {
            if (ReadStatusFlag(StatusFlags.C))
            {
                BranchTo(GetEffectiveAddress(addressingMode));
            }
            NextCycle();
        }

        private void OpBeq(W65C816.AddressingMode addressingMode)
        {
            if (ReadStatusFlag(StatusFlags.Z))
            {
                BranchTo(GetEffectiveAddress(addressingMode));
            }
            NextCycle();
        }

        private void OpBmi(W65C816.AddressingMode addressingMode)
        {
            if (ReadStatusFlag(StatusFlags.N))
            {
                BranchTo(GetEffectiveAddress(addressingMode));
            }
            NextCycle();
        }

        private void OpBne(W65C816.AddressingMode addressingMode)
        {
            if (!ReadStatusFlag(StatusFlags.Z))
            {
                BranchTo(GetEffectiveAddress(addressingMode));
            }
            NextCycle();
        }

        private void OpBpl(W65C816.AddressingMode addressingMode)
        {
            if (!ReadStatusFlag(StatusFlags.N))
            {
                BranchTo(GetEffectiveAddress(addressingMode));
            }
            NextCycle();
        }

        private void OpBra(W65C816.AddressingMode addressingMode)
        {
            BranchTo(GetEffectiveAddress(addressingMode));
            NextCycle();
        }

        private void OpBvc(W65C816.AddressingMode addressingMode)
        {
            if (!ReadStatusFlag(StatusFlags.V))
            {
                BranchTo(GetEffectiveAddress(addressingMode));
            }
            NextCycle();
        }

        private void OpBvs(W65C816.AddressingMode addressingMode)
        {
            if (ReadStatusFlag(StatusFlags.V))
            {
                BranchTo(GetEffectiveAddress(addressingMode));
            }
            NextCycle();
        }
        #endregion

        private void OpBrl(W65C816.AddressingMode addressingMode) 
        {
            NextCycle();
            NextCycle();
            BranchTo(GetEffectiveAddress(addressingMode));  
        }

        #region JMP JSL JSR
        private void OpJmp(W65C816.AddressingMode addressingMode)
        {
            Addr address = GetEffectiveAddress(addressingMode);
            if (addressingMode == W65C816.AddressingMode.AbsoluteLong ||
                addressingMode == W65C816.AddressingMode.AbsoluteIndirectLong)
            {
                _regPB = BankOf(address);
            }
            
            _regPC = (Word)address;
            NextCycle();
                
        }

        private void OpJsl(W65C816.AddressingMode addressingMode)
        {
            PushByte(_regPB);
            PushWord((Word)(_regPC + 3));
            Addr addr = GetEffectiveAddress(addressingMode);
            _regPB = BankOf(addr);
            _regPC = (Word)addr;
            NextCycle();
        }

        private void OpJsr(W65C816.AddressingMode addressingMode)
        {
            PushWord((Word)(_regPC + 2));
            Addr addr = GetEffectiveAddress(addressingMode);
            _regPC = (Word)addr;
            NextCycle();
        }
        #endregion
        #region RTL RTS
        private void OpRtl(W65C816.AddressingMode addressingMode) 
        {
            _regPC = PullWord();
            _regPC++;
        }

        private void OpRts(W65C816.AddressingMode addressingMode)
        {
            _regPC = PullWord();
            _regPC++;
            _regPB = PullByte();
        }
        #endregion
        #region BRK COP
        private void OpBrk(W65C816.AddressingMode addressingMode) 
        { 
            Interrupt(InterruptType.BRK);
        }

        private void OpCop(W65C816.AddressingMode addressingMode) {
            Interrupt(InterruptType.COP);
        }
        #endregion
        #region RTI
        private void OpRti(W65C816.AddressingMode addressingMode)
        {
            NextCycle();
            NextCycle();
            _regSR = (StatusFlags)PullByte();
            _regPC = PullWord();
            if (_flagE)
            {
                _regPB = PullByte();
            }
        }
        #endregion
        #region CLC CLD CLI CLV SEC SED SEI
        private void OpClc(W65C816.AddressingMode addressingMode)
        {
            SetStatusFlag(StatusFlags.C, false);
        }

        private void OpCld(W65C816.AddressingMode addressingMode) 
        { 
            SetStatusFlag(StatusFlags.D, false);
        }

        private void OpCli(W65C816.AddressingMode addressingMode)
        {
            SetStatusFlag(StatusFlags.I, false);
        }

        private void OpClv(W65C816.AddressingMode addressingMode)
        {
            SetStatusFlag(StatusFlags.V, false);
        }

        private void OpSec(W65C816.AddressingMode addressingMode)
        {
            SetStatusFlag(StatusFlags.C, true);
        }

        private void OpSed(W65C816.AddressingMode addressingMode)
        {
            SetStatusFlag(StatusFlags.D, true);
        }

        private void OpSei(W65C816.AddressingMode addressingMode) 
        {
            SetStatusFlag(StatusFlags.I, true);
        }
        #endregion
        #region REP SEP
        private void OpRep(W65C816.AddressingMode addressingMode)
        {
            byte flags = (byte)(ReadImmediate(true));
            if (_flagE)
            {
                // M and X flags cannot be set in emulation mode
                flags &= 0xCF;
            }
            _regSR &= (StatusFlags)~flags;
        }

        private void OpSep(W65C816.AddressingMode addressingMode) { 
            byte flags = (byte)(ReadImmediate(true));
            if (_flagE)
            {
                // M and X flags cannot be set in emulation mode
                flags &= 0xCF;
            }
            _regSR |= (StatusFlags)flags;
            if (IndexesAre8Bit)
            {
                RegXH = 0;
                RegYH = 0;
            }
        }
        #endregion
        #region LDA LDX LDY STA STX STY STZ
        private void OpLda(W65C816.AddressingMode addressingMode)
        {
            Word value;
            if (addressingMode == W65C816.AddressingMode.Immediate)
            {
                value = ReadImmediate(AccumulatorIs8Bit);
            }
            else
            {
                Addr address = GetEffectiveAddress(addressingMode);
                value = ReadValue(AccumulatorIs8Bit, address);
            }
            if (AccumulatorIs8Bit)
            {
                SetNZStatusFlagsFromValue((byte)value);
                RegAL = (byte)value;
            }
            else
            {
                SetNZStatusFlagsFromValue(value);
                _regA = value;
            }
        }

        private void OpLdx(W65C816.AddressingMode addressingMode)
        {
            Word value;
            if (addressingMode == W65C816.AddressingMode.Immediate)
            {
                value = ReadImmediate(IndexesAre8Bit);
            }
            else
            {
                Addr address = GetEffectiveAddress(addressingMode);
                value = ReadValue(IndexesAre8Bit, address);
            }
            if (IndexesAre8Bit)
            {
                SetNZStatusFlagsFromValue((byte)value);
                RegXL = (byte)value;
            }
            else
            {
                SetNZStatusFlagsFromValue(value);
                _regX = value;
            }
        }

        private void OpLdy(W65C816.AddressingMode addressingMode)
        {
            Word value;
            if (addressingMode == W65C816.AddressingMode.Immediate)
            {
                value = ReadImmediate(IndexesAre8Bit);
            }
            else
            {
                Addr address = GetEffectiveAddress(addressingMode);
                value = ReadValue(IndexesAre8Bit, address);
            }
            if (IndexesAre8Bit)
            {
                SetNZStatusFlagsFromValue((byte)value);
                RegYL = (byte)value;
            }
            else
            {
                SetNZStatusFlagsFromValue(value);
                _regY = value;
            }
        }

        private void OpSta(W65C816.AddressingMode addressingMode)
        {
            Addr address = GetEffectiveAddress(addressingMode);
            if (AccumulatorIs8Bit)
            {
                WriteByte(RegAL, address);
            }
            else
            {
                WriteWord(_regA, address);
            }
        }

        private void OpStx(W65C816.AddressingMode addressingMode)
        {
            Addr address = GetEffectiveAddress(addressingMode);
            if (IndexesAre8Bit)
            {
                WriteByte(RegXL, address);
            }
            else
            {
                WriteWord(_regX, address);
            }
        }

        private void OpSty(W65C816.AddressingMode addressingMode)
        {
            Addr address = GetEffectiveAddress(addressingMode);
            if (IndexesAre8Bit)
            {
                WriteByte(RegYL, address);
            }
            else
            {
                WriteWord(_regY, address);
            }
        }

        private void OpStz(W65C816.AddressingMode addressingMode)
        {
            Addr address = GetEffectiveAddress(addressingMode);
            if (AccumulatorIs8Bit)
            {
                WriteByte(0, address);
            }
            else
            {
                WriteWord(0, address);
            }
        }
        #endregion
        #region MVN MVP
        private void OpMvn(W65C816.AddressingMode addressingMode)
        {
            CopyMemory();
            if (IndexesAre8Bit)
            {
                RegXL++;
                RegYL++;
            }
            else
            {
                _regX++;
                _regY++;
            }
        }

        private void OpMvp(W65C816.AddressingMode addressingMode)
        {
            CopyMemory();
            if (IndexesAre8Bit)
            {
                RegXL--;
                RegYL--;
            }
            else
            {
                _regX--;
                _regY--;
            }
        }
        #endregion
        #region NOP WDM
        private void OpNop(W65C816.AddressingMode addressingMode) 
        {
            NextCycle();
        }

        private void OpWdm(W65C816.AddressingMode addressingMode)
        {
            _regPC++;
            NextCycle();
        }
        #endregion
        #region PEA PEI PER
        private void OpPea(W65C816.AddressingMode addressingMode)
        {
            Word value = ReadImmediate(false);
            PushWord(value);
        }

        private void OpPei(W65C816.AddressingMode addressingMode)
        {
            Addr address = GetEffectiveAddress(addressingMode);
            PushWord(ReadWord(address));
        }

        private void OpPer(W65C816.AddressingMode addressingMode)
        {
            Addr address = GetEffectiveAddress(addressingMode);
            PushWord(ReadWord(address));
        }
        #endregion
        #region PHA PHX PHY PLA PLX PLY
        private void OpPha(W65C816.AddressingMode addressingMode)
        {
            if (AccumulatorIs8Bit)
            {
                PushByte(RegAL);
            }
            else
            {
                PushWord(_regA);
            }
            NextCycle();
        }

        private void OpPhx(W65C816.AddressingMode addressingMode)
        {
            if (IndexesAre8Bit)
            {
                PushByte(RegXL);
            }
            else
            {
                PushWord(_regX);
            }
            NextCycle();
        }

        private void OpPhy(W65C816.AddressingMode addressingMode)
        {
            if (IndexesAre8Bit)
            {
                PushByte(RegYL);
            }
            else
            {
                PushWord(_regY);
            }
        }

        private void OpPla(W65C816.AddressingMode addressingMode)
        {
            if (AccumulatorIs8Bit)
            {
                RegAL = PullByte();
                SetNZStatusFlagsFromValue(RegAL);
            }
            else
            {
                _regA = PullWord();
                SetNZStatusFlagsFromValue(_regA);
            }
        }

        private void OpPlx(W65C816.AddressingMode addressingMode)
        {
            if (IndexesAre8Bit)
            {
                RegXL = PullByte();
                SetNZStatusFlagsFromValue(RegXL);
            }
            else
            {
                _regX = PullWord();
                SetNZStatusFlagsFromValue(_regX);
            }
        }

        private void OpPly(W65C816.AddressingMode addressingMode)
        {
            if (IndexesAre8Bit)
            {
                RegYL = PullByte();
                SetNZStatusFlagsFromValue(RegYL);
            }
            else
            {
                _regY = PullWord();
                SetNZStatusFlagsFromValue(_regY);
            }
        }
        #endregion
        #region PHB PHD PHK PHP PLB PLD PLP
        private void OpPhb(W65C816.AddressingMode addressingMode)
        {
            PushByte(_regDB);
            NextCycle();
        }

        private void OpPhd(W65C816.AddressingMode addressingMode)
        {
            PushWord(_regDP);
            NextCycle();
        }

        private void OpPhk(W65C816.AddressingMode addressingMode)
        {
            PushByte(_regPB);
            NextCycle();
        }

        private void OpPhp(W65C816.AddressingMode addressingMode)
        {
            PushByte((byte)_regSR);
            NextCycle();
        }

        private void OpPlb(W65C816.AddressingMode addressingMode)
        {
            _regDB = PullByte();
            SetNZStatusFlagsFromValue(_regDB);
            NextCycle();
            NextCycle();
        }

        private void OpPld(W65C816.AddressingMode addressingMode)
        {
            _regDP = PullWord();
            SetNZStatusFlagsFromValue(_regDP);
            NextCycle();
            NextCycle();
        }

        private void OpPlp(W65C816.AddressingMode addressingMode)
        {
            byte flags = PullByte();
            if(_flagE)
            {
                // M and X flags cannot be set in emulation mode
                flags |= 0b00110000;
            }
            _regSR = (StatusFlags)flags;

        }
        #endregion
        #region STP WAI
        private void OpStp(W65C816.AddressingMode addressingMode)
        {
            NextCycle();
            _stopped = true;
            StopThread();
        }

        private void OpWai(W65C816.AddressingMode addressingMode)
        {
            NextCycle();
            _waiting = true;
        }
        #endregion
        #region TAX TAY TSX TXA TXS TXY TYA TYX
        private void OpTax(W65C816.AddressingMode addressingMode)
        {
            NextCycle();
            if (IndexesAre8Bit)
            {
                RegXL = RegAL;
                SetNZStatusFlagsFromValue(RegXL);
            }
            else
            {
                _regX = _regA;
                SetNZStatusFlagsFromValue(_regX);
            }
        }

        private void OpTay(W65C816.AddressingMode addressingMode)
        {
            NextCycle();
            if (IndexesAre8Bit)
            {
                RegYL = RegAL;
                SetNZStatusFlagsFromValue(RegYL);
            }
            else
            {
                _regY = _regA;
                SetNZStatusFlagsFromValue(_regY);
            }
        }

        private void OpTsx(W65C816.AddressingMode addressingMode)
        {
            NextCycle();
            if (_flagE || IndexesAre8Bit)
            {
                RegXL = RegSL;
                SetNZStatusFlagsFromValue(RegXL);
            }
            else
            {
                _regX = _regSP;
                SetNZStatusFlagsFromValue(_regX);
            } 
        }

        private void OpTxa(W65C816.AddressingMode addressingMode)
        {
            NextCycle();
            if (AccumulatorIs8Bit)
            {
                RegAL = RegXL;
                SetNZStatusFlagsFromValue(RegAL);
            }
            else
            {
                _regA = _regX;
                SetNZStatusFlagsFromValue(_regA);
            }
        }

        private void OpTxs(W65C816.AddressingMode addressingMode)
        {
            NextCycle();
            if (_flagE)
            {
                RegSL = RegXL;
            }
            else if (IndexesAre8Bit)
            {
                _regSP = (Word)(0x0000 | RegXL);
            }
            else
            {
                _regSP = _regX;
            }
        }

        private void OpTxy(W65C816.AddressingMode addressingMode)
        {
            NextCycle();
            if (IndexesAre8Bit)
            {
                RegYL = RegXL;
                SetNZStatusFlagsFromValue(RegYL);
            }
            else
            {
                _regY = _regX;
                SetNZStatusFlagsFromValue(_regY);
            }
        }

        private void OpTya(W65C816.AddressingMode addressingMode) 
        { 
            NextCycle();
            if (IndexesAre8Bit)
            {
                RegAL = RegYL;
                SetNZStatusFlagsFromValue(RegAL);
            }
            else
            {
                _regA = _regY;
                SetNZStatusFlagsFromValue(_regA);
            }
        }

        private void OpTyx(W65C816.AddressingMode addressingMode)
        {
            NextCycle();
            if (IndexesAre8Bit)
            {
                RegXL = RegYL;
                SetNZStatusFlagsFromValue(RegXL);
            }
            else
            {
                _regX = _regY;
                SetNZStatusFlagsFromValue(_regX);
            }
        }
        #endregion
        #region TCD TCS TDC TSC
        private void OpTcd(W65C816.AddressingMode addressingMode)
        {
            NextCycle();
            _regDP = _regA;
            SetNZStatusFlagsFromValue(_regDP);
        }

        private void OpTcs(W65C816.AddressingMode addressingMode)
        {
            NextCycle();
            if (_flagE)
            {
                RegSL = RegAL;
                RegSH = 0x01;
            }
            else
            {
                _regSP = _regA;
            }
        }

        private void OpTdc(W65C816.AddressingMode addressingMode)
        {
            NextCycle();
            _regA = _regDP;
            SetNZStatusFlagsFromValue(_regA);
        }

        private void OpTsc(W65C816.AddressingMode addressingMode)
        {
            NextCycle();
            _regA = _regSP;
            SetNZStatusFlagsFromValue(_regA);
        }
        #endregion
        private void OpXba(W65C816.AddressingMode addressingMode)
        {
            NextCycle();
            _regA = Swap(_regA);
        }

        private void OpXce(W65C816.AddressingMode addressingMode) 
        { 
            NextCycle();
            bool carry = ReadStatusFlag(StatusFlags.C);
            SetStatusFlag(StatusFlags.C, _flagE);
            SetEmulationMode(carry);
            if (_verbose) 
            {
                Console.WriteLine();
                Console.Write($"Emulation flag now {_flagE}");
            }
        }

        private void BranchTo(Addr destination)
        {
            destination &= 0xffff;
            NextCycle();
            if (_flagE && HighByte((Word)destination) != HighByte(_regPC))
            {
                NextCycle();
            }
            _regPC = (Word)destination;
        }

        // Yes, this is going to spam stdout, but it's easier to just treat this as
        // an opcode being called over and over again rather than a special opcode
        // that takes a variable number of cycles to complete!
        private void CopyMemory()
        {
            byte destination = ReadByte();
            _regDB = destination;
            byte source = ReadByte();
            if (_verbose) Console.Write($"${source:x2}, ${destination:x2} (${_regA} bytes left)");
            WriteByte(ReadByte(Address(source, _regX)), Address(destination, _regY));
            NextCycle();
            NextCycle();
            if (--_regA != 0xffff) _regPC -= 3;
        }

        #endregion
        private void Reset()
        {
            _cycles = 0;
            _flagE = true;
            _regPB = 0x00;
            _regDB = 0x00;
            _regDP = 0x0000;
            _regSP = 0x0100;
            _regSR = (StatusFlags)0x34;
            _stopped = false;
            _waiting = false;
            _interruptingMaskable = false;
            _resetting = false;
            
            if (_verbose) Console.WriteLine("RESET");
            LoadInterruptVector(W65C816.Vector.Reset);
        }

        private void Interrupt(InterruptType source)
        {
            if (source == InterruptType.Reset)
            {
                Reset();
            }
            else
            {
                Word addressToPush = (source == InterruptType.BRK || source == InterruptType.COP) ? (Word)(_regPC + 1) : _regPC;
                NextCycle();
                NextCycle();
                if (!_flagE) PushByte(_regPB);
                PushWord(addressToPush);
                if (_flagE && source == InterruptType.BRK)
                {
                    PushByte((byte)(_regSR | StatusFlags.X));
                }
                else
                {
                    PushByte((byte)(_regSR));
                }
                SetStatusFlag(StatusFlags.I, true);
                SetStatusFlag(StatusFlags.D, false);
                W65C816.Vector vector;
                if (_flagE)
                {
                    vector = source switch
                    {
                        InterruptType.BRK => W65C816.Vector.EmulationIRQ,
                        InterruptType.COP => W65C816.Vector.EmulationCOP,
                        InterruptType.IRQ => W65C816.Vector.EmulationIRQ,
                        InterruptType.NMI => W65C816.Vector.EmulationNMI,
                        _ => throw new NotImplementedException(),
                    };
                }
                else
                {
                    vector = source switch
                    {
                        InterruptType.BRK => W65C816.Vector.NativeBRK,
                        InterruptType.COP => W65C816.Vector.NativeCOP,
                        InterruptType.IRQ => W65C816.Vector.NativeIRQ,
                        InterruptType.NMI => W65C816.Vector.NativeNMI,
                        _ => throw new NotImplementedException(),
                    };
                }
                LoadInterruptVector(vector);
            }
        }

        public void ExecuteOperation()
        {
            if (_runThread == null || !_threadRunning) 
            {
                NextOperation(); 
            }
            else throw new InvalidOperationException("Cannot advance operation manually while thread is running.");
        }

        private void NextOperation()
        {
            if (_resetting)
            {
                Reset();
            }
            else if (!_stopped)
            {
                int oldCycles = _cycles;
                if (_interruptingNonMaskable)
                {
                    _waiting = false;
                    Interrupt(InterruptType.NMI);
                }
                else if (_interruptingMaskable)
                {
                    if (!ReadStatusFlag(StatusFlags.I))
                    {
                        _waiting = false;
                        Interrupt(InterruptType.IRQ);
                    }
                    else if (_waiting)
                    {
                        _waiting = false;
                    }
                }
                else if (!_waiting)
                {
                    _regIR = ReadByte();
                    (W65C816.OpCode o, W65C816.AddressingMode m) = W65C816.OpCodeLookup(_regIR);
                    _lastInstruction.Clear();
                    _lastInstruction.Append(o.ToString() + " ");
                    DoOperation operation = GetDoOperation(o);
                    operation(m);
                    if (_verbose) Console.WriteLine();
                }
            }
            else if (_verbose) Console.WriteLine("STOPPED, please reset.");
        }
        private DoOperation GetDoOperation(W65C816.OpCode opCode)
        {
            return opCode switch
            {
                W65C816.OpCode.ADC => OpAdc,
                W65C816.OpCode.AND => OpAnd,
                W65C816.OpCode.ASL => OpAsl,
                W65C816.OpCode.BCC => OpBcc,
                W65C816.OpCode.BCS => OpBcs,
                W65C816.OpCode.BEQ => OpBeq,
                W65C816.OpCode.BMI => OpBmi,
                W65C816.OpCode.BNE => OpBne,
                W65C816.OpCode.BPL => OpBpl,
                W65C816.OpCode.BRK => OpBrk,
                W65C816.OpCode.BRL => OpBrl,
                W65C816.OpCode.BVC => OpBvc,
                W65C816.OpCode.BVS => OpBvs,
                W65C816.OpCode.CLC => OpClc,
                W65C816.OpCode.CLD => OpCld,
                W65C816.OpCode.CLI => OpCli,
                W65C816.OpCode.CLV => OpClv,
                W65C816.OpCode.CMP => OpCmp,
                W65C816.OpCode.CPX => OpCpx,
                W65C816.OpCode.CPY => OpCpy,
                W65C816.OpCode.DEC => OpDec,
                W65C816.OpCode.DEX => OpDex,
                W65C816.OpCode.DEY => OpDey,
                W65C816.OpCode.EOR => OpEor,
                W65C816.OpCode.INC => OpInc,
                W65C816.OpCode.INX => OpInx,
                W65C816.OpCode.INY => OpIny,
                W65C816.OpCode.JMP => OpJmp,
                W65C816.OpCode.JSL => OpJsl,
                W65C816.OpCode.JSR => OpJsr,
                W65C816.OpCode.LDA => OpLda,
                W65C816.OpCode.LDX => OpLdx,
                W65C816.OpCode.LDY => OpLdy,
                W65C816.OpCode.LSR => OpLsr,
                W65C816.OpCode.NOP => OpNop,
                W65C816.OpCode.ORA => OpOra,
                W65C816.OpCode.PEA => OpPea,
                W65C816.OpCode.PEI => OpPei,
                W65C816.OpCode.PER => OpPer,
                W65C816.OpCode.PHA => OpPha,
                W65C816.OpCode.PHB => OpPhb,
                W65C816.OpCode.PHD => OpPhd,
                W65C816.OpCode.PHK => OpPhk,
                W65C816.OpCode.PHP => OpPhp,
                W65C816.OpCode.PHX => OpPhx,
                W65C816.OpCode.PHY => OpPhy,
                W65C816.OpCode.PLA => OpPla,
                W65C816.OpCode.PLB => OpPlb,
                W65C816.OpCode.PLD => OpPld,
                W65C816.OpCode.PLP => OpPlp,
                W65C816.OpCode.PLX => OpPlx,
                W65C816.OpCode.PLY => OpPly,
                W65C816.OpCode.REP => OpRep,
                W65C816.OpCode.ROL => OpRol,
                W65C816.OpCode.ROR => OpRor,
                W65C816.OpCode.RTI => OpRti,
                W65C816.OpCode.RTL => OpRtl,
                W65C816.OpCode.RTS => OpRts,
                W65C816.OpCode.SBC => OpSbc,
                W65C816.OpCode.SEP => OpSep,
                W65C816.OpCode.SEC => OpSec,
                W65C816.OpCode.SED => OpSed,
                W65C816.OpCode.SEI => OpSei,
                W65C816.OpCode.STA => OpSta,
                W65C816.OpCode.STP => OpStp,
                W65C816.OpCode.STX => OpStx,
                W65C816.OpCode.STY => OpSty,
                W65C816.OpCode.STZ => OpStz,
                W65C816.OpCode.TAX => OpTax,
                W65C816.OpCode.TAY => OpTay,
                W65C816.OpCode.TCD => OpTcd,
                W65C816.OpCode.TCS => OpTcs,
                W65C816.OpCode.TDC => OpTdc,
                W65C816.OpCode.TRB => OpTrb,
                W65C816.OpCode.TSB => OpTsb,
                W65C816.OpCode.TSC => OpTsc,
                W65C816.OpCode.TSX => OpTsx,
                W65C816.OpCode.TXA => OpTxa,
                W65C816.OpCode.TXS => OpTxs,
                W65C816.OpCode.TXY => OpTxy,
                W65C816.OpCode.TYA => OpTya,
                W65C816.OpCode.TYX => OpTyx,
                W65C816.OpCode.WAI => OpWai,
                W65C816.OpCode.WDM => OpWdm,
                W65C816.OpCode.XBA => OpXba,
                W65C816.OpCode.XCE => OpXce,
                W65C816.OpCode.BIT => OpBit,
                W65C816.OpCode.BRA => OpBra,
                W65C816.OpCode.COP => OpCop,
                W65C816.OpCode.MVN => OpMvn,
                W65C816.OpCode.MVP => OpMvp,
                _ => throw new NotImplementedException(),
            };
        }

        public Status GetStatus()
        {
            Status result = new()
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

        public class Status
        {
            public int Cycles;
            public UInt16 A, X, Y, DP, SP, PC;
            public Byte DB, PB;
            public bool FlagN, FlagV, FlagM, FlagX, FlagD, FlagI, FlagZ, FlagC, FlagE;

            public override string ToString()
            {
                string flags = $"{(FlagN ? "N" : "-")}{(FlagV ? "V" : "-")}{(FlagE ? "." : (FlagM ? "M" : "-"))}{(FlagX ? (FlagE ? "B" : "X") : "-")}{(FlagD ? "D" : "-")}{(FlagI ? "I" : "-")}{(FlagZ ? "Z" : "-")}{(FlagC ? "C" : "-")} {(FlagE ? "E" : "-")}";
                return $"Cycles: {Cycles}\nA:  0x{A:x4}\nX:  0x{X:x4}\nY:  0x{Y:x4}\nDP: 0x{DP:x4}\nSP: 0x{SP:x4}\nDB:   0x{DB:x2}\nPB: 0x{PB:x2} PC: 0x{PC:x4}\nFlags: {flags}";
            }
        }
    }
}
