using System.Collections.Generic;

namespace PlantSense.Models
{
    /// <summary>A serial adapter (USB stick or HAT UART) found on the host.</summary>
    public class DetectedAdapter
    {
        // Identifying path, e.g. /dev/serial/by-id/usb-ITead_Sonoff_Zigbee...
        public string path { get; set; }
        // Resolved device node, e.g. /dev/ttyACM0
        public string device { get; set; }
        // Best-effort protocol classification: "Zigbee" | "Z-Wave" | "Unknown"
        public string hint { get; set; }
    }

    public class AdapterDetectionResult
    {
        // False when the host OS does not support detection (i.e. not Linux)
        public bool supported { get; set; }
        public string message { get; set; }
        public List<DetectedAdapter> adapters { get; set; } = new();
        // Raspberry Pi HAT EEPROM info, null when no HAT is fitted
        public string hatVendor { get; set; }
        public string hatProduct { get; set; }
    }
}
