var temperature = document.getElementById("temperature");
var humidity = document.getElementById("humidity");
var temperatureSource = document.getElementById("temperatureSource");
var humiditySource = document.getElementById("humiditySource");

GetTempHumData();
GetAirSensorSource();

// Refresh air sensor data every 30 seconds
setInterval(GetTempHumData, 30000);

function GetTempHumData() {
    fetch('api/Sensor/GetTempHumidity')
        .then(response => {
            if (!response.ok) throw new Error(response.statusText);
            return response.json();
        })
        .then(data => updateTempHumCard(data))
        .catch(error => console.error('Unable to get Sensor data.', error));
}

// Updates the Temp/Humidity card — uses textContent to prevent XSS
function updateTempHumCard(result) {
    if (result.temperature == null) {
        temperature.textContent = 'No data';
        humidity.textContent = 'No data';
    } else {
        temperature.textContent = result.temperature + '°';
        humidity.textContent = result.humidity + '%';
    }
}

// Shows which sensor feeds each value, using the name set in the Air Sensor
// settings modal (not the MQTT topic — that's named by the bridge, not by the
// user). The name rarely changes and isn't part of GetTempHumidity, so this is
// fetched once rather than polled.
function GetAirSensorSource() {
    fetch('api/Sensor/GetAirSensorConfig')
        .then(response => {
            if (!response.ok) throw new Error(response.statusText);
            return response.json();
        })
        .then(config => updateAirSensorSource(config))
        .catch(error => console.error('Unable to get air sensor config.', error));
}

function updateAirSensorSource(config) {
    var name = config.name || 'Air Sensor';
    var isZigbee = config.airSensorProtocol === 'Zigbee';
    var tempTopic = isZigbee ? config.zigbeeTemperatureTopic : config.temperatureTopic;
    var humTopic = isZigbee ? config.zigbeeHumidityTopic : config.humidityTopic;

    temperatureSource.textContent = name;
    temperatureSource.title = tempTopic || '';
    humiditySource.textContent = name;
    humiditySource.title = humTopic || '';
}
