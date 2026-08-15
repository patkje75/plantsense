using Microsoft.Extensions.Logging;
using PlantSense.Helpers;
using PlantSense.Models;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PlantSense.Services
{
    public class WateringService
    {
        private ILogger<MaintenanceSrvCronJob> _logger;

        public WateringService(ILogger<MaintenanceSrvCronJob> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Evaluates all enabled pumps and starts any that should run now.
        /// </summary>
        public async Task ManagePumps(string taskStartTime)
        {
            SolutionSettings settings = SettingsManager.ReadFromSettingsFile();
            List<Pump> lstEnabledPumps = new List<Pump>(settings.lstpumps.Where(p => p.enabled == true));

            List<Pump> pumpsToRun = new List<Pump>();
            foreach (Pump pump in lstEnabledPumps)
            {
                if (pump.trigger == PumpTriggers.Threshold)
                {
                    if (isSensorThresholdLessThan(pump, settings))
                    {
                        pumpsToRun.Add(pump);
                    }
                }
                else if (pump.trigger == PumpTriggers.Time)
                {
                    int intToday = (int)DateTime.Now.DayOfWeek;

                    if (pump.waterSchedule.Days.Contains(intToday) && isTriggerTimeNow(pump, taskStartTime))
                    {
                        pumpsToRun.Add(pump);
                    }
                }
                // Manual trigger: no automatic scheduling — do nothing
            }

            if (pumpsToRun.Count == 0) return;

            if (settings.allowConcurrentPumps)
            {
                // Run every triggered pump at once
                await Task.WhenAll(pumpsToRun.Select(pump => StartPump(pump, settings)));
            }
            else
            {
                // Queue: run one at a time in the order they were evaluated
                foreach (Pump pump in pumpsToRun)
                {
                    await StartPump(pump, settings);
                }
            }
        }

        private bool isSensorThresholdLessThan(Pump pump, SolutionSettings settings)
        {
            SensorReading sensorData = SensorManager.GetSensorData(pump.associatedSensorId, settings);

            if (!sensorData.hasReading)
            {
                // No MQTT reading yet — e.g. right after startup, before the bridge has published
                // anything. Treating "unknown" as "bone dry" would fire the pump on stale/default
                // data instead of a real measurement, so skip until an actual reading arrives.
                _logger.LogDebug($"No reading yet for sensor {settings.lstsensors[pump.associatedSensorId].name}, skipping threshold check for pump {pump.name}.");
                return false;
            }

            if ((pump.enabled || sensorData.moisture < 95) && sensorData.moisture < Convert.ToDouble(settings.lstsensors[pump.associatedSensorId].notificationThreshold))
            {
                using (LogContext.PushProperty("AppLog", 1)) using (LogContext.PushProperty("Source", "Watering"))
                {
                    _logger.LogInformation($"The value for Sensor {settings.lstsensors[pump.associatedSensorId].name} is less than threshold, starting pump.");
                }

                return true;
            }

            return false;
        }

        private async Task StartPump(Pump pump, SolutionSettings settings)
        {
            using (LogContext.PushProperty("AppLog", 1)) using (LogContext.PushProperty("Source", "Watering"))
            {
                _logger.LogInformation($"Starting pump: {pump.name}...");
            }

            WateringManager wateringManager = new WateringManager();
            await wateringManager.StartPump(pump);

            using (LogContext.PushProperty("AppLog", 1)) using (LogContext.PushProperty("Source", "Watering"))
            {
                _logger.LogInformation($"Pump {pump.name} finished!");
                settings.lstpumps[pump.id].lastRun = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                SettingsManager.WriteToSettingsFile(settings);
            }
        }

        private bool isTriggerTimeNow(Pump pump, string taskStartTime)
        {
            if (taskStartTime == pump.waterSchedule.Time)
            {
                using (LogContext.PushProperty("AppLog", 1)) using (LogContext.PushProperty("Source", "Watering"))
                {
                    _logger.LogInformation($"Pump: {pump.name} is set to start now, starting pump!");
                }

                return true;
            }

            // Debug, not Information — this is evaluated every minute for every time-triggered
            // pump and is only interesting when actively troubleshooting a schedule
            _logger.LogDebug($"Trigger time for {pump.name} is {pump.waterSchedule.Time}, skipping...");
            return false;
        }
    }
}
