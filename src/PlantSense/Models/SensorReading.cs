namespace PlantSense.Models
{
    /// <summary>A soil moisture reading for one sensor slot.</summary>
    public class SensorReading
    {
        public double moisture { get; set; }
        public double dryness { get; set; }
        // False until the first MQTT reading arrives (e.g. right after startup) — moisture/dryness
        // are meaningless placeholders (0/100) until this is true
        public bool hasReading { get; set; }
        public string date { get; set; }
        public string name { get; set; }
        public int sensorId { get; set; }
    }
}
