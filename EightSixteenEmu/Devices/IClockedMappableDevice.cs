namespace EightSixteenEmu.Devices
{
    public interface IClockedMappableDevice : IMappableDevice
    {
        private static EmuCore EmuCore { get; }
        void OnClockTick();
    }
}