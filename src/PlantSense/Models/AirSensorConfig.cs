namespace PlantSense.Models
{
    public class AirSensorConfig
    {
        // User-editable display name — independent of the MQTT topic, which the
        // bridge (zigbee2mqtt/Z-Wave JS UI) names, not PlantSense
        public string name { get; set; }
        public string temperatureTopic { get; set; }
        public string humidityTopic { get; set; }
        // Protocol discriminator: "ZWave" | "Zigbee"; null treated as "ZWave" for backward compat
        public string airSensorProtocol { get; set; }
        public string zigbeeTemperatureTopic { get; set; }
        public string zigbeeHumidityTopic { get; set; }
    }
}
