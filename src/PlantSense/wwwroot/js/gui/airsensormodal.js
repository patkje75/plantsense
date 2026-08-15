function OnAirSensorProtocolChange() {
    var val = document.getElementById('airSensorProtocol').value;
    document.getElementById('airZwaveSection').style.display  = (val === 'ZWave')  ? '' : 'none';
    document.getElementById('airZigbeeSection').style.display = (val === 'Zigbee') ? '' : 'none';
}

function LoadAirSensorSettings() {
    fetch('api/Sensor/GetAirSensorConfig')
        .then(function(r) {
            if (!r.ok) throw new Error(r.statusText);
            return r.json();
        })
        .then(function(data) {
            document.getElementById('airSensorName').value       = data.name                    || '';
            document.getElementById('airTempTopic').value        = data.temperatureTopic        || '';
            document.getElementById('airHumTopic').value         = data.humidityTopic           || '';
            document.getElementById('airSensorProtocol').value   = data.airSensorProtocol       || 'ZWave';
            document.getElementById('airZigbeeTempTopic').value  = data.zigbeeTemperatureTopic  || '';
            document.getElementById('airZigbeeHumTopic').value   = data.zigbeeHumidityTopic     || '';
            OnAirSensorProtocolChange();
        })
        .catch(function(err) {
            console.error('Unable to get air sensor config.', err);
        });
}

function SaveAirSensorConfig() {
    var config = {
        name:                    document.getElementById('airSensorName').value.trim() || 'Air Sensor',
        temperatureTopic:        document.getElementById('airTempTopic').value.trim(),
        humidityTopic:           document.getElementById('airHumTopic').value.trim(),
        airSensorProtocol:       document.getElementById('airSensorProtocol').value,
        zigbeeTemperatureTopic:  document.getElementById('airZigbeeTempTopic').value.trim(),
        zigbeeHumidityTopic:     document.getElementById('airZigbeeHumTopic').value.trim()
    };
    fetch('api/Sensor/ConfigAirSensor', {
        method: 'POST',
        headers: { 'Accept': 'application/json', 'Content-Type': 'application/json' },
        body: JSON.stringify(config)
    })
    .then(function(r) {
        if (!r.ok) throw new Error(r.statusText);
        return r.json();
    })
    .then(function() {
        document.getElementById('airSensorStatus').textContent = 'Settings saved!';
    })
    .catch(function() {
        document.getElementById('airSensorStatus').textContent = 'Unable to save settings!';
    });
}
