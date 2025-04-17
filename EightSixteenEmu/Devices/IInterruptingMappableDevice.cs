namespace EightSixteenEmu.Devices
{
    public interface IInterruptingMappableDevice : IMappableDevice
    {
        public bool Interrupting { get; }

    }
}
