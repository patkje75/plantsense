// Watering switch controlls
var pumpPinout = document.getElementById('pumpPinout');
// BCM GPIO pins available with Z-PI7 (excludes BCM 1-11, 14, 15: I2C/GPCLK/SPI0 (incl. GPIO 11 SCLK)/UART)
var availablePins = [12,13,16,17,18,19,20,21,22,23,24,25,26,27];
var pumpIdText = document.getElementById('pumpIdText');
var useWateringGroup = document.getElementById('useWateringGroup');
var pumpName = document.getElementById('pumpName');
var enableWateringForPumpSwitch = document.getElementById('enableWateringForPumpSwitch');
var pumpRuntime = document.getElementById('pumpRuntime');
var triggerThresholdGroup = document.getElementById('triggerThresholdGroup');
var wateringTriggerSelector = document.getElementById('wateringTriggerSelector');
var waterThresholdForSensor = document.getElementById('waterThresholdForSensor');
var triggerTimerGroup = document.getElementById('triggerTimerGroup');
var waterTimeSchedule = document.getElementById('waterTimeSchedule');
var waterMonday = null;
var waterTuseday = null;
var waterWednesday = null;
var waterThursday = null;
var waterFriday = null;
var waterSaturday = null;
var waterSunday = null;

// Variables
var pumpObj = null;

// Watering Settings Control State
var useWateringGroupCurrent = null;
var pumpNameCurrent = null;
var enableWateringForPumpSwitchCurrent = null;
var pumpRuntimeCurrent = null;
var triggerThresholdGroupCurrent = null;
var wateringTriggerSelectorCurrent = null;
var waterThresholdForSensorCurrent = null;
var triggerTimerGroupCurrent = null;
var waterTimeScheduleCurrent = null;
var waterMondayCurrent = null;
var waterTusedayCurrent = null;
var waterWednesdayCurrent = null;
var waterThursdayCurrent = null;
var waterFridayCurrent = null;
var waterSaturdayCurrent = null;
var waterSundayCurrent = null;

//Entrypoint when Config buttis is clicked
function ConfigPump(selectedPump) {
    pumpId = selectedPump.replace('pump', '');
    pumpName.value = document.getElementById('pname' + pumpId).value;
    pumpIdText.innerHTML = 'Pump ' + pumpId

    // Call function for loading watering settings
    GetWateringSettings(pumpId);
}

// OnClick for Save Button
function SaveWateringSettings() {
    if (validatePumpSettingsForm()) {
        PostWateringSettings().then(() => {
            Wateringstatus.innerHTML = "";
            $('#WateringSettingsModal').modal('hide');
            location.reload(true);
        }).catch(() => { /* save failed — message already shown, keep modal open */ });
    }
}

// Reset GUI to original state when Close button is clicked
function ResetWateringModal() {
    if (enableWateringForPumpSwitch.checked) {
        useWateringGroup.hidden = useWateringGroupCurrent;
        pumpName.value = pumpNameCurrent;
        enableWateringForPumpSwitch.checked = enableWateringForPumpSwitchCurrent;
        pumpRuntime.value = pumpRuntimeCurrent
        triggerThresholdGroup.hidden = triggerThresholdGroupCurrent;
        wateringTriggerSelector.value = wateringTriggerSelectorCurrent;
        waterThresholdForSensor.value = waterThresholdForSensorCurrent;
        triggerTimerGroup.hidden = triggerTimerGroupCurrent;
        waterTimeSchedule.value = waterTimeScheduleCurrent;
        waterMonday.checked = waterMondayCurrent;
        waterTuseday.checked = waterTusedayCurrent;
        waterWednesday.checked = waterWednesdayCurrent;
        waterThursday.checked = waterThursdayCurrent;
        waterFriday.checked = waterFridayCurrent;
        waterSaturday.checked = waterSaturdayCurrent;
        waterSunday.checked = waterSundayCurrent;
    }
}

