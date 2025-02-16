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
        private readonly SortedDictionary<(Addr start, Addr end), IMappableDevice> devices;

        public int Cycles
        {
            get => cycles;
        }
        public bool Stopped { get => stopped; }

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
        static Word Join(byte l, byte h)
        {
            return (Word)(l | (h << 8));
        }
        static Addr Address(byte b, Word w)
        {
            return (Bank(b) | w);
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

        private Word ReadWord(Addr address)
        {
            return (Word)Join(ReadByte(address), ReadByte(address + 1));
        }

        private Addr ReadAddr(Addr address)
        {
            return Address(ReadByte(address + 2), ReadWord(address));
        }

        private byte ReadByteAtPC()
        {
            byte result = ReadByte(LongPC);
            RegPC += 1;
            return result;
        }

        private Word ReadWordAtPC()
        {
            Word result = ReadWord(LongPC);
            RegPC += 2;
            return result;
        }

        private Addr ReadAddrAtPC()
        {
            Addr result = ReadAddr(LongPC);
            RegPC += 3;
            return result;
        }

        private void WriteByte(Addr address, byte value)
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

        private void WriteWord(Addr address, Word value)
        {
            WriteByte(address, LowByte(value));
            WriteByte(address + 1, HighByte(value));
        }

        private void PushByte(byte value)
        {
            WriteByte(RegSP--, value);
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
            if (!FlagE || (flag & (StatusFlags.M | StatusFlags.X)) == 0)
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
        }

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

        #region Addressing Modes

        private Addr AddrModeAbsolute()
        {
            return Address(RegDB, ReadWordAtPC());
        }

        private Addr AddrModeAbsoluteIndexedX()
        {
            return Address(RegDB, ReadWordAtPC()) + RegX;
        }

        private Addr AddrModeAbsoluteIndexedY()
        {
            return Address(RegDB, ReadWordAtPC()) + RegY;
        }

        private Addr AddrModeAbsoluteIndirect()
        {
            Addr intermediateAddress = Address(0, ReadWordAtPC());
            return Address(0, ReadWord(intermediateAddress));
        }

        private Addr AddrModeAbsoluteIndexedIndirect()
        {
            Addr intermediateAddress = Address(RegPB, ReadWordAtPC()) + RegX;
            return Address(0, ReadWord(intermediateAddress));
        }

        private Addr AddrModeAbsoluteLong()
        {
            return ReadAddrAtPC();
        }

        private Addr AddrModeAbsoluteLongIndexed()
        {
            return ReadAddrAtPC() + RegX;
        }

        private Addr AddrModeAbsoluteIndirectLong()
        {
            Addr intermediateAddress = Address(0,ReadWordAtPC());
            return ReadAddr(intermediateAddress);
        }

        private Addr AddrModeDirect()
        {
            return (Bank(0) | (Word)(RegDP + ReadByteAtPC()));
        }

        private Addr AddrModeDirectIndexedX()
        {
            byte offset = (byte)(ReadByteAtPC() + (byte)RegX);
            return Address(0,(Word)(RegDP + offset));
        }

        private Addr AddrModeDirectIndexedY()
        {
            byte offset = (byte)(ReadByteAtPC() + (byte)RegY);
            return Address(0, (Word)(RegDP + offset));
        }

        private Addr AddrModeDirectIndirect()
        {
            return (Bank(RegDB) | ReadWord((Bank(0) | (Word)(RegDP + ReadByteAtPC()))));
        }

        private Addr AddrModeDirectIndexedIndirect()
        {
            return (Bank(RegDB) | ReadWord((Bank(0) | (Word)(RegDP + ReadByteAtPC() + RegX))));
        }

        private Addr AddrModeDirectIndirectIndexed()
        {
            byte offset = ReadByteAtPC();
            Addr intermediateAddress = (Addr)(Bank(0) | (byte)(RegDP + offset));
            return Address(RegDB, (Word)(ReadWord(intermediateAddress) + RegY));
        }

        private Addr AddrModeDirectIndirectLong()
        {
            byte offset = ReadByteAtPC();
            return ReadAddr(Address(0, (Word)(RegDP + offset)));
        }

        private Addr AddrModeDirectIndirectLongIndexed()
        {
            byte offset = ReadByteAtPC();
            return ReadAddr(Address(0, (Word)(RegDP + offset))) + RegY;
        }

        private Addr AddrModeImmediate(bool use_word)
        {
            Addr result = LongPC;
            RegPC += (Word)(use_word ? 2 : 1);
            return result;
        }

        private Addr AddrModeRelative(bool use_word)
        {
            Word offset = use_word ? ReadWordAtPC() : ReadByteAtPC();
            return Address(RegPB, (Word)(RegPC + offset));
        }

        private Addr AddrModeStackRelative()
        {
            byte offset = ReadByteAtPC();
            return Address(0, (Word)(RegSP + offset));
        }

        private Addr AddrModeStackRelativeIndirectIndexedY()
        {
            Word intermediateAddress = (ushort)(ReadByteAtPC() + RegSP);
            return Address(RegDB, (Word)(intermediateAddress + RegY));
        }

        #endregion

        #region Opcodes
        private void OpAdc(Addr address)
        {
            Word addend = ReadStatusFlag(StatusFlags.M) ? ReadWord(address) : ReadByte(address);
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
            cycles += 2;
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
            interruptingMaskable = false;

            RegPC = ReadWord(0xFFFC);
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
                if (interruptingNonMaskable)
                {
                    InterruptNonMaskable();
                }
                else if (interruptingMaskable && ReadStatusFlag(StatusFlags.I))
                {
                    InterruptMaskable();
                }
                else
                {
                    RegIR = ReadByteAtPC();
                    switch (RegIR)
                    {
                        // ADC SBC
                        case 0x61: OpAdc(AddrModeDirectIndexedIndirect()); break;
                        case 0x63: OpAdc(AddrModeStackRelative()); break;
                        case 0x65: OpAdc(AddrModeDirect()); break;
                        case 0x67: OpAdc(AddrModeDirectIndirectLong()); break;
                        case 0x69: OpAdc(AddrModeImmediate(!ReadStatusFlag(StatusFlags.M))); break;
                        case 0x6d: OpAdc(AddrModeAbsolute()); break;
                        case 0x6f: OpAdc(AddrModeAbsoluteLong()); break;
                        case 0x71: OpAdc(AddrModeDirectIndirectIndexed()); break;

                        // CMP CPX CPY


                        // DEA DEC DEX DEY INA INC INX INY


                        // AND EOR ORA


                        // BIT


                        // TRB TSB


                        // ASL LSR ROL ROR


                        // BCC BCS BEQ BMI BNE BPL BRA BVC BVS


                        // BRL


                        // JMP JSL JSR


                        // RTL RTS


                        // BRK COP


                        // RTI


                        // CLC CLD CLI CLV SEC SED SEI


                        // REP SEP


                        // LDA LDX LDY STA STX STY STZ


                        // MVN MVP


                        // NOP WDM


                        // PEA PEI PER


                        // PHA PHX PHY PLA PLX PLY


                        // PHD PHD PHK PHP PLD PLP


                        // STP WAI


                        // TAX TAY TSX TXA TXS TXY TYA TYX


                        // TCD TCS TDC TSC


                        // XBA


                        // XCE


                        default:
                            throw new NotImplementedException($"Opcode ${RegIR:x2} not yet implemented");
                    }
                }
            }
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
        }
    }
}
