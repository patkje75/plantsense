using System.Collections.Generic;

namespace PlantSense.Models
{
    public class SolutionSettings
    {
        public List<Sensor> lstsensors { get; set; }
        public List<Pump> lstpumps { get; set; }
        public AirSensorConfig airSensor { get; set; }
        // When false (default), pumps whose schedules collide run one after another instead of at once
        public bool allowConcurrentPumps { get; set; }

        public void InitializeSensorSettings()
        {
            lstsensors = new List<Sensor>();
            lstpumps = new List<Pump>();

            for (int i = 0; i <= 7; i++)
            {
                Sensor sensor = new Sensor()
                {
                    id = i,
                    name = "Sensor " + i,
                    notificationThreshold = "0",
                    protocol = "ZWave"
                };

                lstsensors.Add(sensor);
            }

            for (int i = 0; i <= 7; i++)
            {
                Pump pump = new Pump()
                {
                    id = i,
                    pinout = 0,   // unassigned — user must configure per Z-PI7 wiring
                    name = "Pump " + i,
                    associatedSensorId = 0,
                    associatedSensorName = "None",
                    lastRun = "Never",
                    nextRun = "Never",
                    trigger = PumpTriggers.Threshold,
                    enabled = false,
                    runtime = 0,
                    waterSchedule = new WaterSchedule()
                    {
                        Time = "00:00",
                        Days = new List<int>() { 1 }
                    }
                };

                lstpumps.Add(pump);
            }

            airSensor = new AirSensorConfig
            {
                name = "Air Sensor",
                temperatureTopic = string.Empty,
                humidityTopic = string.Empty,
                airSensorProtocol = "ZWave",
                zigbeeTemperatureTopic = string.Empty,
                zigbeeHumidityTopic = string.Empty
            };
        }
    }
}