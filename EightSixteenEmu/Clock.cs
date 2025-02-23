using System;
using System.Threading;
using System.Threading.Tasks;

namespace EightSixteenEmu;
public class Clock
{
    private readonly TimeSpan interval;
    private bool continuous;
    private bool running;
    private CancellationTokenSource? cancellationTokenSource;

    public bool Continuous { get => continuous; }
    public bool Running { get => running; }
    public TimeSpan Interval => interval;

    public void SetSingleShot()
    {
        continuous = false;
    }
    public void SetContinuous()
    {
        continuous = true;
    }

    public Clock(TimeSpan interval, bool continuous = true)
    {
        this.interval = interval;
        this.continuous = continuous;
    }

    public async Task StartAsync()
    {
        cancellationTokenSource = new CancellationTokenSource();
        if (continuous)
        {
            running = true;
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                await Task.Delay(interval, cancellationTokenSource.Token);
                Tick?.Invoke(this, EventArgs.Empty);
            }
            running = false;
        }
        else
        {
            await Task.Delay(interval, cancellationTokenSource.Token);
            Tick?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Stop()
    {
        cancellationTokenSource?.Cancel();
    }

    public event EventHandler? Tick;
}
