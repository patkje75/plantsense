// Dashboard: Pump status + Next watering cards
GetPumpStatusSummary();

// Refresh every 30 seconds, matching the other dashboard cards
setInterval(GetPumpStatusSummary, 30000);

function GetPumpStatusSummary() {
    fetch('api/Watering/GetPumpStatusSummary')
        .then(response => {
            if (!response.ok) throw new Error(response.statusText);
            return response.json();
        })
        .then(data => updatePumpStatusCards(data))
        .catch(error => console.error('Unable to get pump status.', error));
}

function updatePumpStatusCards(pumps) {
    var enabledCount = pumps.filter(p => p.enabled).length;
    var runningCount = pumps.filter(p => p.running).length;

    document.getElementById('pumpStatusSummary').textContent = runningCount + ' running';
    document.getElementById('pumpEnabledDetail').textContent = enabledCount + ' / ' + pumps.length + ' enabled';

    var nextWatering = document.getElementById('nextWatering');
    var nextWateringDetail = document.getElementById('nextWateringDetail');

    var scheduled = pumps.filter(p => p.enabled && p.nextRun);
    if (scheduled.length === 0) {
        nextWatering.textContent = 'None scheduled';
        nextWateringDetail.textContent = '';
        return;
    }

    scheduled.sort((a, b) => new Date(a.nextRun.replace(' ', 'T')) - new Date(b.nextRun.replace(' ', 'T')));
    var next = scheduled[0];
    var nextDate = new Date(next.nextRun.replace(' ', 'T'));

    nextWatering.textContent = next.name;
    nextWateringDetail.textContent = FormatRelativeTime(nextDate);
}

// e.g. "in 3h 12m" — keeps the card readable without needing a full date/time
function FormatRelativeTime(date) {
    var diffMs = date.getTime() - Date.now();
    if (diffMs <= 0) return 'due now';

    var minutes = Math.round(diffMs / 60000);
    if (minutes < 60) return 'in ' + minutes + 'm';

    var hours = Math.floor(minutes / 60);
    var remainderMinutes = minutes % 60;
    if (hours < 24) return 'in ' + hours + 'h ' + remainderMinutes + 'm';

    var days = Math.floor(hours / 24);
    return 'in ' + days + 'd';
}
