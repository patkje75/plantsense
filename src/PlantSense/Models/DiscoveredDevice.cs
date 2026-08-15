using System;
using System.Collections.Generic;

namespace PlantSense.Models
{
    /// <summary>
    /// A device reported by an MQTT bridge (zigbee2mqtt bridge/devices or Z-Wave JS UI nodeinfo),
    /// shown on the Devices page for pick-to-assign.
    /// </summary>
    public class DiscoveredDevice
    {
        // "Zigbee" | "ZWave"
        public string protocol { get; set; }
        // Unique key within the protocol: ieee_address (Zigbee) or node id as string (Z-Wave)
        public string key { get; set; }
        // Z-Wave node id, 0 for Zigbee devices
        public int nodeId { get; set; }
        public string name { get; set; }
        public string vendor { get; set; }
        public string model { get; set; }
        public string deviceType { get; set; }
        // Suggested numeric properties (from zigbee2mqtt exposes), e.g. "soil_moisture"
        public List<string> properties { get; set; } = new();
        // Base state topic, e.g. "zigbee2mqtt/Soil sensor 1" or "zwave/NodeName"
        public string baseTopic { get; set; }
        public DateTime lastSeenUtc { get; set; }
    }
}
