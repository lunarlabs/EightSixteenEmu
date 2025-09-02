namespace EightSixteenEmu
{
    public partial class NewCore
    {
        partial class Processor
        {
            // At the start of each new instruction, treat the internal data and address registers as unknown.
            // No operation uses the values of the previous instruction's internal data or address registers.
            private static readonly Dictionary<W65C816.OpCode, Func<Processor, List<Cycle>>> OpcodeLookupTable
                = new()
                {
                    // Note to self: if there's no variation in cycle count for an operation,
                    // you can use the simplified collection initializer syntax.
                    // But if there are variations, you need to use the full syntax
                    // (i.e. var cycles = new List<Cycle> { ... }; if (statement) { cycles.Add(...) }; return cycles;)
                    [W65C816.OpCode.NOP] = proc =>
                    [
                        new Cycle(
                            proc,
                            Cycle.CycleType.Internal,
                            new List<IMicroOp> {},
                            null
                        )
                    ],
                };

            private static readonly Dictionary<W65C816.AddressingMode, Func<Processor, List<Cycle>>> AddressingModeLookupTable
                = new()
                {
                    [W65C816.AddressingMode.Implied] = proc => [],
                    [W65C816.AddressingMode.Accumulator] = proc => [],
                };

            private static List<Cycle> VectorJump(Processor proc, W65C816.Vector vector) =>
            [
                new Cycle(
                    proc: proc,
                    type: Cycle.CycleType.Read,
                    actions: new List<IMicroOp>
                    {
                        new MicroOpSetRegister(RegisterType.RegPB, 0x00), // Set PB to 0 for the jump
                        new MicroOpMove(RegisterType.DataBus, RegisterType.RegPCL),
                    },
                    address: (uint)vector
                ),
                new Cycle(
                    proc: proc,
                    type: Cycle.CycleType.Read,
                    actions: new List<IMicroOp>
                    {
                        new MicroOpMove(RegisterType.DataBus, RegisterType.RegPCH),
                    },
                    address: (uint)(vector + 1)
                ),
            ];

            private static readonly Dictionary<RegisterType, string> RegisterNames = new()
            {
                { RegisterType.RegA, "A" },
                { RegisterType.RegAL, "AL" },
                { RegisterType.RegAH, "AH" },
                { RegisterType.RegX, "X" },
                { RegisterType.RegXL, "XL" },
                { RegisterType.RegXH, "XH" },
                { RegisterType.RegY, "Y" },
                { RegisterType.RegYL, "YL" },
                { RegisterType.RegYH, "YH" },
                { RegisterType.RegDP, "DP" },
                { RegisterType.RegDL, "DL" },
                { RegisterType.RegDH, "DH" },
                { RegisterType.RegSP, "SP" },
                { RegisterType.RegSL, "SL" },
                { RegisterType.RegSH, "SH" },
                { RegisterType.RegDB, "DB" },
                { RegisterType.RegPB, "PB" },
                { RegisterType.RegPC, "PC" },
                { RegisterType.RegPCL, "PCL" },
                { RegisterType.RegPCH, "PCH" },
                { RegisterType.RegID, "ID" },
                { RegisterType.RegIDL, "IDL" },
                { RegisterType.RegIDH, "IDH" },
                { RegisterType.RegIA, "IA" },
                { RegisterType.RegIAL, "IAL" },
                { RegisterType.RegIAH, "IAH" },
                { RegisterType.DataBus, "MB" },
            };

            private static RegisterType GetRegisterType(string name) =>
                RegisterNames.First(kv => kv.Value.Equals(name, StringComparison.OrdinalIgnoreCase)).Key;

            private static bool IsRegisterByte(RegisterType reg) =>
                reg is RegisterType.RegAL or RegisterType.RegAH or RegisterType.RegXL or RegisterType.RegXH or
                       RegisterType.RegYL or RegisterType.RegYH or RegisterType.RegDL or RegisterType.RegDH or
                       RegisterType.RegSL or RegisterType.RegSH or RegisterType.RegDB or RegisterType.RegPB or
                       RegisterType.RegPCL or RegisterType.RegPCH or RegisterType.RegIDL or RegisterType.RegIDH or
                       RegisterType.RegIAL or RegisterType.RegIAH or RegisterType.DataBus;

            private ushort GetRegisterValue(RegisterType reg)
            {
                return reg switch
                {
                    RegisterType.RegA => _regA,
                    RegisterType.RegAL => RegAL,
                    RegisterType.RegAH => RegAH,
                    RegisterType.RegX => _regX,
                    RegisterType.RegXL => RegXL,
                    RegisterType.RegXH => RegXH,
                    RegisterType.RegY => _regY,
                    RegisterType.RegYL => RegYL,
                    RegisterType.RegYH => RegYH,
                    RegisterType.RegDP => _regDP,
                    RegisterType.RegDL => RegDL,
                    RegisterType.RegDH => RegDH,
                    RegisterType.RegSP => _regSP,
                    RegisterType.RegSL => RegSL,
                    RegisterType.RegSH => RegSH,
                    RegisterType.RegDB => _regDB,
                    RegisterType.RegPB => _regPB,
                    RegisterType.RegPC => _regPC,
                    RegisterType.RegPCL => RegPCL,
                    RegisterType.RegPCH => RegPCH,
                    RegisterType.RegID => _internalData,
                    RegisterType.RegIDL => RegIDL,
                    RegisterType.RegIDH => RegIDH,
                    RegisterType.RegIA => _internalAddress,
                    RegisterType.RegIAL => RegIAL,
                    RegisterType.RegIAH => RegIAH,
                    RegisterType.DataBus => _dataBus,
                    _ => throw new ArgumentOutOfRangeException(nameof(reg), $"Not expected register type value: {reg}"),
                };
            }

            private void SetRegisterValue(RegisterType reg, ushort value)
            {
                switch (reg)
                {
                    case RegisterType.RegA:
                        _regA = value;
                        break;
                    case RegisterType.RegAL:
                        RegAL = (byte)(value & 0x00FF);
                        break;
                    case RegisterType.RegAH:
                        RegAH = (byte)(value & 0x00FF);
                        break;
                    case RegisterType.RegX:
                        _regX = value;
                        break;
                    case RegisterType.RegXL:
                        RegXL = (byte)(value & 0x00FF);
                        break;
                    case RegisterType.RegXH:
                        RegXH = (byte)(value & 0x00FF);
                        break;
                    case RegisterType.RegY:
                        _regY = value;
                        break;
                    case RegisterType.RegYL:
                        RegYL = (byte)(value & 0x00FF);
                        break;
                    case RegisterType.RegYH:
                        RegYH = (byte)(value & 0x00FF);
                        break;
                    case RegisterType.RegDP:
                        _regDP = value;
                        break;
                    case RegisterType.RegDL:
                        RegDL = (byte)(value & 0x00FF);
                        break;
                    case RegisterType.RegDH:
                        RegDH = (byte)(value & 0x00FF);
                        break;
                    case RegisterType.RegSP:
                        _regSP = value;
                        break;
                    case RegisterType.RegSL:
                        RegSL = (byte)(value & 0x00FF);
                        break;
                    case RegisterType.RegSH:
                        RegSH = (byte)(value & 0x00FF);
                        break;
                    case RegisterType.RegDB:
                        _regDB = (byte)(value & 0x00FF);
                        break;
                    case RegisterType.RegPB:
                        _regPB = (byte)(value & 0x00FF);
                        break;
                    case RegisterType.RegPC:
                        _regPC = value;
                        break;
                    case RegisterType.RegPCL:
                        RegPCL = (byte)(value & 0x00FF);
                        break;
                    case RegisterType.RegPCH:
                        RegPCH = (byte)(value & 0x00FF);
                        break;
                    case RegisterType.RegID:
                        _internalData = value;
                        break;
                    case RegisterType.RegIDL:
                        RegIDL = (byte)(value & 0x00FF);
                        break;
                    case RegisterType.RegIDH:
                        RegIDH = (byte)(value & 0x00FF);
                        break;
                    case RegisterType.RegIA:
                        _internalAddress = value;
                        break;
                    case RegisterType.RegIAL:
                        RegIAL = (byte)(value & 0x00FF);
                        break;
                    case RegisterType.RegIAH:
                        RegIAH = (byte)(value & 0x00FF);
                        break;
                    case RegisterType.DataBus:
                        _dataBus = (byte)(value & 0x00FF);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(reg), $"Not expected register type value: {reg}");
                }
            }

            private bool GetFlag(StatusFlags flag)
            {
                return (_regSR & flag) != 0;
            }
            private void SetFlag(StatusFlags flag, bool value)
            {
                if (value)
                {
                    _regSR |= flag;
                    if (((flag & StatusFlags.X) == StatusFlags.X) && !_flagE)
                    {
                        // clear the high bytes
                        _regX &= 0x00FF;
                        _regY &= 0x00FF;
                    }
                }
                else
                {
                    _regSR &= ~flag;
                }
            }
            private void UpdateZeroAndNegativeFlags(ushort result, bool isByte)
            {
                if (isByte)
                {
                    SetFlag(StatusFlags.Z, (result & 0x00FF) == 0);
                    SetFlag(StatusFlags.N, (result & 0x0080) != 0);
                }
                else
                {
                    SetFlag(StatusFlags.Z, result == 0);
                    SetFlag(StatusFlags.N, (result & 0x8000) != 0);
                }
            }
            private bool GetEmulationFlag()
            {
                return _flagE;
            }
            private void SetEmulationFlag(bool value)
            {
                _flagE = value;
                if (value)
                {
                    // When setting E flag to true, force 8-bit mode for A, X, Y and set S to 8-bit
                    SetFlag(StatusFlags.M, true);
                    SetFlag(StatusFlags.X, true);
                    _regSP &= 0x00FF; // Force high byte of SP to 0x01
                    _regSP |= 0x0100;
                }
            }
            private void SetIndexRegisterWidth(bool isByte)
            {
                SetFlag(StatusFlags.X, isByte);
                if (isByte)
                {
                    _regX &= 0x00FF;
                    _regY &= 0x00FF;
                }
            }
            private bool IsIndexRegisterByte()
            {
                return GetFlag(StatusFlags.X) || GetEmulationFlag();
            }
            private bool IsAccumulatorByte()
            {
                return GetFlag(StatusFlags.M) || GetEmulationFlag();
            }

            #region Micro-operations
            private struct MicroOpMove : IMicroOp
            {
                private readonly RegisterType _source;
                private readonly RegisterType _destination;
                public MicroOpMove(RegisterType source, RegisterType destination)
                {
                    _source = source;
                    _destination = destination;
                }
                public void Execute(Processor proc)
                {
                    ushort value = proc.GetRegisterValue(_source);
                    if (IsRegisterByte(_source) && !IsRegisterByte(_destination))
                    {
                        // Moving from byte register to word register, clear high byte
                        value &= 0x00FF;
                    }
                    proc.SetRegisterValue(_destination, value);
                }
                public override string ToString()
                {
                    return $"MOV {RegisterNames[_destination]}, {RegisterNames[_source]}";
                }
            }

            private struct MicroOpSetRegister : IMicroOp
            {
                private readonly RegisterType _register;
                private readonly ushort _value;
                public MicroOpSetRegister(RegisterType register, ushort value)
                {
                    _register = register;
                    _value = value;
                }
                public void Execute(Processor proc)
                {
                    proc.SetRegisterValue(_register, _value);
                }
                public override string ToString()
                {
                    return $"SET {RegisterNames[_register]}, #{_value:X4}";
                }
            }


            private struct MicroOpSetFlag : IMicroOp
            {
                private readonly StatusFlags _flag;
                private readonly bool _value;
                public MicroOpSetFlag(StatusFlags flag, bool value)
                {
                    _flag = flag;
                    _value = value;
                }
                public void Execute(Processor proc)
                {
                    proc.SetFlag(_flag, _value);
                }
                public override string ToString()
                {
                    return $"SETF {_flag}, {_value}";
                }
            }

            private struct MicroOpAddRegisters : IMicroOp
            {
                private readonly RegisterType _register1;
                private readonly RegisterType _register2;
                private readonly RegisterType _destination;
                private readonly bool _withCarry;
                private readonly bool _setOverflowFlag;

                public MicroOpAddRegisters(RegisterType register1, RegisterType register2, RegisterType destination,
                    bool withCarry = false, bool setOverflowFlag = false)
                {
                    _register1 = register1;
                    _register2 = register2;
                    _destination = destination;
                    _withCarry = withCarry;
                    _setOverflowFlag = setOverflowFlag;
                }

                public void Execute(Processor proc)
                {
                    ushort value1 = proc.GetRegisterValue(_register1);
                    ushort value2 = proc.GetRegisterValue(_register2);
                    ushort carryIn = _withCarry && proc.GetFlag(StatusFlags.C) ? (ushort)1 : (ushort)0;
                    ushort result = (ushort)(value1 + value2 + carryIn);
                    if (IsRegisterByte(_destination))
                    {
                        if (_withCarry) proc.SetFlag(StatusFlags.C, (value1 + value2 + carryIn) > 0x00FF);
                        if (_setOverflowFlag) proc.SetFlag(StatusFlags.V, (~(value1 ^ value2) & (value1 ^ result) & 0x0080) != 0);
                        result &= 0x00FF;
                    }
                    else
                    {
                        if (_withCarry) proc.SetFlag(StatusFlags.C, (value1 + value2 + carryIn) > 0xFFFF);
                        if (_setOverflowFlag) proc.SetFlag(StatusFlags.V, (~(value1 ^ value2) & (value1 ^ result) & 0x8000) != 0);
                    }
                    proc.SetRegisterValue(_destination, result);
                }
                public override string ToString()
                {
                    return $"ADD {RegisterNames[_destination]}, {RegisterNames[_register1]}, {RegisterNames[_register2]}" +
                        (_withCarry ? " c" : "") +
                        (_setOverflowFlag ? " v" : "");
                }
            }

            private struct MicroOpIncrementRegister : IMicroOp
            {
                private readonly RegisterType _register;
                private readonly ushort _amount;
                public MicroOpIncrementRegister(RegisterType register, ushort amount = 1)
                {
                    _register = register;
                    _amount = amount;
                }
                public void Execute(Processor proc)
                {
                    ushort value = proc.GetRegisterValue(_register);
                    if (IsRegisterByte(_register)) value += (byte)(_amount & 0x00FF);
                    else value += _amount;

                    proc.SetRegisterValue(_register, value);
                }
                public override string ToString()
                {
                    return $"INC {RegisterNames[_register]}, #{_amount}";
                }
            }

            private struct MicroOpDecrementRegister : IMicroOp
            {
                private readonly RegisterType _register;
                private readonly ushort _amount;
                public MicroOpDecrementRegister(RegisterType register, ushort amount = 1)
                {
                    _register = register;
                    _amount = amount;
                }
                public void Execute(Processor proc)
                {
                    ushort value = proc.GetRegisterValue(_register);
                    if (IsRegisterByte(_register)) value -= (byte)(_amount & 0x00FF);
                    else value -= _amount;
                    proc.SetRegisterValue(_register, value);
                }
                public override string ToString()
                {
                    return $"DEC {RegisterNames[_register]}, #{_amount}";
                }
            }

            private struct MicroOpIncrementRegisterSigned : IMicroOp
            {
                private readonly RegisterType _register;
                private readonly int _amount;

                public MicroOpIncrementRegisterSigned(RegisterType register, int amount = 1)
                {
                    _register = register;
                    _amount = amount;
                }
                public void Execute(Processor proc)
                {
                    ushort value = proc.GetRegisterValue(_register);
                    if (IsRegisterByte(_register)) value = (ushort)((sbyte)(value & 0x00FF) + _amount);
                    else value = (ushort)((short)value + _amount);
                    proc.SetRegisterValue(_register, value);
                }
                public override string ToString()
                {
                    return $"INCS {RegisterNames[_register]}, #{_amount}";
                }
            }

            private struct MicroOpSubtractRegisters : IMicroOp
            {
                private readonly RegisterType _register1;
                private readonly RegisterType _register2;
                private readonly RegisterType _destination;
                private readonly bool _withCarry;
                private readonly bool _setOverflowFlag;

                public MicroOpSubtractRegisters(RegisterType register1, RegisterType register2, RegisterType destination,
                    bool withCarry = false, bool setOverflowFlag = false)
                {
                    _register1 = register1;
                    _register2 = register2;
                    _destination = destination;
                    _withCarry = withCarry;
                    _setOverflowFlag = setOverflowFlag;
                }

                public void Execute(Processor proc)
                {
                    ushort value1 = proc.GetRegisterValue(_register1);
                    ushort value2 = proc.GetRegisterValue(_register2);
                    ushort carryIn = _withCarry && proc.GetFlag(StatusFlags.C) ? (ushort)0 : (ushort)1;
                    ushort result = (ushort)(value1 - value2 - carryIn);
                    if (IsRegisterByte(_destination))
                    {
                        if (_withCarry) proc.SetFlag(StatusFlags.C, (value1 - value2 - carryIn) < 0x100);
                        if (_setOverflowFlag) proc.SetFlag(StatusFlags.V, (~(value1 ^ value2) & (value1 ^ result) & 0x0080) != 0);
                        result &= 0x00FF;
                    }
                    else
                    {
                        if (_withCarry) proc.SetFlag(StatusFlags.C, (value1 - value2 - carryIn) < 0x10000);
                        if (_setOverflowFlag) proc.SetFlag(StatusFlags.V, (~(value1 ^ value2) & (value1 ^ result) & 0x8000) != 0);
                    }
                    proc.SetRegisterValue(_destination, result);
                }
                public override string ToString()
                {
                    return $"SUB {RegisterNames[_destination]}, {RegisterNames[_register1]}, {RegisterNames[_register2]}" +
                        (_withCarry ? " c" : "") +
                        (_setOverflowFlag ? " v" : "");
                }
            }

            private struct MicroOpAddBCDRegisters : IMicroOp
            {
                private readonly RegisterType _register1;
                private readonly RegisterType _register2;
                private readonly RegisterType _destination;
                private readonly bool _withCarry;
                private readonly bool _setOverflowFlag;
                public MicroOpAddBCDRegisters(RegisterType register1, RegisterType register2, RegisterType destination,
                    bool withCarry = false, bool setOverflowFlag = false)
                {
                    _register1 = register1;
                    _register2 = register2;
                    _destination = destination;
                    _withCarry = withCarry;
                    _setOverflowFlag = setOverflowFlag;
                }
                public void Execute(Processor proc)
                {
                    ushort value1 = proc.GetRegisterValue(_register1);
                    ushort value2 = proc.GetRegisterValue(_register2);
                    ushort carryIn = _withCarry && proc.GetFlag(StatusFlags.C) ? (ushort)1 : (ushort)0;
                    ushort result = (ushort)(value1 + value2 + carryIn);
                    bool carryOut = false;
                    if (IsRegisterByte(_destination))
                    {
                        if (((value1 & 0x0F) + (value2 & 0x0F) + carryIn) > 0x09)
                        {
                            result += 0x06;
                        }
                        if (result > 0xFF)
                        {
                            result += 0x60;
                            carryOut = true;
                        }
                        result &= 0x00FF;
                        if (_setOverflowFlag) proc.SetFlag(StatusFlags.V, (~(value1 ^ value2) & (value1 ^ result) & 0x0080) != 0);
                    }
                    else
                    {
                        if (((value1 & 0x000F) + (value2 & 0x000F) + carryIn) > 0x0009)
                        {
                            result += 0x0006;
                        }
                        if (((value1 & 0x00F0) + (value2 & 0x00F0) + (result & 0x000F)) > 0x0090)
                        {
                            result += 0x0060;
                        }
                        if (result > 0xFFFF)
                        {
                            result += 0x6000;
                            carryOut = true;
                        }
                        if (_setOverflowFlag) proc.SetFlag(StatusFlags.V, (~(value1 ^ value2) & (value1 ^ result) & 0x8000) != 0);
                    }
                    proc.SetRegisterValue(_destination, result);
                    proc.SetFlag(StatusFlags.C, carryOut);
                }
                public override string ToString()
                {
                    return $"ADD.BCD {RegisterNames[_destination]}, {RegisterNames[_register1]}, {RegisterNames[_register2]}" +
                        (_withCarry ? " c" : "") +
                        (_setOverflowFlag ? " v" : "");
                }
            }

            private struct MicroOpSubtractBCDRegisters : IMicroOp
            {
                private readonly RegisterType _register1;
                private readonly RegisterType _register2;
                private readonly RegisterType _destination;
                private readonly bool _withCarry;
                private readonly bool _setOverflowFlag;
                public MicroOpSubtractBCDRegisters(RegisterType register1, RegisterType register2, RegisterType destination,
                    bool withCarry = false, bool setOverflowFlag = false)
                {
                    _register1 = register1;
                    _register2 = register2;
                    _destination = destination;
                    _withCarry = withCarry;
                    _setOverflowFlag = setOverflowFlag;
                }
                public void Execute(Processor proc)
                {
                    ushort value1 = proc.GetRegisterValue(_register1);
                    ushort value2 = proc.GetRegisterValue(_register2);
                    ushort carryIn = _withCarry && proc.GetFlag(StatusFlags.C) ? (ushort)0 : (ushort)1;
                    ushort result = (ushort)(value1 - value2 - carryIn);
                    bool carryOut = false;
                    if (IsRegisterByte(_destination))
                    {
                        if (((value1 & 0x0F) - (value2 & 0x0F) - carryIn) > 0x09)
                        {
                            result -= 0x06;
                        }
                        if ((result & 0xFF00) != 0)
                        {
                            result -= 0x60;
                            carryOut = true;
                        }
                        result &= 0x00FF;
                        if (_setOverflowFlag) proc.SetFlag(StatusFlags.V, (~(value1 ^ value2) & (value1 ^ result) & 0x0080) != 0);
                    }
                    else
                    {
                        if (((value1 & 0x000F) - (value2 & 0x000F) - carryIn) > 0x0009)
                        {
                            result -= 0x0006;
                        }
                        if (((value1 & 0x00F0) - (value2 & 0x00F0) - (result & 0x000F)) > 0x0090)
                        {
                            result -= 0x0060;
                        }
                        if ((result & 0xFFFF0000) != 0)
                        {
                            result -= 0x6000;
                            carryOut = true;
                        }
                        if (_setOverflowFlag) proc.SetFlag(StatusFlags.V, (~(value1 ^ value2) & (value1 ^ result) & 0x8000) != 0);
                    }
                    proc.SetRegisterValue(_destination, result);
                    proc.SetFlag(StatusFlags.C, !carryOut);
                }
                public override string ToString()
                {
                    return $"SUB.BCD {RegisterNames[_destination]}, {RegisterNames[_register1]}, {RegisterNames[_register2]}" +
                        (_withCarry ? " c" : "") +
                        (_setOverflowFlag ? " v" : "");
                }
            }

            private struct MicroOpShiftRegisterLeft : IMicroOp
            {
                private readonly RegisterType _register;
                private readonly bool _carryIn;

                public MicroOpShiftRegisterLeft(RegisterType register, bool carryIn = false)
                {
                    _register = register;
                    _carryIn = carryIn;
                }
                public void Execute(Processor proc)
                {
                    ushort value = proc.GetRegisterValue(_register);
                    bool newCarry = (value & (IsRegisterByte(_register) ? 0x80 : 0x8000)) != 0;
                    value <<= 1;
                    if (_carryIn && proc.GetFlag(StatusFlags.C))
                    {
                        value |= 0x0001;
                    }
                    if (IsRegisterByte(_register))
                    {
                        value &= 0x00FF;
                    }
                    proc.SetRegisterValue(_register, value);
                    proc.SetFlag(StatusFlags.C, newCarry);
                }
                public override string ToString()
                {
                    return $"SHL {RegisterNames[_register]}" + (_carryIn ? " c" : "");
                }
            }

            private struct MicroOpShiftRegisterRight : IMicroOp
            {
                private readonly RegisterType _register;
                private readonly bool _carryIn;
                public MicroOpShiftRegisterRight(RegisterType register, bool carryIn = false)
                {
                    _register = register;
                    _carryIn = carryIn;
                }
                public void Execute(Processor proc)
                {
                    ushort value = proc.GetRegisterValue(_register);
                    bool newCarry = (value & 0x0001) != 0;
                    if (_carryIn && proc.GetFlag(StatusFlags.C))
                    {
                        value |= (ushort)(IsRegisterByte(_register) ? 0x80 : 0x8000);
                    }
                    value >>= 1;
                    proc.SetRegisterValue(_register, value);
                    proc.SetFlag(StatusFlags.C, newCarry);
                }
                public override string ToString()
                {
                    return $"SHR {RegisterNames[_register]}" + (_carryIn ? " c" : "");
                }
            }

            private struct MicroOpLogicalAndRegisters : IMicroOp
            {
                private readonly RegisterType _register1;
                private readonly RegisterType _register2;
                private readonly RegisterType _destination;
                public MicroOpLogicalAndRegisters(RegisterType register1, RegisterType register2, RegisterType destination)
                {
                    _register1 = register1;
                    _register2 = register2;
                    _destination = destination;
                }
                public void Execute(Processor proc)
                {
                    ushort value1 = proc.GetRegisterValue(_register1);
                    ushort value2 = proc.GetRegisterValue(_register2);
                    ushort result = (ushort)(value1 & value2);
                    if (IsRegisterByte(_destination))
                    {
                        result &= 0x00FF;
                    }
                    proc.SetRegisterValue(_destination, result);
                }
                public override string ToString()
                {
                    return $"AND {RegisterNames[_destination]}, {RegisterNames[_register1]}, {RegisterNames[_register2]}";
                }
            }

            private struct MicroOpLogicalOrRegisters : IMicroOp
            {
                private readonly RegisterType _register1;
                private readonly RegisterType _register2;
                private readonly RegisterType _destination;
                public MicroOpLogicalOrRegisters(RegisterType register1, RegisterType register2, RegisterType destination)
                {
                    _register1 = register1;
                    _register2 = register2;
                    _destination = destination;
                }
                public void Execute(Processor proc)
                {
                    ushort value1 = proc.GetRegisterValue(_register1);
                    ushort value2 = proc.GetRegisterValue(_register2);
                    ushort result = (ushort)(value1 | value2);
                    if (IsRegisterByte(_destination))
                    {
                        result &= 0x00FF;
                    }
                    proc.SetRegisterValue(_destination, result);
                }
                public override string ToString()
                {
                    return $"OR {RegisterNames[_destination]}, {RegisterNames[_register1]}, {RegisterNames[_register2]}";
                }
            }

            private struct MicroOpLogicalEorRegisters : IMicroOp
            {
                private readonly RegisterType _register1;
                private readonly RegisterType _register2;
                private readonly RegisterType _destination;
                public MicroOpLogicalEorRegisters(RegisterType register1, RegisterType register2, RegisterType destination)
                {
                    _register1 = register1;
                    _register2 = register2;
                    _destination = destination;
                }
                public void Execute(Processor proc)
                {
                    ushort value1 = proc.GetRegisterValue(_register1);
                    ushort value2 = proc.GetRegisterValue(_register2);
                    ushort result = (ushort)(value1 ^ value2);
                    if (IsRegisterByte(_destination))
                    {
                        result &= 0x00FF;
                    }
                    proc.SetRegisterValue(_destination, result);
                }
                public override string ToString()
                {
                    return $"EOR {RegisterNames[_destination]}, {RegisterNames[_register1]}, {RegisterNames[_register2]}";
                }
            }

            private struct MicroOpUpdateZeroAndNegativeFlags : IMicroOp
            {
                private readonly RegisterType _register;

                public MicroOpUpdateZeroAndNegativeFlags(RegisterType register)
                {
                    _register = register;
                }
                public void Execute(Processor proc)
                {
                    ushort value = proc.GetRegisterValue(_register);
                    proc.UpdateZeroAndNegativeFlags(value, IsRegisterByte(_register));
                }
                public override string ToString()
                {
                    return $"UPDZN {RegisterNames[_register]}";
                }
            }

            private struct MicroOpSetEmulationFlag : IMicroOp
            {
                private readonly bool _value;
                public MicroOpSetEmulationFlag(bool value)
                {
                    _value = value;
                }
                public void Execute(Processor proc)
                {
                    proc.SetEmulationFlag(_value);
                }
                public override string ToString()
                {
                    return $"SETE {_value}";
                }
            }

            private struct MicroOpEnterStopMode : IMicroOp
            {
                public MicroOpEnterStopMode()
                {
                }
                public void Execute(Processor proc)
                {
                    proc._clockState = ClockState.Stopped;
                }
                public override string ToString()
                {
                    return $"STOP";
                }
            }

            private struct MicroOpEnterWaitMode : IMicroOp
            {
                public MicroOpEnterWaitMode()
                {
                }
                public void Execute(Processor proc)
                {
                    proc._clockState = ClockState.Waiting;
                }
                public override string ToString()
                {
                    return $"WAIT";
                }
            }
            #endregion
        }
    }
}
