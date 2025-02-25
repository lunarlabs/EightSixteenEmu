using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EightSixteenEmu.Devices
{
    public interface IDMAMappableDevice : IMappableDevice
    {
        EmuCore EmuCore { get; }
        public event EventHandler? DMAStart;
        public event EventHandler? DMAComplete;
    }
}
