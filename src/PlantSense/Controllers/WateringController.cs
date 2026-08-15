using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PlantSense.Helpers;
using PlantSense.Models;
using Serilog.Context;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PlantSense.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WateringController : Controller
    {
        private readonly ILogger<WateringController> _logger;

        public WateringController(ILogger<WateringController> logger)
        {
            _logger = logger;
        }

        [HttpGet("GetSettingsForPump/{id}")]
        public IActionResult GetWateringSettingsForPump(int id)
        {
            if (id < 0 || id >= 8) return BadRequest("Invalid pump ID");
            SolutionSettings settings = SettingsManager.ReadFromSettingsFile();
            return Ok(settings.lstpumps[id]);
        }

        [HttpGet("GetSettingsForAllPumps")]
        public IActionResult GetWateringSettingsForPumps()
        {
            SolutionSettings settings = SettingsManager.ReadFromSettingsFile();
            return Ok(settings.lstpumps);
        }

        [HttpPost]
        [Route("ConfigPump")]
        public IActionResult ConfigPump([FromBody] Pump pump)
        {
            if (pump == null || pump.id < 0 || pump.id >= 8) return BadRequest("Invalid pump");
            SolutionSettings settings = SettingsManager.ReadFromSettingsFile();

            settings.lstpumps[pump.id] = pump;
            SettingsManager.WriteToSettingsFile(settings);

            return Ok(settings.lstpumps[pump.id]);
        }

        [HttpGet("StartPump/{id}")]
        public IActionResult StartPump(int id)
        {
            if (id < 0 || id >= 8) return BadRequest("Invalid pump ID");
            SolutionSettings settings = SettingsManager.ReadFromSettingsFile();
            WateringManager watering = new WateringManager();

            using (LogContext.PushProperty("AppLog", 1)) using (LogContext.PushProperty("Source", "Watering"))
            {
                _logger.LogInformation($"Manual pump start: {settings.lstpumps[id].name} (Pump {id}) for {settings.lstpumps[id].runtime}s via API.");
            }

            // Fire and forget — pump runs in background, HTTP request returns immediately
            _ = watering.StartPump(settings.lstpumps[id]);

            return Ok(new { Status = "Done" });
        }

        [HttpGet("StopPump/{id}")]
        public async Task<IActionResult> StopPump(int id)
        {
            if (id < 0 || id >= 8) return BadRequest("Invalid pump ID");
            SolutionSettings settings = SettingsManager.ReadFromSettingsFile();
            WateringManager watering = new WateringManager();

            using (LogContext.PushProperty("AppLog", 1)) using (LogContext.PushProperty("Source", "Watering"))
            {
                _logger.LogInformation($"Manual pump stop: {settings.lstpumps[id].name} (Pump {id}) via API.");
            }

            await watering.StopPump(settings.lstpumps[id].pinout);

            return Ok(new { Status = "Done" });
        }

        [HttpGet("ManualPump/{id}")]
        public IActionResult ManualPump(int id, int runtime)
        {
            if (id < 0 || id >= 8) return BadRequest("Invalid pump ID");
            SolutionSettings settings = SettingsManager.ReadFromSettingsFile();
            WateringManager watering = new WateringManager();

            using (LogContext.PushProperty("AppLog", 1)) using (LogContext.PushProperty("Source", "Watering"))
            {
                _logger.LogInformation($"Manual pump start: {settings.lstpumps[id].name} (Pump {id}) for {runtime}s via API.");
            }

            watering.ManualPump(id, runtime, settings);

            return Ok(new { Status = "Done" });
        }

        [HttpPost("ConfigOptions")]
        public IActionResult ConfigOptions([FromBody] WateringOptions options)
        {
            if (options == null) return BadRequest("Invalid options");
            SolutionSettings settings = SettingsManager.ReadFromSettingsFile();
            settings.allowConcurrentPumps = options.allowConcurrentPumps;
            SettingsManager.WriteToSettingsFile(settings);

            return Ok(new WateringOptions { allowConcurrentPumps = settings.allowConcurrentPumps });
        }

        // Dashboard summary: running/enabled counts plus each pump's next scheduled run
        [HttpGet("GetPumpStatusSummary")]
        public IActionResult GetPumpStatusSummary()
        {
            SolutionSettings settings = SettingsManager.ReadFromSettingsFile();
            WateringManager watering = new WateringManager();

            var pumps = settings.lstpumps.Select(pump =>
            {
                bool running;
                try
                {
                    running = watering.isPumpRunning(pump).ToUpper() == "HIGH";
                }
                catch (Exception)
                {
                    // GPIO not available (e.g. unassigned pin, or running off-device) — treat as not running
                    running = false;
                }

                string nextRun = null;
                if (pump.enabled && pump.trigger == PumpTriggers.Time && pump.waterSchedule?.Days?.Count > 0)
                {
                    nextRun = watering.GetNextRun(pump).nextRun;
                }

                return new
                {
                    id = pump.id,
                    name = pump.name,
                    enabled = pump.enabled,
                    running,
                    trigger = pump.trigger,
                    nextRun
                };
            }).ToList();

            return Ok(pumps);
        }

        [HttpGet("IsPumpRunning/{id}")]
        public IActionResult IsPumpRunning(int id)
        {
            if (id < 0 || id >= 8) return BadRequest("Invalid pump ID");
            SolutionSettings settings = SettingsManager.ReadFromSettingsFile();
            WateringManager watering = new WateringManager();

            Pump pump = settings.lstpumps.Where(x => x.id == id).First();
            bool pumpRunning = watering.isPumpRunning(pump).ToUpper() == "HIGH";

            return Ok(pumpRunning);
        }
    }
}
