namespace EightSixteenEmu
{
    public partial class NewCore
    {
        private readonly Processor mpu;
        private readonly MemoryMapping.MemoryMapper mapper;


        partial class Processor(NewCore core)
        {
            /*
             * Okay, so the old code was getting kind of smelly, with a lot of 
             * methods that don't really belong in the Processor class.
             * (like threading -- ack!)
             * SO! I'm going to refactor it so that this class just does what
             * the real-world 65816 does, and nothing else.
             * (moving data, ALU operations, etc.)
             * 
             * Expect to see a lot of nested classes, since I don't think
             * that, say, micro-opcodes should be visible outside of the
             * Processor object.
             */
            private readonly NewCore _core = core;

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

            private int _cycles;
            // accessible registers
            private ushort _regA;  // accumulator
            private ushort _regX;  // index register X
            private ushort _regY;  // index register Y
            private ushort _regDP; // direct page pointer
            private ushort _regSP; // stack pointer
            private byte _regDB; // data bank
            private byte _regPB; // program bank
            private ushort _regPC; // program counter
            private StatusFlags _regSR;  // status flags register
            private bool _flagE; // emulation flag
            private ushort _internalData; // internal data register for some operations
            private ushort _internalAddress; // internal address register for some operations
            private byte _dataBus; // last value read from or written to memory

            private W65C816.AddressingMode? currentAddressingMode = null;
            private W65C816.OpCode? currentOpCode = null;

            private readonly Queue<Cycle> _cycleQueue = new Queue<Cycle>();

            class Cycle
            {
                public enum CycleType
                {
                    Internal,
                    Read,
                    Write,
                }
                private CycleType Type { get; }
                private Processor Processor { get; }
                private uint? Address { get; }
                List<Action> MicroOps { get; } = new List<Action>();
                public Cycle(Processor proc, CycleType type, List<Action> actions, uint? address = null)
                {
                    if (Type != CycleType.Internal && address == null)
                    {
                        throw new ArgumentNullException(nameof(address));
                    }
                    Processor = proc;
                    Type = type;
                    MicroOps.AddRange(actions);
                    Address = address;
                }
                public void Execute()
                {
                    if (Type == CycleType.Read)
                    {
                        // regarding CS8629: the null check is done in the constructor, so there must be a value here.
#pragma warning disable CS8629 // Nullable value type may be null.
                        Processor._dataBus = Processor._core.mapper[Address.Value] ?? Processor._dataBus;
#pragma warning restore CS8629 // Nullable value type may be null.
                    }
                    foreach (var microOp in MicroOps)
                    {
                        microOp();
                    }
                    if (Type == CycleType.Write)
                    {
#pragma warning disable CS8629 // Nullable value type may be null.
                        Processor._core.mapper[Address.Value] = Processor._dataBus;
#pragma warning restore CS8629 // Nullable value type may be null.
                    }
                }
            }
        }
    }
}
