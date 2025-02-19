using System;
using System.Threading;
using System.Threading.Tasks;

namespace EightSixteenEmu;
public class Clock
{
    private readonly TimeSpan interval;
    private readonly bool continuous;
    private CancellationTokenSource? cancellationTokenSource;

    public Clock(TimeSpan interval, bool continuous)
    {
        this.interval = interval;
        this.continuous = continuous;
    }

    public async Task StartAsync()
    {
        cancellationTokenSource = new CancellationTokenSource();
        if (continuous)
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                await Task.Delay(interval, cancellationTokenSource.Token);
                Tick?.Invoke(this, EventArgs.Empty);
            }
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
