using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EightSixteenEmu.Devices
{
    public class DevVirtualUART : IMappedReadDevice, IMappedWriteDevice, IInterruptingMappableDevice
    {
        public bool Interrupting => ((UARTStatus)_modemStatusRegister & UARTStatus.ModemStatusChange) == UARTStatus.ModemStatusChange;
        public event EventHandler? Interrupt;
        public uint Size => 4;
        public byte this[uint index]
        {
            get{ switch (index)
                {
                    case 0:
                        return _recieveBuffer;
                    case 1:
                        return _uartStatusRegister;
                    case 2:
                        return _modemControlRegister;
                    case 3:
                        return _modemStatusRegister;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(index));
                }
            }
            set => throw new NotImplementedException();
        }

        public bool DataTerminalReady { get => throw new NotImplementedException(); }
        public bool RequestToSend { get => throw new NotImplementedException(); }
        public bool ClearToSend { set => throw new NotImplementedException(); }
        public bool DataSetReady { set => throw new NotImplementedException(); }

        private byte _uartControlRegister;
        private byte _baudRateSelectRegister;
        private byte _transmitBuffer;
        private byte _recieveBuffer;
        private byte _modemControlRegister;
        private byte _uartStatusRegister;
        private byte _modemStatusRegister;
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

        void IMappableDevice.Init()
        {
            _uartControlRegister = 0;
            _baudRateSelectRegister = 0;
        }

        internal void Reset()
        {
            _uartStatusRegister = (byte)(UARTStatus.TransmissionComplete | UARTStatus.TransmitterBufferRegisterEmpty);
            _modemControlRegister = 0;
        }
    }
}
