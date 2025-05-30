﻿/*    _____      __   __  _____      __               ____          
 *   / __(_)__ _/ /  / /_/ __(_)_ __/ /____ ___ ___  / __/_ _  __ __
 *  / _// / _ `/ _ \/ __/\ \/ /\ \ / __/ -_) -_) _ \/ _//  ' \/ // /
 * /___/_/\_, /_//_/\__/___/_//_\_\\__/\__/\__/_//_/___/_/_/_/\_,_/ 
 *       /___/                                                      
 * 
 *  W65C816S microprocessor emulator
 *  Copyright (C) 2025 Matthias Lamers
 *  Released under GNUGPLv2, see LICENSE.txt for details.
 *  
 *  Microprocessor state handling
 */
using static EightSixteenEmu.Microprocessor;

namespace EightSixteenEmu.MPU
{
    internal abstract class ProcessorState
    {
        internal ProcessorContext? _context;
        public void SetContext(ProcessorContext context)
        {
            _context = context;
        }
        internal virtual void Reset()
        {
            if (_context == null)
            {
                throw new ArgumentNullException(nameof(_context));
            }
            else
            {
                _context.mpu.Cycles = 0;
                _context.mpu.FlagE = true;
                _context.mpu.RegPB = 0;
                _context.mpu.RegDB = 0;
                _context.mpu.RegDP = 0;
                _context.mpu.SetEmulationMode(true);
                _context.mpu.SetStatusFlag(StatusFlags.I, true);
                _context.mpu.SetStatusFlag(StatusFlags.D, false);
                _context.mpu.LoadInterruptVector(W65C816.Vector.Reset);
                if (this is not ProcessorStateRunning)
                    _context.TransitionTo(new ProcessorStateRunning());
            }
        }
        internal virtual void NextInstruction() => throw new InvalidOperationException($"Processor must be in running state to execute instructions. Current state: {GetType().Name}");
        internal virtual void Interrupt(InterruptType type) => throw new InvalidOperationException($"Processor must be in running or waiting state to handle interrupts. Current state: {GetType().Name}");
        internal virtual void Stop() => throw new InvalidOperationException("Stopped state can only be entered via STP opcode (did you mean to use Disable()?)"); // this emulates the processor being halted by STP
        internal virtual void Wait() => throw new InvalidOperationException("Waiting state can only be entered via WAI opcode"); // this emulates the processor being halted by WAI
        internal abstract void BusRequest(); // this emulates pulling the RDY and BE pins low
        internal virtual void BusRelease() => throw new InvalidOperationException("Processor already controls the bus");
        internal virtual void Disable() 
        {
            if(_context == null)
            {
                throw new NullReferenceException(nameof(_context));
            }
            else
            {
                // flush it all away
                _context.mpu.RegA = 0x0000;
                _context.mpu.RegX = 0x0000;
                _context.mpu.RegY = 0x0000;
                _context.mpu.Cycles = 0;
                _context.mpu.FlagE = true;
                _context.mpu.RegPB = 0;
                _context.mpu.RegDB = 0;
                _context.mpu.RegDP = 0;
                _context.mpu.RegSP = 0x0100;
                _context.mpu.RegSR = (StatusFlags)0x34;
                _context?.TransitionTo(new ProcessorStateDisabled());
            }
        } // this emulates power being removed from the processor
        internal virtual void Enable() => throw new InvalidOperationException("Processor is already enabled");
        internal virtual void SetProcessorState(MicroprocessorState state) => throw new InvalidOperationException("Processor must be disabled before setting its state");
    }

    internal class ProcessorContext
    {
        private ProcessorState _state;
        internal Microprocessor mpu;
        private readonly Lock @lock = new();

        public ProcessorContext(Microprocessor mpu, ProcessorState? state = null)
        {
            // I have no idea what state to go with "out of the box" as it were.
            // The way I see it, we can get everything set up before the first instruction is read?
            // In my head, the only reason you'd want the processor in the disabled state
            // is to mess with the registers programatically -- like when loading a save state?
            // Whatever. If null's passed, assume we're starting completely fresh.

            // A few weeks later -- No, you don't want to start it in the running state!
            // The context is in the mpu constructor, which is in the emucore constructor!
            // Before any devices are added to the bus! If we just jump out of the box running,
            // the reset vector pull will just grab from open bus (which is a BAD THING)
            // SO: start disabled, set up the devices, then call Reset() on the context.
            _state = state ?? new ProcessorStateDisabled();
            _state.SetContext(this);
            this.mpu = mpu;
        }