// Store current control states
function controlCurrentStatePumps() {
    waterMonday = document.getElementById('waterMonday');
    waterTuseday = document.getElementById('waterTuseday');
    waterWednesday = document.getElementById('waterWednesday');
    waterThursday = document.getElementById('waterThursday');
    waterFriday = document.getElementById('waterFriday');
    waterSaturday = document.getElementById('waterSaturday');
    waterSunday = document.getElementById('waterSunday');

    useWateringGroupCurrent = useWateringGroup.hidden;
    pumpNameCurrent = pumpName.value
    enableWateringForPumpSwitchCurrent = enableWateringForPumpSwitch.checked
    pumpRuntimeCurrent = pumpRuntime.value;
    triggerThresholdGroupCurrent = triggerThresholdGroup.hidden;
    wateringTriggerSelectorCurrent = wateringTriggerSelector.value;
    waterThresholdForSensorCurrent = waterThresholdForSensor.value;
    triggerTimerGroupCurrent = triggerTimerGroup.hidden;
    waterTimeScheduleCurrent = waterTimeSchedule.value;
    waterMondayCurrent = waterMonday.checked;
    waterTusedayCurrent = waterTuseday.checked;
    waterWednesdayCurrent = waterWednesday.checked;
    waterThursdayCurrent = waterThursday.checked;
    waterFridayCurrent = waterFriday.checked;
    waterSaturdayCurrent = waterSaturday.checked;
    waterSundayCurrent = waterSunday.checked;
}


function buildPinDropdown(currentPumpId, currentPin) {
    pumpPinout.innerHTML = '';

    // Placeholder when unassigned or when current pin is not in the available list
    if (currentPin === 0 || !availablePins.includes(currentPin)) {
        var placeholder = document.createElement('option');
        placeholder.value = 0;
        placeholder.text = '-- No pin assigned --';
        placeholder.disabled = true;
        placeholder.selected = true;
        pumpPinout.appendChild(placeholder);
    }

    // If the stored pin is not in availablePins (e.g. legacy/Z-PI7 reserved),
    // show it as disabled so the user knows to reassign it
    if (currentPin !== 0 && !availablePins.includes(currentPin)) {
        var legacyOpt = document.createElement('option');
        legacyOpt.value = currentPin;
        legacyOpt.text = 'GPIO ' + currentPin + ' (Z-PI7 reserved — reassign)';
        legacyOpt.disabled = true;
        pumpPinout.appendChild(legacyOpt);
    }

    availablePins.forEach(function(pin) {
        var opt = document.createElement('option');
        opt.value = pin;
        opt.text = 'GPIO ' + pin;
        // Only count a pin as "in use" if it's actually assigned (non-zero) by another pump
        var usedByIdx = pumpPinMap.findIndex(function(p, idx) {
            return idx !== currentPumpId && p !== 0 && p === pin;
        });
        if (usedByIdx !== -1) {
            opt.disabled = true;
            opt.text += ' (Pump ' + usedByIdx + ')';
        }
        pumpPinout.appendChild(opt);
    });

    if (currentPin !== 0 && availablePins.includes(currentPin)) {
        pumpPinout.value = currentPin;
    }
}

function GetWateringSettings(pumpId) {
    fetch(`api/Watering/GetSettingsForPump/${pumpId}`)
        .then(response => {
            if (!response.ok) {
                // Handle HTTP errors
                throw new Error('Network response was not ok: ' + response.statusText);
            }
            return response.json();
        })
        .then(data => {
            console.log('Watering settings retrieved successfully:', data);
            UpdateWateringControls(data); // Process the data as needed
        })
        .catch(error => {
            console.error('Unable to get water pump settings.', error);
            // Additional error handling logic can be added here if needed
        });
}




//Update all controlls
function UpdateWateringControls(pump) {
    pumpObj = pump;
    buildPinDropdown(pump.id, pump.pinout);

    if (pump.enabled) {
        useWateringGroup.hidden = false;
        enableWateringForPumpSwitch.checked = true;
        pumpRuntime.value = pump.runtime;
        toggleTriggerType(pump.trigger)
        wateringTriggerSelector.value = pump.trigger

        if (pump.trigger == 1) {
            buildWaterSchedule(pump.waterSchedule)
        }

        if (pump.trigger == 0) {
            waterThresholdForSensor.value = pump.associatedSensorId
            updateSensorThresholdHint();
        }
    }
    else {
        useWateringGroup.hidden = true;
        enableWateringForPumpSwitch.checked = false;
        pumpName.value = document.getElementById('pname' + pumpId).value;
        pumpRuntime.value = 0
        triggerThresholdGroup.hidden = true;
        waterThresholdForSensor.value = 0;
        wateringTriggerSelector.value = 'Choose...';
        triggerTimerGroup.hidden = true;
        waterTimeSchedule.value = ''
        waterMonday.checked = false;
        waterTuseday.checked = false;
        waterWednesday.checked = false;
        waterThursday.checked = false;
        waterFriday.checked = false;
        waterSaturday.checked = false;
        waterSunday.checked = false;
    }

    controlCurrentStatePumps();
}

