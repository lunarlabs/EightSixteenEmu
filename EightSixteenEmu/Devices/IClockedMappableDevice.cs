namespace EightSixteenEmu.Devices
{
    public interface IClockedMappableDevice : IMappableDevice
    {
        private static EmuCore emuCore { get; }
        void OnClockTick();
    }
}