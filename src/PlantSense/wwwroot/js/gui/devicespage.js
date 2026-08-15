// Devices page: bridge/adapter status, configured bindings, discovered devices.
// Reuses the sensor/air settings modals (configsensormodal.js / airsensormodal.js).

var discoveredDevices = [];
var configuredDevices = [];
var assignDevice = null;

function LoadDeviceStatus() {
    fetch('api/Device/GetStatus')
        .then(function (response) {
            if (!response.ok) throw new Error(response.statusText);
            return response.json();
        })
        .then(function (data) {
            UpdateBridgeCard('zwave', data.zwave);
            UpdateBridgeCard('zigbee', data.zigbee);
            UpdateAdapterCard(data.adapters);
        })
        .catch(function (error) { console.error('Unable to get device status.', error); });
}

function UpdateBridgeCard(prefix, bridge) {
    var badge = document.getElementById(prefix + 'StatusBadge');
    badge.textContent = bridge.connected ? 'Connected' : 'Disconnected';
    badge.className = 'badge ' + (bridge.connected ? 'badge-success' : 'badge-danger');

    document.getElementById(prefix + 'Broker').textContent = bridge.broker ? 'Broker: ' + bridge.broker : '';

    var detail;
    var detailElement = document.getElementById(prefix + 'Detail');
    if (!bridge.connected) {
        detail = bridge.lastError ? 'No MQTT broker reachable' : '';
        detailElement.title = bridge.lastError || '';
    } else {
        detailElement.title = '';
        detail = bridge.lastMessageUtc
            ? 'Last message: ' + FormatAge(bridge.lastMessageUtc)
            : 'Broker connected — no messages from the bridge yet';
    }
    detailElement.textContent = detail;
    document.getElementById(prefix + 'Topics').textContent = bridge.configuredTopics + ' topic(s) configured';

    // Explain what a bridge is while there is no connection or no bridge traffic yet
    var showHint = !bridge.connected || !bridge.lastMessageUtc;
    document.getElementById(prefix + 'Hint').style.display = showHint ? '' : 'none';
}

function UpdateAdapterCard(adapters) {
    var message = document.getElementById('adapterMessage');
    var list = document.getElementById('adapterList');
    var hat = document.getElementById('hatInfo');
    list.textContent = '';
    hat.textContent = '';

    var hint = document.getElementById('adapterHint');
    if (!adapters.supported) {
        message.textContent = adapters.message || 'Adapter detection not available.';
        hint.style.display = '';
        return;
    }
    hint.style.display = 'none';

    message.textContent = adapters.adapters.length === 0 ? 'No serial adapters detected.' : '';

    adapters.adapters.forEach(function (adapter) {
        var item = document.createElement('li');
        var badge = document.createElement('span');
        badge.className = 'badge mr-1 ' +
            (adapter.hint === 'Zigbee' ? 'badge-warning'
                : adapter.hint === 'Z-Wave' ? 'badge-primary'
                : adapter.hint === 'UART (HAT)' ? 'badge-info'
                : 'badge-secondary');
        badge.textContent = adapter.hint;
        item.appendChild(badge);

        var name = adapter.path.split('/').pop();
        item.appendChild(document.createTextNode(name + (adapter.device && adapter.device !== adapter.path ? ' (' + adapter.device + ')' : '')));
        list.appendChild(item);
    });

    if (adapters.hatProduct) {
        hat.textContent = 'HAT: ' + (adapters.hatVendor ? adapters.hatVendor + ' ' : '') + adapters.hatProduct;
    }
}

function LoadDevices() {
    fetch('api/Device/GetDevices')
        .then(function (response) {
            if (!response.ok) throw new Error(response.statusText);
            return response.json();
        })
        .then(function (data) {
            configuredDevices = data.configured;
            discoveredDevices = data.discovered;
            RenderConfiguredDevices();
            RenderDiscoveredDevices();
        })
        .catch(function (error) { console.error('Unable to get devices.', error); });
}

function RenderConfiguredDevices() {
    var body = document.getElementById('configuredDevicesBody');
    body.textContent = '';

    configuredDevices.forEach(function (device) {
        var row = document.createElement('tr');
        AppendCell(row, device.slotLabel, 'Slot');
        AppendCell(row, device.name || '', 'Name');
        AppendCell(row, device.protocol, 'Protocol');

        if (device.slot >= 0) {
            AppendCell(row, device.topic || '—', 'MQTT Topic');
            AppendCell(row, device.hasReading ? device.lastValue + ' %' : '—', 'Last value');
        } else {
            AppendCell(row, 'T: ' + (device.temperatureTopic || '—') + '  H: ' + (device.humidityTopic || '—'), 'MQTT Topic');
            AppendCell(row, device.temperature + ' °C / ' + device.humidity + ' %', 'Last value');
        }

        var actionCell = document.createElement('td');
        actionCell.setAttribute('data-label', 'Action');
        actionCell.className = 'ps-cell-actions';
        var editButton = document.createElement('button');
        editButton.className = 'btn btn-sm btn-outline-primary';
        editButton.textContent = 'Edit';
        editButton.onclick = device.slot >= 0
            ? function () { EditSlot(device.slot); }
            : function () { EditAirSensor(); };
        actionCell.appendChild(editButton);
        row.appendChild(actionCell);

        body.appendChild(row);
    });
}

