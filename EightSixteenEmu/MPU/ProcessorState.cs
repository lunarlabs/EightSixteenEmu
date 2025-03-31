using System;
using System.Collections.Generic;
using System.Linq;
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
        internal abstract void NextInstruction();
        internal abstract void Interrupt(Microprocessor.InterruptType type);
        internal abstract void Start();
        internal abstract void Stop(); // this emulates the processor being halted by STP
        internal abstract void Wait(); // this emulates the processor being halted by WAI
        internal abstract void BusRequest(); // this emulates pulling the RDY and BE pins low
        internal abstract void BusRelease();
        internal abstract void Disable(); // this emulates power being removed from the processor
        internal abstract void Enable();
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

        internal void Interrupt(Microprocessor.InterruptType type)
        {
            _state.Interrupt(type);
        }

        internal void Start()
        {
            _state.Start();
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
        internal override void Reset()
        {
            base.Reset();
            _context?.TransitionTo(new ProcessorStateRunning());
        }
        internal override void NextInstruction()
        {
            throw new InvalidOperationException("Processor is stopped");
        }
        internal override void Interrupt(Microprocessor.InterruptType type)
        {
            throw new InvalidOperationException("Processor is stopped");
        }
        internal override void Start()
        {
            _context?.TransitionTo(new ProcessorStateRunning());
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
        internal override void Disable()
        {
           _context?.TransitionTo(new ProcessorStateDisabled());
        }
        internal override void Enable()
        {
            throw new InvalidOperationException("Processor is already enabled");
        }
    }

    internal class ProcessorStateRunning : ProcessorState
    {
        internal override void NextInstruction()
        {
            throw new NotImplementedException();
        }
        internal override void Interrupt(Microprocessor.InterruptType type)
        {
            throw new NotImplementedException();
        }
        internal override void Start()
        {
            throw new InvalidOperationException("Processor is already running");
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
        internal override void Reset()
        {
            throw new InvalidOperationException("Processor is disabled");
        }
        internal override void NextInstruction()
        {
            throw new InvalidOperationException("Processor is disabled");
        }
        internal override void Interrupt(Microprocessor.InterruptType type)
        {
            throw new InvalidOperationException("Processor is disabled");
        }
        internal override void Start()
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
            _context?.TransitionTo(new ProcessorStateStopped());
        }
    }

    internal class ProcessorStateWaiting : ProcessorState
    {
        internal override void NextInstruction()
        {
            throw new InvalidOperationException("Processor is waiting");
        }
        internal override void Interrupt(Microprocessor.InterruptType type)
        {
            throw new NotImplementedException();
        }
        internal override void Start()
        {
            throw new InvalidOperationException("Processor is waiting");
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
        internal override void Interrupt(Microprocessor.InterruptType type)
        {
            throw new InvalidOperationException("Processor is waiting for the bus");
        }
        internal override void Start()
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
