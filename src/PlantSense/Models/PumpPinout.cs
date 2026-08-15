namespace PlantSense.Models
{
    public class PumpPinout
    {
        public int Id { get; set; }
        public int Pin { get; set; }

        public PumpPinout(int id, int pin)
        {
            Pin = pin;
            Id = id;
        }
    }
}