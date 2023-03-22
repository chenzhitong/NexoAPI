using NLog;

namespace NexoAPI
{
    public class BackgroundTask : BackgroundService
    {

        public readonly Logger _logger;

        public BackgroundTask()
        {
            _logger = LogManager.LoadConfiguration("nlog.config").GetCurrentClassLogger();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.Info("MyBackgroundTask is running.");

                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }
    }
}
