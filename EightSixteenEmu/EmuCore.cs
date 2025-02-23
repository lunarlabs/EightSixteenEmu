using EightSixteenEmu.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EightSixteenEmu
{
    public class EmuCore
    {
        private readonly Microprocessor _mpu;
        public Microprocessor MPU { get { return _mpu; } }
        private SortedDictionary<(uint start, uint end), IMappableDevice> _devices = new SortedDictionary<(uint start, uint end), IMappableDevice>();

        public EmuCore()
        {
            _mpu = new Microprocessor(this);
        }

        #region Device Management
        public void AddDevice(IMappableDevice device)
        {
            uint start = device.BaseAddress;
            uint end = device.BaseAddress + device.Size - 1;
            if (start > 0xFFFFFF)
            {
                throw new ArgumentOutOfRangeException($"Addresses for {device.GetType()} fall outside the 24-bit address space.");
            }
            else
            {
                foreach (var dev in _devices)
                {
                    if (Math.Max(start, dev.Key.start) <= Math.Min(end, dev.Key.end)) // Corrected condition
                    {
                        throw new InvalidOperationException($"Addresses for {device.GetType()} (${start:x6} - ${end:x6}) conflict with existing device {dev.Value} at ${dev.Key.start:x6} - ${dev.Key.end:x6}");
                    }
                }
                _devices.Add((device.BaseAddress, device.BaseAddress + device.Size - 1), device);
            }
        }

        public void RemoveDevice(IMappableDevice device)
        {
            _devices.Remove((_devices.First(x => x.Value == device).Key));
        }

        public void RemoveDevice(uint address)
        {
            _devices.Remove((_devices.First(x => x.Key.start <= address && x.Key.end >= address).Key));
        }

        public void ClearDevices()
        {
            _devices.Clear();
        }

        public string DeviceList()
        {
            string result = "";
            uint lastUsedAddress = 0xffffffff;
            foreach (var device in _devices)
            {
                (uint start, uint end) = device.Key;
                if (start != lastUsedAddress + 1)
                {
                    result += $"${lastUsedAddress + 1:x6} - ${start - 1:x6}: Unused\n";
                }
                result += $"${start:x6} - ${end:x6}: {device.Value}\n"; // Corrected line
                lastUsedAddress = end;
            }
            return result;
        }

        internal IMappableDevice? GetDevice(uint address)
        {
            IMappableDevice? result = null;
            SortedDictionary<(uint start, uint end), IMappableDevice>.KeyCollection ranges = _devices.Keys;
            foreach ((uint s, uint e) in ranges)
            {
                if ((address >= s && address <= e))
                {
                    result = _devices[(s, e)];
                }
            }
            return result;
        }
        #endregion
    }
}
