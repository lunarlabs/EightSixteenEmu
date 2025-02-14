namespace EightSixteenEmu
{
    public interface IMappableDevice
    {
        uint Size { get; }
        uint BaseAddress { get; }
        byte this[uint index]
        { get; set; }
    }
}
