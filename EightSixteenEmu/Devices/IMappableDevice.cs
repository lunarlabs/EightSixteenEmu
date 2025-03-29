namespace EightSixteenEmu.Devices
{
    public interface IMappableDevice
    {
        uint Size { get; } 
        internal void Init();
    }
}
