/*    _____      __   __  _____      __               ____          
 *   / __(_)__ _/ /  / /_/ __(_)_ __/ /____ ___ ___  / __/_ _  __ __
 *  / _// / _ `/ _ \/ __/\ \/ /\ \ / __/ -_) -_) _ \/ _//  ' \/ // /
 * /___/_/\_, /_//_/\__/___/_//_\_\\__/\__/\__/_//_/___/_/_/_/\_,_/ 
 *       /___/                                                      
 * 
 *  W65C816S microprocessor emulator
 *  Copyright (C) 2025 Matthias Lamers
 *  Released under GNUGPLv2, see LICENSE.txt for details.
 *  
 *  Virtual terminal inferface device
 */

namespace EightSixteenEmu.Devices
{
    public class DevVirtualTerm : MappableDevice
    {
        const int WatchdogCount = 64; //64 cycles of CPU clock
        private readonly byte[] fillThreshold = [1, 8, 12, 16];

        private readonly Queue<byte> _transmitQueue = new(16);
        private readonly Queue<byte> _receiveQueue = new(16);
        private readonly Queue<byte> _hostRecieved = new();
        private byte _txHold;
        private byte _rxHold;
        private bool _txEnable = false;
        private bool _rxEnable = false;
        private bool _overrun = false;
        private bool _breaking = false;
        private bool _hostBreak = false;
        private bool _clearToSend = false;
        private bool _requestToSend = false;
        private VTSettings _settings;
        private VTInterrupts _interruptStatus;
        private VTInterrupts _interruptMask;
        private int _watchdogTimer = WatchdogCount;

        #region Enums
        public enum ModeSelect : byte
        {
            Data = 0,
            Command = 1,
            Status = 2,
            Interrupt = 3,
        }

        [Flags]
        public enum VTStatus : byte
        {
            None = 0,
            TransmitEmpty = 1 << 0, // no data in _transmitQueue
            TransmitReady = 1 << 1, // _transmitQueue is not full
            ReceiveReady = 1 << 2, // data in _receiveQueue
            ReceiveFull = 1 << 3, // _receiveQueue is full
            OverrunError = 1 << 4, // data lost in _receiveQueue
            FramingError = 1 << 5, // data lost in _receiveQueue
            BreakReceived = 1 << 6, // break received
            ClearToSend = 1 << 7, // clear to send signal from the host
        }

        [Flags]
        public enum VTSettings : byte
        {
            None = 0,
            ChannelModeOne = 1 << 0, // channel mode bit 1
            ChannelModeTwo = 1 << 1, // channel mode bit 2
            WatchdogActive = 1 << 2, // watchdog active
            WaitForClearToSend = 1 << 3, // wait for clear to send signal before each transmit
            ErrorBlockMode = 1 << 4, // if set, errors are for entire fifo, not per byte
            InterruptFillLevelOne = 1 << 5, // interrupt fill level bit 1
            InterruptFillLevelTwo = 1 << 6, // interrupt fill level bit 2
            RTSAutoDisable = 1 << 7, // if set, RTS auto disables if _receiveQueue is full
        }

        [Flags]
        public enum VTCommand : byte
        {
            None = 0,
            TxEnable = 1 << 0, // transmit enable
            TxDisable = 1 << 1, // transmit disable
            RxEnable = 1 << 2, // receive enable
            RxDisable = 1 << 3, // receive disable
            CommandBitOne = 1 << 4, // command bit 1
            CommandBitTwo = 1 << 5, // command bit 2
            CommandBitThree = 1 << 6, // command bit 3
            CommandBitFour = 1 << 7, // command bit 4
        }

        [Flags]
        public enum VTInterrupts : byte
        {
            None = 0,
            TxFillLevel = 1 << 0, // transmit fill level interrupt
            RxReady = 1 << 1, // ready buffer reached threshold or watchdog timer timeout
            BreakChange = 1 << 2, // break change interrupt
        }

        public enum VTMiscCommands : byte
        {
            Nop = 0b0000,
            ResetRxQueue = 0b0010,
            ResetTxQueue = 0b0011,
            ResetErrors = 0b0100,
            ResetBreakChange = 0b0101,
            StartBreak = 0b0110,
            StopBreak = 0b0111,
            AssertRTS = 0b1000,
            NegateRTS = 0b1001,
            EnablePowerDown = 0b1110,
            DisablePowerDown = 0b1111,

        }
        #endregion

        public DevVirtualTerm() : base(4, AccessMode.ReadWrite)
        {
        }

        public DevVirtualTerm(Guid? guid) : base(4, AccessMode.ReadWrite, guid)
        {
        }


        #region host-side methods
        public bool ClearToSend => _clearToSend;
        public bool GetRequestToSend()
        {
            if(_settings.HasFlag(VTSettings.RTSAutoDisable))
            {
                return (_receiveQueue.Count != _receiveQueue.Capacity) && _rxEnable;
            }
            else return _requestToSend;
        }
        public void Write(byte data)
        {
            if (_rxEnable)
            {
                if (_receiveQueue.Count < _receiveQueue.Capacity)
                {
                    _receiveQueue.Enqueue(data);
                }
                else
                    _overrun = true;
            }
        }

        public byte[] GetBytes()
        {
            byte[] data = new byte[_hostRecieved.Count];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = _hostRecieved.Dequeue();
            }
            return data;
        }
        #endregion

        #region emulator-side methods
        internal override byte this[uint index]
        {
            get
            {
                return base[index];
            }
            set => base[index] = value;
        }
        #endregion

        private VTStatus GetVTStatus() => ((_transmitQueue.Count == 0) ? VTStatus.TransmitEmpty : 0)
                | ((_transmitQueue.Count == _transmitQueue.Capacity) ? 0 : VTStatus.TransmitReady)
                | ((_receiveQueue.Count > 0) ? VTStatus.ReceiveReady : 0)
                | ((_receiveQueue.Count == _receiveQueue.Capacity) ? VTStatus.ReceiveFull : 0)
                | (_overrun ? VTStatus.OverrunError : 0)
                | (_hostBreak ? VTStatus.BreakReceived : 0)
                | (_clearToSend ? VTStatus.ClearToSend : 0);

        private void DoMiscCommand(VTMiscCommands command)
        {
            switch (command)
            {
                case VTMiscCommands.ResetRxQueue:
                    _receiveQueue.Clear();
                    _rxEnable = false;
                    break;
                case VTMiscCommands.ResetTxQueue:
                    _transmitQueue.Clear();
                    _txEnable = false;
                    break;
                case VTMiscCommands.ResetErrors:
                    _overrun = false;
                    break;
                case VTMiscCommands.ResetBreakChange:
                    _hostBreak = false;
                    break;
                case VTMiscCommands.StartBreak:
                    _breaking = true;
                    break;
                case VTMiscCommands.StopBreak:
                    _breaking = false;
                    break;
                case VTMiscCommands.AssertRTS:
                    _requestToSend = true;
                    break;
                case VTMiscCommands.NegateRTS:
                    _requestToSend = false;
                    break;
                default:
                    break;
            }
        }

        private void MoveTransmitted()
        {
            _hostRecieved.Enqueue(_transmitQueue.Dequeue());
        }
    }
}