function RenderDiscoveredDevices() {
    var body = document.getElementById('discoveredDevicesBody');
    body.textContent = '';

    if (discoveredDevices.length === 0) {
        var row = document.createElement('tr');
        var cell = document.createElement('td');
        cell.colSpan = 6;
        cell.className = 'text-muted';
        cell.textContent = 'No devices discovered yet.';
        row.appendChild(cell);
        body.appendChild(row);
        return;
    }

    discoveredDevices.forEach(function (device, index) {
        var row = document.createElement('tr');
        AppendCell(row, device.protocol === 'ZWave' ? 'Z-Wave' : device.protocol, 'Protocol');
        AppendCell(row, device.name || '', 'Name');
        AppendCell(row, [device.vendor, device.model].filter(Boolean).join(' / '), 'Vendor / Model');
        AppendCell(row, device.deviceType || '', 'Type');
        AppendCell(row, device.lastSeenUtc ? FormatAge(device.lastSeenUtc) : '', 'Last seen');

        var actionCell = document.createElement('td');
        actionCell.setAttribute('data-label', 'Action');
        actionCell.className = 'ps-cell-actions';
        var assignButton = document.createElement('button');
        assignButton.className = 'btn btn-sm btn-primary';
        assignButton.textContent = 'Assign…';
        assignButton.onclick = function () { OpenAssign(index); };
        actionCell.appendChild(assignButton);
        row.appendChild(actionCell);

        body.appendChild(row);
    });
}

function AppendCell(row, text, label) {
    var cell = document.createElement('td');
    cell.textContent = text;
    if (label) cell.setAttribute('data-label', label);
    row.appendChild(cell);
}

function FormatAge(utcString) {
    var seconds = Math.max(0, Math.round((Date.now() - new Date(utcString).getTime()) / 1000));
    if (seconds < 60) return seconds + 's ago';
    if (seconds < 3600) return Math.round(seconds / 60) + 'm ago';
    if (seconds < 86400) return Math.round(seconds / 3600) + 'h ago';
    return Math.round(seconds / 86400) + 'd ago';
}

// Opens the existing sensor settings modal for a slot (do not use LoadSensorSettings:
// it reads dashboard-only elements)
function EditSlot(id) {
    sensorid = id;
    GetSensorSettings(id);
    $('#sensorSettingsModal').modal({ backdrop: 'static', keyboard: false });
}

function EditAirSensor() {
    LoadAirSensorSettings();
    $('#airSensorSettingsModal').modal({ backdrop: 'static', keyboard: false });
}

function RefreshDiscovery() {
    fetch('api/Device/Resubscribe')
        .then(function (response) {
            if (!response.ok) throw new Error(response.statusText);
            return response.json();
        })
        .then(function () {
            // Retained bridge payloads are re-delivered on resubscribe; give them a moment
            setTimeout(function () {
                LoadDevices();
                LoadDeviceStatus();
            }, 1000);
        })
        .catch(function (error) { console.error('Unable to refresh discovery.', error); });
}

function OpenAssign(index) {
    assignDevice = discoveredDevices[index];
    if (!assignDevice) return;

    var summaryParts = [assignDevice.name, assignDevice.vendor, assignDevice.model].filter(Boolean);
    document.getElementById('assignDeviceSummary').textContent =
        (assignDevice.protocol === 'ZWave' ? 'Z-Wave' : assignDevice.protocol) + ': ' + summaryParts.join(' — ');

    var target = document.getElementById('assignTargetSlot');
    target.textContent = '';

    configuredDevices.forEach(function (device) {
        if (device.slot < 0) return;
        var option = document.createElement('option');
        option.value = device.slot;
        option.textContent = device.slotLabel + (device.name ? ' — ' + device.name : '');
        target.appendChild(option);
    });

    AppendOption(target, 'air-temp', 'Air sensor — temperature');
    AppendOption(target, 'air-hum', 'Air sensor — humidity');
    if (assignDevice.protocol === 'Zigbee') {
        AppendOption(target, 'air-both', 'Air sensor — temperature + humidity');
    }

    var propertySelect = document.getElementById('assignProperty');
    propertySelect.textContent = '';
    (assignDevice.properties || []).forEach(function (property) {
        AppendOption(propertySelect, property, property);
    });

    document.getElementById('assignstatus').textContent = ' ';
    OnAssignTargetChange();
    $('#assignDeviceModal').modal({ backdrop: 'static', keyboard: false });
}

