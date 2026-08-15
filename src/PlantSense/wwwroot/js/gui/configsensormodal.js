var sensorid = null;

// Sensors switch controls
var sensorName = document.getElementById('sensorName');
var notificationThreshold = document.getElementById('notificationThreshold');
var sensorstatus = document.getElementById('sensorstatus');
var zWaveNodeId = document.getElementById('zWaveNodeId');
var zWaveMqttTopic = document.getElementById('zWaveMqttTopic');
var sensorProtocol = document.getElementById('sensorProtocol');
var zigbeeNodeId = document.getElementById('zigbeeNodeId');
var zigbeeMqttTopic = document.getElementById('zigbeeMqttTopic');

function OnSensorProtocolChange() {
    var val = sensorProtocol.value;
    document.getElementById('zwaveBindingSection').style.display  = (val === 'ZWave')  ? '' : 'none';
    document.getElementById('zigbeeBindingSection').style.display = (val === 'Zigbee') ? '' : 'none';
}

function SaveSensorConfig() {
    if (validateSensorForm()) {
        PostSensorConfig();
        sensorstatus.innerHTML = "";
        $('#sensorSettingsModal').modal('hide');
    }
}

// Validates the Sensor Modal form input controls
function validateSensorForm() {
    if (isNaN(notificationThreshold.value)) {
        sensorstatus.innerHTML = "Threshold must be a number!";
        return false;
    } else {
        if ((notificationThreshold.value < 0) || (notificationThreshold.value > 100)) {
            sensorstatus.innerHTML = "Threshold must be between 0-100!";
            return false;
        }
    }

    // Handle empty sensor name
    if (sensorName.value === "") {
        sensorName.value = 'Sensor ' + sensorid;
    }

    return true;
}

//Calls the GetSensorSettings API to get Sensor settings
function GetSensorSettings(id) {
    fetch('api/Sensor/GetSensorConfig/' + id)
        .then(response => {
            if (!response.ok) throw new Error(response.statusText);
            return response.json();
        })
        .then(data => UpdateSensorSettingsControls(data))
        .catch(error => console.error('Unable to get Sensor Settings.', error));
}

//Updates the sensor controls
function UpdateSensorSettingsControls(sensor) {
    sensorName.value = sensor.name;
    notificationThreshold.value = sensor.notificationThreshold;
    zWaveNodeId.value = sensor.zWaveNodeId || 0;
    zWaveMqttTopic.value = sensor.zWaveMqttTopic || '';
    sensorProtocol.value = sensor.protocol || 'ZWave';
    zigbeeNodeId.value = sensor.zigbeeNodeId || 0;
    zigbeeMqttTopic.value = sensor.zigbeeMqttTopic || '';
    OnSensorProtocolChange();
}

//Calls the ConfigSensor API to save Sensor settings
function PostSensorConfig() {
    var sensorSettings = {
        "id": sensorid,
        "name": sensorName.value,
        "notificationThreshold": notificationThreshold.value,
        "zWaveNodeId": parseInt(zWaveNodeId.value) || 0,
        "zWaveMqttTopic": zWaveMqttTopic.value.trim(),
        "protocol": sensorProtocol.value,
        "zigbeeNodeId": parseInt(zigbeeNodeId.value) || 0,
        "zigbeeMqttTopic": zigbeeMqttTopic.value.trim()
    };

    fetch('api/Sensor/ConfigSensor', {
        method: 'POST',
        headers: {
            'Accept': 'application/json',
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(sensorSettings)
    })
        .then(response => {
            if (!response.ok) throw new Error(response.statusText);
            return response.json();
        })
        .then(() => { sensorstatus.innerHTML = "Settings Saved!"; })
        .catch(() => { sensorstatus.innerHTML = "Unable to save settings!"; });
}

function LoadSensorSettings() {
    var sensorName = document.getElementById("sensorName");

    sensorid = event.target.id.charAt(event.target.id.length - 1);
    sensorName.value = document.getElementById("sensor" + sensorid).textContent;
    GetSensorSettings(sensorid);
}
