var pumpCountdownIntervals = {};

GetAllWateringSettings();
window.onload = checkPumpStatusOnLoad();

async function checkPumpStatusOnLoad() {
    for (let i = 0; i <= 7; i++) {
        const status = await IsPumpRunning(i);
        updateRunningPumpGUI(i, status);
        if (status) {
            var timerJson = localStorage.getItem('ps_pump_timer_' + i);
            if (timerJson) {
                var t = JSON.parse(timerJson);
                var remaining = (t.startTime + t.durationMs - Date.now()) / 1000;
                if (remaining > 0) startCountdown(i, remaining);
            }
        } else {
            localStorage.removeItem('ps_pump_timer_' + i);
        }
    }
}

//Calls the IsPumpRunning API to see if the pin is open
function IsPumpRunning(pumpId) {
    return fetch('api/Watering/IsPumpRunning/' + pumpId)
        .then(response => {
            if (!response.ok) throw new Error(response.statusText);
            return response.json();
        })
        .catch(error => {
            console.error('Unable to get pin status.', error);
            return null;
        });
}

function updateRunningPumpGUI(pumpId, running) {
    var img = document.getElementById('runningImg' + pumpId);
    var txt = document.getElementById('runningTxt' + pumpId);
    var countdown = document.getElementById(pumpId + '-countdown');
    const buttons = [1, 2, 5, 10];

    if (running) {
        img.removeAttribute("hidden");
        txt.textContent = "Yes";

        // Hide manual buttons
        buttons.forEach(button => {
            document.getElementById(`${pumpId}-${button}`).setAttribute("hidden", "hidden");
        });

        // Show Stop button
        var stopButton = document.getElementById(pumpId + '-stop');
        stopButton.removeAttribute("hidden");

        if (countdown) countdown.removeAttribute('hidden');
    } else {
        img.setAttribute("hidden", "hidden");

        // Show manual buttons
        buttons.forEach(button => {
            document.getElementById(`${pumpId}-${button}`).removeAttribute("hidden");
        });

        // Hide stop button
        var stopButton = document.getElementById(pumpId + '-stop');
        stopButton.setAttribute("hidden", "hidden");

        if (countdown) { countdown.setAttribute('hidden', 'hidden'); countdown.textContent = ''; }
        if (pumpCountdownIntervals[pumpId]) {
            clearInterval(pumpCountdownIntervals[pumpId]);
            delete pumpCountdownIntervals[pumpId];
        }
        localStorage.removeItem('ps_pump_timer_' + pumpId);

        if (typeof running !== 'boolean') {
            txt.textContent = "Error fetching pump status!";
        } else {
            txt.textContent = "No";
        }
    }
}

function startCountdown(pumpId, remainingSeconds) {
    var countdown = document.getElementById(pumpId + '-countdown');
    if (!countdown) return;
    countdown.removeAttribute('hidden');

    if (pumpCountdownIntervals[pumpId]) clearInterval(pumpCountdownIntervals[pumpId]);

    var endTime = Date.now() + remainingSeconds * 1000;

    function tick() {
        var secs = (endTime - Date.now()) / 1000;
        if (secs <= 0) {
            clearInterval(pumpCountdownIntervals[pumpId]);
            delete pumpCountdownIntervals[pumpId];
            countdown.setAttribute('hidden', 'hidden');
            countdown.textContent = '';
            localStorage.removeItem('ps_pump_timer_' + pumpId);
            IsPumpRunning(pumpId).then(r => updateRunningPumpGUI(pumpId, r));
        } else {
            var m = Math.floor(secs / 60);
            var s = Math.floor(secs % 60);
            countdown.textContent = m + ':' + (s < 10 ? '0' : '') + s + ' remaining';
        }
    }
    tick();
    pumpCountdownIntervals[pumpId] = setInterval(tick, 500);
}

