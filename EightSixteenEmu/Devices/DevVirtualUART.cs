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
 *  Virtual UART device
 *  Based on the Renesas 82C52 UART
 *  Datasheet: https://www.mouser.com/datasheet/2/698/Renesas-Electronics-1996235.pdf
 */

namespace EightSixteenEmu.Devices
{
    public class DevVirtualUART : IMappedReadDevice, IMappedWriteDevice, IInterruptingDevice
    {
        const byte UCRParityControlMask = 0b0000_1110;
        const byte UCRWordLengthMask = 0b0011_0000;
        const byte MCRModeSelectMask = 0b0001_1000;

        private byte _uartControlRegister;
        private byte _baudRateSelectRegister;
        private byte _transmitBuffer;
        private byte _transmitter;
        private byte _recieveBuffer;
        private byte _echoBuffer;
        private ModemControl _modemControlRegister;
        private UARTStatus _uartStatusRegister;
        private ModemStatus _modemStatusRegister;
        private bool _ucrInterrupting;
        private bool _mcrInterrupting;
        private bool _prevInterrupting;

        public event EventHandler<bool>? InterruptStatusChanged;
        public event EventHandler<byte>? DataOut;

        public bool Interrupting => false;
        public uint Size => 4;
        public byte this[uint index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return _recieveBuffer;
                    case 1:
                        _ucrInterrupting = false;
                        CheckInterrupt();
                        return (byte)_uartStatusRegister;
                    case 2:
                        _mcrInterrupting = false;
                        CheckInterrupt();
                        return (byte)_modemControlRegister;
                    case 3:
                        return (byte)_modemStatusRegister;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(index));
                }
            }

            set
            {
                switch (index)
                {
                    case 0:
                        _transmitBuffer = value;
                        _uartStatusRegister &= ~UARTStatus.TransmitterBufferRegisterEmpty;
                        break;
                    case 1:
                        _uartControlRegister = value;
                        break;
                    case 2:
                        _modemControlRegister = (ModemControl)value;
                        break;
                    case 3:
                        // does nothing in the emulator but putting this here
                        // so that no exception is thrown
                        _baudRateSelectRegister = value;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(index));
                }
            }
        }

        public bool DataTerminalReady { get => (_modemControlRegister & ModemControl.DataTerminalReady) == 0; }
        public bool RequestToSend { get => (_modemControlRegister & ModemControl.RequestToSend) == 0; }
        public bool ClearToSend
        {
            set
            {
                if (value)
                {
                    _modemStatusRegister |= ModemStatus.ClearToSend;
                    if (!TransmitBufferRegisterEmpty)
                    {

                    }
                }
                else
                {
                    _modemStatusRegister &= ~ModemStatus.ClearToSend;
                }
                _mcrInterrupting = true;
                _uartStatusRegister |= UARTStatus.ModemStatusChange;
                CheckInterrupt();
            }
        }
        public bool DataSetReady
        {
            set
            {
                if (value)
                {
                    _modemStatusRegister |= ModemStatus.DataSetReady;
                }
                else
                {
                    _modemStatusRegister &= ~ModemStatus.DataSetReady;
                }
                _mcrInterrupting = true;
                CheckInterrupt();
            }
        }
        public bool TransmitBufferRegisterEmpty => (_uartStatusRegister & UARTStatus.TransmitterBufferRegisterEmpty) != 0;
        public bool DataReady => (_uartStatusRegister & UARTStatus.DataReady) != 0;

        private enum Operation : byte
        {
            TransmitRecieve = 0b00,
            UARTControlStatus = 0b01,
            ModemControlRegister = 0b10,
            BitRateStatusRegister = 0b11
        }

        [Flags]
        internal enum UARTStatus : byte
        {
            None = 0,
            ParityError = 0b0000_0001,
            FramingError = 0b0000_0010,
            OverrunError = 0b0000_0100,
            RecievedBreak = 0b0000_1000,
            ModemStatusChange = 0b0001_0000,
            TransmissionComplete = 0b0010_0000,
            TransmitterBufferRegisterEmpty = 0b0100_0000,
            DataReady = 0b1000_0000
        }

        internal enum WordLength : byte
        {
            FiveBits = 0b0000_0000,
            SixBits = 0b0001_0000,
            SevenBits = 0b0010_0000,
            EightBits = 0b0011_0000,
        }

        [Flags]
        internal enum ModemControl : byte
        {
            None = 0,
            RequestToSend = 0b0000_0001,
            DataTerminalReady = 0b0000_0010,
            InterruptEnable = 0b0000_0100,
            ModeSelectBitOne = 0b0000_1000,
            ModeSelectBitTwo = 0b0001_0000,
            ReceiverEnable = 0b0010_0000,
            ModemInterruptEnable = 0b0100_0000,
        }

        internal enum ModemControlMode : byte
        {
            Normal = 0b0000_0000,
            TransmitBreak = 0b0000_1000,
            Echo = 0b0001_0000,
            LoopTest = 0b0001_1000
        }

        [Flags]
        internal enum ModemStatus : byte
        {
            None = 0,
            ClearToSend = 0b0000_0001,
            DataSetReady = 0b0000_0010,
        }

        private enum CharacterBitMasks : byte
        {
            Five = 0b0001_1111,
            Six = 0b0011_1111,
            Seven = 0b0111_1111,
            Eight = 0b1111_1111,
        }

        void IMappableDevice.Init()
        {
            _uartControlRegister = 0;
            _baudRateSelectRegister = 0;
            Reset();
        }

        internal void Reset()
        {
            _uartStatusRegister = (UARTStatus.TransmissionComplete | UARTStatus.TransmitterBufferRegisterEmpty);
            _modemControlRegister = 0;
        }

        private void CheckInterrupt()
        {
            if (Interrupting != _prevInterrupting)
            {
                _prevInterrupting = Interrupting;
                InterruptStatusChanged?.Invoke(this, Interrupting);
            }
        }

        private void MoveToTransmitter()
        {
            _transmitter = _transmitBuffer;
            _uartStatusRegister |= UARTStatus.TransmitterBufferRegisterEmpty;
        }

        private void SendData()
        {
            switch ((ModemControlMode)(_modemControlRegister & (ModemControl)MCRModeSelectMask))
            {
                case ModemControlMode.Normal:
                    DataOut?.Invoke(this, _transmitter);
                    break;
                case ModemControlMode.TransmitBreak:
                    DataOut?.Invoke(this, 0);
                    break;
                case ModemControlMode.Echo:
                    // toss it into the bit bucket
                    break;
                case ModemControlMode.LoopTest:
                    throw new NotImplementedException("Loop test mode not implemented");
                default:
                    throw new ArgumentOutOfRangeException(nameof(_modemControlRegister));
            }
            _uartStatusRegister |= UARTStatus.TransmissionComplete;
        }

        private void MoveToRecieveBuffer(byte data)
        {
            if (DataReady)
            {

            }
        }
    }
}
