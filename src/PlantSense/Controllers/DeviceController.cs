using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PlantSense.Helpers;
using PlantSense.Models;
using PlantSense.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PlantSense.Controllers
{
    /// <summary>
    /// API backing the Devices page: bridge/adapter status, configured device bindings
    /// with last readings, discovered devices, and manual resubscription.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DeviceController : Controller
    {
        private readonly ILogger<DeviceController> _logger;
        private readonly ZWaveMqttService _zWaveMqttService;
        private readonly ZigbeeMqttService _zigbeeMqttService;

        public DeviceController(ILogger<DeviceController> logger,
            ZWaveMqttService zWaveMqttService, ZigbeeMqttService zigbeeMqttService)
        {
            _logger = logger;
            _zWaveMqttService = zWaveMqttService;
            _zigbeeMqttService = zigbeeMqttService;
        }

        [HttpGet("GetStatus")]
        public IActionResult GetStatus()
        {
            SolutionSettings settings = SettingsManager.ReadFromSettingsFile();

            var zwaveTopics = settings.lstsensors.Count(s => !string.IsNullOrWhiteSpace(s.zWaveMqttTopic))
                + (string.IsNullOrWhiteSpace(settings.airSensor?.temperatureTopic) ? 0 : 1)
                + (string.IsNullOrWhiteSpace(settings.airSensor?.humidityTopic) ? 0 : 1);

            var zigbeeTopics = settings.lstsensors.Count(s => !string.IsNullOrWhiteSpace(s.zigbeeMqttTopic))
                + (string.IsNullOrWhiteSpace(settings.airSensor?.zigbeeTemperatureTopic) ? 0 : 1)
                + (string.IsNullOrWhiteSpace(settings.airSensor?.zigbeeHumidityTopic) ? 0 : 1);

            return Ok(new
            {
                zwave = BuildBridgeStatus(MqttStatusCache.ZWave, zwaveTopics),
                zigbee = BuildBridgeStatus(MqttStatusCache.Zigbee, zigbeeTopics),
                adapters = AdapterDetector.Detect()
            });
        }

        [HttpGet("GetDevices")]
        public IActionResult GetDevices()
        {
            SolutionSettings settings = SettingsManager.ReadFromSettingsFile();
            var configured = new List<object>();

            foreach (var sensor in settings.lstsensors)
            {
                var protocol = string.IsNullOrWhiteSpace(sensor.protocol) ? "ZWave" : sensor.protocol;
                var isZigbee = string.Equals(protocol, "Zigbee", StringComparison.OrdinalIgnoreCase);
                var topic = isZigbee ? sensor.zigbeeMqttTopic : sensor.zWaveMqttTopic;
                double? moisture = isZigbee
                    ? ZigbeeSensorCache.GetMoisture(sensor.id)
                    : ZWaveSensorCache.GetMoisture(sensor.id);

                configured.Add(new
                {
                    slot = sensor.id,
                    slotLabel = $"Sensor {sensor.id}",
                    name = sensor.name,
                    protocol,
                    topic = topic ?? string.Empty,
                    lastValue = moisture,
                    hasReading = moisture.HasValue
                });
            }

            var airProtocol = string.IsNullOrWhiteSpace(settings.airSensor?.airSensorProtocol)
                ? "ZWave"
                : settings.airSensor.airSensorProtocol;
            var airIsZigbee = string.Equals(airProtocol, "Zigbee", StringComparison.OrdinalIgnoreCase);
            var (temperature, humidity) = airIsZigbee ? ZigbeeSensorCache.GetAir() : ZWaveSensorCache.GetAir();

            configured.Add(new
            {
                slot = -1,
                slotLabel = "Air sensor",
                name = string.IsNullOrWhiteSpace(settings.airSensor?.name) ? "Air sensor" : settings.airSensor.name,
                protocol = airProtocol,
                temperatureTopic = (airIsZigbee ? settings.airSensor?.zigbeeTemperatureTopic : settings.airSensor?.temperatureTopic) ?? string.Empty,
                humidityTopic = (airIsZigbee ? settings.airSensor?.zigbeeHumidityTopic : settings.airSensor?.humidityTopic) ?? string.Empty,
                temperature,
                humidity
            });

            return Ok(new
            {
                configured,
                discovered = DiscoveredDeviceCache.GetAll()
            });
        }

        [HttpGet("Resubscribe")]
        public async Task<IActionResult> Resubscribe()
        {
            try
            {
                await _zWaveMqttService.ReloadSubscriptionsAsync();
                await _zigbeeMqttService.ReloadSubscriptionsAsync();
            }
            catch (Exception ex)
            {
                Serilog.Log.ForContext("AppLog", 1).ForContext("Source", "System")
                    .Warning(ex, "Manual MQTT resubscribe failed");
            }

            return Ok(new { status = "Done" });
        }

        private static object BuildBridgeStatus(MqttBridgeStatus status, int configuredTopics)
            => new
            {
                connected = status.IsConnected,
                broker = status.Broker,
                lastMessageUtc = status.LastMessageUtc,
                lastError = status.LastError,
                configuredTopics
            };
    }
}
