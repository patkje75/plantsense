using Newtonsoft.Json.Linq;
using System.Globalization;

namespace PlantSense.Helpers
{
    /// <summary>
    /// Extracts a numeric value from an MQTT payload that is either a bare number
    /// (Z-Wave JS UI value topics, zigbee2mqtt attribute mode) or a JSON object
    /// (zigbee2mqtt default mode, e.g. {"soil_moisture":41.5,"battery":100}).
    /// </summary>
    public static class MqttPayloadParser
    {
        public static readonly string[] MoistureProps    = { "soil_moisture", "moisture", "humidity", "value" };
        public static readonly string[] TemperatureProps = { "temperature", "value" };
        public static readonly string[] HumidityProps    = { "humidity", "value" };

        public static bool TryExtract(string payload, string[] candidateProperties, out double value)
        {
            value = 0;

            if (string.IsNullOrWhiteSpace(payload))
                return false;

            // Fast path: bare number
            if (double.TryParse(payload, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                return true;

            // JSON object: first present candidate property wins (top level, then one level deep)
            try
            {
                var obj = JObject.Parse(payload);
                foreach (var prop in candidateProperties)
                {
                    if (TryGetNumber(obj[prop], out value))
                        return true;
                }

                foreach (var child in obj.Properties())
                {
                    if (child.Value is JObject nested)
                    {
                        foreach (var prop in candidateProperties)
                        {
                            if (TryGetNumber(nested[prop], out value))
                                return true;
                        }
                    }
                }
            }
            catch
            {
                // Not JSON — nothing to extract
            }

            return false;
        }

        private static bool TryGetNumber(JToken token, out double value)
        {
            value = 0;
            if (token == null)
                return false;

            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
            {
                value = token.Value<double>();
                return true;
            }

            return token.Type == JTokenType.String
                && double.TryParse(token.Value<string>(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }
    }
}