        public string StateName => _state.GetType().Name;

        internal void TransitionTo(ProcessorState state)
        {
            lock (@lock) {
                 _state = state;
                _state.SetContext(this);
                Console.WriteLine($"Transitioning to state: {state.GetType().Name}");
            }
        }

        internal void Reset()
        {
            lock (@lock) _state.Reset();
            
        }

        internal void NextInstruction()
        {
            lock (@lock) _state.NextInstruction();
            
        }

        internal void Interrupt(InterruptType type)
        {
            lock (@lock) _state.Interrupt(type);
            
        }

        internal void Stop()
        {
            lock (@lock) _state.Stop();
            
        }

        internal void Wait()
        {
            lock (@lock) _state.Wait();
        }

        internal void BusRequest()
        {
            lock (@lock) _state.BusRequest();
        }

        internal void BusRelease()
        {
            lock (@lock) _state.BusRelease();
        }

        internal void Disable()
        {
            lock (@lock) _state.Disable();
        }

        internal void Enable()
        {
            lock (@lock) _state.Enable();
        }

        internal void SetProcessorState(MicroprocessorState state)
        {
            lock (@lock) _state.SetProcessorState(state);
        }
    }

    internal class ProcessorStateStopped : ProcessorState
    {
        internal override void BusRequest()
        {
            throw new InvalidOperationException("Processor is stopped");
        }
    }

    internal class ProcessorStateRunning : ProcessorState
    {
        internal override void NextInstruction()
        {
            if (_context != null)
            {
                // When interrupting, the microprocessor finishes its current instruction
                // IRQ is level triggered, NMI is edge triggered
                if (_context.mpu.NMICalled)
                {
                    Interrupt(InterruptType.NMI);
                }
                else if (_context.mpu.IRQ && !_context.mpu.ReadStatusFlag(StatusFlags.I))
                {
                    Interrupt(InterruptType.IRQ);
                }
                else
                {
                    _context.mpu.DecodeInstruction();
                    _context.mpu.Instruction.Execute(_context.mpu);
                }
            }
            else
            {
                throw new NullReferenceException(nameof(_context));
            }
        }
        internal override void Interrupt(InterruptType type)
        {
            if (_context != null)
            { 
                ushort addressToPush = _context.mpu.RegPC;
                if (!_context.mpu.FlagE) _context.mpu.PushByte(_context.mpu.RegPB);
                _context.mpu.PushWord(addressToPush);
                if(_context.mpu.FlagE)
                {
                    if (type == InterruptType.BRK)
                    {
                    _context.mpu.PushByte((byte)(_context.mpu.RegSR | StatusFlags.X));
                    }
                    else
                    {
                        _context.mpu.PushByte((byte)(_context.mpu.RegSR & ~StatusFlags.X));
                    }
                }
                else
                {
                    _context.mpu.PushByte((byte)_context.mpu.RegSR);
                }
                _context.mpu.SetStatusFlag(StatusFlags.I, true);
                _context.mpu.SetStatusFlag(StatusFlags.D, false);
                W65C816.Vector vector;
                if (_context.mpu.FlagE)
                {
                    vector = type switch
                    {
                        InterruptType.BRK => W65C816.Vector.EmulationIRQ,
                        InterruptType.COP => W65C816.Vector.EmulationCOP,
                        InterruptType.IRQ => W65C816.Vector.EmulationIRQ,
                        InterruptType.NMI => W65C816.Vector.EmulationNMI,
                        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
                    };
                }
                else
                {
                    vector = type switch
                    {
                        InterruptType.BRK => W65C816.Vector.NativeBRK,
                        InterruptType.COP => W65C816.Vector.NativeCOP,
                        InterruptType.IRQ => W65C816.Vector.NativeIRQ,
                        InterruptType.NMI => W65C816.Vector.NativeNMI,
                        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
                    };
                }
                if (type == InterruptType.NMI) _context.mpu.NMICalled = false;
                _context.mpu.LoadInterruptVector(vector);
            }
            else
            {
                throw new NullReferenceException(nameof(_context));
            }
        }
        internal override void Stop()
        {
            _context?.TransitionTo(new ProcessorStateStopped());
        }
        internal override void Wait()
        {
            _context?.TransitionTo(new ProcessorStateWaiting());
        }
        internal override void BusRequest()
        {
            _context?.TransitionTo(new ProcessorStateBusBusy());
        }
    }


