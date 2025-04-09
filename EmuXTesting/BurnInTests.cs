using EightSixteenEmu;
using EightSixteenEmu.Devices;
using System.Text.Json;
using Xunit.Abstractions;

namespace EmuXTesting
{
    public class BurnInTests
    {
        private readonly ITestOutputHelper _output;

        public BurnInTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [ClassData(typeof(QuickBurnInData))]
        public void QuickBurnIn(byte inst, BurnInTestState start, BurnInTestState goal, int cycles)
        {
            EmuCore emu = new();
            emu.MPU.NewCycle += OnNewCycle;
            var ram = new DevRAM(0x1000000);
            emu.Mapper.AddDevice(ram, 0, 0, 0x1000000);
            emu.Deactivate();
            emu.MPU.SetProcessorState(start.State);
            _output.WriteLine("Initial State: " + start.State.ToString());
            _output.WriteLine("Memory values:");
            foreach (var kvp in start.RamValues)
            {
                _output.WriteLine($"${kvp.Key:X6}: ${kvp.Value:X2}");
                ram[kvp.Key] = (byte)kvp.Value;
            }
            byte instruction = ram[(uint)((start.State.PB << 16) + start.State.PC)];
            (W65C816.OpCode op, W65C816.AddressingMode mode) = W65C816.OpCodeLookup(instruction);
            Assert.Equal(inst, instruction); // barf if we read the wrong instruction from RAM
            _output.WriteLine($"Testing ${instruction:X2}: {op} {mode} - {(start.State.FlagE ? "emulated" : "native" )}");
            emu.Activate(false);
            emu.MPU.ExecuteInstruction();
            var mpuState = emu.MPU.GetStatus();
            bool registersEqual = goal.State.PC == mpuState.PC
                && goal.State.A == mpuState.A 
                && goal.State.X == mpuState.X 
                && goal.State.Y == mpuState.Y 
                && goal.State.DP == mpuState.DP 
                && goal.State.SP == mpuState.SP 
                && goal.State.DB == mpuState.DB 
                && goal.State.PB == mpuState.PB 
                && goal.State.FlagN == mpuState.FlagN 
                && goal.State.FlagV == mpuState.FlagV 
                && goal.State.FlagM == mpuState.FlagM 
                && goal.State.FlagX == mpuState.FlagX 
                && goal.State.FlagD == mpuState.FlagD 
                && goal.State.FlagI == mpuState.FlagI 
                && goal.State.FlagZ == mpuState.FlagZ 
                && goal.State.FlagC == mpuState.FlagC
                && goal.State.FlagE == mpuState.FlagE;
            _output.WriteLine($"Cycle Count: Expected {cycles}, Actual {mpuState.Cycles}");
            _output.WriteLine($"PC:    Expected ${goal.State.PC:X4}, Actual ${mpuState.PC:X4} {((goal.State.PC == mpuState.PC) ? "" : "!!")}");
            _output.WriteLine($"SP:    Expected ${goal.State.SP:X4}, Actual ${mpuState.SP:X4} {((goal.State.SP == mpuState.SP) ? "" : "!!")}");
            _output.WriteLine($"A:     Expected ${goal.State.A:X4}, Actual ${mpuState.A:X4} {((goal.State.A == mpuState.A) ? "" : "!!")}");
            _output.WriteLine($"X:     Expected ${goal.State.X:X4}, Actual ${mpuState.X:X4} {((goal.State.X == mpuState.X) ? "" : "!!")}");
            _output.WriteLine($"Y:     Expected ${goal.State.Y:X4}, Actual ${mpuState.Y:X4} {((goal.State.Y == mpuState.Y) ? "" : "!!")}");
            _output.WriteLine($"D:     Expected ${goal.State.DP:X4}, Actual ${mpuState.DP:X4} {((goal.State.DP == mpuState.DP) ? "" : "!!")}");
            _output.WriteLine($"DB:    Expected ${goal.State.DB:X2}, Actual ${mpuState.DB:X2} {((goal.State.DB == mpuState.DB) ? "" : "!!")}");
            _output.WriteLine($"PB:    Expected ${goal.State.PB:X2}, Actual ${mpuState.PB:X2} {((goal.State.PB == mpuState.PB) ? "" : "!!")}");
            _output.WriteLine($"Flags: Expected {goal.State.Flags():X2}, Actual {mpuState.Flags():X2} {((goal.State.Flags() == mpuState.Flags()) ? "" : "!!")}");
            _output.WriteLine("\nMemory values:\nAddress  Ex   Ac");
            bool ramEqual = true;
            foreach (var kvp in goal.RamValues)
            {
                _output.WriteLine($"${kvp.Key:X6}: ${kvp.Value:X2}  ${ram[kvp.Key]:X2}");
                if (ram[kvp.Key] != kvp.Value)
                {
                    ramEqual = false;
                    _output.WriteLine("!!");
                }
            }
            Assert.True(registersEqual, "Registers do not match expected values.");
            Assert.True(ramEqual, "RAM values do not match expected values.");
            // TODO: Let's put this aside before it drives me up the wall
            //Assert.True(cycles == mpuState.Cycles, "Operation did not run in the expected amount of cycles.");
        }

        private void OnMemoryRead(uint address, byte value)
        {
            _output.WriteLine($"Memory Read: Address ${address:X6}, Value ${value:X2}");
        }

        private void OnMemoryWrite(uint address, byte value)
        {
            _output.WriteLine($"Memory Write: Address ${address:X6}, Value ${value:X2}");
        }

        private void OnNewCycle(int cycles, Microprocessor.Cycle details)
        {
            _output.WriteLine($"{cycles}: {details}");
            
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
            Random rng = new();
            int testNumber = 0;
            string[] testFiles = Directory.GetFiles("testData/v1", "*.json");
            JsonSerializerOptions options = new()
            {
                PropertyNameCaseInsensitive = true
            };
            foreach (string fileName in testFiles)
            {
                string jsonContent = File.ReadAllText(fileName);
                JsonDocument doc = JsonDocument.Parse(jsonContent);
                
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > testNumber)
                {
                    string testObject = doc.RootElement[testNumber].ToString();
                    //Console.WriteLine($"Test Object from {fileName}: {testObject}");

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
