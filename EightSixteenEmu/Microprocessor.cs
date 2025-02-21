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
        private int cycles;
        private bool resetting;
        private bool interruptingMaskable;
        private bool interruptingNonMaskable;
        private bool aborting;
        private bool stopped;
        private bool waiting;
        private bool operationComplete;
        private bool breakActive;
        private bool verbose;
        private readonly SortedDictionary<(Addr start, Addr end), IMappableDevice> devices;
        private readonly Clock clock;

        public bool Verbose
        {
            get => verbose;
#if !DEBUG
            set => verbose = value;
#endif
        }

        public int Cycles
        {
            get => cycles;
        }
        public bool Stopped { get => stopped; }

        private delegate Task DoOperation(W65C816.AddressingMode mode);

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

        // accessible registers
        private Word RegA;  // accumulator
        private Word RegX;  // index register X
        private Word RegY;  // index register Y
        private Word RegDP; // direct page pointer
        private Word RegSP; // stack pointer
        private byte RegDB; // data bank
        private byte RegPB; // program bank
        private Word RegPC; // program counter
        private StatusFlags RegSR;  // status flags register
        private bool FlagE; // emulation flag

        private byte RegAH // high byte of accumulator
        {
            get => HighByte(RegA);
            set => RegA = Join(LowByte(RegA), value);
        }
        private byte RegAL // low byte of accumulator
        {
            get => LowByte(RegA);
            set => RegA = Join(value, HighByte(RegA));
        }
        private byte RegXH // high byte of X register
        {
            get => HighByte(RegX);
            set => RegX = Join(LowByte(RegX), value);
        }
        private byte RegXL // low byte of X register
        {
            get => LowByte(RegX);
            set => RegX = Join(value, HighByte(RegX));
        }
        private byte RegYH // high byte of Y register
        {
            get => HighByte(RegY);
            set => RegY = Join(LowByte(RegY), value);
        }
        private byte RegYL // low byte of Y register
        {
            get => LowByte(RegY);
            set => RegY = Join(value, HighByte(RegY));
        }
        private byte RegSH // high byte of stack pointer
        {
            get => HighByte(RegSP);
            set => RegSP = Join(LowByte(RegSP), value);
        }
        private byte RegSL // low byte of stack pointer
        {
            get => LowByte(RegSP);
            set => RegSP = Join(value, HighByte(RegSP));
        }

        // non-accessible registers
        private byte RegIR; // instruction register
        private byte RegMD; // memory data register

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
        public Microprocessor(List<IMappableDevice> deviceList, Clock clock)
        {
            this.clock = clock;
            this.clock.Tick += OnClockTick;

            RegA = 0x0000;
            RegX = 0x0000;
            RegY = 0x0000;
            RegDP = 0x0000;
            RegSP = 0x0100;
            RegDB = 0x00;
            RegPB = 0x00;
            RegPC = 0x0000;
            RegSR = (StatusFlags)0x34;
            FlagE = false;
            RegMD = 0x00;

            cycles = 0;
            resetting = true;
            interruptingMaskable = false;
            interruptingNonMaskable = false;
            stopped = false;
            breakActive = false;
#if DEBUG
            verbose = true;
#endif

            devices = new SortedDictionary<(Addr start, Addr end), IMappableDevice>();
            foreach (IMappableDevice newDevice in deviceList)
            {
                SortedDictionary<(Addr start, Addr end), IMappableDevice>.KeyCollection ranges = devices.Keys;
                Addr top = newDevice.BaseAddress;
                Addr bottom = newDevice.BaseAddress + newDevice.Size;
                if (bottom > 0xFFFFFF)
                {
                    throw new ArgumentOutOfRangeException($"Addresses for {newDevice.GetType()} fall outside the 24-bit address space.");
                }
                else
                {
                    foreach ((Addr s, Addr e) in ranges)
                    {
                        if (Math.Min(top, s) - Math.Min(bottom, e) > 0)
                        {
                            throw new InvalidOperationException($"Addresses for {newDevice.GetType()} (${top:x6} - ${bottom:x6}) conflict with existing device at ${s:x6} - ${e:x6}");
                        }
                    }
                    devices.Add((top, bottom), newDevice);
                }
            }
        }
        
        private TaskCompletionSource<bool> tickTcs = new TaskCompletionSource<bool>();

        private void OnResetSignal(object? sender, EventArgs e)
        {
            resetting = true;
        }
        private void OnClockTick(object? sender, EventArgs e)
        {
            tickTcs.SetResult(!resetting);
            if (operationComplete)
            {
                ExecuteOperationAsync().ConfigureAwait(false);
            }
        }

        private async Task WaitForTickAsync()
        {
            bool continueOp = await tickTcs.Task;
            tickTcs = new TaskCompletionSource<bool>();
            if (!continueOp)
            {
                cycles++;
            }
            else
            {
                Reset();
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
        private Addr LongPC { get => Address(RegPB, RegPC); }

        #region Memory Access

        private IMappableDevice? GetDevice(Addr address)
        {
            IMappableDevice? result = null;
            SortedDictionary<(Addr start, Addr end), IMappableDevice>.KeyCollection ranges = devices.Keys;
            foreach ((Addr s, Addr e) in ranges)
            {
                if ((address >= s && address <= e))
                {
                    result = devices[(s, e)];
                }
            }
            return result;
        }

        private async Task<Word> ReadValue(bool isByte, Addr address)
        {
            return isByte switch
            {
                false => await ReadWord(address),
                true => await ReadByte(address),
            };
        }

        private async Task <Word> ReadImmediate(bool isByte)
        {
            Word result = isByte switch
            {
                false => await ReadWord(),
                true => await ReadByte(),
            };
            if (verbose)
            {
                string arg = isByte ? $"${result:x2}" : $"${result:x4}";
                Console.WriteLine(arg);
            }
            return result;
        }

        private async Task WriteValue(Word value, bool isByte, Addr address)
        {
            if (!isByte)
            {
                await WriteWord(value, address);
            } 
            else
            {
                await WriteByte((byte)value, address);
            }
        }

        private async Task<byte> ReadByte(Addr address)
        {
            await WaitForTickAsync();
            IMappableDevice? device = GetDevice(address);
            if (device == null)
            {
                Console.WriteLine($"WARN: Attempted read from open bus address ${address:x6}");
            }
            else
            {
                RegMD = device[address];
            }
            return RegMD;
        }

        private async Task<Word> ReadWord(Addr address, bool wrapping = false)
        {
            if (!wrapping) return Join(await ReadByte(address), await ReadByte(address + 1));
            else
            {
                byte b = BankOf(address);
                Word a = (Word)address;
                return Join(await ReadByte(Address(b, a)), await ReadByte(Address(b, (Word)(a + 1))));
            }
        }

        private async Task<Addr> ReadAddr(Addr address, bool wrapping = false)
        {
            if (!wrapping) return Address(await ReadByte(address + 2), await ReadWord(address));
            else
            {
                byte b = BankOf(address);
                Word a = (Word)address;
                return Address(await ReadByte(Address(b, (Word)(a + 2))), await ReadWord(address, true));
            }
        }

        private async Task<byte> ReadByte()
        {
            byte result = await ReadByte(LongPC);
            RegPC += 1;
            return result;
        }

        private async Task<Word> ReadWord()
        {
            Word result = await ReadWord(LongPC, true);
            RegPC += 2;
            return result;
        }

        private async Task<Addr> ReadAddr()
        {
            Addr result = await ReadAddr(LongPC, true);
            RegPC += 3;
            return result;
        }

        private async Task WriteByte(byte value, Addr address)
        {
            await WaitForTickAsync();
            if (aborting == false)
            {
                RegMD = value;
                IMappableDevice? device = GetDevice(address);
                if (device == null)
                {
                    Console.WriteLine($"WARN: Attempted write to open bus address ${address:x6}");
                }
                else
                {
                    device[address] = RegMD;
                }
            }
        }

        private async Task WriteWord(Word value, Addr address)
        {
            await WriteByte(LowByte(value), address);
            await WriteByte(HighByte(value), address + 1);
        }

        private async Task PushByte(byte value)
        {
            await WriteByte(value, RegSP--);
            if (FlagE)
            {
                RegSL = 0x01;
            }
        }

        private async Task PushWord(Word value)
        {
            await PushByte(HighByte(value));
            await PushByte(LowByte(value));
        }

        private async Task<byte> PullByte()
        {
            byte result = await ReadByte(++RegSP);
            if (FlagE)
            {
                RegSP = Join(LowByte(RegSP), 0x01);
            }
            return result;
        }

        private async Task<Word> PullWord()
        {
            byte l = await PullByte();
            byte h = await PullByte();
            return Join(l, h);
        }

        #endregion

        private void SetStatusFlag(StatusFlags flag, bool value)
        {
                if (value)
                {
                    RegSR |= flag;
                }
                else
                {
                    RegSR &= ~flag;
                }
        }
        private bool AccumulatorIs8Bit { get { return ReadStatusFlag(StatusFlags.M); } }
        private bool IndexesAre8Bit { get { return FlagE || ReadStatusFlag(StatusFlags.X); } }

        private bool ReadStatusFlag(StatusFlags flag)
        {
            return (RegSR & flag) != 0;
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
                RegX = (Word)LowByte(RegX);
                RegY = (Word)LowByte(RegY);
                RegSP = (Word)(0x0100 | LowByte(RegSP));
                FlagE = true;
            }
            else { FlagE = false; }
        }

        private async Task<Addr> GetEffectiveAddress(W65C816.AddressingMode addressingMode)
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
                    if (verbose) Console.Write("#");
                    return LongPC;
                case W65C816.AddressingMode.Accumulator:
                    if (verbose) Console.WriteLine("A");
                    return 0;
                case W65C816.AddressingMode.ProgramCounterRelative:
                    offsetS8 = (sbyte)(await ReadByte());
                    if (verbose) Console.WriteLine($"{offsetS8:+0,-#}");
                    return Address(RegPB, RegPC + offsetS8);
                case W65C816.AddressingMode.ProgramCounterRelativeLong:
                    offsetS16 = (short)(await ReadWord());
                    if (verbose) Console.WriteLine($"{offsetS16:+0,-#}");
                    return Address(RegPB, RegPC + offsetS16);
                case W65C816.AddressingMode.Implied:
                    return 0;
                case W65C816.AddressingMode.Stack:
                    return 0;
                case W65C816.AddressingMode.Direct:
                    offsetU8 = await ReadByte();
                    if (verbose) Console.WriteLine($"${offsetU8:x2}");
                    return Address(0, RegDP + offsetU8);
                case W65C816.AddressingMode.DirectIndexedWithX:
                    offsetU8 = await ReadByte();
                    if (verbose) Console.WriteLine($"${offsetU8:x2}, X");
                    if (FlagE && LowByte(RegDP) == 0)
                    {
                        return Address(0, Join((byte)(offsetU8 + (byte)RegX), HighByte(RegDP)));
                    }
                    else
                    {
                        return Address(0, RegDP + offsetU8 + (byte)RegX);
                    }
                case W65C816.AddressingMode.DirectIndexedWithY:
                    offsetU8 = await ReadByte();
                    if (verbose) Console.WriteLine($"${offsetU8:x2}, Y");
                    if (FlagE && LowByte(RegDP) == 0)
                    {
                        return Address(0, Join((byte)(offsetU8 + (byte)RegY), HighByte(RegDP)));
                    }
                    else
                    {
                        return Address(0, RegDP + offsetU8 + (byte)RegY);
                    }
                case W65C816.AddressingMode.DirectIndirect:
                    offsetU8 = await ReadByte();
                    if (verbose) Console.WriteLine($"(${offsetU8:x2})");
                    if (FlagE && LowByte(RegDP) == 0)
                    {
                        pointer = Address(0, Join((byte)(offsetU8), HighByte(RegDP)));
                    }
                    else
                    {
                        pointer = Address(0, RegDP + offsetU8);
                    }
                    return Address(RegDB, await ReadWord(pointer));
                case W65C816.AddressingMode.DirectIndexedIndirect:
                    offsetU8 = await ReadByte();
                    if (verbose) Console.WriteLine($"(${offsetU8:x2}, X)");
                    if (FlagE && LowByte(RegDP) == 0)
                    {
                        pointer = Address(0, Join((byte)(offsetU8 + (byte)RegX), HighByte(RegDP)));
                    }
                    else
                    {
                        pointer = Address(0, RegDP + offsetU8 + (byte)RegX);
                    }
                    return Address(RegDB, await ReadWord(pointer));
                case W65C816.AddressingMode.DirectIndirectIndexed:
                    offsetU8 = await ReadByte();
                    if (verbose) Console.WriteLine($"(${offsetU8:x2}), Y");
                    if (FlagE && LowByte(RegDP) == 0)
                    {
                        pointer = Address(0, Join((byte)(offsetU8), HighByte(RegDP)));
                    }
                    else
                    {
                        pointer = Address(0, RegDP + offsetU8);
                    }
                    return Address(RegDB, await ReadWord(pointer + RegY));
                case W65C816.AddressingMode.DirectIndirectLong:
                    offsetU8 = await ReadByte();
                    if (verbose) Console.WriteLine($"[${offsetU8:x2}]");
                    return await ReadAddr(Address(0, RegDP + offsetU8), true);
                case W65C816.AddressingMode.DirectIndirectLongIndexed:
                    offsetU8 = await ReadByte();
                    if (verbose) Console.WriteLine($"[${offsetU8:x2}], Y");
                    return await ReadAddr(Address(0, RegDP + offsetU8), true) + RegY;
                case W65C816.AddressingMode.Absolute:
                    // WARN: Special case for JMP and JSR -- replace RegDB with RegPB
                    location = await ReadWord();
                    if (verbose) Console.WriteLine($"${location:x4}");
                    return Address(RegDB, location);
                case W65C816.AddressingMode.AbsoluteIndexedWithX:
                    location = await ReadWord();
                    if (verbose) Console.WriteLine($"${location:x4}, X");
                    return Address(RegDB, location + RegX);
                case W65C816.AddressingMode.AbsoluteIndexedWithY:
                    location = await ReadWord();
                    if (verbose) Console.WriteLine($"${location:x4}, Y");
                    return Address(RegDB, location + RegY);
                case W65C816.AddressingMode.AbsoluteLong:
                    pointer = await ReadAddr();
                    if (verbose) Console.WriteLine($"{pointer:x6}");
                    return pointer;
                case W65C816.AddressingMode.AbsoluteLongIndexed:
                    pointer = await ReadAddr();
                    if (verbose) Console.WriteLine($"{pointer:x6}, X");
                    return pointer + RegX;
                case W65C816.AddressingMode.StackRelative:
                    offsetU8 = await ReadByte();
                    if (verbose) Console.WriteLine($"{offsetU8:x2}, S");
                    return Address(0, offsetU8 + RegSP);
                case W65C816.AddressingMode.StackRelativeIndirectIndexed:
                    offsetU8 = await ReadByte();
                    if (verbose) Console.WriteLine($"({offsetU8:x2}, S), Y");
                    pointer = Address(0, offsetU8 + RegSP);
                    return Address(RegDB, await ReadWord(pointer + RegY));
                case W65C816.AddressingMode.AbsoluteIndirect:
                    location = await ReadWord();
                    if (verbose) Console.WriteLine($"(${location:x4})");
                    pointer = Address(0, location);
                    return Address(RegPB, await ReadWord(pointer));
                case W65C816.AddressingMode.AbsoluteIndexedIndirect:
                    location = await ReadWord();
                    if (verbose) Console.WriteLine($"(${location:x4}, X)");
                    pointer = Address(RegPB, location);
                    return Address(RegPB, await ReadWord(pointer) + RegX);
                case W65C816.AddressingMode.BlockMove:
                    byte destination = await ReadByte();
                    byte source = await ReadByte();
                    if (verbose) Console.WriteLine($"${source:x2}, ${destination:x2}");
                    // WARN: Decode source and destination banks in the operation function
                    return Address(0, Join(destination, source));
                default:
                    return 0;
            }
        }

        private async Task LoadInterruptVector(W65C816.Vector vector)
        {
            RegPC = await ReadWord((Addr)vector);
            RegPB = 0x00;
        }

        #region Opcodes

        #region ADC SBC
        private async Task OpAdc(W65C816.AddressingMode addressingMode)
        {
            Word addend;
            if (addressingMode == W65C816.AddressingMode.Immediate)
            {
                addend = await ReadImmediate(AccumulatorIs8Bit);
            }
            else
            {
                Addr address = await GetEffectiveAddress(addressingMode);
                addend = AccumulatorIs8Bit ? await ReadByte(address) : await ReadWord(address);
            }
            byte carry = (byte)(ReadStatusFlag(StatusFlags.C) ? 1 : 0);
            if (!ReadStatusFlag(StatusFlags.M))
            {
                int al = LowByte(RegA) + addend + carry;
                if (ReadStatusFlag(StatusFlags.D))
                {
                    if (((al) & 0x0f) > 0x09) al += 0x06;
                    if (((al) & 0xf0) > 0x90) al += 0x60;
                }
                SetStatusFlag(StatusFlags.C, al > 0xffu);
                SetStatusFlag(StatusFlags.V, ((Word)((~(RegA ^ addend)) & (RegA ^ al) & 0x80) != 0));
                SetNZStatusFlagsFromValue((byte)al);
                RegA = Join((byte)al, HighByte(RegA));
            }
            else
            {
                int al = RegA + addend + carry;
                if (ReadStatusFlag(StatusFlags.D))
                {
                    if (((al) & 0x000f) > 0x0009) al += 0x0006;
                    if (((al) & 0x00f0) > 0x0090) al += 0x0060;
                    if (((al) & 0x0f00) > 0x0900) al += 0x0600;
                    if (((al) & 0xf000) > 0x9000) al += 0x6000;
                }
                SetStatusFlag(StatusFlags.C, al > 0xffffu);
                SetStatusFlag(StatusFlags.V, ((Word)((~(RegA ^ addend)) & (RegA ^ al) & 0x8000) != 0));
                SetNZStatusFlagsFromValue((Word)al);
                RegA = (Word)al;
            }
            await WaitForTickAsync();
        }

        private async Task OpSbc(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region CMP CPX CPY
        private async Task OpCmp(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpCpx(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpCpy(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region DEA DEC DEX DEY INA INC INX INY
        private async Task OpDea(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpDec(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpDex(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpDey(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpIna(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpInc(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpInx(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpIny(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region AND EOR ORA
        private async Task OpAnd(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpEor(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpOra(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion

        private async Task OpBit(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        #region TRB TSB
        private async Task OpTrb(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpTsb(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region ASL LSR ROL ROR
        private async Task OpAsl(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpLsr(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpRol(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpRor(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region BCC BCS BEQ BMI BNE BPL BRA BVC BVS
        private async Task OpBcc(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpBcs(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpBeq(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpBmi(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpBne(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpBpl(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpBra(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpBvc(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpBvs(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion

        private async Task OpBrl(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        #region JMP JSL JSR
        private async Task OpJmp(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpJsl(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpJsr(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region RTL RTS
        private async Task OpRtl(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpRts(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region BRK COP
        private async Task OpBrk(W65C816.AddressingMode addressingMode) 
        { 
            if (FlagE)
            {
                await PushWord((Word)(RegPC + 1));
                await PushByte((byte)(RegSR | StatusFlags.X));
                SetStatusFlag(StatusFlags.I, true);
                SetStatusFlag(StatusFlags.D, false);
                await LoadInterruptVector(W65C816.Vector.EmulationIRQ);
            }
            else
            {
                await PushByte(RegPB);
                await PushWord((Word)(RegPC + 1));
                await PushByte((byte)(RegSR));
                SetStatusFlag(StatusFlags.I, true);
                SetStatusFlag(StatusFlags.D, false);
                await LoadInterruptVector(W65C816.Vector.NativeBRK);
            }
        }

        private async Task OpCop(W65C816.AddressingMode addressingMode) {
            if (FlagE)
            {
                await PushWord((Word)(RegPC + 1));
                await PushByte((byte)(RegSR));
                SetStatusFlag(StatusFlags.I, true);
                SetStatusFlag(StatusFlags.D, false);
                await LoadInterruptVector(W65C816.Vector.EmulationCOP);
            }
            else
            {
                await PushByte(RegPB);
                await PushWord((Word)(RegPC + 1));
                await PushByte((byte)(RegSR));
                SetStatusFlag(StatusFlags.I, true);
                SetStatusFlag(StatusFlags.D, false);
                await LoadInterruptVector(W65C816.Vector.NativeCOP);
            }
        }
        #endregion
        #region RTI
        private async Task OpRti(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region CLC CLD CLI CLV SEC SED SEI
        private async Task OpClc(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpCld(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpCli(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpClv(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpSec(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpSed(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpSei(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region REP SEP
        private async Task OpRep(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpSep(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region LDA LDX LDY STA STX STY STZ
        private async Task OpLda(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpLdx(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpLdy(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpSta(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpStx(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpSty(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpStz(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region MVN MVP
        private async Task OpMvn(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpMvp(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region NOP WDM
        private async Task OpNop(W65C816.AddressingMode addressingMode) 
        {
            await WaitForTickAsync();
        }

        private async Task OpWdm(W65C816.AddressingMode addressingMode)
        {
            RegPC++;
            await WaitForTickAsync();
        }
        #endregion
        #region PEA PEI PER
        private async Task OpPea(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpPei(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpPer(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region PHA PHX PHY PLA PLX PLY
        private async Task OpPha(W65C816.AddressingMode addressingMode)
        {
            if (AccumulatorIs8Bit)
            {
                await PushByte(RegAL);
            }
            else
            {
                await PushWord(RegA);
            }
            await WaitForTickAsync();
        }

        private async Task OpPhx(W65C816.AddressingMode addressingMode)
        {
            if (IndexesAre8Bit)
            {
                await PushByte(RegXL);
            }
            else
            {
                await PushWord(RegX);
            }
            await WaitForTickAsync();
        }

        private async Task OpPhy(W65C816.AddressingMode addressingMode)
        {
            if (IndexesAre8Bit)
            {
                await PushByte(RegYL);
            }
            else
            {
                await PushWord(RegY);
            }
        }

        private async Task OpPla(W65C816.AddressingMode addressingMode)
        {
            if (AccumulatorIs8Bit)
            {
                RegAL = await PullByte();
                SetNZStatusFlagsFromValue(RegAL);
            }
            else
            {
                RegA = await PullWord();
                SetNZStatusFlagsFromValue(RegA);
            }
        }

        private async Task OpPlx(W65C816.AddressingMode addressingMode)
        {
            if (IndexesAre8Bit)
            {
                RegXL = await PullByte();
                SetNZStatusFlagsFromValue(RegXL);
            }
            else
            {
                RegX = await PullWord();
                SetNZStatusFlagsFromValue(RegX);
            }
        }

        private async Task OpPly(W65C816.AddressingMode addressingMode)
        {
            if (IndexesAre8Bit)
            {
                RegYL = await PullByte();
                SetNZStatusFlagsFromValue(RegYL);
            }
            else
            {
                RegY = await PullWord();
                SetNZStatusFlagsFromValue(RegY);
            }
        }
        #endregion
        #region PHB PHD PHK PHP PLB PLD PLP
        private async Task OpPhb(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpPhd(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpPhk(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpPhp(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpPlb(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpPld(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpPlp(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region STP WAI
        private async Task OpStp(W65C816.AddressingMode addressingMode)
        {
            await WaitForTickAsync();
            stopped = true;
            clock.Stop();
        }

        private async Task OpWai(W65C816.AddressingMode addressingMode)
        {
            await WaitForTickAsync();
            waiting = true;
        }
        #endregion
        #region TAX TAY TSX TXA TXS TXY TYA TYX
        private async Task OpTax(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        
        private async Task OpTay(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpTsx(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpTxa(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpTxs(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpTxy(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpTya(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpTyx(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region TCD TCS TDC TSC
        private async Task OpTcd(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpTcs(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpTdc(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private async Task OpTsc(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        private async Task OpXba(W65C816.AddressingMode addressingMode)
        {
            await WaitForTickAsync();
            RegA = Swap(RegA);
        }

        private async Task OpXce(W65C816.AddressingMode addressingMode) 
        { 
            await WaitForTickAsync();
            bool carry = ReadStatusFlag(StatusFlags.C);
            SetStatusFlag(StatusFlags.C, FlagE);
            SetEmulationMode(carry);
        }

        #endregion

        #region HW Interrupts
        private void Reset()
        {
            cycles = 0;
            FlagE = true;
            RegPB = 0x00;
            RegDB = 0x00;
            RegDP = 0x0000;
            RegSP = 0x0100;
            RegSR = (StatusFlags)0x34;
            stopped = false;
            waiting = false;
            interruptingMaskable = false;
            resetting = false;
            
            if (verbose) Console.WriteLine("RESET");
            LoadInterruptVector(W65C816.Vector.Reset);
        }

        private async Task InterruptMaskable()
        {
            throw new NotImplementedException();
        }
        private async Task InterruptNonMaskable()
        {
            throw new NotImplementedException();
        }
        #endregion

        private async Task ExecuteOperationAsync()
        {
            if (resetting)
            {
                Reset();
            }
            else if (!stopped)
            {
                int oldCycles = cycles;
                operationComplete = false;
                if (interruptingNonMaskable)
                {
                    waiting = false;
                    await InterruptNonMaskable();
                }
                else if (interruptingMaskable)
                {
                    if (!ReadStatusFlag(StatusFlags.I))
                    {
                        waiting = false;
                        await InterruptMaskable();
                    }
                    else if (waiting)
                    {
                        waiting = false;
                    }
                }
                else if (!waiting)
                {
                    RegIR = await ReadByte();
                    (W65C816.OpCode o, W65C816.AddressingMode m) = W65C816.OpCodeLookup(RegIR);
                    if (verbose) Console.Write(o.ToString() + " ");
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
                    await operation(m);
                    if (!stopped) operationComplete = true;
#if DEBUG
                    int cyclesThisOp = cycles - oldCycles;
                    Console.WriteLine($"Cycles: {cyclesThisOp}");

                    string flags = FormatStatusFlags();

                    Console.WriteLine($"A: 0x{RegA:x4}\n X: 0x{RegX:x4}\n Y: 0x{RegY:x4}\n DP: 0x{RegDP:x4}\n SP: 0x{RegSP:x4}\n DB: 0x{RegDP:x2}");
                    Console.WriteLine($"PB: 0x{RegPB:x2} PC: 0x{RegPC:x4}");
                    Console.WriteLine($"Flags: {flags}");
#endif
                }
            }
        }
        public string DeviceList()
        {
            string result = "";
            Addr lastUsedAddress = 0xffffffff;
            foreach (var device in devices)
            {
                (Addr start, Addr end) = device.Key;
                if (start != lastUsedAddress + 1)
                {
                    result += $"${lastUsedAddress + 1:x6} - ${start - 1:x6}: Unused\n";
                }
                result += $"${start:x6} - ${end:x6}: {device.Value}\n";
            }
            return result;
        }
        private string FormatStatusFlags()
        {
            string flags = "";
            flags += ReadStatusFlag(StatusFlags.N) ? "N" : "-";
            flags += ReadStatusFlag(StatusFlags.V) ? "V" : "-";
            if (FlagE)
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
            flags += FlagE ? "E" : "-";
            return flags;
        }

        public Status GetStatus()
        {
            Status result = new()
            {
                Cycles = cycles,
                A = RegA,
                X = RegX,
                Y = RegY,
                DP = RegDP,
                SP = RegSP,
                PC = RegPC,
                DB = RegDB,
                PB = RegPB,
                FlagN = (RegSR & StatusFlags.N) == StatusFlags.N,
                FlagV = (RegSR & StatusFlags.V) == StatusFlags.V,
                FlagM = (RegSR & StatusFlags.M) == StatusFlags.M,
                FlagX = (RegSR & StatusFlags.X) == StatusFlags.X,
                FlagD = (RegSR & StatusFlags.D) == StatusFlags.D,
                FlagI = (RegSR & StatusFlags.I) == StatusFlags.I,
                FlagZ = (RegSR & StatusFlags.Z) == StatusFlags.Z,
                FlagC = (RegSR & StatusFlags.C) == StatusFlags.C,
                FlagE = FlagE
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
                return $"Cycles: {Cycles}\nA: 0x{A:x4}\n X: 0x{X:x4}\n Y: 0x{Y:x4}\n DP: 0x{DP:x4}\n SP: 0x{SP:x4}\n DB: 0x{DB:x2}\nPB: 0x{PB:x2} PC: 0x{PC:x4}\nFlags: {flags}";
            }
        }
    }
}
