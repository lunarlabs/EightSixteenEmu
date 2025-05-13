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
 *  Somewhat like a TL28L92 but with only the Channel A interface.
 *  Things like timers, GPIO, etc. are not implemented.
 *  
 *  So be careful with this device. If you're just using it for
 *  serial on channel A, you'll probably be fine. Code that
 *  uses the other features of the TL28L92 will likely not work.
 */

namespace EightSixteenEmu.Devices
{
    public class DevVirtualTerm() : MappableDevice(16, AccessMode.ReadWrite)
    {
        Queue<byte> _transmitQueue = new(16);
        Queue<byte> _receiveQueue = new(16);

        [Flags]
        public enum ModemStatus : byte
        {
            None = 0,
            RxReady = 1,
            RxFull = 2,
            TxReady = 4,
            TxEmpty = 8,
            OverrunError = 16,
            ParityError = 32,
            FramingError = 64,
            Break = 128
        }

        [Flags]
        public enum InterruptStatus: byte
        {
            None = 0,
            TxReady = 1,
            RxReady = 2,
            BreakChange = 4,
        }

    }
}
