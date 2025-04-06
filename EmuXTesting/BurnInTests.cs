using EightSixteenEmu;
using EightSixteenEmu.Devices;
using System.Text.Json;

namespace EmuXTesting
{
    public class BurnInTests
    {
        [Theory]
        [ClassData(typeof(QuickBurnInData))]
        public void QuickBurnIn(byte inst, BurnInTestState start, BurnInTestState goal, int cycles)
        {
            EmuCore emu = new EmuCore();
            var ram = new DevRAM(0x1000000);
            emu.Mapper.AddDevice(ram, 0, 0, 0x1000000);
            emu.Deactivate();
            emu.MPU.SetProcessorState(start.State);
            foreach (var kvp in start.RamValues)
            {
                ram[kvp.Key] = (byte)kvp.Value;
            }
            byte instruction = ram[(uint)((start.State.PB << 16) + start.State.PC)];
            Assert.Equal(inst, instruction); // barf if we read the wrong instruction from RAM
            (W65C816.OpCode op, W65C816.AddressingMode mode) = W65C816.OpCodeLookup(instruction);
            Console.WriteLine($"Testing ${instruction:X2}: {op} {mode} - {(start.State.FlagE ? "emulated" : "native" )}");

            emu.Activate(false);
            emu.MPU.ExecuteInstruction();
            var mpuState = emu.MPU.GetStatus();
            // Assert.Equal(cycles, mpuState.Cycles);
            Assert.Equal(goal.State.PC, mpuState.PC);
            Assert.Equal(goal.State.SP, mpuState.SP);
            Assert.Equal(goal.State.A, mpuState.A);
            Assert.Equal(goal.State.X, mpuState.X);
            Assert.Equal(goal.State.Y, mpuState.Y);
            Assert.Equal(goal.State.DP, mpuState.DP);
            Assert.Equal(goal.State.DB, mpuState.DB);
            Assert.Equal(goal.State.PB, mpuState.PB);
            Assert.Equal(goal.State.FlagN, mpuState.FlagN);
            Assert.Equal(goal.State.FlagV, mpuState.FlagV);
            Assert.Equal(goal.State.FlagM, mpuState.FlagM);
            Assert.Equal(goal.State.FlagX, mpuState.FlagX);
            Assert.Equal(goal.State.FlagD, mpuState.FlagD);
            Assert.Equal(goal.State.FlagI, mpuState.FlagI);
            Assert.Equal(goal.State.FlagZ, mpuState.FlagZ);
            Assert.Equal(goal.State.FlagC, mpuState.FlagC);
            Assert.Equal(goal.State.FlagE, mpuState.FlagE);
            foreach (var kvp in goal.RamValues)
            {
                Assert.Equal(kvp.Value, ram[kvp.Key]);
            }
        }

        /*
        [Theory]
        public void FullBurnIn()
        {
            // this is going to execute 5,120,000 instructions. God help me.
            // should I have the classdata be by file?
        }
        */


    }

    public class QuickBurnInData : TheoryData<byte, BurnInTestState, BurnInTestState, int>
    {
        /* Goal: After setting up the emulator with the first GauntletTestState, one instruction is run.
         * The state of the emulator is then compared to the second GauntletTestState, and the cycles counter is compared to the int.
         * If the test passes, the behavior of the instruction is identical to the behavior of the real hardware.
         * 
         * Unfortunately, the test jsons have filenames that are just the hex values of the instruction --
         * so we'll want to have some way to tell what instruction was being tested instead of running to the datasheet
         * and finding the instruction mnemonic.
         * 
         * i.e. "af.n.json"'s friendly name becomes "LDA #FEDBCA - Native" or something like that.
         * Maybe write that to the console when the test is run.
         */


        public QuickBurnInData()
        {
            Random rng = new Random();
            int testNumber = 0;
            string[] testFiles = Directory.GetFiles("testData/v1", "*.json");
            foreach (string fileName in testFiles)
            {
                string jsonContent = File.ReadAllText(fileName);
                JsonDocument doc = JsonDocument.Parse(jsonContent);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > testNumber)
                {
                    string testObject = doc.RootElement[testNumber].ToString();
                    Console.WriteLine($"Test Object from {fileName}: {testObject}");

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    BurnInParameters? parameters = JsonSerializer.Deserialize<BurnInParameters>(testObject, options);
                    if (parameters != null)
                    {
                        byte inst = byte.Parse(Path.GetFileNameWithoutExtension(fileName).Substring(0,2), System.Globalization.NumberStyles.HexNumber);
                        BurnInTestState start = CreateBurnInTestState(parameters.Initial);
                        BurnInTestState goal = CreateBurnInTestState(parameters.Final);
                        int cycles = parameters.Cycles.Count;
                        Add(inst, start, goal, cycles);
                    }
                    else
                    {
                        throw new JsonException("Failed to deserialize JSON object.");
                    }
                }
            }
        }

        private static BurnInTestState CreateBurnInTestState(BurnInMpuState state)
        {
            var microprocessorState = new Microprocessor.MicroprocessorState
            {
                Cycles = 0,
                PC = (ushort)state.PC,
                SP = (ushort)state.S,
                A = (ushort)state.A,
                X = (ushort)state.X,
                Y = (ushort)state.Y,
                DP = (ushort)state.D,
                DB = (byte)state.DBR,
                PB = (byte)state.PBR,
                FlagN = (state.P & 0x80) != 0,
                FlagV = (state.P & 0x40) != 0,
                FlagM = (state.P & 0x20) != 0,
                FlagX = (state.P & 0x10) != 0,
                FlagD = (state.P & 0x08) != 0,
                FlagI = (state.P & 0x04) != 0,
                FlagZ = (state.P & 0x02) != 0,
                FlagC = (state.P & 0x01) != 0,
                FlagE = state.E != 0,
            };
            var ramValues = new Dictionary<uint, byte>();
            foreach (var ram in state.RAM)
            {
                ramValues[(uint)ram[0]] = (byte)ram[1];
            }
            return new BurnInTestState(microprocessorState, ramValues);
        }
    }

    public class BurnInTestState(Microprocessor.MicroprocessorState state, Dictionary<uint, byte> ramValues)
    {
        public readonly Microprocessor.MicroprocessorState State = state;
        public readonly Dictionary<uint, byte> RamValues = ramValues;
    }

    public class BurnInParameters
    {
        public string Name { get; set; }
        public BurnInMpuState Initial { get; set; }
        public BurnInMpuState Final { get; set; }
        public List<List<object>> Cycles { get; set; }
    }

    public class BurnInMpuState
    {
        public int PC { get; set; }
        public int S { get; set; }
        public int P { get; set; }
        public int A { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int DBR { get; set; }
        public int D { get; set; }
        public int PBR { get; set; }
        public int E { get; set; }
        public List<List<int>> RAM { get; set; }
    }

}
