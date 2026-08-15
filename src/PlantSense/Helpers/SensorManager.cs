using PlantSense.Models;
using PlantSense.Services;
using System;

namespace PlantSense.Helpers
{
    public class SensorManager
    {
        private static double? ResolveMoisture(int id, SolutionSettings settings)
        {
            var protocol = settings.lstsensors[id].protocol;
            if (string.Equals(protocol, "Zigbee", StringComparison.OrdinalIgnoreCase))
                return ZigbeeSensorCache.GetMoisture(id);
            // null/"ZWave"/"None" all fall through to Z-Wave cache (backward compat)
            return ZWaveSensorCache.GetMoisture(id);
        }

        public static SensorReading GetSensorData(int id, SolutionSettings settings)
        {
            double? rawMoisture = ResolveMoisture(id, settings);
            double moisture = Math.Clamp(Math.Round(rawMoisture ?? 0), 0, 100);
            double dryness = 100 - moisture;

            return new SensorReading
            {
                sensorId = id,
                moisture = moisture,
                dryness = dryness,
                hasReading = rawMoisture.HasValue,
                date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                name = settings.lstsensors[id].name
            };
        }

        public static AirSensor GetTempHumidity(SolutionSettings settings)
        {
            var protocol = settings.airSensor?.airSensorProtocol;
            var (temperature, humidity) = string.Equals(protocol, "Zigbee", StringComparison.OrdinalIgnoreCase)
                ? ZigbeeSensorCache.GetAir()
                : ZWaveSensorCache.GetAir();
            return new AirSensor
            {
                temperature = temperature,
                humidity = humidity
            };
        }
    }
}
