using System.Net;
using System.Runtime.ConstrainedExecution;
using System.Runtime.Intrinsics.Arm;
using System.Xml.Serialization;
using static System.Net.WebRequestMethods;
using Addr = System.UInt32;
using Word = System.UInt16;

namespace EightSixteenEmu
{
    public class Microprocessor
    {
        private int cycles;
        private bool resetting;
        private bool interruptingMaskable;
        private bool interruptingNonMaskable;
        private bool aborting;
        private bool stopped;
        private bool waiting;
        private bool breakActive;
        private bool verbose;
        private readonly SortedDictionary<(Addr start, Addr end), IMappableDevice> devices;

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
        // non-accessible registers
        private byte RegIR; // instruction register
        private byte RegMD; // memory data register

        public Microprocessor(List<IMappableDevice> deviceList)
        {
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
                    throw new Exception($"Addresses for {newDevice.GetType()} fall outside the 24-bit address space.");
                }
                else
                {
                    foreach ((Addr s, Addr e) in ranges)
                    {
                        if (Math.Min(top, s) - Math.Min(bottom, e) > 0)
                        {
                            throw new Exception($"Addresses for {newDevice.GetType()} (${top:x6} - ${bottom:x6}) conflict with existing device at ${s:x6} - ${e:x6}");
                        }
                    }
                    devices.Add((top, bottom), newDevice);
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
            if (verbose)
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
            cycles++;
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
                return Address(ReadByte(Address(b, (Word)(a + 2))),ReadWord(address, true));
            }
        }

        private byte ReadByte()
        {
            byte result = ReadByte(LongPC);
            RegPC += 1;
            return result;
        }

        private Word ReadWord()
        {
            Word result = ReadWord(LongPC, true);
            RegPC += 2;
            return result;
        }

        private Addr ReadAddr()
        {
            Addr result = ReadAddr(LongPC, true);
            RegPC += 3;
            return result;
        }