//Entrypoint when Stop button is clicked.
async function StopPumpButton(selectedPump) {
    var pumpId = selectedPump.split('-')[0];

    localStorage.removeItem('ps_pump_timer_' + pumpId);
    if (pumpCountdownIntervals[pumpId]) {
        clearInterval(pumpCountdownIntervals[pumpId]);
        delete pumpCountdownIntervals[pumpId];
    }

    await StopPump(pumpId);

    const running = await IsPumpRunning(pumpId);
    updateRunningPumpGUI(pumpId, running);
}

//Entrypoint when Manual button is clicked.
async function ManualPumpButton(selectedPump) {
    var pumpId = selectedPump.split('-')[0];
    var runMinutes = selectedPump.split('-')[1];
    var runSeconds = runMinutes * 60;

    const result = await StartPump(pumpId, runSeconds);
    if (!result) return; // fetch failed — leave UI unchanged

    localStorage.setItem('ps_pump_timer_' + pumpId, JSON.stringify({
        startTime: Date.now(),
        durationMs: runSeconds * 1000
    }));

    // Optimistic update: HTTP 200 confirms command accepted.
    // Do NOT call IsPumpRunning here — the GPIO write is async and
    // the pin may not read HIGH yet, which would leave the UI broken.
    updateRunningPumpGUI(pumpId, true);
    startCountdown(pumpId, runSeconds);
}

//Calls the StopPump API to stop the water pump
function StopPump(pumpId) {
    return fetch('api/Watering/StopPump/' + pumpId)
        .then(response => {
            if (!response.ok) throw new Error(response.statusText);
            return response.json();
        })
        .catch(error => console.error('Unable to stop the pump.', error));
}

//Calls the ManualPump API to start the water pump manually
function StartPump(pumpId, runtime) {
    return fetch('api/Watering/ManualPump/' + pumpId + '?runtime=' + runtime)
        .then(response => {
            if (!response.ok) throw new Error(response.statusText);
            return response.json();
        })
        .catch(error => console.error('Unable to start the pump.', error));
}

//Calls the ConfigOptions API to save the global pump concurrency setting
function SaveWateringOptions() {
    var status = document.getElementById('wateringOptionsStatus');
    var allowConcurrentPumps = document.getElementById('allowConcurrentPumpsSwitch').checked;

    fetch('api/Watering/ConfigOptions', {
        method: 'POST',
        headers: { 'Accept': 'application/json', 'Content-Type': 'application/json' },
        body: JSON.stringify({ allowConcurrentPumps: allowConcurrentPumps })
    })
        .then(response => {
            if (!response.ok) throw new Error(response.statusText);
            return response.json();
        })
        .then(() => { status.textContent = 'Saved!'; })
        .catch(error => {
            console.error('Unable to save watering options.', error);
            status.textContent = 'Unable to save!';
        });
}

//Calls the GetWateringSettings API to get water pump settings
function GetAllWateringSettings() {
    fetch('api/Watering/GetSettingsForAllPumps')
        .then(response => {
            if (!response.ok) throw new Error(response.statusText);
            return response.json();
        })
        .then(data => UpdateAllWateringControls(data))
        .catch(error => console.error('Unable to get water pump settings.', error));
}

function UpdateAllWateringControls(pumps) {
    var trigger = null;
    for (var pump of pumps) {
        if (pump.trigger == 0) trigger = "Threshold";
        else if (pump.trigger == 2) trigger = "Manual";
        else trigger = "Time";

        document.getElementById('pname' + pump.id).value = pump.name;
        document.getElementById('penabled' + pump.id).value = pump.lastrun;
        document.getElementById('ptrigger' + pump.id).value = trigger;
        document.getElementById('passoc' + pump.id).value = pump.associatedSensorName;
        document.getElementById('pnextrun' + pump.id).value = pump.nextrun;
        document.getElementById('plastrun' + pump.id).value = pump.lastrun;

        [1, 2, 5, 10].forEach(function(min) {
            var btn = document.getElementById(pump.id + '-' + min);
            if (btn) {
                if (pump.enabled) btn.removeAttribute('disabled');
                else btn.setAttribute('disabled', 'disabled');
            }
        });
    }
}
