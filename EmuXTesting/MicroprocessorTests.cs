using EightSixteenEmu;
using EightSixteenEmu.Devices;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Xunit.Abstractions;

namespace EmuXTesting
{
    public class MicroprocessorTests
    {

        private readonly ITestOutputHelper _output;
        List<Microprocessor.Cycle> executionCycles;
        List<Microprocessor.MicroprocessorState> microprocessorStates;

        public MicroprocessorTests(ITestOutputHelper output)
        {
            _output = output;
            executionCycles = [];
            microprocessorStates = [];
        }

        /*
         * Okay, so let's think about what we need externally for the HW interrupt tests. There are four "hardware" interrupts:
         * Reset, Abort, IRQ, and NMI -- although we don't really need to bother with Abort since, to my knowledge, there
         * haven't been any '816 builds that actually used that line. (For completeness's sake: Abort is a hardware interrupt
         * that, when triggered, finishes the current instruction but in a way that does not affect the CPU state or memory--
         * basically turns the current operation into a NOP in most cases). 
         */

        const string romFilePrefix = "interruptTests";
        const ushort startStackPointer = 0x01FF; // the stack pointer is initialized to 0x01FF, and decremented with the first push
        const ushort goalNativeStackPointer = 0x01FB;
        const ushort goalEmulatedStackPointer = 0x01FC;
        [Fact]
        public void Reset_ShouldLoadProperVector()
        {
            /* 
             * Reset is a special little "interrupt" because it doesn't push anything to the stack, and there's only one vector
             * (since a reset always puts the '816 in emulation mode). According to the datasheet, the reset line needs to be
             * held low for two clock cycles -- do I need to simulate that? Probably not. The registers that are affected by
             * Reset are:
             * D = 0x0000
             * DB = 0x00
             * PB = 0x00
             * SH = 0x01
             * XH = 0x00
             * YH = 0x00
             * SL, XL, YL, and A are untouched, and can be treated as "don't care" values.
             * As for the status register, M, X, I, and E are all set, while D is cleared. N, V, Z, and C are unaffected, and
             * can also be treated as "don't care" values.
             */

            // Arrange
            EmuCore emu = new();
            emu.MPU.NewCycle += OnNewCycle;
            emu.MPU.NewInstruction += OnNewInstruction;
            var ram = new DevRAM(0x8000);
            emu.Mapper.AddDevice(ram, 0x0000);
            var rom = new DevROM(romFilePrefix + ".rom");
            emu.Mapper.AddDevice(rom, 0x8000);

            Microprocessor.MicroprocessorState startState = new()
            {
                PB = 0x0b,
                PC = 0xbabe,
                A = 0xcafe,
                X = 0xdead,
                Y = 0xbeef,
                DB = 0xec,
                DP = 0xd00d,
                SP = 0xecc0,
                FlagC = true,
                FlagD = true,
                FlagV = true
            };
            emu.MPU.SetProcessorState(startState);
            _output.WriteLine($"Start state: {emu.MPU.Status}");

            // Act
            emu.Activate(false);
            emu.MPU.Reset();
            var mpuState = emu.MPU.Status;

            // Assert
            _output.WriteLine($"Reset state: {emu.MPU.Status}");
            Assert.Equal(0x01, emu.MPU.RegSH);
            Assert.Equal(0x00, emu.MPU.RegXH);
            Assert.Equal(0x00, emu.MPU.RegYH);
            Assert.Equal(0x00, emu.MPU.RegPB);
            Assert.Equal(0x00, emu.MPU.RegDB);
            Assert.Equal(0x0000, emu.MPU.RegDP);
            Assert.Equal(0x8000, emu.MPU.RegPC);
            Assert.True(mpuState.FlagM);
            Assert.True(mpuState.FlagX);
            Assert.True(mpuState.FlagI);
            Assert.True(mpuState.FlagE);
            Assert.False(mpuState.FlagD);
        }

