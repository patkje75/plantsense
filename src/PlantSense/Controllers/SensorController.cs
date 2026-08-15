using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PlantSense.Helpers;
using PlantSense.Models;
using PlantSense.Services;
using System;
using System.Threading.Tasks;

namespace PlantSense.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SensorController : Controller
    {
        private readonly ILogger<SensorController> _logger;
        private readonly ZWaveMqttService _zWaveMqttService;
        private readonly ZigbeeMqttService _zigbeeMqttService;

        public SensorController(ILogger<SensorController> logger,
            ZWaveMqttService zWaveMqttService, ZigbeeMqttService zigbeeMqttService)
        {
            _logger = logger;
            _zWaveMqttService = zWaveMqttService;
            _zigbeeMqttService = zigbeeMqttService;
        }

        [HttpGet("GetSensorData/{id}")]
        public IActionResult GetSensorData(int id)
        {
            if (id < 0 || id >= 8) return BadRequest("Invalid sensor ID");
            SolutionSettings settings = SettingsManager.ReadFromSettingsFile();
            SensorReading sensorData = SensorManager.GetSensorData(id, settings);
            return Ok(sensorData);
        }

        [HttpPost]
        [Route("ConfigSensor")]
        public async Task<IActionResult> ConfigSensor([FromBody] Sensor sensor)
        {
            if (sensor == null || sensor.id < 0 || sensor.id >= 8) return BadRequest("Invalid sensor");
            SolutionSettings settings = SettingsManager.ReadFromSettingsFile();

            settings.lstsensors[sensor.id] = sensor;
            SettingsManager.WriteToSettingsFile(settings);

            await ReloadMqttSubscriptionsAsync();

            return Ok(settings.lstsensors[sensor.id]);
        }

        [HttpGet("GetSensorConfig/{id}")]
        public IActionResult GetSensorConfig(int id)
        {
            if (id < 0 || id >= 8) return BadRequest("Invalid sensor ID");
            SolutionSettings settings = SettingsManager.ReadFromSettingsFile();
            return Ok(settings.lstsensors[id]);
        }

        [HttpGet("GetTempHumidity")]
        public IActionResult GetTempHumidity()
        {
            SolutionSettings settings = SettingsManager.ReadFromSettingsFile();
            AirSensor airsensor = SensorManager.GetTempHumidity(settings);
            return Ok(airsensor);
        }

        [HttpGet("GetAirSensorConfig")]
        public IActionResult GetAirSensorConfig()
        {
            var settings = SettingsManager.ReadFromSettingsFile();
            return Ok(settings.airSensor ?? new AirSensorConfig());
        }

        [HttpPost("ConfigAirSensor")]
        public async Task<IActionResult> ConfigAirSensor([FromBody] AirSensorConfig config)
        {
            if (config == null) return BadRequest("Invalid config");
            var settings = SettingsManager.ReadFromSettingsFile();
            settings.airSensor = config;
            SettingsManager.WriteToSettingsFile(settings);

            await ReloadMqttSubscriptionsAsync();

            return Ok(settings.airSensor);
        }

        // Applies topic changes immediately; the save must not fail on MQTT trouble
        private async Task ReloadMqttSubscriptionsAsync()
        {
            try
            {
                await _zWaveMqttService.ReloadSubscriptionsAsync();
                await _zigbeeMqttService.ReloadSubscriptionsAsync();
            }
            catch (Exception ex)
            {
                Serilog.Log.ForContext("AppLog", 1).ForContext("Source", "System")
                    .Warning(ex, "Sensor config saved but reloading MQTT subscriptions failed");
            }
        }
    }
}