// Builds the Schedule and update controls
function buildWaterSchedule(waterSchedule) {
    waterMonday = document.getElementById('waterMonday');
    waterTuseday = document.getElementById('waterTuseday');
    waterWednesday = document.getElementById('waterWednesday');
    waterThursday = document.getElementById('waterThursday');
    waterFriday = document.getElementById('waterFriday');
    waterSaturday = document.getElementById('waterSaturday');
    waterSunday = document.getElementById('waterSunday');

    // Reset all checkboxes before populating to avoid stale state from a previous pump
    waterMonday.checked = false;
    waterTuseday.checked = false;
    waterWednesday.checked = false;
    waterThursday.checked = false;
    waterFriday.checked = false;
    waterSaturday.checked = false;
    waterSunday.checked = false;

    // API returns camelCase: "time" and "days" — <input type="time"> takes "HH:mm" directly
    waterTimeSchedule.value = waterSchedule.time || '';

    for (var day of (waterSchedule.days || [])) {
        if (day == 1) {
            waterMonday.checked = true;
        }
        if (day == 2) {
            waterTuseday.checked = true;
        }
        if (day == 3) {
            waterWednesday.checked = true;
        }
        if (day == 4) {
            waterThursday.checked = true;
        }
        if (day == 5) {
            waterFriday.checked = true;
        }
        if (day == 6) {
            waterSaturday.checked = true;
        }
        if (day == 0) {
            waterSunday.checked = true;
        }
    }
}

// Shows the selected sensor's current moisture threshold below the dropdown,
// since the threshold itself is configured on the sensor, not here.
function updateSensorThresholdHint() {
    var hint = document.getElementById('sensorThresholdHint');
    var selected = waterThresholdForSensor.options[waterThresholdForSensor.selectedIndex];
    if (!selected) {
        hint.textContent = '';
        return;
    }
    var threshold = selected.dataset.threshold;
    hint.textContent = threshold
        ? 'Current threshold for ' + selected.text + ': ' + threshold + '%'
        : 'No threshold set yet for ' + selected.text + ' — set one in that sensor\'s settings.';
}

//Toggles visibility for trigger groups (Threshold or Time)
function toggleTriggerType(value) {
    if (isNaN(value)) {
        triggerThresholdGroup.hidden = true;
        triggerThresholdGroup.disabled = true;
        triggerTimerGroup.hidden = true;
        triggerTimerGroup.disabled = true;
    } else {
        if (value == 0) {
            triggerThresholdGroup.hidden = false;
            triggerThresholdGroup.disabled = false;
            triggerTimerGroup.hidden = true;
            triggerTimerGroup.disabled = true;
            updateSensorThresholdHint();
        }

        if (value == 1) {
            triggerTimerGroup.hidden = false;
            triggerTimerGroup.disabled = false;
            triggerThresholdGroup.hidden = true;
            triggerThresholdGroup.disabled = true;
        }

        if (value == 2) {
            triggerThresholdGroup.hidden = true;
            triggerThresholdGroup.disabled = true;
            triggerTimerGroup.hidden = true;
            triggerTimerGroup.disabled = true;
        }
    }
}

// Valdiate form inputs
function validatePumpSettingsForm() {
    if (enableWateringForPumpSwitch.checked) {
        if (isNaN(pumpRuntime.value)) {
            Wateringstatus.innerHTML = "Runtime must be numeric!";
            return false;
        }

        if (!(wateringTriggerSelector.value >= 0)) {
            Wateringstatus.innerHTML = "Select trigger!";
            return false;
        }

        if (pumpName.value == null || pumpName.value == "" || pumpRuntime.value == null || pumpRuntime.value == "") {
            Wateringstatus.innerHTML = "Please Fill All Required Field";
            return false;
        }

        if (wateringTriggerSelector.value == 1 && !waterTimeSchedule.value) {
            Wateringstatus.innerHTML = "Please select a time!";
            return false;
        }

        return true
    }
    else {
        if (pumpName.value == null || pumpName.value == "") {
            Wateringstatus.innerHTML = "Name cannot be empty!";
            return false;
        }
        else {
            return true
        }
    }
}