function AppendOption(select, value, text) {
    var option = document.createElement('option');
    option.value = value;
    option.textContent = text;
    select.appendChild(option);
}

// Clears the leftover "Assigned!"/error message from a previous save so it
// doesn't look like it applies to whatever the user is about to assign next.
function ResetAssignStatus() {
    document.getElementById('assignstatus').textContent = ' ';
}

function OnAssignTargetChange() {
    ResetAssignStatus();
    if (!assignDevice) return;
    var isZigbee = assignDevice.protocol === 'Zigbee';

    document.getElementById('assignPropertySection').style.display =
        (isZigbee && (assignDevice.properties || []).length > 0) ? '' : 'none';

    var zwaveSection = document.getElementById('assignZwaveTopicSection');
    zwaveSection.style.display = isZigbee ? 'none' : '';
    if (!isZigbee) {
        document.getElementById('assignZwaveTopic').value = (assignDevice.baseTopic || '') + '/';
    }
}

function SaveAssignment() {
    if (!assignDevice) return;
    var target = document.getElementById('assignTargetSlot').value;
    var status = document.getElementById('assignstatus');
    var isZigbee = assignDevice.protocol === 'Zigbee';
    var topic = isZigbee ? assignDevice.baseTopic : document.getElementById('assignZwaveTopic').value.trim();

    if (!topic) {
        status.textContent = 'Topic is required!';
        return;
    }

    if (target === 'air-temp' || target === 'air-hum' || target === 'air-both') {
        SaveAirAssignment(target, isZigbee, topic, status);
    } else {
        SaveSensorAssignment(parseInt(target, 10), isZigbee, topic, status);
    }
}

function SaveSensorAssignment(slot, isZigbee, topic, status) {
    fetch('api/Sensor/GetSensorConfig/' + slot)
        .then(function (response) {
            if (!response.ok) throw new Error(response.statusText);
            return response.json();
        })
        .then(function (sensor) {
            sensor.protocol = isZigbee ? 'Zigbee' : 'ZWave';
            if (isZigbee) {
                sensor.zigbeeMqttTopic = topic;
                sensor.zigbeeNodeId = assignDevice.nodeId || 0;
            } else {
                sensor.zWaveMqttTopic = topic;
                sensor.zWaveNodeId = assignDevice.nodeId || 0;
            }
            return fetch('api/Sensor/ConfigSensor', {
                method: 'POST',
                headers: { 'Accept': 'application/json', 'Content-Type': 'application/json' },
                body: JSON.stringify(sensor)
            });
        })
        .then(function (response) {
            if (!response.ok) throw new Error(response.statusText);
            status.textContent = 'Assigned!';
            LoadDevices();
        })
        .catch(function () { status.textContent = 'Unable to assign device!'; });
}

function SaveAirAssignment(target, isZigbee, topic, status) {
    fetch('api/Sensor/GetAirSensorConfig')
        .then(function (response) {
            if (!response.ok) throw new Error(response.statusText);
            return response.json();
        })
        .then(function (config) {
            config.airSensorProtocol = isZigbee ? 'Zigbee' : 'ZWave';
            if (isZigbee) {
                if (target === 'air-temp' || target === 'air-both') config.zigbeeTemperatureTopic = topic;
                if (target === 'air-hum' || target === 'air-both') config.zigbeeHumidityTopic = topic;
            } else {
                if (target === 'air-temp') config.temperatureTopic = topic;
                if (target === 'air-hum') config.humidityTopic = topic;
            }
            return fetch('api/Sensor/ConfigAirSensor', {
                method: 'POST',
                headers: { 'Accept': 'application/json', 'Content-Type': 'application/json' },
                body: JSON.stringify(config)
            });
        })
        .then(function (response) {
            if (!response.ok) throw new Error(response.statusText);
            status.textContent = 'Assigned!';
            LoadDevices();
        })
        .catch(function () { status.textContent = 'Unable to assign device!'; });
}

// jQuery loads at the end of the layout body, so defer initialization until DOM ready
document.addEventListener('DOMContentLoaded', function () {
    // Refresh the configured table after edits made through the reused settings modals
    $('#sensorSettingsModal').on('hidden.bs.modal', LoadDevices);
    $('#airSensorSettingsModal').on('hidden.bs.modal', LoadDevices);

    LoadDeviceStatus();
    LoadDevices();
    setInterval(LoadDeviceStatus, 10000);
});
