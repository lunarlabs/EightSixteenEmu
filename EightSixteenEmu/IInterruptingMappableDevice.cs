namespace EightSixteenEmu
{
    public interface IInterruptingMappableDevice : IMappableDevice
    {
        public event EventHandler? Interrupt;
    }
}
