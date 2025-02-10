using Addr = System.UInt32;
using Word = System.UInt16;

namespace EightSixteenEmu
{
    public class Microprocessor
    {
        int cycles;
        bool interrupted;
        bool stopped;
        bool breakActive;

        public int Cycles { 
            get => cycles; 
        }

        Word C;
        Word X;
        Word Y;
        Word DP;
        Word SP;
        Byte DB;
        Byte PB;
        Word PC;
        Byte P;
        bool E;

        [Flags]
        public enum StatusFlags : byte
        {
            C = 0x01,
            Z = 0x02,
            I = 0x04,
            D = 0x08,
            X = 0x10,
            M = 0x20,
            V = 0x40,
            N = 0x80,
        }
    }
}