    internal class ProcessorStateDisabled : ProcessorState
    {
        internal override void Reset()
        {
            // acts as a cold reset
            _context.mpu.RegA = 0x0000;
            _context.mpu.RegX = 0x0000;
            _context.mpu.RegY = 0x0000;
            base.Reset();
        }
        internal override void NextInstruction()
        {
            throw new InvalidOperationException("Processor is disabled");
        }
        internal override void Interrupt(InterruptType type)
        {
            throw new InvalidOperationException("Processor is disabled");
        }
        internal override void Stop()
        {
            throw new InvalidOperationException("Processor is disabled");
        }
        internal override void Wait()
        {
            throw new InvalidOperationException("Processor is disabled");
        }
        internal override void BusRequest()
        {
            throw new InvalidOperationException("Processor is disabled");
        }
        internal override void BusRelease()
        {
            throw new InvalidOperationException("Processor is disabled");
        }
        internal override void Disable()
        {
            throw new InvalidOperationException("Processor is already disabled");
        }
        internal override void Enable()
        {
            _context?.TransitionTo(new ProcessorStateRunning());
        }
        internal override void SetProcessorState(MicroprocessorState state)
        {
            if (_context != null)
            {
                _context.mpu.Status = state;
            }
            else
            {
                throw new NullReferenceException(nameof(_context));
            }
        }
    }
    internal class ProcessorStateWaiting : ProcessorState
    {
        internal override void NextInstruction()
        {
            if (_context != null)
            {
                // When interrupting, the microprocessor finishes its current instruction
                // IRQ is level triggered, NMI is edge triggered
                if (_context.mpu.NMICalled)
                {
                    _context.TransitionTo(new ProcessorStateRunning());
                    _context.Interrupt(InterruptType.NMI);
                }
                else if (_context.mpu.IRQ)
                {
                    _context.TransitionTo(new ProcessorStateRunning());
                    if (!_context.mpu.ReadStatusFlag(StatusFlags.I)) 
                    {
                        _context.Interrupt(InterruptType.IRQ); 
                    }
                    else
                    {
                        _context.NextInstruction();
                    }
                }
                else
                {
                    throw new InvalidOperationException("Processor is waiting for interrupt");
                }
            }
            else
            {
                throw new NullReferenceException(nameof(_context));
            }
        }
        internal override void Interrupt(InterruptType type)
        {
            throw new NotImplementedException();
        }
        internal override void Stop()
        {
            throw new InvalidOperationException("Processor is waiting");
        }
        internal override void Wait()
        {
            throw new InvalidOperationException("Processor is already waiting");
        }
        internal override void BusRequest()
        {
            throw new NotImplementedException();
        }
        internal override void BusRelease()
        {
            throw new InvalidOperationException("Processor is waiting");
        }
        internal override void Enable()
        {
            throw new InvalidOperationException("Processor is already enabled");
        }
    }

    internal class ProcessorStateBusBusy : ProcessorState
    {
        internal override void NextInstruction()
        {
            throw new InvalidOperationException("Processor is waiting for the bus");
        }
        internal override void Interrupt(InterruptType type)
        {
            throw new InvalidOperationException("Processor is waiting for the bus");
        }
        internal override void Stop()
        {
            throw new InvalidOperationException("Processor is waiting for the bus");
        }
        internal override void Wait()
        {
            throw new InvalidOperationException("Processor is waiting for the bus");
        }
        internal override void BusRequest()
        {
            throw new InvalidOperationException("Processor is already waiting for the bus");
        }
        internal override void BusRelease()
        {
            _context?.TransitionTo(new ProcessorStateRunning());
        }
        internal override void Disable()
        {
            // todo: whatever disables the processor is more likely than not going to shut down any DMA device as well?
            // this is a guess. If so, just use the base method and use Init() on the device.
            throw new NotImplementedException();
        }
    }
}
