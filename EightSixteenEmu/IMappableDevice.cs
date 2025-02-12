using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EightSixteenEmu
{
    public interface IMappableDevice
    {
        uint size { get; }
        uint base_address { get; }
        byte this[uint index]
        {  get; set; }
    }
}
