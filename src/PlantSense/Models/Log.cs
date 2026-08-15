using System;

namespace PlantSense.Models
{
    public class Properties
    {
        public string SourceContext { get; set; }
        public int AppLog { get; set; }
        // Log source category: "ZWave" | "Zigbee" | "Watering" | "System"; null => "App"
        public string Source { get; set; }
    }

    public class Log
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string MessageTemplate { get; set; }
        // Message with placeholders filled in (JsonFormatter renderMessage: true)
        public string RenderedMessage { get; set; }
        public Properties Properties { get; set; }
        public string Source { get; set; }

        public string DisplayMessage
            => string.IsNullOrEmpty(RenderedMessage) ? MessageTemplate : RenderedMessage;
    }
}
