namespace EightSixteenEmu.Devices
{
    internal interface IInterruptListener
    {
        internal void OnInterruptChange(object? sender, bool value);

        public void AddInterruptingDevice(IInterruptingDevice device)
        {
            device.InterruptStatusChanged += OnInterruptChange;
        }

        public void RemoveInterruptingDevice(IInterruptingDevice device)
        {
            device.InterruptStatusChanged -= OnInterruptChange;
        }
    }
}
