using static EightSixteenEmu.MPU.Microprocessor;

namespace EightSixteenEmu
{
    public partial class NewCore
    {
        private readonly Processor mpu;
        private readonly MemoryMapping.MemoryMapper mapper;

        private bool _enabled = true;

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public MemoryMapping.MemoryMapper Mapper => mapper;

        public void SetProcessorState(ProcessorState state)
        {
            throw new NotImplementedException();
        }

        public ProcessorState GetProcessorState()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }   

        public Cycle CycleStep()
        {
            throw new NotImplementedException();
        }

        public NewCore(MemoryMapping.MemoryMapper? memoryMapper = null)
        {
            memoryMapper ??= new MemoryMapping.MemoryMapper();
            mapper = memoryMapper;
            mpu = new Processor(this);
        }

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

            private byte RegAL { get => (byte)(_regA & 0x00FF); set => _regA = (ushort)((_regA & 0xFF00) | value); }
            private byte RegAH { get => (byte)(_regA >> 8); set => _regA = (ushort)((_regA & 0x00FF) | (value << 8)); }
            private byte RegXL { get => (byte)(_regX & 0x00FF); set => _regX = (ushort)((_regX & 0xFF00) | value); }
            private byte RegXH { get => (byte)(_regX >> 8); set => _regX = (ushort)((_regX & 0x00FF) | (value << 8)); }
            private byte RegYL { get => (byte)(_regY & 0x00FF); set => _regY = (ushort)((_regY & 0xFF00) | value); }
            private byte RegYH { get => (byte)(_regY >> 8); set => _regY = (ushort)((_regY & 0x00FF) | (value << 8)); }
            private byte RegDL { get => (byte)(_regDP & 0x00FF); set => _regDP = (ushort)((_regDP & 0xFF00) | value); }
            private byte RegDH { get => (byte)(_regDP >> 8); set => _regDP = (ushort)((_regDP & 0x00FF) | (value << 8)); }
            private byte RegSL { get => (byte)(_regSP & 0x00FF); set => _regSP = (ushort)((_regSP & 0xFF00) | value); }
            private byte RegSH { get => (byte)(_regSP >> 8); set => _regSP = (ushort)((_regSP & 0x00FF) | (value << 8)); }
            private byte RegPCL { get => (byte)(_regPC & 0x00FF); set => _regPC = (ushort)((_regPC & 0xFF00) | value); }
            private byte RegPCH { get => (byte)(_regPC >> 8); set => _regPC = (ushort)((_regPC & 0x00FF) | (value << 8)); }
            private byte RegIDL { get => (byte)(_internalData & 0x00FF); set => _internalData = (ushort)((_internalData & 0xFF00) | value); }
            private byte RegIDH { get => (byte)(_internalData >> 8); set => _internalData = (ushort)((_internalData & 0x00FF) | (value << 8)); }
            private byte RegIAL { get => (byte)(_internalAddress & 0x00FF); set => _internalAddress = (ushort)((_internalAddress & 0xFF00) | value); }
            private byte RegIAH { get => (byte)(_internalAddress >> 8); set => _internalAddress = (ushort)((_internalAddress & 0x00FF) | (value << 8)); }

            private bool _resetSignal = true;
            private bool _resetting = false;
            private bool _nmiPending = false;
            private bool _busReady = true;

            private W65C816.AddressingMode? currentAddressingMode = null;
            private W65C816.OpCode? currentOpCode = null;
            private ClockState _clockState = ClockState.Running;

            internal enum ClockState
            {
                Running,
                Stopped,
                Waiting,
            }

            internal enum RegisterType
            {
                RegA,
                RegAH,
                RegAL,
                RegX,
                RegXH,
                RegXL,
                RegY,
                RegYH,
                RegYL,
                RegDP,
                RegDL,
                RegDH,
                RegSP,
                RegSL,
                RegSH,
                RegDB,
                RegPB,
                RegPC,
                RegPCH,
                RegPCL,
                RegID,
                RegIDH,
                RegIDL,
                RegIA,
                RegIAH,
                RegIAL,
                DataBus,
            }

            private readonly Queue<Cycle> _cycleQueue = new Queue<Cycle>();

            private void EnqueueMultiple(List<Cycle> cycles)
            {
                foreach (var cycle in cycles)
                {
                    _cycleQueue.Enqueue(cycle);
                }
            }

            private void OnClockTick()
            {
                if (_resetSignal)
                {
                    if (!_resetting)
                    {
                        _resetting = true;
                        _clockState = ClockState.Running;
                        _regDP = 0;
                        _regDB = 0;
                        _regPB = 0;
                        RegSH = 0x01;
                        RegXH = 0x00;
                        RegYH = 0x00;
                        SetFlag(StatusFlags.M | StatusFlags.X | StatusFlags.I, true);
                        SetFlag(StatusFlags.D, false);
                        _flagE = true;
                        _cycleQueue.Clear();
                        EnqueueMultiple(VectorJump(this, W65C816.Vector.Reset));
                    }
                }
            }

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
                List<IMicroOp> MicroOps { get; } = new List<IMicroOp>();
                public Cycle(Processor proc, CycleType type, List<IMicroOp> actions, uint? address = null)
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
                        microOp.Execute(proc: Processor);
                    }
                    if (Type == CycleType.Write)
                    {
#pragma warning disable CS8629 // Nullable value type may be null.
                        Processor._core.mapper[Address.Value] = Processor._dataBus;
#pragma warning restore CS8629 // Nullable value type may be null.
                    }
                }
            }

            private interface IMicroOp
            {
                void Execute(Processor proc);
            }

        }

        public struct ProcessorState
        {
            public ushort A;
            public ushort X;
            public ushort Y;
            public ushort DP;
            public ushort SP;
            public byte DB;
            public byte PB;
            public ushort PC;
            public byte SR;
            public bool E;

            public override string ToString()
            {
                return $"A:{A:X4} X:{X:X4} Y:{Y:X4} DP:{DP:X4} SP:{SP:X4} DB:{DB:X2} PB:{PB:X2} PC:{PC:X4} SR:{SR:X2} E:{E}";
            }
        }

        public struct Cycle
        {
            public enum CycleType
            {
                Internal,
                Read,
                Write,
            }
            public enum ClockState
            {
                Running,
                Stopped,
                Waiting,
            }
            public int CycleCount;
            public CycleType Type;
            public uint AddressBus;
            public byte DataBus;

        }
    }
}
