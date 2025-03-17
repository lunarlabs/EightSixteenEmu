/*    _____      __   __  _____      __               ____          
 *   / __(_)__ _/ /  / /_/ __(_)_ __/ /____ ___ ___  / __/_ _  __ __
 *  / _// / _ `/ _ \/ __/\ \/ /\ \ / __/ -_) -_) _ \/ _//  ' \/ // /
 * /___/_/\_, /_//_/\__/___/_//_\_\\__/\__/\__/_//_/___/_/_/_/\_,_/ 
 *       /___/                                                      
 * 
 *  Emulation Core
 *  Copyright (C) 2025 Matthias Lamers
 *  Released under GNUGPLv2, see LICENSE.txt for details.
 *  
 *  Device manager and address translator
 */

using EightSixteenEmu.Devices;

namespace EightSixteenEmu.MemoryMapping
{
    public class MemoryMapper(EmuCore core)
    {
        private readonly EmuCore _core = core;
        private readonly List<IMappableDevice> _devices = [];
        private readonly SortedList<uint, (uint length, IMappableDevice dev, uint offset)> _memmap = [];

        // So I don't end up confusing myself when documenting this, here are the definitions I'll use:
        // The BUS ADDRESS SPACE is the 24-bit address space that the 65C816 can address.
        // The DEVICE ADDRESS SPACE is the device's own internal address space, which may be larger than the bus address space.
        // Think of it like connecting the address lines to non-corresponding pins on the device.
        // Also, ONE device address can be mapped to MULTIPLE bus addresses, but not vice versa. This means mirroring is easy to implement.

        public static bool CheckOverlap(uint start1, uint end1, uint start2, uint end2) => (start1 < end2 && start2 < end1);

        public byte? this[uint index]
        {
            get
            {
                if (index > 0xffffff)
                {
                    throw new IndexOutOfRangeException();
                }
                else
                {
                    var kvp = SeekDevice(index);
                    if (kvp is not null)
                    {
                        if (kvp.Value.Value is IMappedReadDevice readDev)
                        {
                            return readDev[TranslateAddress(kvp.Value, index)];
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            set
            {
                if (!value.HasValue)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                if (index > 0xffffff)
                {
                    throw new IndexOutOfRangeException();
                }
                var kvp = SeekDevice(index);
                if (kvp is not null)
                {
                    if (kvp.Value.Value is IMappedWriteDevice writeDev)
                    {
                        writeDev[TranslateAddress(kvp.Value, index)] = (byte)value;
                    }
                }
            }
        }

        public void AddDevice(IMappableDevice device, uint mapLocation, uint offset = 0, long length = -1)
        {
            ArgumentNullException.ThrowIfNull(device);

            if (mapLocation > 0xFFFFFF)
            {
                throw new ArgumentOutOfRangeException(nameof(mapLocation), $"Requested bus address ${mapLocation:x8} exceeds the 24-bit address space.");
            }
            else if (offset > device.Size)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), $"Requested device offset ${offset:x8} exceeds the device size ${device.Size:x8}");
            }
            else if (length == -1)
            {
                if (offset == device.Size)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be less than the device size.");
                }
                else
                {
                    length = device.Size - offset;
                }
            }

            ulong mapEnd = (ulong)mapLocation + (ulong)length;

            if (length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than zero.");
            }
            else if (mapEnd > 0xFFFFFF)
            {
                throw new ArgumentOutOfRangeException(nameof(mapLocation), $"Requested bus address range ${mapLocation:x8} - ${mapEnd:x8} exceeds the 24-bit address space.");
            }
            else if (offset + length > device.Size)
            {
                throw new ArgumentException($"Requested device address range for {device} ${mapLocation:x8} - ${mapEnd:x8} out of range (highest valid address: ${device.Size:x8})", nameof(length));
            }
            
            // Efficient overlap check using SortedList
            List<uint> locations = [.. _memmap.Keys];
            int index = locations.BinarySearch(mapLocation);
            if (index < 0) index = ~index; // Get insertion point if exact match not found

            // Check the previous entry (if any)
            if (index > 0)
            {
                var prev = _memmap.ElementAt(index - 1);
                if (CheckOverlap(mapLocation, (uint)mapEnd, prev.Key, prev.Key + prev.Value.length))
                {
                    throw new InvalidOperationException($"Device {device} overlaps with existing device at ${prev.Key:x6} - ${prev.Key + prev.Value.length:x6}");
                }
            }

            // Check the next entry (if any)
            if (index < _memmap.Count)
            {
                var next = _memmap.ElementAt(index);
                if (CheckOverlap(mapLocation, (uint)mapEnd, next.Key, next.Key + next.Value.length))
                {
                    throw new InvalidOperationException($"Device {device} overlaps with existing device at ${next.Key:x6} - ${next.Key + next.Value.length:x6}");
                }
            }

            if (!_devices.Contains(device))
            {
                _devices.Add(device);
            }

            _memmap.Add(mapLocation, ((uint)length, device, offset));
        }

        private KeyValuePair<uint, IMappableDevice>? SeekDevice(uint address)
        {
            KeyValuePair<uint, IMappableDevice>? result = null;
            List<uint> locations = [.. _memmap.Keys];
            int index = locations.BinarySearch(address);
            if (index >= 0) // Hit the first byte of an entry
            {
                result = new KeyValuePair<uint, IMappableDevice>(locations[index], _memmap[locations[index]].dev);
            }
            else if (index < 0) index = ~index; // Get insertion point if exact match not found

            // If we didn't hit the starting byte of an entry, see if we're in range of the previous one
            if (index > 0)
            {
                var prev = _memmap.ElementAt(index - 1);
                if (address >= prev.Key && address < prev.Key + prev.Value.length)
                {
                    result = new KeyValuePair<uint, IMappableDevice>(prev.Key, prev.Value.dev);
                }
            }

            // No use checking the next entry, since the address is obviously before the entry's start
            return result;
        }

        private uint TranslateAddress(KeyValuePair<uint, IMappableDevice> entry, uint address)
        {
            return address - entry.Key + _memmap[entry.Key].offset;
        }

        public void RemoveDevice(IMappableDevice device)
        {
            if (_devices.Contains(device))
            {
                List<uint> locations = [.. _memmap.Keys];
                foreach (var loc in locations)
                {
                    if (_memmap[loc].dev == device)
                    {
                        _memmap.Remove(loc);
                    }
                }
                _devices.Remove(device);
            }
        }

        public void RemoveMapping(uint address)
        {
            KeyValuePair<uint, IMappableDevice>? entry = SeekDevice(address);
            if (entry is not null)
            {
                _memmap.Remove(entry.Value.Key);
            }
        }
    }
}
