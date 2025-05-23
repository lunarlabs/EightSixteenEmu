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
using EightSixteenEmu.Factories;
using System.Security.AccessControl;
using System.Text.Json.Nodes;

namespace EightSixteenEmu.MemoryMapping
{
    /// <summary>
    /// Manages the mapping of <see cref="MappableDevice"/> instances into the 24-bit bus address space of the emulated 65C816 system.
    /// </summary>
    public class MemoryMapper() : IInterruptListener
    {
        private readonly List<MappableDevice> _devices = [];
        private readonly List<IInterruptingDevice> _interruptingDevices = [];
        private readonly SortedList<uint, (uint length, MappableDevice dev, uint offset)> _memmap = [];

        // So I don't end up confusing myself when documenting this, here are the definitions I'll use:
        // The BUS ADDRESS SPACE is the 24-bit address space that the 65C816 can address.
        // The DEVICE ADDRESS SPACE is the device's own internal address space, which may be larger than the bus address space.
        // Think of it like connecting the address lines to non-corresponding pins on the device.
        // Also, ONE device address can be mapped to MULTIPLE bus addresses, but not vice versa. This means mirroring is easy to implement.

        public bool DeviceInterrupting => _interruptingDevices.Count > 0;

        public static bool CheckOverlap(uint start1, uint end1, uint start2, uint end2) => (start1 < end2 && start2 < end1);


