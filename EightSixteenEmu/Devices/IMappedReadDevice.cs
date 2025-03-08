namespace EightSixteenEmu.Devices
{
    interface IMappedReadDevice : IMappableDevice
    {
        byte this[uint index]
        { get; }
    }
}
