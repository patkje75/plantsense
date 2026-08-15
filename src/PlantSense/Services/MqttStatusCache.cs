using System;
using System.Threading;

namespace PlantSense.Services
{
    /// <summary>
    /// Thread-safe connection status for one MQTT bridge.
    /// Written by the owning MQTT service, read by <see cref="PlantSense.Controllers.DeviceController"/>.
    /// </summary>
    public sealed class MqttBridgeStatus
    {
        private long _lastMessageUtcTicks;
        private volatile bool _isConnected;
        private volatile string _broker = string.Empty;
        private volatile string _lastError = string.Empty;

        public bool IsConnected
        {
            get => _isConnected;
            set => _isConnected = value;
        }

        public string Broker
        {
            get => _broker;
            set => _broker = value ?? string.Empty;
        }

        public string LastError
        {
            get => _lastError;
            set => _lastError = value ?? string.Empty;
        }

        /// <summary>UTC time the last MQTT message arrived, or null if none received yet.</summary>
        public DateTime? LastMessageUtc
        {
            get
            {
                var ticks = Interlocked.Read(ref _lastMessageUtcTicks);
                return ticks == 0 ? (DateTime?)null : new DateTime(ticks, DateTimeKind.Utc);
            }
        }

        /// <summary>Records that a message was just received.</summary>
        public void Touch()
            => Interlocked.Exchange(ref _lastMessageUtcTicks, DateTime.UtcNow.Ticks);
    }

    /// <summary>
    /// Thread-safe in-memory store for MQTT bridge connection status, one entry per protocol.
    /// </summary>
    public static class MqttStatusCache
    {
        public static readonly MqttBridgeStatus ZWave = new();
        public static readonly MqttBridgeStatus Zigbee = new();
    }
}
