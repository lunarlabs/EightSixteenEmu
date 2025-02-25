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
        private List<IInterruptingMappableDevice> interruptingMappableDevices = new List<IInterruptingMappableDevice>();

        public event EventHandler? ClockTick;
        public event EventHandler? Reset;
        public event EventHandler? NMI;

        public EmuCore()
        {
            _mpu = new Microprocessor(this);
        }

        #region Device Management
        public void AddDevice(IMappableDevice device, uint baseAddress)
        {
            uint start = baseAddress;
            uint end = baseAddress + device.Size - 1;
            if (end > 0xFFFFFF)
            {
                throw new ArgumentOutOfRangeException($"Addresses for {device.GetType()} fall outside the 24-bit address space.");
            }
            else
            {
                foreach (var dev in _devices)
                {
                    if (Math.Max(start, dev.Key.start) <= Math.Min(end, dev.Key.end))
                    {
                        throw new InvalidOperationException($"Addresses for {device.GetType()} (${start:x6} - ${end:x6}) conflict with existing device {dev.Value} at ${dev.Key.start:x6} - ${dev.Key.end:x6}");
                    }
                }
                _devices.Add((baseAddress, baseAddress + device.Size - 1), device);
                if (device is IInterruptingMappableDevice interruptingDevice)
                {
                    interruptingDevice.Interrupt += _mpu.OnInterrupt;
                    interruptingMappableDevices.Add(interruptingDevice);
                }
            }
        }

        public void Mirror(IMappableDevice device, uint baseAddress, uint startOffset = 0, int endOffset = -1)
        {
            AddDevice(new MirrorDevice(device, startOffset, endOffset), baseAddress);
        }

        public void RemoveDevice(IMappableDevice device)
        {
            _devices.Remove(_devices.First(x => x.Value == device).Key);
            if (device is IInterruptingMappableDevice interruptingDevice)
            {
                interruptingDevice.Interrupt -= _mpu.OnInterrupt;
                interruptingMappableDevices.Remove(interruptingDevice);
            }
        }

        public void RemoveDevice(uint address)
        {
            IMappableDevice? device = GetDevice(address);
            if (device != null)
            {
                RemoveDevice(device);
            }
            else Console.WriteLine($"Attempt to remove device at ${address:x6} failed because there is already no device at that address.");
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
                result += $"${start:x6} - ${end:x6}: {device.Value}\n";
                lastUsedAddress = end;
            }
            if (lastUsedAddress < 0xffffff)
            {
                result += $"${lastUsedAddress + 1:x6} - $ffffff: Unused\n";
            }
            return result;
        }

        internal byte? Read(uint address)
        {
            byte? result = null;
            IMappableDevice? device = GetDevice(address);
            if (device != null)
            {
                result = device[address - _devices.First(x => x.Value == device).Key.start];
            }
            else Console.WriteLine($"Open bus read at ${address:x6}");
            return result;
        }

        internal void Write(uint address, byte value)
        {
            IMappableDevice? device = GetDevice(address);
            if (device != null)
            {
                device[address - _devices.First(x => x.Value == device).Key.start] = value;
            }
            else Console.WriteLine($"Open bus write at ${address:x6}");
        }

        private IMappableDevice? GetDevice(uint address)
        {
            var device = _devices.FirstOrDefault(d => address >= d.Key.start && address <= d.Key.end).Value;
            return device;
        }

        private class MirrorDevice : IMappableDevice
        {
            uint _size;
            IMappableDevice _sourceDevice;
            uint _start;
            uint IMappableDevice.Size { get { return _size; } }
            internal MirrorDevice(IMappableDevice device, uint start = 0, int end = -1)
            {
                _sourceDevice = device;
                _start = start;
                if (end == -1)
                {
                    end = (int)(device.Size - 1);
                }
                _size = (uint)(end - start + 1);
            }
            public byte this[uint index]
            {
                get
                {
                    return _sourceDevice[index - _start];
                }
                set
                {
                    _sourceDevice[index - _start] = value;
                }
            }

            public override string ToString()
            {
                return $"Mirror of {_sourceDevice} (offsets ${_start:x6} - ${_size - 1:x6})";
            }
        }
        #endregion

        #region Event Management
        public void OnInterrupt(object sender, EventArgs e)
        {
            _mpu.OnInterrupt(sender, e);
        }

        #endregion

        #region Clock Management

        #endregion
    }
}
