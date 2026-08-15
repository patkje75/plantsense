using PlantSense.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace PlantSense.Services
{
    /// <summary>
    /// Thread-safe in-memory store for devices discovered via the MQTT bridges.
    /// Zigbee entries are replaced wholesale (zigbee2mqtt publishes a full snapshot at
    /// bridge/devices); Z-Wave entries are upserted per node (nodeinfo arrives per topic).
    /// </summary>
    public static class DiscoveredDeviceCache
    {
        private static readonly ConcurrentDictionary<string, DiscoveredDevice> _zigbee = new();
        private static readonly ConcurrentDictionary<string, DiscoveredDevice> _zwave = new();

        public static void ReplaceZigbee(IEnumerable<DiscoveredDevice> devices)
        {
            var incoming = devices.Where(d => !string.IsNullOrWhiteSpace(d?.key)).ToList();
            var keys = new HashSet<string>(incoming.Select(d => d.key));

            foreach (var stale in _zigbee.Keys.Where(k => !keys.Contains(k)).ToList())
                _zigbee.TryRemove(stale, out _);

            foreach (var device in incoming)
                _zigbee[device.key] = device;
        }

        public static void UpsertZWave(DiscoveredDevice device)
        {
            if (string.IsNullOrWhiteSpace(device?.key))
                return;
            _zwave[device.key] = device;
        }

        public static IReadOnlyCollection<DiscoveredDevice> GetAll()
            => _zigbee.Values.Concat(_zwave.Values)
                     .OrderBy(d => d.protocol)
                     .ThenBy(d => d.name)
                     .ToList();
    }
}
