using EightSixteenEmu;
using EightSixteenEmu.Devices;
using EightSixteenEmu.MPU;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace EmuXTesting
{
    public class BurnInTests
    {
        private readonly ITestOutputHelper _output;
        List<Microprocessor.Cycle> executionCycles;
        List<Microprocessor.MicroprocessorState> microprocessorStates;

        public BurnInTests(ITestOutputHelper output)
        {
            _output = output;
            executionCycles = [];
            microprocessorStates = [];
        }

        /*
         * Okay, why are we excluding the block move tests?
         * Simply put, the JSON data is broken. It seems to just be the first 100 cycles of the test. The nature of the block moves
         * means that the accumulator will always be 0xFFFF after the copy is finished. The tests in the JSON set the accumulator to
         * something like 0xFE8B (65,163 bytes!!) and then the goal state is set after only 100 cycles, meaning only about 14 or so
         * bytes are copied at test's end!
         * What a shame. I'll probably write a test for block move instructions that use a ROM file instead of JSON data.
         */
        //[Theory]
        //[ClassData(typeof(QuickBurnInData))]
        //public void QuickBurnIn(BurnInTest test)
        //{
        //    if (test.Inst == 0x54 || test.Inst == 0x44) Assert.Fail("Block Move test data is broken.");
        //    executionCycles.Clear();
        //    EmuCore emu = new();
        //    emu.MPU.NewCycle += OnNewCycle;
        //    var ram = new DevRAM(0x1000000);
        //    emu.Mapper.AddDevice(ram, 0, 0, 0x1000000);
        //    emu.MPU.SetProcessorState(test.Start.State);
        //    _output.WriteLine("Initial State: " + test.Start.State.ToString());
        //    _output.WriteLine("Memory values:");
        //    foreach (var kvp in test.Start.RamValues)
        //    {
        //        _output.WriteLine($"${kvp.Key:X6}: ${kvp.Value:X2}");
        //        ram[kvp.Key] = (byte)kvp.Value;
        //    }
        //    byte instruction = ram[(uint)((test.Start.State.PB << 16) + test.Start.State.PC)];
        //    (W65C816.OpCode op, W65C816.AddressingMode mode) = W65C816.OpCodeLookup(instruction);
        //    Assert.Equal(test.Inst, instruction); // barf if we read the wrong instruction from RAM
        //    _output.WriteLine($"Testing ${instruction:X2}: {op} {mode} - {(test.Start.State.FlagE ? "emulated" : "native")}");
        //    emu.Activate(false);
        //    emu.MPU.ExecuteInstruction();
        //    _output.WriteLine($"Cyc |{"Expected",-21}|{"Actual",-21}");
        //    _output.WriteLine($"    |{"Address",-7} {"Val",-3} {"Type",-9}|{"Address",-7} {"Val",-3} {"Type",-9}");
        //    for (int i = 0; i < Math.Max(test.Cycles.Count, executionCycles.Count); i++)
        //    {
        //        string s = $"{i,3} |";
        //        if (i < test.Cycles.Count)
        //        {
        //            s += $"${test.Cycles[i].Address:X6} ${test.Cycles[i].Value:X2} {test.Cycles[i].Type,-9}|";
        //        }
        //        else
        //        {
        //            s += $"{"N/A",-21}|";
        //        }
        //        if (i < executionCycles.Count)
        //        {
        //            s += $"${executionCycles[i].Address:X6} ${executionCycles[i].Value:X2} {executionCycles[i].Type,-9}";
        //        }
        //        else
        //        {
        //            s += $"{"N/A",-21}";
        //        }
        //        _output.WriteLine(s);
        //    }
        //    var mpuState = emu.MPU.GetStatus();
        //    bool registersEqual = test.Goal.State.PC == mpuState.PC
        //        && test.Goal.State.A == mpuState.A
        //        && test.Goal.State.X == mpuState.X
        //        && test.Goal.State.Y == mpuState.Y
        //        && test.Goal.State.DP == mpuState.DP
        //        && test.Goal.State.SP == mpuState.SP
        //        && test.Goal.State.DB == mpuState.DB
        //        && test.Goal.State.PB == mpuState.PB;

        //    bool ignoreV = (op == W65C816.OpCode.ADC || op == W65C816.OpCode.SBC) && mpuState.FlagD;
        //    bool vMatch = ignoreV || test.Goal.State.FlagV == mpuState.FlagV;

        //    bool checkM = !mpuState.FlagE;
        //    bool mMatch = !checkM || test.Goal.State.FlagM == mpuState.FlagM;

        //    bool flagsEqual = test.Goal.State.FlagN == mpuState.FlagN
        //        && vMatch
        //        && mMatch
        //        && test.Goal.State.FlagX == mpuState.FlagX
        //        && test.Goal.State.FlagD == mpuState.FlagD
        //        && test.Goal.State.FlagI == mpuState.FlagI
        //        && test.Goal.State.FlagZ == mpuState.FlagZ
        //        && test.Goal.State.FlagC == mpuState.FlagC
        //        && test.Goal.State.FlagE == mpuState.FlagE;

        //    _output.WriteLine($"Cycle Count: Expected {test.Cycles.Count}, Actual {mpuState.Cycles}");
        //    _output.WriteLine($"PC:    Expected ${test.Goal.State.PC:X4}, Actual ${mpuState.PC:X4} {((test.Goal.State.PC == mpuState.PC) ? "" : "!!")}");
        //    _output.WriteLine($"SP:    Expected ${test.Goal.State.SP:X4}, Actual ${mpuState.SP:X4} {((test.Goal.State.SP == mpuState.SP) ? "" : "!!")}");
        //    _output.WriteLine($"A:     Expected ${test.Goal.State.A:X4}, Actual ${mpuState.A:X4} {((test.Goal.State.A == mpuState.A) ? "" : "!!")}");
        //    _output.WriteLine($"X:     Expected ${test.Goal.State.X:X4}, Actual ${mpuState.X:X4} {((test.Goal.State.X == mpuState.X) ? "" : "!!")}");
        //    _output.WriteLine($"Y:     Expected ${test.Goal.State.Y:X4}, Actual ${mpuState.Y:X4} {((test.Goal.State.Y == mpuState.Y) ? "" : "!!")}");
        //    _output.WriteLine($"D:     Expected ${test.Goal.State.DP:X4}, Actual ${mpuState.DP:X4} {((test.Goal.State.DP == mpuState.DP) ? "" : "!!")}");
        //    _output.WriteLine($"DB:    Expected ${test.Goal.State.DB:X2}, Actual ${mpuState.DB:X2} {((test.Goal.State.DB == mpuState.DB) ? "" : "!!")}");
        //    _output.WriteLine($"PB:    Expected ${test.Goal.State.PB:X2}, Actual ${mpuState.PB:X2} {((test.Goal.State.PB == mpuState.PB) ? "" : "!!")}");
        //    _output.WriteLine($"Flags: Expected {test.Goal.State.Flags()}, Actual {mpuState.Flags()} {(flagsEqual ? "" : "!!")}");
        //    _output.WriteLine("\nMemory values:\nAddress  Ex   Ac");
        //    bool ramEqual = true;
        //    foreach (var kvp in test.Goal.RamValues)
        //    {
        //        _output.WriteLine($"${kvp.Key:X6}: ${kvp.Value:X2}  ${ram[kvp.Key]:X2}");
        //        if (ram[kvp.Key] != kvp.Value)
        //        {
        //            ramEqual = false;
        //            _output.WriteLine("!!");
        //        }
        //    }
        //    Assert.True(registersEqual, "Registers do not match expected values.");
        //    Assert.True(flagsEqual, "Flags do not match expected values.");
        //    Assert.True(ramEqual, "RAM values do not match expected values.");
        //    // TODO: Let's put this aside before it drives me up the wall
        //    // Time to figure out where to put Internal cycles
        //    Assert.True(test.Cycles.Count == executionCycles.Count, "Operation did not run in the expected amount of cycles.");
        //}

        

        [Fact]
        public void MvnMvpInstructions_WorkProperly()
        {
            // Arrange
            byte[] data =
            {
                0x0f, 0xf0, 0xf0, 0xf0, 0x0f, 0xf0, 0xf0, 0xf0,
                0x34, 0x12, 0x78, 0x56, 0xbc, 0x9a, 0xf0, 0xde,
                0x80, 0x7f, 0x80, 0x7f, 0x80, 0x7f, 0x80, 0x7f,
                0x80, 0x7f, 0x80, 0x7f, 0x80, 0x7f, 0x80, 0x7f,
            };
            // d'oh! forgot about the endianess...

            EmuCore emu = new();
            emu.MPU.NewCycle += OnNewCycle;
            emu.MPU.NewInstruction += OnNewInstruction;
            var rom = new DevROM("MoveTests.rom");
            emu.Mapper.AddDevice(rom, 0x8000);
            var ram = new DevRAM(0x1000000);
            emu.Mapper.AddDevice(ram, 0, 0, 0x8000);
            emu.Mapper.AddDevice(ram, 0x10000, 0x10000, 0x40000);

            string s = "";
            _output.WriteLine("ROM data:");
            for (int i = 0; i < data.Length; i++)
            {
                s += $"{emu.Mapper[(uint)(0x8100 + i)]:X2} ";
            }
            _output.WriteLine(s);

            // Act
            _output.WriteLine("");
            _output.WriteLine("Resetting MPU...");
            emu.MPU.Reset();
            while (emu.MPU.ExecutionState == "ProcessorStateRunning")
            {
                emu.MPU.ExecuteInstruction();
                if (emu.MPU.Status.Cycles > 2500)
                {
                    _output.WriteLine("Execution timed out.");
                    break;
                }
            }
            _output.WriteLine("End of execution.");

            // Assert
            _output.WriteLine("Final State:");
            s = "MVN: ";
            byte[] mvnRange = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                mvnRange[i] = emu.Mapper[(uint)(0x0200 + i)] ?? 0xFF;
                s += $"{mvnRange[i]:X2} ";
            }
            _output.WriteLine(s);

            s = "MVP: ";
            byte[] mvpRange = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                mvpRange[i] = emu.Mapper[(uint)(0x010000 + i)] ?? 0xFF;
                s += $"{mvpRange[i]:X2} ";
            }
            _output.WriteLine(s);

            Assert.Equal(data, mvnRange);
            Assert.Equal(data, mvpRange);
            _output.WriteLine("MVN and MVP instructions executed correctly. Check cycle counts.");

            void OnNewCycle(int cycles, Microprocessor.Cycle details, Microprocessor.MicroprocessorState state)
            {
                executionCycles.Add(details);
                microprocessorStates.Add(state);
                _output.WriteLine($"  Cycle {cycles}: {details.Address:X6} {details.Value:X2} {details.Type}");
                _output.WriteLine($"   PB PC: {state.PB:X2} {state.PC:X4} SP: {state.SP:X4} A: {state.A:X4} X: {state.X:X4} Y: {state.Y:X4} DB: {state.DB:X2} DP: {state.DP:X2}");
                _output.WriteLine($"   Flags: {state.Flags()}");
            }
             void OnNewInstruction(W65C816.OpCode opCode, string operand)
            {
                _output.WriteLine("");
                _output.WriteLine($"Instruction: {opCode} {operand}");
            }
        }

        [Theory]
        [ClassData(typeof(FullBurnInFile))]
        public void FullBurnIn(string fileName)
        {
            // this is going to execute 5,120,000 instructions. God help me.
            var inst = byte.Parse(Path.GetFileNameWithoutExtension(fileName)[..2], System.Globalization.NumberStyles.HexNumber);
            (W65C816.OpCode op, W65C816.AddressingMode mode) = W65C816.OpCodeLookup(inst);

            _output.WriteLine($"Testing ${Path.GetFileNameWithoutExtension(fileName)}: {op} {mode}");

            EmuCore emu = new();
            emu.MPU.NewCycle += OnNewCycle;
            var ram = new DevRAM(0x1000000);
            emu.Mapper.AddDevice(ram, 0, 0, 0x1000000);
            BurnInTest test;
            int testRun = 0;
            string testResult;
            bool pass = true;

            string jsonContent = File.ReadAllText(fileName);
            JsonDocument doc = JsonDocument.Parse(jsonContent);

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement testElement in doc.RootElement.EnumerateArray())
                {
                    testResult = "";
                    testRun++;
                    executionCycles.Clear();
                    test = new(testElement);
                    Dictionary<string, ushort> goalStateRegisters = test.Goal.State.RegistersAsDictionary;
                    foreach (var kvp in test.Start.RamValues)
                    {
                        ram.Write(kvp.Key, kvp.Value);
                    }
                    //byte instruction = ram[(uint)((test.Start.State.PB << 16) + test.Start.State.PC)];
                    byte instruction = ram.Read((uint)((test.Start.State.PB << 16) + test.Start.State.PC));
                    emu.MPU.SetProcessorState(test.Start.State);
                    emu.Activate(false);
                    try
                    {
                        emu.MPU.ExecuteInstruction();
                        var mpuState = emu.MPU.Status;

                        bool registersEqual = true;
                        foreach (KeyValuePair<string, ushort> kvp in mpuState.RegistersAsDictionary)
                        {
                            if (kvp.Value != goalStateRegisters[kvp.Key])
                            {
                                testResult += $"Register {kvp.Key} differs from goal.\n";
                                registersEqual = false;
                            }
                        }

                        bool ignoreV = (op == W65C816.OpCode.ADC || op == W65C816.OpCode.SBC) && mpuState.FlagD;
                        bool vMatch = ignoreV || test.Goal.State.FlagV == mpuState.FlagV;

                        bool checkM = !mpuState.FlagE;
                        bool mMatch = !checkM || test.Goal.State.FlagM == mpuState.FlagM;

                        bool flagsEqual = test.Goal.State.FlagN == mpuState.FlagN
                            && vMatch
                            && mMatch
                            && test.Goal.State.FlagX == mpuState.FlagX
                            && test.Goal.State.FlagD == mpuState.FlagD
                            && test.Goal.State.FlagI == mpuState.FlagI
                            && test.Goal.State.FlagZ == mpuState.FlagZ
                            && test.Goal.State.FlagC == mpuState.FlagC
                            && test.Goal.State.FlagE == mpuState.FlagE;

                        if (!flagsEqual) testResult += "Status flags differ from goal.\n";

                        bool cyclesEqual = test.Cycles.Count == executionCycles.Count;
                        if (!cyclesEqual) testResult += "Operation did not take the specified number of cycles.\n";

                        if (!registersEqual || !flagsEqual || !cyclesEqual)
                        {
                            pass = false;
                            _output.WriteLine(testResult);

                            _output.WriteLine("Registers:");
                            _output.WriteLine($"{"",6} {"Expected",-10} | {"Actual",10}");
                            _output.WriteLine($"{"A:",6} {test.Goal.State.A,10:X4} | {mpuState.A,10:X4}");
                            _output.WriteLine($"{"X:",6} {test.Goal.State.X,10:X4} | {mpuState.X,10:X4}");
                            _output.WriteLine($"{"Y:",6} {test.Goal.State.Y,10:X4} | {mpuState.Y,10:X4}");
                            _output.WriteLine($"{"D:",6} {test.Goal.State.DP,10:X4} | {mpuState.DP,10:X4}");
                            _output.WriteLine($"{"DBR:",6} {test.Goal.State.DB,10:X2} | {mpuState.DB,10:X2}");
                            _output.WriteLine($"{"SP:",6} {test.Goal.State.SP,10:X4} | {mpuState.SP,10:X4}");
                            _output.WriteLine("");
                            _output.WriteLine($"{"PB PC:",6} {test.Goal.State.PB,5:X2} {test.Goal.State.PC:X4} | {mpuState.PB,5:X2} {mpuState.PC:X4}");
                            _output.WriteLine("");
                            _output.WriteLine($"{"Flags:",6} {test.Goal.State.Flags(),10} | {mpuState.Flags(),10:X4}");
                            _output.WriteLine("");
                            _output.WriteLine("Memory:");
                            _output.WriteLine("Address  St  Ex  Ac");
                            foreach (var kvp in test.Goal.RamValues)
                            {
                                string s = $"{kvp.Key,7:X6}  ";
                                s += test.Start.RamValues.TryGetValue(kvp.Key, out byte value) ? $"{value:X2}  " : "XX  ";
                                s += $"{kvp.Value:X2}  {ram.Read(kvp.Key):X2}";
                                _output.WriteLine(s);
                            }
                            _output.WriteLine("");
                            _output.WriteLine("XX: not set");
                            _output.WriteLine("");
                            _output.WriteLine("Cycles:");
                            _output.WriteLine($"    |{"Expected",-21}|{"Actual",-21}");
                            _output.WriteLine($"    |{"Address",-7} {"Val",-3} {"Type",-9}|{"Address",-7} {"Val",-3} {"Type",-9}");
                            for (int i = 0; i < Math.Max(test.Cycles.Count, executionCycles.Count); i++)
                            {
                                string s = $"{i,3} |";
                                if (i < test.Cycles.Count)
                                {
                                    s += $"${test.Cycles[i].Address:X6} ${test.Cycles[i].Value:X2} {test.Cycles[i].Type,-9}|";
                                }
                                else
                                {
                                    s += $"{"N/A",-21}|";
                                }
                                if (i < executionCycles.Count)
                                {
                                    s += $"${executionCycles[i].Address:X6} ${executionCycles[i].Value:X2} {executionCycles[i].Type,-9}";
                                }
                                else
                                {
                                    s += $"{"N/A",-21}";
                                }
                                _output.WriteLine(s);
                            }
                            break;
                        }
                    }
                    catch (Exception ex) 
                    {
                        _output.WriteLine(ex.ToString());
                        _output.WriteLine(ex.StackTrace);
                        pass = false;
                        break;
                    }
                    finally
                    { emu.Deactivate(); }
                }
                Assert.True(pass, $"Failed at test no. {testRun}");
                _output.WriteLine("PASS");
            }
            doc.Dispose();

            void OnNewCycle(int cycles, Microprocessor.Cycle details, Microprocessor.MicroprocessorState state)
            {
                executionCycles.Add(details);
                microprocessorStates.Add(state);
            }
        }

    }

    public class FullBurnInFile : TheoryData<string>
    {
        public FullBurnInFile()
        {
            string[] testFiles = Directory.GetFiles("testData/v1", "*.json");
            foreach (string s in testFiles)
            {
                if (Path.GetFileNameWithoutExtension(s)[..2] != "54" && Path.GetFileNameWithoutExtension(s)[..2] != "44") Add(s);
            }
        }
    }

    //public class QuickBurnInData : TheoryData<BurnInTest>
    //{
    //    /* Goal: After setting up the emulator with the first GauntletTestState, one instruction is run.
    //     * The state of the emulator is then compared to the second GauntletTestState, and the cycles counter is compared to the int.
    //     * If the test passes, the behavior of the instruction is identical to the behavior of the real hardware.
    //     * 
    //     * Unfortunately, the test jsons have filenames that are just the hex values of the instruction --
    //     * so we'll want to have some way to tell what instruction was being tested instead of running to the datasheet
    //     * and finding the instruction mnemonic.
    //     * 
    //     * i.e. "af.n.json"'s friendly name becomes "LDA #FEDBCA - Native" or something like that.
    //     * Maybe write that to the console when the test is run.
    //     */


    //    public QuickBurnInData()
    //    {
    //        Random rng = new();
    //        int testNumber = 1;
    //        string[] testFiles = Directory.GetFiles("testData/v1", "*.json");
    //        string cacheFile = $"{Path.GetTempPath()}{testNumber:D5}-testCache.json";
    //        JsonSerializerOptions options = new()
    //        {
    //            PropertyNameCaseInsensitive = true
    //        };
    //        if (File.Exists(cacheFile))
    //        {
    //            string jsonContent = File.ReadAllText(cacheFile);
    //            JsonDocument doc = JsonDocument.Parse(jsonContent);
    //            if (doc.RootElement.ValueKind == JsonValueKind.Array)
    //            {
    //                foreach (JsonElement testElement in doc.RootElement.EnumerateArray())
    //                {
    //                    BurnInTest test = new(testElement);
    //                    Console.WriteLine($"Test no. {test.Inst:X2}, {test.IsEmulated}");
    //                    Add(test);
    //                }
    //            }
    //        }
    //        else
    //        {
    //            JsonArray cacheArray = [];
    //            foreach (string fileName in testFiles)
    //            {
    //                if (Path.GetFileNameWithoutExtension(fileName)[..2] != "54" && Path.GetFileNameWithoutExtension(fileName)[..2] != "44") // skip the block move tests, they're broken
    //                {
    //                    Console.WriteLine($"Read from {fileName}");
    //                    string jsonContent = File.ReadAllText(fileName);
    //                    JsonDocument doc = JsonDocument.Parse(jsonContent);

    //                    if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > testNumber)
    //                    {
    //                        JsonElement testElement = doc.RootElement[testNumber];
    //                        cacheArray.Add(JsonNode.Parse(testElement.GetRawText()));

    //                        Add(new BurnInTest(testElement));

    //                    }
    //                }
    //            }
    //            File.WriteAllText(cacheFile, cacheArray.ToJsonString());
    //        }
    //    }
    //}

    public class BurnInTest
    {
        public readonly byte Inst;
        public readonly bool IsEmulated;
        public readonly SystemState Start;
        public readonly SystemState Goal;
        public readonly List<Microprocessor.Cycle> Cycles;

        public BurnInTest(byte inst, bool isEmulated, SystemState start, SystemState goal, List<Microprocessor.Cycle> cycles)
        {
            Inst = inst;
            IsEmulated = isEmulated;
            Start = start;
            Goal = goal;
            Cycles = cycles;
        }

        public BurnInTest(JsonElement json)
        {
            JsonSerializerOptions options = new()
            {
                PropertyNameCaseInsensitive = true
            };
            if (json.ValueKind == JsonValueKind.Object)
            {
                string? name = json.GetProperty("name").GetString();
                if (name == null)
                {
                    throw new JsonException("Does not appear to be a proper burn in JSON");
                }
                else
                {
                    Inst = byte.Parse(name[..2], System.Globalization.NumberStyles.HexNumber);
                    IsEmulated = name[3] switch
                    {
                        'e' => true,
                        'n' => false,
                        _ => throw new JsonException(),
                    };
                }

                string intermediateStateJson = json.GetProperty("initial").GetRawText();
                BurnInIntermediateState? inIntermediateState = JsonSerializer.Deserialize<BurnInIntermediateState>(intermediateStateJson, options) ?? throw new JsonException();
                Start = new SystemState(inIntermediateState);
                intermediateStateJson = json.GetProperty("final").GetRawText();
                inIntermediateState = JsonSerializer.Deserialize<BurnInIntermediateState>(intermediateStateJson, options) ?? throw new JsonException();
                Goal = new SystemState(inIntermediateState);

                Cycles = new List<Microprocessor.Cycle>();
                JsonElement intermediateCycleList = json.GetProperty("cycles");
                if (intermediateCycleList.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement element in intermediateCycleList.EnumerateArray())
                    {
                        Microprocessor.Cycle cycle = new Microprocessor.Cycle();
                        if (element[0].ValueKind == JsonValueKind.Number)
                        {
                            cycle.Address = element[0].GetUInt32();
                            if (element[1].ValueKind == JsonValueKind.Null)
                            {
                                cycle.Type = Microprocessor.CycleType.Internal;
                                cycle.Value = 0x00;
                            }
                            else
                            {
                                cycle.Value = element[1].GetByte();
                                cycle.Type = element[2].GetString()[3] switch
                                {
                                    'r' => Microprocessor.CycleType.Read,
                                    'w' => Microprocessor.CycleType.Write,
                                    _ => throw new JsonException(),
                                };
                            }
                            Cycles.Add(cycle);
                        }
                        //else
                        //{
                        //    Console.WriteLine("You should only see this if the instruction is STP or WAI");
                        //}
                    }
                }

            }
            else throw new JsonException();
        }
    }

    public class SystemState
    {
        public readonly Microprocessor.MicroprocessorState State;
        public readonly Dictionary<uint, byte> RamValues;

        public SystemState(Microprocessor.MicroprocessorState state, Dictionary<uint, byte> ramValues)
        {
            State = state;
            RamValues = ramValues;
        }

        public SystemState(BurnInIntermediateState state)
        {
            State = new Microprocessor.MicroprocessorState
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
            RamValues = new Dictionary<uint, byte>();
            foreach (var ram in state.RAM)
            {
                RamValues[(uint)ram[0]] = (byte)ram[1];
            }
        }
    }

    public class BurnInParameters
    {
        public string Name { get; set; }
        public BurnInIntermediateState Initial { get; set; }
        public BurnInIntermediateState Final { get; set; }
        public List<List<object>> Cycles { get; set; }
    }

    public class BurnInIntermediateState
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
