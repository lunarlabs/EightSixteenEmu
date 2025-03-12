using EightSixteenEmu.Devices;

namespace EightSixteenEmu.MemoryMapping
{
    class MemoryMapper(EmuCore core)
    {
        private readonly EmuCore _core = core;
        private readonly List<IMappableDevice> _devices = [];
        private readonly SortedList<(uint start, uint end), (IMappableDevice dev, uint offset)> _memmap = [];

        public static bool CheckOverlap(uint start1, uint end1, uint start2, uint end2) => Math.Max(start1, start2) <= Math.Min(end1, end2);

        public byte this[uint index]
        {
            get
            {
                if (index > 0xffffff)
                {
                    throw new IndexOutOfRangeException();
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                if (index > 0xffffff)
                {
                    throw new IndexOutOfRangeException();
                }
            }
        }

        public void AddDevice(IMappableDevice device, uint mapLocation, uint offset = 0, long length = -1)
        {
            ArgumentNullException.ThrowIfNull(device);

            if (length == -1)
            {
                length = device.Size - offset;
            }

            ulong mapEnd = (ulong)mapLocation + (ulong)length;

            if (mapEnd > 0xFFFFFF || offset + length > device.Size || length <= 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            // Efficient overlap check using SortedList
            List<(uint start, uint end)> locations = _memmap.Keys.ToList();
            int index =locations.BinarySearch((mapLocation, (uint)mapEnd));
            if (index < 0) index = ~index; // Get insertion point if exact match not found

            // Check the previous entry (if any)
            if (index > 0)
            {
                var prev = _memmap.Keys[index - 1];
                if (CheckOverlap(mapLocation, (uint)mapEnd, prev.start, prev.end))
                {
                    throw new InvalidOperationException($"Device {device} overlaps with existing device at ${prev.start:x6} - ${prev.end:x6}");
                }
            }

            // Check the next entry (if any)
            if (index < _memmap.Count)
            {
                var next = _memmap.Keys[index];
                if (CheckOverlap(mapLocation, (uint)mapEnd, next.start, next.end))
                {
                    throw new InvalidOperationException($"Device {device} overlaps with existing device at ${next.start:x6} - ${next.end:x6}");
                }
            }

            if (!_devices.Contains(device))
            {
                _devices.Add(device);
            }

            _memmap.Add((mapLocation, (uint)mapEnd), (device, offset));
        }

    }
}
