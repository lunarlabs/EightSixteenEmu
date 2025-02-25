namespace EightSixteenEmu.Devices
{
    public interface IMappableDevice
    {
        uint Size { get; }
        byte this[uint index]
        { get; set; }
    }
}
