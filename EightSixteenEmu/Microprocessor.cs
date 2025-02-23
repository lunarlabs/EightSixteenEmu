using System.Net;
using System.Runtime.ConstrainedExecution;
using System.Runtime.Intrinsics.Arm;
using System.Xml.Serialization;
using static System.Net.WebRequestMethods;
using Addr = System.UInt32;
using Word = System.UInt16;

namespace EightSixteenEmu
{
    /// <summary>
    /// A class representing the W65C816 microprocessor.
    /// </summary>
    /// <remarks>
    /// This class is a work in progress. Most opcodes are not yet implemented.
    /// Steps are by operation, not by cycle. This is a simplification for now.
    /// The ABORT signal is not implemented. Since very few actual designs use it, it has been deemed unnecessary.
    /// </remarks>
    public class Microprocessor
    {
        private int _cycles;
        private bool _resetting;
        private bool _interruptingMaskable;
        private bool _interruptingNonMaskable;
        private bool _aborting;
        private bool _stopped;
        private bool _waiting;
        private bool _operationComplete;
        private bool _verbose;
        private readonly SortedDictionary<(Addr start, Addr end), IMappableDevice> _devices;

        public bool Verbose
        {
            get => _verbose;
#if !DEBUG
            set => verbose = value;
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
        internal enum StatusFlags : byte
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

        private byte _regAH // high byte of accumulator
        {
            get => HighByte(_regA);
            set => _regA = Join(LowByte(_regA), value);
        }
        private byte _regAL // low byte of accumulator
        {
            get => LowByte(_regA);
            set => _regA = Join(value, HighByte(_regA));
        }
        private byte _regXH // high byte of X register
        {
            get => HighByte(_regX);
            set => _regX = Join(LowByte(_regX), value);
        }
        private byte _regXL // low byte of X register
        {
            get => LowByte(_regX);
            set => _regX = Join(value, HighByte(_regX));
        }
        private byte _regYH // high byte of Y register
        {
            get => HighByte(_regY);
            set => _regY = Join(LowByte(_regY), value);
        }
        private byte _regYL // low byte of Y register
        {
            get => LowByte(_regY);
            set => _regY = Join(value, HighByte(_regY));
        }
        private byte _regSH // high byte of stack pointer
        {
            get => HighByte(_regSP);
            set => _regSP = Join(LowByte(_regSP), value);
        }
        private byte _regSL // low byte of stack pointer
        {
            get => LowByte(_regSP);
            set => _regSP = Join(value, HighByte(_regSP));
        }

        public bool FlagE { get => _flagE; }
        public bool FlagM { get => ReadStatusFlag(StatusFlags.M); }
        public bool FlagX { get => ReadStatusFlag(StatusFlags.X); }

        // non-accessible registers
        private byte _regIR; // instruction register
        private byte _regMD; // memory data register

        /// <summary>
        /// Creates a new instance of the W65C816 microprocessor.
        /// </summary>
        /// <param name="deviceList">
        /// A list of <c>IMappableDevice</c> to assign to the microprocessor's address space.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the base address of a device falls outside the 24-bit address space.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the address range of a device conflicts with an existing device.
        /// </exception>
        public Microprocessor(List<IMappableDevice> deviceList)
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
            _flagE = false;
            _regMD = 0x00;

            _cycles = 0;
            _resetting = true;
            _interruptingMaskable = false;
            _interruptingNonMaskable = false;
            _stopped = false;
#if DEBUG
            _verbose = true;
#endif

            _devices = new SortedDictionary<(Addr start, Addr end), IMappableDevice>();
            foreach (IMappableDevice newDevice in deviceList)
            {
                SortedDictionary<(Addr start, Addr end), IMappableDevice>.KeyCollection ranges = _devices.Keys;
                Addr top = newDevice.BaseAddress;
                Addr bottom = newDevice.BaseAddress + newDevice.Size - 1; // Corrected calculation
                if (bottom > 0xFFFFFF)
                {
                    throw new ArgumentOutOfRangeException($"Addresses for {newDevice.GetType()} fall outside the 24-bit address space.");
                }
                else
                {
                    foreach ((Addr s, Addr e) in ranges)
                    {
                        if (Math.Max(top, s) <= Math.Min(bottom, e)) // Corrected condition
                        {
                            throw new InvalidOperationException($"Addresses for {newDevice.GetType()} (${top:x6} - ${bottom:x6}) conflict with existing device at ${s:x6} - ${e:x6}");
                        }
                    }
                    _devices.Add((top, bottom), newDevice);
                }
            }
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

        // to avoid headaches when adding offsets to Words
        static Addr Address(byte bank, int address) 
        { 
            return Address(bank, (Word)address);
        }
        static Word Swap(Word w)
        {
            return (Word)((w >> 8) | (w << 8));
        }
        private Addr LongPC { get => Address(_regPB, _regPC); }

        #region Memory Access

        private IMappableDevice? GetDevice(Addr address)
        {
            IMappableDevice? result = null;
            SortedDictionary<(Addr start, Addr end), IMappableDevice>.KeyCollection ranges = _devices.Keys;
            foreach ((Addr s, Addr e) in ranges)
            {
                if ((address >= s && address <= e))
                {
                    result = _devices[(s, e)];
                }
            }
            return result;
        }

        private Word ReadValue(bool isByte, Addr address)
        {
            return isByte switch
            {
                false => ReadWord(address),
                true => ReadByte(address),
            };
        }

        private Word ReadImmediate(bool isByte)
        {
            Word result = isByte switch
            {
                false => ReadWord(),
                true => ReadByte(),
            };
            if (_verbose)
            {
                string arg = isByte ? $"${result:x2}" : $"${result:x4}";
                Console.WriteLine(arg);
            }
            return result;
        }

        private void WriteValue(Word value, bool isByte, Addr address)
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

        private byte ReadByte(Addr address)
        {
            _cycles++;
            IMappableDevice? device = GetDevice(address);
            if (device == null)
            {
                Console.WriteLine($"WARN: Attempted read from open bus address ${address:x6}");
            }
            else
            {
                _regMD = device[address];
            }
            return _regMD;
        }

        private Word ReadWord(Addr address, bool wrapping = false)
        {
            if (!wrapping) return Join(ReadByte(address), ReadByte(address + 1));
            else
            {
                byte b = BankOf(address);
                Word a = (Word)address;
                return Join(ReadByte(Address(b, a)), ReadByte(Address(b, (Word)(a + 1))));
            }
        }

        private Addr ReadAddr(Addr address, bool wrapping = false)
        {
            if (!wrapping) return Address(ReadByte(address + 2), ReadWord(address));
            else
            {
                byte b = BankOf(address);
                Word a = (Word)address;
                return Address(ReadByte(Address(b, (Word)(a + 2))), ReadWord(address, true));
            }
        }

        private byte ReadByte()
        {
            byte result = ReadByte(LongPC);
            _regPC += 1;
            return result;
        }

        private Word ReadWord()
        {
            Word result = ReadWord(LongPC, true);
            _regPC += 2;
            return result;
        }

        private Addr ReadAddr()
        {
            Addr result = ReadAddr(LongPC, true);
            _regPC += 3;
            return result;
        }

        private void WriteByte(byte value, Addr address)
        {
            _cycles++;
            if (_aborting == false)
            {
                _regMD = value;
                IMappableDevice? device = GetDevice(address);
                if (device == null)
                {
                    Console.WriteLine($"WARN: Attempted write to open bus address ${address:x6}");
                }
                else
                {
                    device[address] = _regMD;
                }
            }
        }

        private void WriteWord(Word value, Addr address)
        {
            WriteByte(LowByte(value), address);
            WriteByte(HighByte(value), address + 1);
        }

        private void PushByte(byte value)
        {
            WriteByte(value, _regSP--);
            if (_flagE)
            {
                _regSL = 0x01;
            }
        }

        private void PushWord(Word value)
        {
            PushByte(HighByte(value));
            PushByte(LowByte(value));
        }

        private byte PullByte()
        {
            byte result = ReadByte(++_regSP);
            if (_flagE)
            {
                _regSP = Join(LowByte(_regSP), 0x01);
            }
            return result;
        }

        private Word PullWord()
        {
            byte l = PullByte();
            byte h = PullByte();
            return Join(l, h);
        }

        #endregion

        private void SetStatusFlag(StatusFlags flag, bool value)
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
        private bool AccumulatorIs8Bit { get { return ReadStatusFlag(StatusFlags.M); } }
        private bool IndexesAre8Bit { get { return _flagE || ReadStatusFlag(StatusFlags.X); } }

        private bool ReadStatusFlag(StatusFlags flag)
        {
            return (_regSR & flag) != 0;
        }

        private void SetNZStatusFlagsFromValue(byte value)
        {
            SetStatusFlag(StatusFlags.N, (value & 0x80) != 0);
            SetStatusFlag(StatusFlags.Z, value == 0);
        }

        private void SetNZStatusFlagsFromValue(Word value)
        {
            SetStatusFlag(StatusFlags.N, (value & 0x8000) != 0);
            SetStatusFlag(StatusFlags.Z, value == 0);
        }

        private void SetEmulationMode(bool value)
        {
            if (value)
            {
                SetStatusFlag(StatusFlags.M | StatusFlags.X, true);
                _regX = (Word)LowByte(_regX);
                _regY = (Word)LowByte(_regY);
                _regSP = (Word)(0x0100 | LowByte(_regSP));
                _flagE = true;
            }
            else { _flagE = false; }
        }

        private Addr GetEffectiveAddress(W65C816.AddressingMode addressingMode)
        {
            Addr pointer;
            byte offsetU8;
            Word location;
            sbyte offsetS8;
            short offsetS16;
            switch (addressingMode)
            {
                case W65C816.AddressingMode.Immediate:
                    // WARN: Do the reads (and subsequent RegPC advances) in the operation
                    if (_verbose) Console.Write("#");
                    return LongPC;
                case W65C816.AddressingMode.Accumulator:
                    if (_verbose) Console.WriteLine("A");
                    return 0;
                case W65C816.AddressingMode.ProgramCounterRelative:
                    offsetS8 = (sbyte)(ReadByte());
                    if (_verbose) Console.WriteLine($"{offsetS8:+0,-#}");
                    return Address(_regPB, _regPC + offsetS8);
                case W65C816.AddressingMode.ProgramCounterRelativeLong:
                    offsetS16 = (short)(ReadWord());
                    if (_verbose) Console.WriteLine($"{offsetS16:+0,-#}");
                    return Address(_regPB, _regPC + offsetS16);
                case W65C816.AddressingMode.Implied:
                    return 0;
                case W65C816.AddressingMode.Stack:
                    return 0;
                case W65C816.AddressingMode.Direct:
                    offsetU8 = ReadByte();
                    if (_verbose) Console.WriteLine($"${offsetU8:x2}");
                    return Address(0, _regDP + offsetU8);
                case W65C816.AddressingMode.DirectIndexedWithX:
                    offsetU8 = ReadByte();
                    if (_verbose) Console.WriteLine($"${offsetU8:x2}, X");
                    if (_flagE && LowByte(_regDP) == 0)
                    {
                        return Address(0, Join((byte)(offsetU8 + (byte)_regX), HighByte(_regDP)));
                    }
                    else
                    {
                        return Address(0, _regDP + offsetU8 + (byte)_regX);
                    }
                case W65C816.AddressingMode.DirectIndexedWithY:
                    offsetU8 = ReadByte();
                    if (_verbose) Console.WriteLine($"${offsetU8:x2}, Y");
                    if (_flagE && LowByte(_regDP) == 0)
                    {
                        return Address(0, Join((byte)(offsetU8 + (byte)_regY), HighByte(_regDP)));
                    }
                    else
                    {
                        return Address(0, _regDP + offsetU8 + (byte)_regY);
                    }
                case W65C816.AddressingMode.DirectIndirect:
                    offsetU8 = ReadByte();
                    if (_verbose) Console.WriteLine($"(${offsetU8:x2})");
                    if (_flagE && LowByte(_regDP) == 0)
                    {
                        pointer = Address(0, Join((byte)(offsetU8), HighByte(_regDP)));
                    }
                    else
                    {
                        pointer = Address(0, _regDP + offsetU8);
                    }
                    return Address(_regDB, ReadWord(pointer));
                case W65C816.AddressingMode.DirectIndexedIndirect:
                    offsetU8 = ReadByte();
                    if (_verbose) Console.WriteLine($"(${offsetU8:x2}, X)");
                    if (_flagE && LowByte(_regDP) == 0)
                    {
                        pointer = Address(0, Join((byte)(offsetU8 + (byte)_regX), HighByte(_regDP)));
                    }
                    else
                    {
                        pointer = Address(0, _regDP + offsetU8 + (byte)_regX);
                    }
                    return Address(_regDB, ReadWord(pointer));
                case W65C816.AddressingMode.DirectIndirectIndexed:
                    offsetU8 = ReadByte();
                    if (_verbose) Console.WriteLine($"(${offsetU8:x2}), Y");
                    if (_flagE && LowByte(_regDP) == 0)
                    {
                        pointer = Address(0, Join((byte)(offsetU8), HighByte(_regDP)));
                    }
                    else
                    {
                        pointer = Address(0, _regDP + offsetU8);
                    }
                    return Address(_regDB, ReadWord(pointer + _regY));
                case W65C816.AddressingMode.DirectIndirectLong:
                    offsetU8 = ReadByte();
                    if (_verbose) Console.WriteLine($"[${offsetU8:x2}]");
                    return ReadAddr(Address(0, _regDP + offsetU8), true);
                case W65C816.AddressingMode.DirectIndirectLongIndexed:
                    offsetU8 = ReadByte();
                    if (_verbose) Console.WriteLine($"[${offsetU8:x2}], Y");
                    return ReadAddr(Address(0, _regDP + offsetU8), true) + _regY;
                case W65C816.AddressingMode.Absolute:
                    // WARN: Special case for JMP and JSR -- replace RegDB with RegPB
                    location = ReadWord();
                    if (_verbose) Console.WriteLine($"${location:x4}");
                    return Address(_regDB, location);
                case W65C816.AddressingMode.AbsoluteIndexedWithX:
                    location = ReadWord();
                    if (_verbose) Console.WriteLine($"${location:x4}, X");
                    return Address(_regDB, location + _regX);
                case W65C816.AddressingMode.AbsoluteIndexedWithY:
                    location = ReadWord();
                    if (_verbose) Console.WriteLine($"${location:x4}, Y");
                    return Address(_regDB, location + _regY);
                case W65C816.AddressingMode.AbsoluteLong:
                    pointer = ReadAddr();
                    if (_verbose) Console.WriteLine($"{pointer:x6}");
                    return pointer;
                case W65C816.AddressingMode.AbsoluteLongIndexed:
                    pointer = ReadAddr();
                    if (_verbose) Console.WriteLine($"{pointer:x6}, X");
                    return pointer + _regX;
                case W65C816.AddressingMode.StackRelative:
                    offsetU8 = ReadByte();
                    if (_verbose) Console.WriteLine($"{offsetU8:x2}, S");
                    return Address(0, offsetU8 + _regSP);
                case W65C816.AddressingMode.StackRelativeIndirectIndexed:
                    offsetU8 = ReadByte();
                    if (_verbose) Console.WriteLine($"({offsetU8:x2}, S), Y");
                    pointer = Address(0, offsetU8 + _regSP);
                    return Address(_regDB, ReadWord(pointer + _regY));
                case W65C816.AddressingMode.AbsoluteIndirect:
                    location = ReadWord();
                    if (_verbose) Console.WriteLine($"(${location:x4})");
                    pointer = Address(0, location);
                    return Address(_regPB, ReadWord(pointer));
                case W65C816.AddressingMode.AbsoluteIndexedIndirect:
                    location = ReadWord();
                    if (_verbose) Console.WriteLine($"(${location:x4}, X)");
                    pointer = Address(_regPB, location);
                    return Address(_regPB, ReadWord(pointer) + _regX);
                case W65C816.AddressingMode.BlockMove:
                    byte destination = ReadByte();
                    byte source = ReadByte();
                    if (_verbose) Console.WriteLine($"${source:x2}, ${destination:x2}");
                    // WARN: Decode source and destination banks in the operation function
                    return Address(0, Join(destination, source));
                default:
                    return 0;
            }
        }

        private void LoadInterruptVector(W65C816.Vector vector)
        {
            _regPC = ReadWord((Addr)vector);
            _regPB = 0x00;
            _operationComplete = true;
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
                _regAL = (byte)al;
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
            _cycles++;
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
                int al = _regAL + ~(byte)subtrahend + (byte)(ReadStatusFlag(StatusFlags.C) ? 0 : 1);
                if (ReadStatusFlag(StatusFlags.D))
                {
                    if (((al) & 0x0f) > 0x09) al += 0x06;
                    if (((al) & 0xf0) > 0x90) al += 0x60;
                }
                SetStatusFlag(StatusFlags.C, (al & 0x100u) != 0);
                SetStatusFlag(StatusFlags.V, (Word)((~(_regA ^ subtrahend)) & (_regA ^ al) & 0x80) != 0);
                SetNZStatusFlagsFromValue((byte)al);
                _regAL = (byte)al;
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
        private void OpCmp(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpCpx(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpCpy(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region DEA DEC DEX DEY INA INC INX INY
        private void OpDec(W65C816.AddressingMode addressingMode)
        {
            if (addressingMode == W65C816.AddressingMode.Accumulator)
            {
                if (AccumulatorIs8Bit)
                {
                    byte al = _regAL;
                    SetNZStatusFlagsFromValue(--al);
                    _regAL = al;
                }
                else
                {
                    Word a = _regA;
                    SetNZStatusFlagsFromValue(--a);
                    _regA = a;
                }
                _cycles++;
            }
            else
            {
                Addr address = GetEffectiveAddress(addressingMode);
                Word value = ReadValue(AccumulatorIs8Bit, address);
                value -= 1;
                _cycles++;
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
                SetNZStatusFlagsFromValue(--_regXL);
            }
            else
            {
                SetNZStatusFlagsFromValue(--_regX);
            }
            _cycles++;
        }

        private void OpDey(W65C816.AddressingMode addressingMode)
        {
            if (IndexesAre8Bit)
            {
                SetNZStatusFlagsFromValue(--_regYL);
            }
            else
            {
                SetNZStatusFlagsFromValue(--_regY);
            }
            _cycles++;
        }

        private void OpInc(W65C816.AddressingMode addressingMode)
        {
            if (addressingMode == W65C816.AddressingMode.Accumulator)
            {
                if (AccumulatorIs8Bit)
                {
                    byte al = _regAL;
                    SetNZStatusFlagsFromValue(++al);
                    _regAL = al;
                }
                else
                {
                    Word a = _regA;
                    SetNZStatusFlagsFromValue(++a);
                    _regA = a;
                }
                _cycles++;
            }
            else
            {
                Addr address = GetEffectiveAddress(addressingMode);
                Word value = ReadValue(AccumulatorIs8Bit, address);
                value += 1;
                _cycles++;
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
                SetNZStatusFlagsFromValue(++_regXL);
            }
            else
            {
                SetNZStatusFlagsFromValue(++_regX);
            }
            _cycles++;
        }

        private void OpIny(W65C816.AddressingMode addressingMode)
        {
            if (IndexesAre8Bit)
            {
                SetNZStatusFlagsFromValue(++_regYL);
            }
            else
            {
                SetNZStatusFlagsFromValue(++_regY);
            }
            _cycles++;
        }
        #endregion
        #region AND EOR ORA
        private void OpAnd(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpEor(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpOra(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion

        private void OpBit(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        #region TRB TSB
        private void OpTrb(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpTsb(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region ASL LSR ROL ROR
        private void OpAsl(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpLsr(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpRol(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpRor(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region BCC BCS BEQ BMI BNE BPL BRA BVC BVS
        private void OpBcc(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpBcs(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpBeq(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpBmi(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpBne(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpBpl(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpBra(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpBvc(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpBvs(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion

        private void OpBrl(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        #region JMP JSL JSR
        private void OpJmp(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpJsl(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpJsr(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region RTL RTS
        private void OpRtl(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpRts(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
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
                _regAL = (byte)value;
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
                _regXL = (byte)value;
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
                _regYL = (byte)value;
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
                WriteByte(_regAL, address);
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
                WriteByte(_regXL, address);
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
                WriteByte(_regYL, address);
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
        private void OpMvn(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpMvp(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region NOP WDM
        private void OpNop(W65C816.AddressingMode addressingMode) 
        {
            _cycles++;
        }

        private void OpWdm(W65C816.AddressingMode addressingMode)
        {
            _regPC++;
            _cycles++;
        }
        #endregion
        #region PEA PEI PER
        private void OpPea(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpPei(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpPer(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region PHA PHX PHY PLA PLX PLY
        private void OpPha(W65C816.AddressingMode addressingMode)
        {
            if (AccumulatorIs8Bit)
            {
                PushByte(_regAL);
            }
            else
            {
                PushWord(_regA);
            }
            _cycles++;
        }

        private void OpPhx(W65C816.AddressingMode addressingMode)
        {
            if (IndexesAre8Bit)
            {
                PushByte(_regXL);
            }
            else
            {
                PushWord(_regX);
            }
            _cycles++;
        }

        private void OpPhy(W65C816.AddressingMode addressingMode)
        {
            if (IndexesAre8Bit)
            {
                PushByte(_regYL);
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
                _regAL = PullByte();
                SetNZStatusFlagsFromValue(_regAL);
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
                _regXL = PullByte();
                SetNZStatusFlagsFromValue(_regXL);
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
                _regYL = PullByte();
                SetNZStatusFlagsFromValue(_regYL);
            }
            else
            {
                _regY = PullWord();
                SetNZStatusFlagsFromValue(_regY);
            }
        }
        #endregion
        #region PHB PHD PHK PHP PLB PLD PLP
        private void OpPhb(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpPhd(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpPhk(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpPhp(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpPlb(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpPld(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpPlp(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region STP WAI
        private void OpStp(W65C816.AddressingMode addressingMode)
        {
            _cycles++;
            _stopped = true;
        }

        private void OpWai(W65C816.AddressingMode addressingMode)
        {
            _cycles++;
            _waiting = true;
        }
        #endregion
        #region TAX TAY TSX TXA TXS TXY TYA TYX
        private void OpTax(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        
        private void OpTay(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpTsx(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpTxa(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpTxs(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpTxy(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpTya(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpTyx(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region TCD TCS TDC TSC
        private void OpTcd(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpTcs(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpTdc(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpTsc(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        private void OpXba(W65C816.AddressingMode addressingMode)
        {
            _cycles++;
            _regA = Swap(_regA);
        }

        private void OpXce(W65C816.AddressingMode addressingMode) 
        { 
            _cycles++;
            bool carry = ReadStatusFlag(StatusFlags.C);
            SetStatusFlag(StatusFlags.C, _flagE);
            SetEmulationMode(carry);
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
                _cycles++;
                _cycles++;
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
            if (_resetting)
            {
                Reset();
            }
            else if (!_stopped)
            {
                int oldCycles = _cycles;
                _operationComplete = false;
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
                    if (_verbose) Console.Write(o.ToString() + " ");
                    DoOperation operation = o switch
                    #region operation switch
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
                    #endregion
                    operation(m);
                    if (!_stopped) _operationComplete = true;
#if DEBUG
                    int cyclesThisOp = _cycles - oldCycles;
                    Console.WriteLine($"Cycles: {cyclesThisOp}");

                    string flags = FormatStatusFlags();

                    Console.WriteLine($"A: 0x{_regA:x4}\n X: 0x{_regX:x4}\n Y: 0x{_regY:x4}\n DP: 0x{_regDP:x4}\n SP: 0x{_regSP:x4}\n DB: 0x{_regDP:x2}");
                    Console.WriteLine($"PB: 0x{_regPB:x2} PC: 0x{_regPC:x4}");
                    Console.WriteLine($"Flags: {flags}");
#endif
                }
            }
            else if (_verbose) Console.WriteLine("STOPPED, please reset.");
        }
        public string DeviceList()
        {
            string result = "";
            Addr lastUsedAddress = 0xffffffff;
            foreach (var device in _devices)
            {
                (Addr start, Addr end) = device.Key;
                if (start != lastUsedAddress + 1)
                {
                    result += $"${lastUsedAddress + 1:x6} - ${start - 1:x6}: Unused\n";
                }
                result += $"${start:x6} - ${end:x6}: {device.Value}\n"; // Corrected line
                lastUsedAddress = end;
            }
            return result;
        }
        private string FormatStatusFlags()
        {
            string flags = "";
            flags += ReadStatusFlag(StatusFlags.N) ? "N" : "-";
            flags += ReadStatusFlag(StatusFlags.V) ? "V" : "-";
            if (_flagE)
            {
                flags += ".";
                flags += ReadStatusFlag(StatusFlags.X) ? "B" : "-";
            }
            else
            {
                flags += ReadStatusFlag(StatusFlags.M) ? "M" : "-";
                flags += ReadStatusFlag(StatusFlags.X) ? "X" : "-";
            }
            flags += ReadStatusFlag(StatusFlags.D) ? "D" : "-";
            flags += ReadStatusFlag(StatusFlags.I) ? "I" : "-";
            flags += ReadStatusFlag(StatusFlags.Z) ? "Z" : "-";
            flags += ReadStatusFlag(StatusFlags.C) ? "C" : "-";
            flags += " ";
            flags += _flagE ? "E" : "-";
            return flags;
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
                string flags = "";
                flags += FlagN ? "N" : "-";
                flags += FlagV ? "V" : "-";
                if (FlagE)
                {
                    flags += ".";
                    flags += FlagX ? "B" : "-";
                }
                else
                {
                    flags += FlagM ? "M" : "-";
                    flags += FlagX ? "X" : "-";
                }
                flags += FlagD ? "D" : "-";
                flags += FlagI ? "I" : "-";
                flags += FlagZ ? "Z" : "-";
                flags += FlagC ? "C" : "-";
                flags += " ";
                flags += FlagE ? "E" : "-";
                return $"Cycles: {Cycles}\nA:  0x{A:x4}\nX:  0x{X:x4}\nY:  0x{Y:x4}\nDP: 0x{DP:x4}\nSP: 0x{SP:x4}\nDB:   0x{DB:x2}\nPB: 0x{PB:x2} PC: 0x{PC:x4}\nFlags: {flags}";
            }
        }
    }
}
