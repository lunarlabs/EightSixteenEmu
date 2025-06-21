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
        private bool _enabled = false;
        private TimingMode _timingMode = TimingMode.ManualStep;
        private TimeSpan _clockPeriod = TimeSpan.FromMilliseconds(1); // Default to 1ms clock frequency
        private readonly List<IInterruptingDevice> interruptingMappableDevices = [];
        public Microprocessor MPU { get { return _mpu; } }
        public MemoryMapper Mapper { get { return _mapper; } }
        public TimeSpan ClockPeriod { get { return _clockPeriod; } }
        public double ClockFrequency
        {
            get
            {
                if (_clockPeriod == TimeSpan.Zero) return double.MaxValue;
                return 1.0 / _clockPeriod.TotalSeconds;
            }
        }
        public TimingMode CurrentTimingMode
        {
            get { return _timingMode; }
            set
            {
                if (_timingMode != value)
                {
                    ChangeTimingMode(value);
                    _timingMode = value;
                }
            }
        }

        public event EventHandler? ClockTick;
        public event EventHandler? Reset;
        public event EventHandler? NMI;

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

        internal void RegisterInterruptingDevice(IInterruptingDevice device)
        {
            interruptingMappableDevices.Add(device);
        }

        internal void UnregisterInterruptingDevice(IInterruptingDevice device)
        {
            interruptingMappableDevices.Remove(device);
        }

        #endregion

        #region Event Management

        public void Deactivate(bool resetDevices = true)
        {
            _mpu.Disable();
            if (resetDevices)
            { 
                _mapper.InitAll(); 
            }
        }

        public void Activate(bool usingReset = true)
        {
            if (usingReset)
            {
                _mpu.Reset();
            }
            else
            {
                _mpu.Enable();
            }
        }
        #endregion

        #region Clock Management

        public enum TimingMode
        {
            ManualStep, // manual step mode, clock ticks only on request, instruction or cycle step
            Frequency, // active clock, runs at a fixed frequency
            FreeRunning, // active clock, runs as fast as possible
        }

        public void SetClockPeriod(TimeSpan period)
        {
            if (period <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(period), "Clock period must be greater than zero.");
            _clockPeriod = period;
        }

        public void SetClockFrequency(double frequency)
        {
            if (frequency <= 0) throw new ArgumentOutOfRangeException(nameof(frequency), "Clock frequency must be greater than zero.");
            _clockPeriod = TimeSpan.FromSeconds(1.0 / frequency);
        }

        private void ChangeTimingMode(TimingMode mode)
        {
            if (_timingMode == mode) return;
            switch (mode)
            {
                case TimingMode.ManualStep:
                    throw new NotImplementedException();
                case TimingMode.Frequency:
                    throw new NotImplementedException();
                case TimingMode.FreeRunning:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }
        #endregion
    }
}
