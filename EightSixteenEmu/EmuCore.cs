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

        private static EmuCore? _instance;
        private static readonly Lock _lock = new();

        public static EmuCore Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new EmuCore();
                    return _instance;
                }
            }
        }

        private readonly Microprocessor _mpu;
        private readonly MemoryMapper _mapper;
        public Microprocessor MPU { get { return _mpu; } }
        public MemoryMapper Mapper { get { return _mapper; } }
        private readonly List<IInterruptingMappableDevice> interruptingMappableDevices = [];

        public event EventHandler? ClockTick;
        public event EventHandler? Reset;
        public event EventHandler? NMI;
        public event EventHandler? IRQ;

        private EmuCore()
        {
            _mpu = new(this);
            _mapper = new();
        }

        #region Device Management

        internal void RegisterInterruptingDevice(IInterruptingMappableDevice device)
        {
            interruptingMappableDevices.Add(device);
        }

        internal void UnregisterInterruptingDevice(IInterruptingMappableDevice device)
        {
            interruptingMappableDevices.Remove(device);
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
