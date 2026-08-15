using Microsoft.Extensions.Logging;
using Serilog.Context;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PlantSense.Services
{
    public class MaintenanceSrvCronJob : CronJobService
    {
        private readonly ILogger<MaintenanceSrvCronJob> _logger;

        public MaintenanceSrvCronJob(IScheduleConfig<MaintenanceSrvCronJob> config, ILogger<MaintenanceSrvCronJob> logger)
            : base(config.CronExpression, config.TimeZoneInfo)
        {
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            return base.StartAsync(cancellationToken);
        }

        public override async Task DoWork(CancellationToken cancellationToken)
        {
            using (LogContext.PushProperty("AppLog", 1)) using (LogContext.PushProperty("Source", "Watering"))
            {
                string taskStartTime = DateTime.Now.ToString("HH:mm");

                // Watering Service
                try
                {
                    WateringService watering = new WateringService(_logger);
                    await watering.ManagePumps(taskStartTime);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in WateringService.ManagePumps");
                }

                // Debug, not Information — this fires every minute and is only useful when
                // actively troubleshooting whether the cron tick is running at all
                _logger.LogDebug("Done!");
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            return base.StopAsync(cancellationToken);
        }
    }
}
