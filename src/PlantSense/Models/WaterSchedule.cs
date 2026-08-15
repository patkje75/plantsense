using System.Collections.Generic;

namespace PlantSense.Models
{
    public class WaterSchedule
    {
        public string Time { get; set; }
        public List<int> Days { get; set; }
    }
}