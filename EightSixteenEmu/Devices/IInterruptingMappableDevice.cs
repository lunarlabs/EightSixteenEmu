namespace EightSixteenEmu.Devices
{
    public interface IInterruptingMappableDevice : IMappableDevice
    {
        public event EventHandler? Interrupt;
    }
}
