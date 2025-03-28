namespace EightSixteenEmu.Devices
{
    public interface IInterruptingMappableDevice : IMappableDevice
    {
        public bool Interrupting { get; }
        public event EventHandler? Interrupt;

        internal void InvokeInterrupt();
    }
}
