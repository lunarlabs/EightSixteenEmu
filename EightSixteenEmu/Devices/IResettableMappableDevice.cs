﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EightSixteenEmu.Devices
{
    public interface IResettableMappableDevice : IMappableDevice
    {
        void OnReset();
    }
}
