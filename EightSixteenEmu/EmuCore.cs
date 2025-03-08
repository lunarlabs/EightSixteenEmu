/*    _____      __   __  _____      __               ____          
 *   / __(_)__ _/ /  / /_/ __(_)_ __/ /____ ___ ___  / __/_ _  __ __
 *  / _// / _ `/ _ \/ __/\ \/ /\ \ / __/ -_) -_) _ \/ _//  ' \/ // /
 * /___/_/\_, /_//_/\__/___/_//_\_\\__/\__/\__/_//_/___/_/_/_/\_,_/ 
 *       /___/                                                      
 * 
 *  Emulation Core
 *  Copyright (C) 2025 Matthias Lamers
 *  Released under GNUGPLv2, see LICENSE.txt for details.
 */
using EightSixteenEmu.Devices;

namespace EightSixteenEmu
{
    public class EmuCore
    {
        private readonly Microprocessor _mpu;
        public Microprocessor MPU { get { return _mpu; } }
        private byte[] ram = new byte[0x01000000];
        private SortedSet<(uint start, uint end)> _ramBlocks = [];
        private SortedList<(uint start, uint end), IMappableDevice> _devices = [];
        private SortedList<(uint start, uint end), uint> _mirrors = [];
        private List<IInterruptingMappableDevice> interruptingMappableDevices = [];

        private SortedList<(uint start, uint end), AddressAllocation> _allocationMap { get { return GetAllocationMap(); } }

        public event EventHandler? ClockTick;
        public event EventHandler? Reset;
        public event EventHandler? NMI;
        public event EventHandler? IRQ;

        public enum AddressAllocation
        {
            None,
            Mirror,
            Device,
            RAM,
        }

        public EmuCore()
        {
            _mpu = new(this);
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

        public void AddRAMBlock(uint startAddress, uint endAddress)
        {
            if (endAddress > 0xFFFFFF)
            {
                throw new ArgumentOutOfRangeException($"Addresses for RAM block fall outside the 24-bit address space.");
            }
            else
            {
                foreach (var block in _ramBlocks)
                {
                    if (CheckForRangeOverlap((startAddress, endAddress), block))
                    {
                        throw new InvalidOperationException($"RAM block (${startAddress:x6} - ${endAddress:x6}) overlaps with existing block (${block.start:x6} - ${block.end:x6})");
                    }
                }
                _ramBlocks.Add((startAddress, endAddress));
            }
        }

        public void Mirror(uint baseAddress, uint mirrorAddress, int length = -1)
        {
            AddressAllocation allocationType;
            IMappableDevice? device;
            GetAllocation(baseAddress, out allocationType, out device);
            switch (allocationType)
            {
                case AddressAllocation.None:
                    throw new InvalidOperationException($"No device at ${baseAddress:x6} to mirror.");
                case AddressAllocation.Mirror:
                    throw new InvalidOperationException($"Cannot mirror a mirror at ${baseAddress:x6}.");
                case AddressAllocation.RAM:
                    throw new NotImplementedException("Mirroring RAM is not yet implemented.");
                case AddressAllocation.Device:
                    throw new NotImplementedException("Mirroring devices is not yet implemented.");
                default:
                    throw new InvalidOperationException("Unknown address allocation type.");
            }
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

        private SortedList<(uint start, uint end), AddressAllocation> GetAllocationMap()
        {
            SortedList<(uint start, uint end), AddressAllocation> allocationMap = new();
            foreach (var block in _ramBlocks)
            {
                allocationMap.Add(block, AddressAllocation.RAM);
            }
            foreach (var dev in _devices)
            {
                allocationMap.Add(dev.Key, AddressAllocation.Device);
            }
            foreach (var mirror in _mirrors)
            {
                allocationMap.Add(mirror.Key, AddressAllocation.Mirror);
            }
            return allocationMap;
        }

        public AddressAllocation GetAllocation(uint address)
        {
            foreach (var range in _allocationMap)
            {
                if (address >= range.Key.start && address <= range.Key.end)
                {
                    return range.Value;
                }
            }
            return AddressAllocation.None;
        }

        private uint GetMirroredAddress(uint address)
        {
            uint mirroredAddress = address;
            foreach (var mirror in _mirrors)
            {
                if (address >= mirror.Key.start && address <= mirror.Key.end)
                {
                    mirroredAddress = address - mirror.Key.start + mirror.Value;
                    break;
                }
            }
            return mirroredAddress;
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
        private bool CheckForRangeOverlap((uint s, uint e) range1, (uint s, uint e) range2)
        {
            return(Math.Max(range1.s, range2.s) <= Math.Min(range1.e, range2.e));
        }
        private IMappableDevice? GetDevice(uint address)
        {
            var device = _devices.FirstOrDefault(d => address >= d.Key.start && address <= d.Key.end).Value;
            return device;
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
