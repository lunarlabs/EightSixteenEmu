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
using EightSixteenEmu.MemoryMapping;

namespace EightSixteenEmu
{
    public class EmuCore
    {
        private readonly Microprocessor _mpu;
        private readonly MemoryMapper _mapper;
        public Microprocessor MPU { get { return _mpu; } }
        public MemoryMapper Mapper { get { return _mapper; } }
        private readonly byte[] _fastRam = new byte[0x10000];
        private SortedList<(uint start, uint end), IMappableDevice> _devices = [];
        private List<IInterruptingMappableDevice> interruptingMappableDevices = [];

        public event EventHandler? ClockTick;
        public event EventHandler? Reset;
        public event EventHandler? NMI;
        public event EventHandler? IRQ;

        public EmuCore()
        {
            _mpu = new(this);
            _mapper = new();
        }

        public EmuCore(MemoryMapper mapper)
        {
            _mpu = new(this);
            _mapper = mapper;
        }

        #region Device Management
        //public void AddDevice(IMappableDevice device, uint baseAddress)
        //{
        //    uint start = baseAddress;
        //    uint end = baseAddress + device.Size - 1;
        //    if (end > 0xFFFFFF)
        //    {
        //        throw new ArgumentOutOfRangeException($"Addresses for {device.GetType()} fall outside the 24-bit address space.");
        //    }
        //    else
        //    {
        //        foreach (var dev in _devices)
        //        {
        //            if (Math.Max(start, dev.Key.start) <= Math.Min(end, dev.Key.end))
        //            {
        //                throw new InvalidOperationException($"Addresses for {device.GetType()} (${start:x6} - ${end:x6}) conflict with existing device {dev.Value} at ${dev.Key.start:x6} - ${dev.Key.end:x6}");
        //            }
        //        }
        //        _devices.Add((baseAddress, baseAddress + device.Size - 1), device);
        //        if (device is IInterruptingMappableDevice interruptingDevice)
        //        {
        //            interruptingDevice.Interrupt += _mpu.OnInterrupt;
        //            interruptingMappableDevices.Add(interruptingDevice);
        //        }
        //    }
        //}

        //public void RemoveDevice(IMappableDevice device)
        //{
        //    _devices.Remove(_devices.First(x => x.Value == device).Key);
        //    if (device is IInterruptingMappableDevice interruptingDevice)
        //    {
        //        interruptingDevice.Interrupt -= _mpu.OnInterrupt;
        //        interruptingMappableDevices.Remove(interruptingDevice);
        //    }
        //}

        //public void RemoveDevice(uint address)
        //{
        //    KeyValuePair<(uint start, uint end), IMappableDevice>? entry = GetDevice(address);
        //    if (entry is not null)
        //    {
        //        _devices.Remove(entry.Value.Key);
        //        if (entry.Value.Value is IInterruptingMappableDevice interruptingDevice)
        //        {
        //            interruptingDevice.Interrupt -= _mpu.OnInterrupt;
        //            interruptingMappableDevices.Remove(interruptingDevice);
        //        }
        //    }
        //    else Console.WriteLine($"Attempt to remove device at ${address:x6} failed because there is already no device at that address.");
        //}

        //public void ClearDevices()
        //{
        //    _devices.Clear();
        //}


        //public string DeviceList()
        //{
        //    string result = "";
        //    uint lastUsedAddress = 0xffffffff;
        //    foreach (var device in _devices)
        //    {
        //        (uint start, uint end) = device.Key;
        //        if (start != lastUsedAddress + 1)
        //        {
        //            result += $"${lastUsedAddress + 1:x6} - ${start - 1:x6}: Unused\n";
        //        }
        //        result += $"${start:x6} - ${end:x6}: {device.Value}\n";
        //        lastUsedAddress = end;
        //    }
        //    if (lastUsedAddress < 0xffffff)
        //    {
        //        result += $"${lastUsedAddress + 1:x6} - $ffffff: Unused\n";
        //    }
        //    return result;
        //}

        //internal byte? Read(uint address)
        //{
        //    byte? result = null;
        //    KeyValuePair<(uint start, uint end), IMappableDevice>? entry = GetDevice(address);
        //    if (entry is not null)
        //    {
        //        uint devAddress = address - entry.Value.Key.start;
        //        if (entry.Value.Value is MirrorDevice mirror)
        //        {
        //            result = Read(devAddress + mirror.BaseAddress);
        //        }
        //        else if (entry.Value.Value is FastRAMDevice)
        //        {
        //            result = _fastRam[address];
        //        }
        //        else if (entry.Value.Value is IMappedReadDevice device)
        //        {
        //            result = device[devAddress];
        //        }
        //        else Console.WriteLine($"Attempt to read from write-only device {entry.Value.Value} at ${address:x6}");
        //    }
        //    else Console.WriteLine($"Open bus read at ${address:x6}");
        //    return result;
        //}

        //internal void Write(uint address, byte value)
        //{
        //    KeyValuePair<(uint start, uint end), IMappableDevice>? entry = GetDevice(address);
        //    if (entry is not null)
        //    {
        //        uint devAddress = address - entry.Value.Key.start;
        //        if (entry.Value.Value is MirrorDevice mirror)
        //        {
        //            Write(devAddress + mirror.BaseAddress, value);
        //        }
        //        else if (entry.Value.Value is FastRAMDevice)
        //        {
        //            _fastRam[address] = value;
        //        }
        //        else if (entry.Value.Value is IMappedWriteDevice device)
        //        {
        //            device[devAddress] = value;
        //        }
        //        else Console.WriteLine($"Attempt to write to read-only device {entry.Value.Value} at ${address:x6}");
        //    }
        //    else Console.WriteLine($"Open bus write at ${address:x6}");
        //}

        //private KeyValuePair<(uint start, uint end), IMappableDevice>? GetDevice(uint address)
        //{
        //    var device = _devices.FirstOrDefault(d => address >= d.Key.start && address <= d.Key.end);
        //    return device;
        //}

        //private class MirrorDevice(uint baseAddress, uint size) : IMappableDevice
        //{
        //    readonly uint _size = size;
        //    readonly uint _baseAddress = baseAddress;
        //    public uint Size => _size;
        //    public uint BaseAddress => _baseAddress;

        //    public override string ToString()
        //    {
        //        return $"Mirror of ${_baseAddress:x6} - ${_baseAddress + _size - 1:x6})";
        //    }
        //}

        //private class FastRAMDevice(uint size) : IMappableDevice
        //{
        //    readonly uint _size = size;
        //    public uint Size => _size;
        //    public override string ToString()
        //    {
        //        return $"Fast RAM";
        //    }
        //}

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
