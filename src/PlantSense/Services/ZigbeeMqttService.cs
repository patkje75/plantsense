using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json.Linq;
using PlantSense.Helpers;
using PlantSense.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PlantSense.Services
{
    /// <summary>
    /// Hosted service that connects to the Zigbee MQTT bridge (zigbee2mqtt) and populates
    /// <see cref="ZigbeeSensorCache"/> with live soil-moisture and air-sensor readings.
    /// Also subscribes to the bridge's device list for discovery and supports live
    /// resubscription when sensor topics change.
    /// </summary>
    public class ZigbeeMqttService : IHostedService, IDisposable
    {
        private readonly IConfiguration _configuration;
        // Contextual logger: events land in the application log tagged Source=Zigbee
        private readonly Serilog.ILogger _log =
            Serilog.Log.ForContext("AppLog", 1).ForContext("Source", "Zigbee");
        private IMqttClient _client;
        private readonly MqttFactory _factory = new();
        private readonly SemaphoreSlim _reloadLock = new(1, 1);
        private MqttClientOptions _options;
        private string _baseTopic;
        private string _discoveryTopic;

        // Swapped wholesale by ReloadSubscriptionsAsync; readers copy the reference once
        private volatile Dictionary<string, int> _topicToSensorId = new();
        private volatile string _airTemperatureTopic = string.Empty;
        private volatile string _airHumidityTopic = string.Empty;
        // Topics currently subscribed on the broker (guarded by _reloadLock)
        private HashSet<string> _subscribedTopics = new();

        public ZigbeeMqttService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var host = _configuration["Zigbee:MqttHost"] ?? "localhost";
            var port = int.TryParse(_configuration["Zigbee:MqttPort"], out var p) ? p : 1883;
            _baseTopic = _configuration["Zigbee:BaseTopic"] ?? "zigbee2mqtt";
            _discoveryTopic = $"{_baseTopic}/bridge/devices";

            MqttStatusCache.Zigbee.Broker = $"{host}:{port}";

            _client = _factory.CreateMqttClient();
            _client.ApplicationMessageReceivedAsync += OnMessageReceived;
            _client.DisconnectedAsync += args =>
            {
                MqttStatusCache.Zigbee.IsConnected = false;
                _log.Warning("ZigbeeMqttService disconnected from broker at {Broker} ({Reason})",
                    MqttStatusCache.Zigbee.Broker, args.Reason);
                return Task.CompletedTask;
            };

            _options = new MqttClientOptionsBuilder()
                .WithTcpServer(host, port)
                .WithCleanSession()
                .Build();

            // Connects and subscribes; failures are logged and reflected in MqttStatusCache
            await ReloadSubscriptionsAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_client?.IsConnected == true)
            {
                await _client.DisconnectAsync(new MqttClientDisconnectOptions(), cancellationToken);
            }
        }

        /// <summary>
        /// Rebuilds the topic map from plantsettings.json and updates broker subscriptions.
        /// Safe to call at any time (config save, Devices page refresh); also reconnects
        /// if the broker connection was lost.
        /// </summary>
        public async Task ReloadSubscriptionsAsync(CancellationToken cancellationToken = default)
        {
            await _reloadLock.WaitAsync(cancellationToken);
            try
            {
                var newMap = new Dictionary<string, int>();
                var airTemperatureTopic = string.Empty;
                var airHumidityTopic = string.Empty;

                try
                {
                    var settings = SettingsManager.ReadFromSettingsFile();
                    airTemperatureTopic = settings.airSensor?.zigbeeTemperatureTopic ?? string.Empty;
                    airHumidityTopic = settings.airSensor?.zigbeeHumidityTopic ?? string.Empty;

                    foreach (var sensor in settings.lstsensors)
                    {
                        if (!string.IsNullOrWhiteSpace(sensor.zigbeeMqttTopic))
                            newMap[sensor.zigbeeMqttTopic] = sensor.id;
                    }
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "ZigbeeMqttService could not read sensor topic map from settings; sensor subscriptions may be incomplete");
                }

                _topicToSensorId = newMap;
                _airTemperatureTopic = airTemperatureTopic;
                _airHumidityTopic = airHumidityTopic;

                if (_client == null)
                    return;

                if (!_client.IsConnected)
                {
                    try
                    {
                        await _client.ConnectAsync(_options, cancellationToken);
                        MqttStatusCache.Zigbee.IsConnected = true;
                        MqttStatusCache.Zigbee.LastError = string.Empty;

                        _log.Information("ZigbeeMqttService connected to MQTT broker at {Broker}", MqttStatusCache.Zigbee.Broker);
                    }
                    catch (Exception ex)
                    {
                        MqttStatusCache.Zigbee.IsConnected = false;
                        MqttStatusCache.Zigbee.LastError = ex.Message;
                        _log.Error(ex, "ZigbeeMqttService failed to connect to broker at {Broker}", MqttStatusCache.Zigbee.Broker);
                        // Topic state is already swapped — the next successful connect subscribes correctly
                        return;
                    }
                }

                var newTopics = new HashSet<string>(newMap.Keys) { _discoveryTopic };
                if (!string.IsNullOrWhiteSpace(airTemperatureTopic))
                    newTopics.Add(airTemperatureTopic);
                if (!string.IsNullOrWhiteSpace(airHumidityTopic))
                    newTopics.Add(airHumidityTopic);

                var staleTopics = _subscribedTopics.Where(t => !newTopics.Contains(t)).ToList();
                if (staleTopics.Count > 0)
                {
                    var unsubscribeBuilder = _factory.CreateUnsubscribeOptionsBuilder();
                    foreach (var topic in staleTopics)
                        unsubscribeBuilder.WithTopicFilter(topic);
                    await _client.UnsubscribeAsync(unsubscribeBuilder.Build(), cancellationToken);
                }

                // Subscribing the full set is idempotent and re-delivers retained payloads,
                // which refreshes discovery and last sensor values immediately
                var subscribeBuilder = _factory.CreateSubscribeOptionsBuilder();
                foreach (var topic in newTopics)
                    subscribeBuilder.WithTopicFilter(topic);
                await _client.SubscribeAsync(subscribeBuilder.Build(), cancellationToken);

                _subscribedTopics = newTopics;

                if (newMap.Count == 0)
                    _log.Warning("ZigbeeMqttService: no sensor topics configured — check sensor zigbeeMqttTopic fields and Zigbee appsettings");
            }
            finally
            {
                _reloadLock.Release();
            }
        }

        private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs args)
        {
            var topic = args.ApplicationMessage.Topic;
            var payload = args.ApplicationMessage.ConvertPayloadToString();

            MqttStatusCache.Zigbee.Touch();

            if (topic == _discoveryTopic)
            {
                ParseBridgeDevices(payload);
                return Task.CompletedTask;
            }

            var topicMap = _topicToSensorId;
            var airTemperatureTopic = _airTemperatureTopic;
            var airHumidityTopic = _airHumidityTopic;

            if (topicMap.TryGetValue(topic, out var sensorId)
                && MqttPayloadParser.TryExtract(payload, MqttPayloadParser.MoistureProps, out var moisture))
            {
                ZigbeeSensorCache.UpdateMoisture(sensorId, moisture);
            }

            // Independent ifs: with zigbee2mqtt one topic can carry both temperature and
            // humidity in the same JSON payload
            if (topic == airTemperatureTopic
                && MqttPayloadParser.TryExtract(payload, MqttPayloadParser.TemperatureProps, out var temperature))
            {
                var (_, humidity) = ZigbeeSensorCache.GetAir();
                ZigbeeSensorCache.UpdateAir(temperature, humidity);
            }

            if (topic == airHumidityTopic
                && MqttPayloadParser.TryExtract(payload, MqttPayloadParser.HumidityProps, out var humidityValue))
            {
                var (currentTemperature, _) = ZigbeeSensorCache.GetAir();
                ZigbeeSensorCache.UpdateAir(currentTemperature, humidityValue);
            }

            return Task.CompletedTask;
        }

        private void ParseBridgeDevices(string payload)
        {
            try
            {
                var deviceArray = JArray.Parse(payload);
                var devices = new List<DiscoveredDevice>();

                foreach (var token in deviceArray)
                {
                    if (token is not JObject obj)
                        continue;

                    var type = (string)obj["type"] ?? string.Empty;
                    if (string.Equals(type, "Coordinator", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var ieeeAddress = (string)obj["ieee_address"];
                    if (string.IsNullOrWhiteSpace(ieeeAddress))
                        continue;

                    var friendlyName = (string)obj["friendly_name"];
                    var displayName = string.IsNullOrWhiteSpace(friendlyName) ? ieeeAddress : friendlyName;
                    var definition = obj["definition"] as JObject;

                    var device = new DiscoveredDevice
                    {
                        protocol = "Zigbee",
                        key = ieeeAddress,
                        nodeId = 0,
                        name = displayName,
                        vendor = (string)definition?["vendor"] ?? string.Empty,
                        model = (string)definition?["model"] ?? string.Empty,
                        deviceType = type,
                        baseTopic = $"{_baseTopic}/{displayName}",
                        lastSeenUtc = DateTime.UtcNow
                    };
                    CollectNumericProperties(definition?["exposes"] as JArray, device.properties);
                    devices.Add(device);
                }

                DiscoveredDeviceCache.ReplaceZigbee(devices);

                _log.Information("ZigbeeMqttService discovered {Count} Zigbee device(s) from the bridge", devices.Count);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "ZigbeeMqttService could not parse the bridge/devices payload");
            }
        }

        private static void CollectNumericProperties(JArray exposes, List<string> target)
        {
            if (exposes == null)
                return;

            foreach (var token in exposes)
            {
                if (token is not JObject expose)
                    continue;

                if ((string)expose["type"] == "numeric")
                {
                    var property = (string)expose["property"];
                    if (!string.IsNullOrWhiteSpace(property) && !target.Contains(property))
                        target.Add(property);
                }

                if (expose["features"] is JArray features)
                    CollectNumericProperties(features, target);
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
            _reloadLock.Dispose();
        }
    }
}
