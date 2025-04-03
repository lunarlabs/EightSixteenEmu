using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
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
                _context.mpu.RegSP = 0x0100;
                _context.mpu.RegSR = (StatusFlags)0x34;
                _context.mpu.LoadInterruptVector(W65C816.Vector.Reset);
            }
        }
        internal virtual void NextInstruction() => throw new InvalidOperationException($"Processor must be in running state to execute instructions. Current state: {GetType().Name}");
        internal virtual void Interrupt(InterruptType type) => throw new InvalidOperationException($"Processor must be in running or waiting state to handle interrupts. Current state: {GetType().Name}");
        internal virtual void Stop() => throw new InvalidOperationException("Stopped state can only be entered via STP opcode (did you mean to use Disable()?)"); // this emulates the processor being halted by STP
        internal virtual void Wait() => throw new InvalidOperationException("Waiting state can only be entered via WAI opcode"); // this emulates the processor being halted by WAI
        internal abstract void BusRequest(); // this emulates pulling the RDY and BE pins low
        internal virtual void BusRelease() => throw new InvalidOperationException("Processor already controls the bus");
        internal virtual void Disable() => _context?.TransitionTo(ProcessorStateDisabled.Instance); // this emulates power being removed from the processor
        internal virtual void Enable() => throw new InvalidOperationException("Processor is already enabled");
        internal virtual void SetProcessorState(MicroprocessorState state) => throw new InvalidOperationException("Processor must be disabled before setting its state");
    }

    internal class ProcessorContext
    {
        private ProcessorState _state;
        internal Microprocessor mpu;

        public ProcessorContext(ProcessorState state, Microprocessor mpu)
        {
            _state = state;
            this.mpu = mpu;
        }

        public void TransitionTo(ProcessorState state)
        {
            _state = state;
            _state.SetContext(this);
        }

        internal void Reset()
        {
            _state.Reset();
        }

        internal void NextInstruction()
        {
            _state.NextInstruction();
        }

        internal void Interrupt(InterruptType type)
        {
            _state.Interrupt(type);
        }

        internal void Stop()
        {
            _state.Stop();
        }

        internal void Wait()
        {
            _state.Wait();
        }

        internal void BusRequest()
        {
            _state.BusRequest();
        }

        internal void BusRelease()
        {
            _state.BusRelease();
        }

        internal void Disable()
        {
            _state.Disable();
        }

        internal void Enable()
        {
            _state.Enable();
        }
    }

    internal class ProcessorStateStopped : ProcessorState
    {
        private static ProcessorStateStopped? _instance;
        private static readonly object _lock = new object();

        internal static ProcessorStateStopped Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new ProcessorStateStopped();
                    return _instance;
                }
            }
        }

        private ProcessorStateStopped() { }

        internal override void Reset()
        {
            base.Reset();
            _context?.TransitionTo(new ProcessorStateRunning());
        }
        internal override void NextInstruction()
        {
            throw new InvalidOperationException("Processor is stopped");
        }
        internal override void Interrupt(InterruptType type)
        {
            throw new InvalidOperationException("Processor is stopped");
        }
        internal override void Stop()
        {
            throw new InvalidOperationException("Processor is already stopped");
        }
        internal override void Wait()
        {
            throw new InvalidOperationException("Processor is stopped");
        }
        internal override void BusRequest()
        {
            throw new InvalidOperationException("Processor is stopped");
        }
        internal override void BusRelease()
        {
            throw new InvalidOperationException("Processor is stopped");
        }
    }

    internal class ProcessorStateRunning : ProcessorState
    {
        internal override void NextInstruction()
        {
            throw new NotImplementedException();
        }
        internal override void Interrupt(InterruptType type)
        {
            throw new NotImplementedException();
        }
        internal override void Stop()
        {
            _context?.TransitionTo(ProcessorStateStopped.Instance);
        }
        internal override void Wait()
        {
            _context?.TransitionTo(new ProcessorStateWaiting());
        }
        internal override void BusRequest()
        {
            _context?.TransitionTo(new ProcessorStateBusBusy());
        }
        internal override void BusRelease()
        {
            throw new InvalidOperationException("Processor is already bus director");
        }
        internal override void Disable()
        {
            throw new NotImplementedException();
        }
        internal override void Enable()
        {
            throw new InvalidOperationException("Processor is already enabled");
        }
    }

    internal class ProcessorStateDisabled : ProcessorState
    {
        private static ProcessorStateDisabled? _instance;
        private static readonly object _lock = new object();

        internal static ProcessorStateDisabled Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new ProcessorStateDisabled();
                    return _instance;
                }
            }
        }
        private ProcessorStateDisabled() { }
        internal override void Reset()
        {
            throw new InvalidOperationException("Processor is disabled");
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
            _context?.TransitionTo(ProcessorStateStopped.Instance);
        }
    }

    internal class ProcessorStateWaiting : ProcessorState
    {
        internal override void NextInstruction()
        {
            throw new InvalidOperationException("Processor is waiting");
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
        internal override void Disable()
        {
            throw new NotImplementedException();
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
            throw new InvalidOperationException("Processor is waiting for the bus");
        }
        internal override void Enable()
        {
            throw new InvalidOperationException("Processor is already enabled");
        }
    }
}
