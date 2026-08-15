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
    /// Hosted service that connects to the Z-Wave JS UI MQTT bridge and populates
    /// <see cref="ZWaveSensorCache"/> with live soil-moisture and air-sensor readings.
    /// Also subscribes to the gateway's nodeinfo topics for discovery and supports live
    /// resubscription when sensor topics change.
    /// </summary>
    public class ZWaveMqttService : IHostedService, IDisposable
    {
        private const string NodeInfoSuffix = "/nodeinfo";

        private readonly IConfiguration _configuration;
        // Contextual logger: events land in the application log tagged Source=ZWave
        private readonly Serilog.ILogger _log =
            Serilog.Log.ForContext("AppLog", 1).ForContext("Source", "ZWave");
        private IMqttClient _client;
        private readonly MqttFactory _factory = new();
        private readonly SemaphoreSlim _reloadLock = new(1, 1);
        private MqttClientOptions _options;
        private string[] _discoveryTopicFilters = Array.Empty<string>();

        // Swapped wholesale by ReloadSubscriptionsAsync; readers copy the reference once
        private volatile Dictionary<string, int> _topicToSensorId = new();
        private volatile string _airTemperatureTopic = string.Empty;
        private volatile string _airHumidityTopic = string.Empty;
        // Topics currently subscribed on the broker (guarded by _reloadLock)
        private HashSet<string> _subscribedTopics = new();

        public ZWaveMqttService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var host = _configuration["ZWave:MqttHost"] ?? "localhost";
            var port = int.TryParse(_configuration["ZWave:MqttPort"], out var p) ? p : 1883;
            _discoveryTopicFilters = _configuration.GetSection("ZWave:DiscoveryTopicFilters").Get<string[]>()
                ?? new[] { "zwave/+/nodeinfo", "zwave/+/+/nodeinfo" };

            MqttStatusCache.ZWave.Broker = $"{host}:{port}";

            _client = _factory.CreateMqttClient();
            _client.ApplicationMessageReceivedAsync += OnMessageReceived;
            _client.DisconnectedAsync += args =>
            {
                MqttStatusCache.ZWave.IsConnected = false;
                _log.Warning("ZWaveMqttService disconnected from broker at {Broker} ({Reason})",
                    MqttStatusCache.ZWave.Broker, args.Reason);
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
                    airTemperatureTopic = settings.airSensor?.temperatureTopic ?? string.Empty;
                    airHumidityTopic = settings.airSensor?.humidityTopic ?? string.Empty;

                    foreach (var sensor in settings.lstsensors)
                    {
                        if (!string.IsNullOrWhiteSpace(sensor.zWaveMqttTopic))
                            newMap[sensor.zWaveMqttTopic] = sensor.id;
                    }
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "ZWaveMqttService could not read sensor topic map from settings; sensor subscriptions may be incomplete");
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
                        MqttStatusCache.ZWave.IsConnected = true;
                        MqttStatusCache.ZWave.LastError = string.Empty;

                        _log.Information("ZWaveMqttService connected to MQTT broker at {Broker}", MqttStatusCache.ZWave.Broker);
                    }
                    catch (Exception ex)
                    {
                        MqttStatusCache.ZWave.IsConnected = false;
                        MqttStatusCache.ZWave.LastError = ex.Message;
                        _log.Error(ex, "ZWaveMqttService failed to connect to broker at {Broker}", MqttStatusCache.ZWave.Broker);
                        // Topic state is already swapped — the next successful connect subscribes correctly
                        return;
                    }
                }

                var newTopics = new HashSet<string>(newMap.Keys);
                foreach (var filter in _discoveryTopicFilters)
                    newTopics.Add(filter);
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
                    _log.Warning("ZWaveMqttService: no sensor topics configured — check sensor zWaveMqttTopic fields and ZWave appsettings");
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

            MqttStatusCache.ZWave.Touch();

            if (topic.EndsWith(NodeInfoSuffix, StringComparison.Ordinal))
            {
                ParseNodeInfo(topic, payload);
                return Task.CompletedTask;
            }

            var topicMap = _topicToSensorId;
            var airTemperatureTopic = _airTemperatureTopic;
            var airHumidityTopic = _airHumidityTopic;

            if (topicMap.TryGetValue(topic, out var sensorId)
                && MqttPayloadParser.TryExtract(payload, MqttPayloadParser.MoistureProps, out var moisture))
            {
                ZWaveSensorCache.UpdateMoisture(sensorId, moisture);
            }

            // Independent ifs: the same topic may carry both temperature and humidity
            // when a combined JSON payload is used
            if (topic == airTemperatureTopic
                && MqttPayloadParser.TryExtract(payload, MqttPayloadParser.TemperatureProps, out var temperature))
            {
                var (_, humidity) = ZWaveSensorCache.GetAir();
                ZWaveSensorCache.UpdateAir(temperature, humidity);
            }

            if (topic == airHumidityTopic
                && MqttPayloadParser.TryExtract(payload, MqttPayloadParser.HumidityProps, out var humidityValue))
            {
                var (currentTemperature, _) = ZWaveSensorCache.GetAir();
                ZWaveSensorCache.UpdateAir(currentTemperature, humidityValue);
            }

            return Task.CompletedTask;
        }

        private void ParseNodeInfo(string topic, string payload)
        {
            try
            {
                var obj = JObject.Parse(payload);
                var nodeId = (int?)obj["id"] ?? 0;
                var name = (string)obj["name"];
                if (nodeId == 0 && string.IsNullOrWhiteSpace(name))
                    return;

                var baseTopic = topic.Substring(0, topic.Length - NodeInfoSuffix.Length);

                var device = new DiscoveredDevice
                {
                    protocol = "ZWave",
                    key = nodeId != 0 ? nodeId.ToString() : baseTopic,
                    nodeId = nodeId,
                    name = string.IsNullOrWhiteSpace(name) ? $"Node {nodeId}" : name,
                    vendor = (string)obj["manufacturer"] ?? string.Empty,
                    model = (string)obj["productLabel"] ?? (string)obj["productDescription"] ?? string.Empty,
                    deviceType = (string)obj["productDescription"] ?? string.Empty,
                    baseTopic = baseTopic,
                    lastSeenUtc = DateTime.UtcNow
                };

                DiscoveredDeviceCache.UpsertZWave(device);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "ZWaveMqttService could not parse nodeinfo payload on topic {Topic}", topic);
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
            _reloadLock.Dispose();
        }
    }
}