        public byte? this[uint index]
        {
            get
            {
                index &= 0xffffff;

                var kvp = SeekDevice(index);
                if (kvp is not null)
                {
                    if (kvp.Value.Value.Access != MappableDevice.AccessMode.Write)
                    {
                        return kvp.Value.Value[TranslateAddress(kvp.Value, index)];
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
            set
            {
                if (!value.HasValue)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                index &= 0xffffff;
                var kvp = SeekDevice(index);
                if (kvp is not null)
                {
                    if (kvp.Value.Value.Access != MappableDevice.AccessMode.Read)
                    {
                        kvp.Value.Value[TranslateAddress(kvp.Value, index)] = (byte)value;
                    }
                }
            }
        }

        /// <summary>
        /// Adds a <see cref="MappableDevice"/> to the memory map at the specified bus address.
        /// </summary>
        /// <param name="device">The device to map into the address space.</param>
        /// <param name="mapLocation">The 24-bit bus address where the device will be mapped.</param>
        /// <param name="offset">The offset into the device's address space to start mapping from. Defaults to 0.</param>
        /// <param name="length">
        /// The number of bytes to map from the device. If set to -1 (default), the mapping will extend from <paramref name="offset"/> to the end of the device.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="device"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="mapLocation"/> is outside the 24-bit address space,
        /// if <paramref name="offset"/> is greater than the device size,
        /// if <paramref name="offset"/> equals the device size and <paramref name="length"/> is -1,
        /// if <paramref name="length"/> is zero,
        /// or if the mapped address range exceeds the 24-bit address space.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the requested device address range exceeds the device's size.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the requested mapping overlaps with an existing device mapping.
        /// </exception>
        public void AddDevice(MappableDevice device, uint mapLocation, uint offset = 0, long length = -1)
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

            ulong mapEnd = (ulong)mapLocation + (ulong)length - 1;

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

        private KeyValuePair<uint, MappableDevice>? SeekDevice(uint address)
        {
            KeyValuePair<uint, MappableDevice>? result = null;
            List<uint> locations = [.. _memmap.Keys];
            int index = locations.BinarySearch(address);
            if (index >= 0) // Hit the first byte of an entry
            {
                result = new KeyValuePair<uint, MappableDevice>(locations[index], _memmap[locations[index]].dev);
            }
            else if (index < 0) index = ~index; // Get insertion point if exact match not found

            // If we didn't hit the starting byte of an entry, see if we're in range of the previous one
            if (index > 0)
            {
                var prev = _memmap.ElementAt(index - 1);
                if (address >= prev.Key && address < prev.Key + prev.Value.length)
                {
                    result = new KeyValuePair<uint, MappableDevice>(prev.Key, prev.Value.dev);
                }
            }

            // No use checking the next entry, since the address is obviously before the entry's start
            return result;
        }

        public MappableDevice? GetDeviceAt(uint address)
        {
            KeyValuePair<uint, MappableDevice>? entry = SeekDevice(address);
            if (entry is not null)
            {
                return entry.Value.Value;
            }
            else
            {
                return null;
            }
        }

        private uint TranslateAddress(KeyValuePair<uint, MappableDevice> entry, uint address)
        {
            return address - entry.Key + _memmap[entry.Key].offset;
        }

        /// <summary>
        /// Removes a device and all its mappings from the memory map.
        /// </summary>
        /// <param name="device">The device to remove</param>
        public void RemoveDevice(MappableDevice device)
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

        /// <summary>
        /// Removes a mapping from the memory map.
        /// </summary>
        /// <remarks>
        /// If there are no other mappings for the device, it will be removed from the device list as well.
        /// </remarks>
        /// <param name="address">The address of the mapping to remove.</param>
        public void RemoveMapping(uint address)
        {
            KeyValuePair<uint, MappableDevice>? entry = SeekDevice(address);
            if (entry is not null)
            {
                var device = entry.Value.Value;
                _memmap.Remove(entry.Value.Key);
                if (!IsDeviceMapped(device)) _devices.Remove(device);
            }
        }

        private bool IsDeviceMapped(MappableDevice device)
        {
            foreach (var entry in _memmap)
            {
                if (entry.Value.dev == device) return true;
            }
            return false;
        }

        public void Clear()
        {
            _devices.Clear();
            _memmap.Clear();
        }

        internal void InitAll()
        {
            foreach (var device in _devices)
            {
                device.Init();
            }
        }

        public void OnInterruptChange(object? sender, bool value)
        {
            if (sender is IInterruptingDevice device)
            {
                if (value)
                {
                    _interruptingDevices.Add(device);
                }
                else
                {
                    _interruptingDevices.Remove(device);
                }
            }
        }

        public void AddInterruptingDevice(IInterruptingDevice device)
        {
            device.InterruptStatusChanged += OnInterruptChange;
        }

        public void RemoveInterruptingDevice(IInterruptingDevice device)
        {
            _interruptingDevices.Remove(device);
            device.InterruptStatusChanged -= OnInterruptChange;
        }

        private JsonArray GetDeviceList()
        {
            JsonArray result = new();
            foreach (var device in _devices)
            {
                result.Add(device.GetDefinition());
            }
            return result;
        }

        private JsonArray GetMapList()
        {
            JsonArray result = new();
            foreach (var entry in _memmap)
            {
                JsonObject mapEntry = new()
                {
                    { "address", entry.Key },
                    { "length", entry.Value.length },
                    { "device", entry.Value.dev.guid.ToString() },
                    { "offset", entry.Value.offset }
                };
                result.Add(mapEntry);
            }
            return result;
        }

        /// <summary>
        /// Serializes the memory map to a JSON object, including all device definitions and mappings.
        /// </summary>
        /// <returns>
        /// A JSON object containing:
        /// devices: a list of device definitions,
        /// mappings: a list of mappings.
        /// </returns>
        public JsonObject ToJson()
        {
            JsonObject result = new()
            {
                { "devices", GetDeviceList() },
                { "mappings", GetMapList() }
            };
            return result;
        }

        public void FromJson(JsonObject obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj), "Params object cannot be null.");
            else
            {
                if (obj["devices"] == null)
                    throw new ArgumentNullException(nameof(obj), "Devices parameter is required.");
                else
                {
                    Dictionary<Guid, MappableDevice> devices = new();
                    foreach (var deviceDef in obj["devices"].AsArray())
                    {
                        var dev = DeviceFactory.CreateFromJson(deviceDef.AsObject());
                        if (dev is MappableDevice mappableDev)
                        {
                            devices.Add(mappableDev.guid, mappableDev);
                        }
                    }
                    if (obj["mappings"] == null)
                        throw new ArgumentNullException(nameof(obj), "Mappings parameter is required.");
                    else
                    {
                        foreach (var mapDef in obj["mappings"].AsArray())
                        {
                            uint address = mapDef["address"].GetValue<uint>();
                            uint length = mapDef["length"].GetValue<uint>();
                            Guid guid = Guid.Parse(mapDef["device"].GetValue<string>());
                            uint offset = mapDef["offset"].GetValue<uint>();
                            if (devices.TryGetValue(guid, out MappableDevice device))
                            {
                                AddDevice(device, address, offset, length);
                            }
                        }
                    }
                }
            }
        }

        public JsonArray GetState()
        {
            JsonArray result = new();
            foreach (var device in _devices)
            {
                JsonObject entry = new()
                {
                    { "guid", device.guid.ToString() },
                    { "state", device.GetState() }
                };
            }
            return result;
        }
    }
}
