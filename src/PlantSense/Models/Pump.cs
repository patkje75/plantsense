namespace PlantSense.Models
{
    public class Pump
    {
        public int id { get; set; }
        public string name { get; set; }
        public int associatedSensorId { get; set; }
        public string associatedSensorName { get; set; }
        public bool enabled { get; set; }
        public PumpTriggers trigger { get; set; }
        public int runtime { get; set; }
        public int pinout { get; set; }
        public string lastRun { get; set; }
        public string nextRun { get; set; }
        public WaterSchedule waterSchedule { get; set; }
    }

    public enum PumpTriggers
    {
        Threshold,
        Time,
        Manual
    }
}