using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace My_RequestHandler.HostedServices;

public sealed class TimerService: IHostedService, IAsyncDisposable
{
    private readonly ILogger<TimerService> _logger;
    private readonly Task _completedTask = Task.CompletedTask;
    private int _executionCount = 0;
    private Timer? _timer;

    public TimerService(ILogger<TimerService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Service} is running.", nameof(TimerService));
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

        return _completedTask;
    }

    private void DoWork(object? state)
    {
        int count = Interlocked.Increment(ref _executionCount);

        _logger.LogInformation(
            "{Service} is working, execution count: {Count:#,0}",
            nameof(TimerService),
            count);
    }

    public Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "{Service} is stopping.", nameof(TimerService));

        _timer?.Change(Timeout.Infinite, 0);

        return _completedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_timer is IAsyncDisposable timer)
        {
            await timer.DisposeAsync();
        }

        _timer = null;
    }
}