//Toggle Water group visibility
function toggleUseWateringGroup() {
    if (useWateringGroup.hidden) {
        useWateringGroup.hidden = false;
    }
    else {
        useWateringGroup.hidden = true;
    }
}

//Post Pump Settings to API
function PostWateringSettings() {
    waterMonday = document.getElementById('waterMonday');
    waterTuseday = document.getElementById('waterTuseday');
    waterWednesday = document.getElementById('waterWednesday');
    waterThursday = document.getElementById('waterThursday');
    waterFriday = document.getElementById('waterFriday');
    waterSaturday = document.getElementById('waterSaturday');
    waterSunday = document.getElementById('waterSunday');

    var pumpSettings = null;
    var waterSchedDays = [];
    var waterSchedule = null

    //If Trigger is Time write selected days from controlls to array
    if (triggerTimerGroup.hidden == false) {
        if (waterMonday.checked) {
            waterSchedDays.push(1);
        }
        if (waterTuseday.checked) {
            waterSchedDays.push(2);
        }
        if (waterWednesday.checked) {
            waterSchedDays.push(3);
        }
        if (waterThursday.checked) {
            waterSchedDays.push(4);
        }
        if (waterFriday.checked) {
            waterSchedDays.push(5);
        }
        if (waterSaturday.checked) {
            waterSchedDays.push(6);
        }
        if (waterSunday.checked) {
            waterSchedDays.push(0);
        }

        waterSchedule = {
            "Time": waterTimeSchedule.value,
            "Days": waterSchedDays
        }
    }
    else {
        //If Trigger is Threshold set water Schedule to default
        waterSchedule = {
            "Time": "00:00",
            "Days": []
        }
    }

    //If water pump is enabled get settingsa from controlls except pinout, lastRun and nextRun.
    if (enableWateringForPumpSwitch.checked) {
        //If trigger is sensor threshold
        if (wateringTriggerSelector.value == 0) {
            pumpSettings = {
                "id": pumpId,
                "name": pumpName.value,
                "associatedSensorId": parseInt(waterThresholdForSensor.value),
                "associatedSensorName": waterThresholdForSensor.options[waterThresholdForSensor.selectedIndex].text,
                "enabled": enableWateringForPumpSwitch.checked,
                "trigger": parseInt(wateringTriggerSelector.value),
                "runtime": parseInt(pumpRuntime.value),
                "pinout": parseInt(pumpPinout.value),
                "lastRun": pumpObj.lastRun,
                "nextRun": "None",
                "waterSchedule": waterSchedule
            }
        }
        else {
            //If trigger is time
            pumpSettings = {
                "id": pumpId,
                "name": pumpName.value,
                "associatedSensorId": 0,
                "associatedSensorName": "None",
                "enabled": enableWateringForPumpSwitch.checked,
                "trigger": parseInt(wateringTriggerSelector.value),
                "runtime": pumpRuntime.value,
                "pinout": parseInt(pumpPinout.value),
                "lastRun": pumpObj.lastRun,
                "nextRun": pumpObj.nextRun,
                "waterSchedule": waterSchedule
            }
        }
    }
    else {
        //If water pump is NOT enabled reset al values.
        pumpSettings = {
            "id": pumpId,
            "name": pumpName.value,
            "associatedSensorId": 0,
            "associatedSensorName": "None",
            "enabled": false,
            "trigger": 0,
            "runtime": 0,
            "pinout": parseInt(pumpPinout.value),
            "lastRun": "Never",
            "nextRun": "Never",
            "waterSchedule": waterSchedule
        }
    }

    var savePromise = fetch('api/Watering/ConfigPump', {
        method: 'POST',
        headers: {
            'Accept': 'application/json',
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(pumpSettings)
        })
        .then(response => {
            if (!response.ok) {
                // Handle HTTP errors
                throw new Error('Network response was not ok: ' + response.statusText);
            }
            return response.json();
        })
        .then(data => {
            console.log('Pump configuration successful:', data);
            pumpPinMap[pumpId] = parseInt(pumpPinout.value);
        })
        .catch(error => {
            console.error('There was a problem with the fetch operation:', error);
            Wateringstatus.innerHTML = "Unable to save settings!";
            throw error;
        });

    controlCurrentStatePumps();

    return savePromise;
}

