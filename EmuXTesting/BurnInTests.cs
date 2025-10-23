using EightSixteenEmu;
using EightSixteenEmu.Devices;
using EightSixteenEmu.MPU;
using System.Text.Json;
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
            //// Arrange
            //byte[] data =
            //{
            //    0x0f, 0xf0, 0xf0, 0xf0, 0x0f, 0xf0, 0xf0, 0xf0,
            //    0x34, 0x12, 0x78, 0x56, 0xbc, 0x9a, 0xf0, 0xde,
            //    0x80, 0x7f, 0x80, 0x7f, 0x80, 0x7f, 0x80, 0x7f,
            //    0x80, 0x7f, 0x80, 0x7f, 0x80, 0x7f, 0x80, 0x7f,
            //};
            //// d'oh! forgot about the endianess...

            //EmuCore emu = new();
            //emu.MPU.NewCycle += OnNewCycle;
            //emu.MPU.NewInstruction += OnNewInstruction;
            //var rom = new DevROM("MoveTests.rom");
            //emu.Mapper.AddDevice(rom, 0x8000);
            //var ram = new DevRAM(0x1000000);
            //emu.Mapper.AddDevice(ram, 0, 0, 0x8000);
            //emu.Mapper.AddDevice(ram, 0x10000, 0x10000, 0x40000);

            //string s = "";
            //_output.WriteLine("ROM data:");
            //for (int i = 0; i < data.Length; i++)
            //{
            //    s += $"{emu.Mapper[(uint)(0x8100 + i)]:X2} ";
            //}
            //_output.WriteLine(s);

            //// Act
            //_output.WriteLine("");
            //_output.WriteLine("Resetting MPU...");
            //emu.MPU.Reset();
            //while (emu.MPU.ExecutionState == "ProcessorStateRunning")
            //{
            //    emu.MPU.ExecuteInstruction();
            //    if (emu.MPU.Status.Cycles > 2500)
            //    {
            //        _output.WriteLine("Execution timed out.");
            //        break;
            //    }
            //}
            //_output.WriteLine("End of execution.");

            //// Assert
            //_output.WriteLine("Final State:");
            //s = "MVN: ";
            //byte[] mvnRange = new byte[data.Length];
            //for (int i = 0; i < data.Length; i++)
            //{
            //    mvnRange[i] = emu.Mapper[(uint)(0x0200 + i)] ?? 0xFF;
            //    s += $"{mvnRange[i]:X2} ";
            //}
            //_output.WriteLine(s);

            //s = "MVP: ";
            //byte[] mvpRange = new byte[data.Length];
            //for (int i = 0; i < data.Length; i++)
            //{
            //    mvpRange[i] = emu.Mapper[(uint)(0x010000 + i)] ?? 0xFF;
            //    s += $"{mvpRange[i]:X2} ";
            //}
            //_output.WriteLine(s);

            //Assert.Equal(data, mvnRange);
            //Assert.Equal(data, mvpRange);
            //_output.WriteLine("MVN and MVP instructions executed correctly. Check cycle counts.");

            //void OnNewCycle(int cycles, Microprocessor.Cycle details, Microprocessor.MicroprocessorState state)
            //{
            //    executionCycles.Add(details);
            //    microprocessorStates.Add(state);
            //    _output.WriteLine($"  Cycle {cycles}: {details.Address:X6} {details.Value:X2} {details.Type}");
            //    _output.WriteLine($"   PB PC: {state.PB:X2} {state.PC:X4} SP: {state.SP:X4} A: {state.A:X4} X: {state.X:X4} Y: {state.Y:X4} DB: {state.DB:X2} DP: {state.DP:X2}");
            //    _output.WriteLine($"   Flags: {state.Flags()}");
            //}
            // void OnNewInstruction(W65C816.OpCode opCode, string operand)
            //{
            //    _output.WriteLine("");
            //    _output.WriteLine($"Instruction: {opCode} {operand}");
            //}
            Assert.Fail("This test is not implemented yet.");
        }

        [Theory]
        [ClassData(typeof(FullBurnInFile))]
        public void FullBurnIn(string fileName)
        {
            string currentTestLog;
            List<String> Fails = [];
            // Arrange:
            // this is going to execute 5,120,000 instructions. God help me.
            var inst = byte.Parse(Path.GetFileNameWithoutExtension(fileName)[..2], System.Globalization.NumberStyles.HexNumber);
            (W65C816.OpCode op, W65C816.AddressingMode mode) = W65C816.OpCodeLookup(inst);

            _output.WriteLine($"Testing ${Path.GetFileNameWithoutExtension(fileName)}: {op} {mode}");
            _output.WriteLine("Creating emulation environment...");
            var ram = new DevRAM(0x1000000);
            var core = new NewCore();
            core.Enabled = false;
            core.Mapper.AddDevice(ram, 0);

            _output.WriteLine("Loading test data...");
            Queue<BurnInTest> tests = FullBurnInFile.CreateTests(fileName);

            //Act:
            while (tests.Count > 0)
            {
                BurnInTest test = tests.Dequeue();
                currentTestLog = $"Test ${test.Inst:X2} {(test.IsEmulated ? "emulated" : "native")}\n";
                core.SetProcessorState(test.Start.State);
                foreach (var kvp in test.Start.RamValues)
                {
                    ram.Write(kvp.Key, kvp.Value);
                }
                core.Enabled = true;
                for (int cycleIndex = 0; cycleIndex < test.Cycles.Count; cycleIndex++)
                {
                    NewCore.Cycle expectedCycle = test.Cycles[cycleIndex];
                    NewCore.Cycle actualCycle = core.CycleStep();
                    currentTestLog += $"Cycle {cycleIndex,3}: Expected ${expectedCycle.AddressBus:X6} ${expectedCycle.DataBus:X2} {expectedCycle.Type}, Actual ${actualCycle.AddressBus:X6} ${actualCycle.DataBus:X2} {actualCycle.Type}\n";
                    if (expectedCycle.AddressBus != actualCycle.AddressBus || expectedCycle.DataBus != actualCycle.DataBus || expectedCycle.Type != actualCycle.Type)
                    {
                        currentTestLog += " Cycle Mismatch!\n";
                        Fails.Add(currentTestLog);
                        break; // move to next test
                    }
                }
                core.Enabled = false;
                var finalState = core.GetProcessorState();
                bool registersEqual = test.Goal.State.PC == finalState.PC
                    && test.Goal.State.A == finalState.A
                    && test.Goal.State.X == finalState.X
                    && test.Goal.State.Y == finalState.Y
                    && test.Goal.State.DP == finalState.DP
                    && test.Goal.State.SP == finalState.SP
                    && test.Goal.State.SR == finalState.SR
                    && test.Goal.State.DB == finalState.DB
                    && test.Goal.State.PB == finalState.PB;
                currentTestLog += $"Final State Check: {(registersEqual ? "PASS" : "FAIL")}\n";
                if (!registersEqual)
                {
                    currentTestLog += $" Expected PC:${test.Goal.State.PC:X4} A:${test.Goal.State.A:X4} X:${test.Goal.State.X:X4} Y:${test.Goal.State.Y:X4} DP:${test.Goal.State.DP:X4} SP:${test.Goal.State.SP:X4} SR:${test.Goal.State.SR:X2} DB:${test.Goal.State.DB:X2} PB:${test.Goal.State.PB:X2}\n";
                    currentTestLog += $" Actual   PC:${finalState.PC:X4} A:${finalState.A:X4} X:${finalState.X:X4} Y:${finalState.Y:X4} DP:${finalState.DP:X4} SP:${finalState.SP:X4} SR:${finalState.SR:X2} DB:${finalState.DB:X2} PB:${finalState.PB:X2}\n";
                    Fails.Add(currentTestLog);
                }
                foreach (var kvp in test.Goal.RamValues)
                {
                    byte actualValue = ram.Read(kvp.Key);
                    if (actualValue != kvp.Value)
                    {
                        currentTestLog += $"RAM Mismatch at ${kvp.Key:X6}: Expected ${kvp.Value:X2}, Actual ${actualValue:X2}\n";
                        Fails.Add(currentTestLog);
                        break; // move to next test
                    }
                }
            }
            // Assert:
            if (Fails.Count > 0)
            {
                foreach (var failLog in Fails)
                {
                    _output.WriteLine("----- TEST FAILURE -----");
                    _output.WriteLine(failLog);
                }
                Assert.Fail($"{Fails.Count} tests failed. See output for details.");
            }
            else
            {
                _output.WriteLine("All tests passed.");
            }

        }

        public class FullBurnInFile : TheoryData<string>
        {
            public FullBurnInFile()
            {
                string[] testFiles = Directory.GetFiles("testData/v1", "*.json");
                foreach (string s in testFiles)
                {
                    /*
                     * Okay, why are we excluding the block move tests?
                     * Simply put, the JSON data is broken. It seems to just be the first 100 cycles of the test. The nature of the block moves
                     * means that the accumulator will always be 0xFFFF after the copy is finished. The tests in the JSON set the accumulator to
                     * something like 0xFE8B (65,163 bytes!!) and then the goal state is set after only 100 cycles, meaning only about 14 or so
                     * bytes are copied at test's end!
                     * What a shame. 
                     */
                    if (Path.GetFileNameWithoutExtension(s)[..2] != "54" && Path.GetFileNameWithoutExtension(s)[..2] != "44") Add(s);
                }
            }

            public static Queue<BurnInTest> CreateTests(string fileLocation)
            {
                Queue<BurnInTest> result = new Queue<BurnInTest>();
                JsonSerializerOptions options = new()
                {
                    PropertyNameCaseInsensitive = true
                };

                if (File.Exists(fileLocation))
                {
                    string jsonContent = File.ReadAllText(fileLocation);
                    JsonDocument doc = JsonDocument.Parse(jsonContent);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement testElement in doc.RootElement.EnumerateArray())
                        {
                            BurnInTest test = new(testElement);
                            result.Enqueue(test);
                        }
                    }
                    else
                    {
                        throw new JsonException("Does not appear to be a proper burn in JSON");
                    }
                }
                else
                {
                    throw new FileNotFoundException("Could not find burn in test file.", fileLocation);
                }
                return result;
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
            public readonly List<NewCore.Cycle> Cycles;

            public BurnInTest(byte inst, bool isEmulated, SystemState start, SystemState goal, List<NewCore.Cycle> cycles)
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

                    Start = new SystemState(json.GetProperty("initial"));
                    Goal = new SystemState(json.GetProperty("final"));

                    Cycles = new List<NewCore.Cycle>();
                    JsonElement intermediateCycleList = json.GetProperty("cycles");
                    if (intermediateCycleList.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement element in intermediateCycleList.EnumerateArray())
                        {
                            NewCore.Cycle cycle = new NewCore.Cycle();
                            if (element[0].ValueKind == JsonValueKind.Number)
                            {
                                cycle.AddressBus = element[0].GetUInt32();
                                string outputsString = element[2].GetString() ?? "---remx-";
                                if (outputsString.Length < 4)
                                {
                                    throw new JsonException("Cycle type string is too short.");
                                }
                                bool vdaActive = outputsString[0] == 'd';
                                bool vpaActive = outputsString[1] == 'p';
                                bool vpbActive = outputsString[2] == 'v';
                                if (vdaActive || vpaActive || vpbActive)
                                {
                                    // we have a memory access cycle, what type?
                                    cycle.Type = outputsString[3] switch
                                    {
                                        'r' => NewCore.Cycle.CycleType.Read,
                                        'w' => NewCore.Cycle.CycleType.Write,
                                        _ => throw new JsonException("Invalid outputs string in test json"),
                                    };
                                    if (element[1].ValueKind == JsonValueKind.Null)
                                    {
                                        throw new JsonException("Cycle value is null, but type is not Internal.");
                                    }
                                    else cycle.DataBus = element[1].GetByte();
                                }
                                else
                                {
                                    // no memory access, so we don't care about the data bus
                                    cycle.Type = NewCore.Cycle.CycleType.Internal;
                                    cycle.DataBus = 0; // no data bus value
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
            public readonly NewCore.ProcessorState State;
            public readonly Dictionary<uint, byte> RamValues;

            public SystemState(NewCore.ProcessorState state, Dictionary<uint, byte> ramValues)
            {
                State = state;
                RamValues = ramValues;
            }

            public SystemState(JsonElement json)
            {
                State = new NewCore.ProcessorState();
                RamValues = new Dictionary<uint, byte>();

                JsonSerializerOptions options = new()
                {
                    PropertyNameCaseInsensitive = true
                };

                if (json.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in json.EnumerateObject())
                    {
                        string propName = prop.Name.ToLower();
                        switch (propName)
                        {
                            case "pc":
                                State.PC = prop.Value.GetUInt16();
                                break;
                            case "s":
                                State.SP = prop.Value.GetUInt16();
                                break;
                            case "p":
                                State.SR = prop.Value.GetByte();
                                break;
                            case "a":
                                State.A = prop.Value.GetUInt16();
                                break;
                            case "x":
                                State.X = prop.Value.GetUInt16();
                                break;
                            case "y":
                                State.Y = prop.Value.GetUInt16();
                                break;
                            case "dbr":
                                State.DB = prop.Value.GetByte();
                                break;
                            case "d":
                                State.DP = prop.Value.GetUInt16();
                                break;
                            case "pbr":
                                State.PB = prop.Value.GetByte();
                                break;
                            case "e":
                                State.E = prop.Value.GetInt32() != 0;
                                break;
                            case "ram":
                                var ramJson = prop.Value;
                                if (ramJson.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var ramEntry in ramJson.EnumerateArray())
                                    {
                                        uint address = ramEntry[0].GetUInt32();
                                        byte value = ramEntry[1].GetByte();
                                        RamValues[address] = value;
                                    }
                                }
                                else
                                {
                                    throw new JsonException("RAM property is not an array.");
                                }
                                break;
                            default:
                                throw new JsonException($"Unknown property in system state JSON: {prop.Name}");
                        }
                    }
                }

            }
        }

    }
}
