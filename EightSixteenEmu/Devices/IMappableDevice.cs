namespace EightSixteenEmu.Devices
{
    public interface IMappableDevice
    {
        uint Size { get; }

        // In the field, Init should really be only called by MemoryMapper, and all devices should be part of
        // the Devices namespace...
        // but I'm using an inner class to test interrupts in EmuXTesting, so it has to be public
        // in order to inherit... too bad!
#if DEBUG
        public void Init();
#else
        internal void Init();
#endif
    }
}