        /* As for the other hardware interrupts, their mechanics are quite like the COP and BRK instructions, so much so
         * that they are actually implemented as such in the emulator (ProcessorStateRunning.Interrupt in ProcessorState.cs).
         * NMI, being non-maskable, only needs two test cases: one for native and one for emulated mode. IRQ, on the other hand,
         * depends on both the I flag and whether the processor is in the running or waiting state. If I is clear, IRQs are not
         * masked, and the processor will handle them as normal in both states (note that there are different vector locations
         * for native and emulated mode). If I is set, the processor will ignore IRQs in the running state, but will
         * have special behavior in the waiting state. if I is set and the processor receives an IRQ while waiting, it will
         * execute the instruction following the WAI. So, what we need to test is:
         * NMI, emulated mode
         * NMI, native mode
         * IRQ, emulated mode, I = 0, running state
         * IRQ, emulated mode, I = 0, waiting state
         * IRQ, emulated mode, I = 1, running state
         * IRQ, emulated mode, I = 1, waiting state
         * IRQ, native mode, I = 0, running state
         * IRQ, native mode, I = 0, waiting state
         * IRQ, native mode, I = 1, running state
         * IRQ, native mode, I = 1, waiting state
         * (Yes, IRQ when in the waiting mode with I set should act the same in both native and emulated mode, but we should
         * be thorough and test both.)
         * 
         * During the arrange phase, byte 0x000000 should be set to configure the processor into the state we need
         * We'll be using:
         * bit 7 - the desired state of the emulation mode flag
         * bit 6 - the desired state of the I (interrupt disable) flag
         * bit 5 - if set, use waiting state, loop otherwise
         * 
         * TODO: check to see how to export symbols from ca65/ld65 so we can check PC register
         */

        [Fact]
        public void NativeNMI_ShouldJumpProperly()
        {
            // Arrange
            const ushort expectedA = 0xbeef;
            var labels = LabelParser.ParseLabels(romFilePrefix + ".listing.txt", romFilePrefix + ".mapfile.txt");
            ushort expectedPC = (ushort)labels["nativenmi"];
            EmuCore emu = new();
            emu.MPU.NewCycle += OnNewCycle;
            emu.MPU.NewInstruction += OnNewInstruction;
            var ram = new DevRAM(0x8000);
            emu.Mapper.AddDevice(ram, 0x0000);
            var rom = new DevROM(romFilePrefix + ".rom");
            emu.Mapper.AddDevice(rom, 0x8000);

            // Act
            emu.Activate();
            do {
                emu.MPU.ExecuteInstruction();
                _output.WriteLine(emu.MPU.Status.ToString());
                if (emu.MPU.Cycles >= 1000) Assert.Fail("Timeout!");
            } while (emu.MPU.Status.X == 0x00);
            emu.MPU.SetNMI();
            _output.WriteLine("\n!NMI!");
            emu.MPU.ExecuteInstruction(); // fires the interrupt handler
            var actualPC = emu.MPU.Status.PC;
            _output.WriteLine(emu.MPU.Status.ToString());
            emu.MPU.ExecuteInstruction();
            _output.WriteLine(emu.MPU.Status.ToString());

            // Assert
            Assert.Equal(expectedPC, actualPC);
            Assert.Equal(expectedA, emu.MPU.Status.A);
            Assert.Equal(goalNativeStackPointer, emu.MPU.Status.SP);
            Assert.True(emu.MPU.Status.FlagI);
            Assert.False(emu.MPU.Status.FlagD);

        }

