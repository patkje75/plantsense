namespace PlantSense.Models
{
    public class Sensor
    {
        public int id { get; set; }
        public string name { get; set; }
        public string notificationThreshold { get; set; }
        // Z-Wave node binding — 0 = unset
        public int zWaveNodeId { get; set; }
        public string zWaveMqttTopic { get; set; }
        // Protocol discriminator: "ZWave" | "Zigbee" | "None"; null treated as "ZWave" for backward compat
        public string protocol { get; set; }
        // Zigbee node binding — 0 = unset
        public int zigbeeNodeId { get; set; }
        public string zigbeeMqttTopic { get; set; }
    }
}