        private void WriteByte(byte value, Addr address)
        {
            cycles++;
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

        private void WriteWord(Word value, Addr address)
        {
            WriteByte(LowByte(value), address);
            WriteByte(HighByte(value), address + 1);
        }

        private void PushByte(byte value)
        {
            WriteByte(value, RegSP--);
            if (FlagE)
            {
                RegSP = Join(LowByte(RegSP), 0x01);
            }
        }

        private void PushWord(Word value)
        {
            PushByte(HighByte(value));
            PushByte(LowByte(value));
        }

        private byte PullByte()
        {
            byte result = ReadByte(++RegSP);
            if (FlagE)
            {
                RegSP = Join(LowByte(RegSP), 0x01);
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
                    RegSR |= flag;
                }
                else
                {
                    RegSR &= ~flag;
                }
        }
        private bool AccumulatorIs8Bit { get { return ReadStatusFlag(StatusFlags.M); } }
        private bool IndexesAre8Bit { get { return ReadStatusFlag(StatusFlags.X); } }

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
                    if (verbose) Console.Write("#");
                    return LongPC;
                case W65C816.AddressingMode.Accumulator:
                    if (verbose) Console.WriteLine("A");
                    return 0;
                case W65C816.AddressingMode.ProgramCounterRelative:
                    offsetS8 = (sbyte)(ReadByte());
                    if (verbose) Console.WriteLine($"{offsetS8:+0,-#}");
                    return Address(RegPB, RegPC + offsetS8);
                case W65C816.AddressingMode.ProgramCounterRelativeLong:
                    offsetS16 = (short)(ReadWord());
                    if (verbose) Console.WriteLine($"{offsetS16:+0,-#}");
                    return Address(RegPB, RegPC + offsetS16);
                case W65C816.AddressingMode.Implied:
                    return 0;
                case W65C816.AddressingMode.Stack:
                    return 0;
                case W65C816.AddressingMode.Direct:
                    offsetU8 = ReadByte();
                    if (verbose) Console.WriteLine($"${offsetU8:x2}");
                    return Address(0, RegDP + offsetU8);
                case W65C816.AddressingMode.DirectIndexedWithX:
                    offsetU8 = ReadByte();
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
                    offsetU8 = ReadByte();
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
                    offsetU8 = ReadByte();
                    if (verbose) Console.WriteLine($"(${offsetU8:x2})");
                    if (FlagE && LowByte(RegDP) == 0)
                    {
                        pointer = Address(0, Join((byte)(offsetU8), HighByte(RegDP)));
                    }
                    else
                    {
                        pointer = Address(0, RegDP + offsetU8);
                    }
                    return Address(RegDB, ReadWord(pointer));
                case W65C816.AddressingMode.DirectIndexedIndirect:
                    offsetU8 = ReadByte();
                    if (verbose) Console.WriteLine($"(${offsetU8:x2}, X)");
                    if (FlagE && LowByte(RegDP) == 0)
                    {
                        pointer = Address(0, Join((byte)(offsetU8 + (byte)RegX), HighByte(RegDP)));
                    }
                    else
                    {
                        pointer = Address(0, RegDP + offsetU8 + (byte)RegX);
                    }
                    return Address(RegDB, ReadWord(pointer));
                case W65C816.AddressingMode.DirectIndirectIndexed:
                    offsetU8 = ReadByte();
                    if (verbose) Console.WriteLine($"(${offsetU8:x2}), Y");
                    if (FlagE && LowByte(RegDP) == 0)
                    {
                        pointer = Address(0, Join((byte)(offsetU8), HighByte(RegDP)));
                    }
                    else
                    {
                        pointer = Address(0, RegDP + offsetU8);
                    }
                    return Address(RegDB, ReadWord(pointer + RegY));
                case W65C816.AddressingMode.DirectIndirectLong:
                    offsetU8 = ReadByte();
                    if (verbose) Console.WriteLine($"[${offsetU8:x2}]");
                    return ReadAddr(Address(0, RegDP + offsetU8), true);
                case W65C816.AddressingMode.DirectIndirectLongIndexed:
                    offsetU8 = ReadByte();
                    if (verbose) Console.WriteLine($"[${offsetU8:x2}], Y");
                    return ReadAddr(Address(0, RegDP + offsetU8), true) + RegY;
                case W65C816.AddressingMode.Absolute:
                    // WARN: Special case for JMP and JSR -- replace RegDB with RegPB
                    location = ReadWord();
                    if (verbose) Console.WriteLine($"${location:x4}");
                    return Address(RegDB, location);
                case W65C816.AddressingMode.AbsoluteIndexedWithX:
                    location = ReadWord();
                    if (verbose) Console.WriteLine($"${location:x4}, X");
                    return Address(RegDB, location + RegX);
                case W65C816.AddressingMode.AbsoluteIndexedWithY:
                    location = ReadWord();
                    if (verbose) Console.WriteLine($"${location:x4}, Y");
                    return Address(RegDB, location + RegY);
                case W65C816.AddressingMode.AbsoluteLong:
                    pointer = ReadAddr();
                    if (verbose) Console.WriteLine($"{pointer:x6}");
                    return pointer;
                case W65C816.AddressingMode.AbsoluteLongIndexed:
                    pointer = ReadAddr();
                    if (verbose) Console.WriteLine($"{pointer:x6}, X");
                    return pointer + RegX;
                case W65C816.AddressingMode.StackRelative:
                    offsetU8 = ReadByte();
                    if (verbose) Console.WriteLine($"{offsetU8:x2}, S");
                    return Address(0, offsetU8 + RegSP);
                case W65C816.AddressingMode.StackRelativeIndirectIndexed:
                    offsetU8 = ReadByte();
                    if (verbose) Console.WriteLine($"({offsetU8:x2}, S), Y");
                    pointer = Address(0, offsetU8 + RegSP);
                    return Address(RegDB, ReadWord(pointer + RegY));
                case W65C816.AddressingMode.AbsoluteIndirect:
                    location = ReadWord();
                    if (verbose) Console.WriteLine($"(${location:x4})");
                    pointer = Address(0, location);
                    return Address(RegPB, ReadWord(pointer));
                case W65C816.AddressingMode.AbsoluteIndexedIndirect:
                    location = ReadWord();
                    if (verbose) Console.WriteLine($"(${location:x4}, X)");
                    pointer = Address(RegPB, location);
                    return Address(RegPB, ReadWord(pointer) + RegX);
                case W65C816.AddressingMode.BlockMove:
                    byte destination = ReadByte();
                    byte source = ReadByte();
                    if (verbose) Console.WriteLine($"${source:x2}, ${destination:x2}");
                    // WARN: Decode source and destination banks in the operation function
                    return Address(0, Join(destination, source));
                default:
                    return 0;
            }
        }

        private void LoadInterruptVector(W65C816.Vector vector)
        {
            RegPC = ReadWord((Addr)vector);
            RegPB = 0x00;
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
                addend = AccumulatorIs8Bit ? ReadByte(address) : ReadWord(address);
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
            cycles += 1;
        }

        private void OpSbc(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region CMP CPX CPY
        private void OpCmp(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpCpx(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpCpy(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region DEA DEC DEX DEY INA INC INX INY
        private void OpDea(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpDec(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpDex(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpDey(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpIna(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpInc(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpInx(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpIny(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
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
            if (FlagE)
            {
                PushWord((Word)(RegPC + 1));
                PushByte((byte)(RegSR | StatusFlags.X));
                SetStatusFlag(StatusFlags.I, true);
                SetStatusFlag(StatusFlags.D, false);
                LoadInterruptVector(W65C816.Vector.EmulationIRQ);
            }
            else
            {
                PushByte(RegPB);
                PushWord((Word)(RegPC + 1));
                PushByte((byte)(RegSR));
                SetStatusFlag(StatusFlags.I, true);
                SetStatusFlag(StatusFlags.D, false);
                LoadInterruptVector(W65C816.Vector.NativeBRK);
            }
        }

        private void OpCop(W65C816.AddressingMode addressingMode) {
            if (FlagE)
            {
                PushWord((Word)(RegPC + 1));
                PushByte((byte)(RegSR));
                SetStatusFlag(StatusFlags.I, true);
                SetStatusFlag(StatusFlags.D, false);
                LoadInterruptVector(W65C816.Vector.EmulationCOP);
            }
            else
            {
                PushByte(RegPB);
                PushWord((Word)(RegPC + 1));
                PushByte((byte)(RegSR));
                SetStatusFlag(StatusFlags.I, true);
                SetStatusFlag(StatusFlags.D, false);
                LoadInterruptVector(W65C816.Vector.NativeCOP);
            }
        }
        #endregion
        #region RTI
        private void OpRti(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region CLC CLD CLI CLV SEC SED SEI
        private void OpClc(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpCld(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpCli(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpClv(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpSec(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpSed(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpSei(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region REP SEP
        private void OpRep(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpSep(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region LDA LDX LDY STA STX STY STZ
        private void OpLda(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpLdx(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpLdy(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpSta(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpStx(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpSty(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpStz(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region MVN MVP
        private void OpMvn(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpMvp(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region NOP WDM
        private void OpNop(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpWdm(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region PEA PEI PER
        private void OpPea(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpPei(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpPer(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
        #endregion
        #region PHA PHX PHY PLA PLX PLY
        private void OpPha(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpPhx(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpPhy(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpPla(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpPlx(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpPly(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
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
        private void OpStp(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpWai(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }
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
        private void OpXba(W65C816.AddressingMode addressingMode) { throw new NotImplementedException(); }

        private void OpXce(W65C816.AddressingMode addressingMode) 
        { 
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

            LoadInterruptVector(W65C816.Vector.Reset);
            resetting = false;
        }
        private void InterruptMaskable()
        {
            throw new NotImplementedException();
        }
        private void InterruptNonMaskable()
        {
            throw new NotImplementedException();
        }
        #endregion

        public void Step()
        {
            if (resetting)
            {
                Reset();
            }
            else if (!stopped)
            {
                int oldCycles = cycles;
                if (interruptingNonMaskable)
                {
                    waiting = false;
                    InterruptNonMaskable();
                }
                else if (interruptingMaskable)
                {
                    if (!ReadStatusFlag(StatusFlags.I))
                    {
                        waiting = false;
                        InterruptMaskable();
                    }
                    else if (waiting)
                    {
                        waiting = false;
                    }
                }
                else if (!waiting)
                {
                    RegIR = ReadByte();
                    cycles += 1;
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
                    operation(m);
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
