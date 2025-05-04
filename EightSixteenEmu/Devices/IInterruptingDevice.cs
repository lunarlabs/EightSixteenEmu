namespace EightSixteenEmu.Devices
{
    public interface IInterruptingDevice
    {

        public bool Interrupting { get; }

        event EventHandler<bool> InterruptStatusChanged;

    }
}
