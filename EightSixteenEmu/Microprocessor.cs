using System.ComponentModel;
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
        private bool stopped;
        private bool waiting;
        private bool breakActive;
        private readonly SortedDictionary<(Addr start, Addr end), IMappableDevice> devices;

        public int Cycles { 
            get => cycles; 
        }

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

        private Word RegC;  // accumulator
        private Word RegX;  // index register X
        private Word RegY;  // index register Y
        private Word RegDP; // direct page pointer
        private Word RegSP; // stack pointer
        private Byte RegDB; // data bank
        private Byte RegPB; // program bank
        private Word RegPC; // program counter
        private StatusFlags RegSR;  // status flags register
        private bool FlagE; // emulation flag
        private byte RegMD; // memory data

        public Microprocessor(List<IMappableDevice> deviceList) { 
            RegC = 0x0000;
            RegX = 0x0000;
            RegY = 0x0000;
            RegDP = 0x0000;
            RegSP = 0x0100;
            RegDB = 0x00;
            RegPB = 0x00;
            RegPC = 0x0000;
            RegSR = StatusFlags.None;
            FlagE = false;
            RegMD = 0x00;

            cycles = 0;
            resetting = true;
            interruptingMaskable = false;
            interruptingNonMaskable = false;
            stopped = false;
            breakActive = false;

            devices = new SortedDictionary<(Addr start, Addr end), IMappableDevice> ();
            foreach (IMappableDevice newDevice in deviceList)
            {
                SortedDictionary<(Addr start, Addr end), IMappableDevice>.KeyCollection ranges = devices.Keys;
                Addr top = newDevice.base_address;
                Addr bottom = newDevice.base_address + newDevice.size;
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
        static Addr Join(byte l, byte h)
        {
            return (Addr)(l |  (h << 8));
        }
        static Addr Join(byte b, Word w)
        {
            return (Bank(b) | w);
        }
        static Word Swap(Word w)
        {
            return (Word)((w >> 8) | (w << 8));
        }

        public bool IsStopped()
        {
            return stopped; 
        }

        private IMappableDevice? GetDevice(Addr address)
        {
            IMappableDevice result = null;
            SortedDictionary<(Addr start, Addr end), IMappableDevice>.KeyCollection ranges = devices.Keys;
            foreach ((Addr s, Addr e) in ranges)
            {   
                if ((address >= s && address <= e))
                {
                    result = devices[(s,e)];
                }
            }
            return result;
        }

        private byte ReadByte(Addr address)
        {
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
            return (Word)Join(ReadByte(address),ReadByte(address + 1));
        }

        private Addr ReadAddr(Addr address)
        {
            return Join(ReadByte(address + 2),ReadWord(address));
        }

        private void WriteByte(Addr address, byte value)
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

        private void WriteWord(Addr address, Word value)
        {
            WriteByte(address, LowByte(value));
            WriteByte(address + 1, HighByte(value));
        }

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

        private bool ReadStatusFlag(StatusFlags flag)
        {
            return (RegSR & flag) != 0;
        }

        private void SetEmulationMode(bool value)
        {
            if (value)
            {

            }
        }

        public Status GetStatus()
        {
            Status result = new()
            {
                Cycles = cycles,
                A = RegC,
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
