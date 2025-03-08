
namespace EightSixteenEmu.Devices
{
    interface IMappedWriteDevice : IMappableDevice
    {
        byte this[uint index]
        { set; }
    }
}
