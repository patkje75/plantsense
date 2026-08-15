using System;
using System.Collections.Concurrent;
using System.Threading;

namespace PlantSense.Services
{
    /// <summary>
    /// Thread-safe in-memory store for the most recent Z-Wave sensor readings.
    /// Updated by <see cref="ZWaveMqttService"/> and read by <see cref="PlantSense.Helpers.SensorManager"/>.
    /// </summary>
    public static class ZWaveSensorCache
    {
        // key = sensor ID (0-7), value = last received moisture %
        private static readonly ConcurrentDictionary<int, double> _moisture = new();

        // volatile is not allowed on double; use long bits + Interlocked for atomic reads/writes
        private static long _temperatureBits;
        private static long _humidityBits;

        public static void UpdateMoisture(int sensorId, double moisturePercent)
            => _moisture[sensorId] = moisturePercent;

        /// <summary>Returns the last cached moisture % for <paramref name="sensorId"/>, or null if no reading has arrived yet.</summary>
        public static double? GetMoisture(int sensorId)
            => _moisture.TryGetValue(sensorId, out var v) ? v : (double?)null;

        public static void UpdateAir(double temperature, double humidity)
        {
            Interlocked.Exchange(ref _temperatureBits, BitConverter.DoubleToInt64Bits(temperature));
            Interlocked.Exchange(ref _humidityBits, BitConverter.DoubleToInt64Bits(humidity));
        }

        public static (double temperature, double humidity) GetAir()
            => (BitConverter.Int64BitsToDouble(Interlocked.Read(ref _temperatureBits)),
                BitConverter.Int64BitsToDouble(Interlocked.Read(ref _humidityBits)));
    }
}
