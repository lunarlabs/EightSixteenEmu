using EightSixteenEmu;
using System.Data;

namespace EmuXTesting
{
    public class BurnInTests
    {
        [Theory]
        [ClassData(typeof(QuickBurnInData))]
        public void QuickBurnIn(BurnInTestState start, BurnInTestState goal, int cycles)
        {

        }

        [Theory]
        public void FullBurnIn()
        {
            // this is going to execute 5,120,000 instructions. God help me.
            // should I have the classdata be by file?
        }


    }

    public class QuickBurnInData : TheoryData<BurnInTestState, BurnInTestState, int>
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
    }

    public class BurnInTestState(Microprocessor.MicroprocessorState state, Dictionary<uint, uint> ramValues)
    {
        public readonly Microprocessor.MicroprocessorState State = state;
        public readonly Dictionary<uint, uint> RamValues = ramValues;
    }
}