        [Fact]
        public void EmulatedNMI_ShouldJumpProperly()
        {
            // Arrange
            const ushort expectedA = 0x00d0;
            var labels = LabelParser.ParseLabels(romFilePrefix + ".listing.txt", romFilePrefix + ".mapfile.txt");
            ushort expectedPC = (ushort)labels["emunmi"];
            EmuCore emu = new();
            emu.MPU.NewCycle += OnNewCycle;
            emu.MPU.NewInstruction += OnNewInstruction;
            var ram = new DevRAM(0x8000);
            ram[0] = 0x80; // set emulation mode
            emu.Mapper.AddDevice(ram, 0x0000);
            var rom = new DevROM(romFilePrefix + ".rom");
            emu.Mapper.AddDevice(rom, 0x8000);

            // Act
            emu.Activate();
            do
            {
                emu.MPU.ExecuteInstruction();
                _output.WriteLine(emu.MPU.Status.ToString());
                if (emu.MPU.Cycles >= 1000) Assert.Fail("Timeout!");
            } while (emu.MPU.Status.X == 0x00);
            emu.MPU.SetNMI();
            _output.WriteLine("\n!NMI!");
            emu.MPU.ExecuteInstruction(); // fires the interrupt handler
            var actualPC = emu.MPU.Status.PC;
            _output.WriteLine(emu.MPU.Status.ToString());
            emu.MPU.ExecuteInstruction();
            _output.WriteLine(emu.MPU.Status.ToString());

            // Assert
            Assert.Equal(expectedPC, actualPC);
            Assert.Equal(expectedA, emu.MPU.Status.A);
            Assert.Equal(goalEmulatedStackPointer, emu.MPU.Status.SP);
            Assert.True(emu.MPU.Status.FlagI);
            Assert.False(emu.MPU.Status.FlagD);
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, false)]
        [InlineData(false, true, true)]
        [InlineData(true, false, false)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        public void IRQ_ShouldBehaveProperly(bool emulated, bool interruptsDisabled, bool isWaiting)
        {
            ushort expectedA;
            ushort expectedY;
            ushort expectedSP;
            ushort expectedPC;
            //Arrange
            var labels = LabelParser.ParseLabels(romFilePrefix + ".listing.txt", romFilePrefix + ".mapfile.txt");
            byte signature = (byte)((emulated ? (byte)0x80 : (byte)0x00) 
                | (interruptsDisabled ? (byte)0x40 : (byte)0x00) 
                | (isWaiting ? (byte)0x20 : (byte)0x00));

            expectedA = (ushort)(isWaiting ? (emulated ? 0x000e : 0x0b0e) : 0x00);
            if (interruptsDisabled)
            {  
                expectedPC = (ushort)(isWaiting ? (labels["goWait"] + 4) : labels["spin"]);
                expectedSP = startStackPointer;
                expectedY = 0x00;
            }
            else if (emulated)
            {
                expectedPC = (ushort)(labels["emuirq"]);
                expectedSP = goalEmulatedStackPointer;
                expectedY = 0xab;
            }
            else
            {
                expectedPC = (ushort)(labels["nativeirq"]);
                expectedSP = goalNativeStackPointer;
                expectedY = 0xcafe;
            }
            EmuCore emu = new();
            emu.MPU.NewCycle += OnNewCycle;
            emu.MPU.NewInstruction += OnNewInstruction;
            var ram = new DevRAM(0x8000);
            ram[0] = signature;
            emu.Mapper.AddDevice(ram, 0x0000);
            var rom = new DevROM(romFilePrefix + ".rom");
            var dummyDevice = new DummyInterruptingDevice();
            emu.Mapper.AddInterruptingDevice(dummyDevice);
            emu.Mapper.AddDevice(rom, 0x8000);

            // Act
            emu.Activate();
            do
            {
                emu.MPU.ExecuteInstruction();
                _output.WriteLine(emu.MPU.Status.ToString());
                if (emu.MPU.Cycles >= 1000) Assert.Fail("Timeout!");
            } while (isWaiting ? emu.MPU.ExecutionState != "ProcessorStateWaiting": (emu.MPU.Status.X == 0x00));
            var currentX = emu.MPU.Status.X;
            dummyDevice.Interrupting = true;
            _output.WriteLine("\n!IRQ!");
            emu.MPU.ExecuteInstruction(); // fires the interrupt handler OR loads the magic number if I is set
            var actualPC = emu.MPU.Status.PC;
            var actualSP = emu.MPU.Status.SP;
            var actualI = emu.MPU.Status.FlagI;
            var actualD = emu.MPU.Status.FlagD;
            dummyDevice.Interrupting = false;
            _output.WriteLine(emu.MPU.Status.ToString());
            do
            {
                emu.MPU.ExecuteInstruction();
                _output.WriteLine(emu.MPU.Status.ToString());
                if (emu.MPU.Cycles >= 1000) Assert.Fail("Timeout!");
            } while (emu.MPU.Status.X == currentX);

            // Assert
            Assert.Equal(expectedPC, actualPC);
            Assert.Equal(expectedSP, actualSP);
            Assert.Equal(expectedA, emu.MPU.Status.A);
            Assert.Equal(expectedY, emu.MPU.Status.Y);
            Assert.True(actualI);
            Assert.False(actualD);
        }

        private void OnNewInstruction(W65C816.OpCode opCode, string operand)
        {
            _output.WriteLine("");
            _output.WriteLine($"Instruction: {opCode} {operand}");
        }

        private void OnNewCycle(int cycles, Microprocessor.Cycle details, Microprocessor.MicroprocessorState state)
        {
            executionCycles.Add(details);
            microprocessorStates.Add(state);
            _output.WriteLine($"  Cycle {cycles}: {details.Address:X6} {details.Value:X2} {details.Type}");
        }

        class DummyInterruptingDevice : IInterruptingDevice
        {
            public event EventHandler<bool>? InterruptStatusChanged;
            private bool _interrupting;
            public bool Interrupting
            {
                get => _interrupting;
                set
                {
                    if (_interrupting != value) InterruptStatusChanged?.Invoke(this, value);
                    _interrupting = value;
                }
            }
            public uint Size => 1;
            public void Reset()
            {
                // Dummy implementation for testing
            }

        }

        /*
         * Finally, we have the dreaded free-running clock. This will be a headache, since threads will be involved and
         * I have no clue on earth how to test it. In all honestly, I'll probably make sure that the logic is correct
         * in instruction-step mode first, and then design the test suite for free-running clock.
         */
    }
}
