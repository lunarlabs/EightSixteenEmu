using EightSixteenEmu;
using EightSixteenEmu.Devices;

namespace EmuXTesting
{
    public class MicroprocessorTests
    {
        /*
         * Okay, so let's think about what we need externally for the HW interrupt tests. There are four "hardware" interrupts:
         * Reset, Abort, IRQ, and NMI -- although we don't really need to bother with Abort since, to my knowledge, there
         * haven't been any '816 builds that actually used that line. (For completeness's sake: Abort is a hardware interrupt
         * that, when triggered, finishes the current instruction but in a way that does not affect the CPU state or memory--
         * basically turns the current operation into a NOP in most cases). 
         */
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
            EmuCore emu = new EmuCore();
            // TODO: make rom file
            // Act
            // Assert
            Assert.Fail("I didn't code the interrupts ROM yet!");
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
         */

        /*
         * Finally, we have the dreaded free-running clock. This will be a headache, since threads will be involved and
         * I have no clue on earth how to test it. In all honestly, I'll probably make sure that the logic is correct
         * in instruction-step mode first, and then design the test suite for free-running clock.
         */
    }
}
