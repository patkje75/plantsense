function LoadTimeSettings() {
    fetch('api/System/GetTime')
        .then(function (response) {
            if (!response.ok) throw new Error(response.statusText);
            return response.json();
        })
        .then(function (data) {
            document.getElementById('currentDeviceTime').textContent = data.dateTime + ' (' + data.timeZone + ')';
            // "yyyy-MM-dd HH:mm:ss" -> datetime-local value "yyyy-MM-ddTHH:mm"
            document.getElementById('systemDateTime').value = data.dateTime.substring(0, 16).replace(' ', 'T');
            document.getElementById('timestatus').textContent = data.supported
                ? ' '
                : 'Setting the clock only works on the Raspberry Pi (Linux).';
            document.getElementById('setTimeButton').disabled = !data.supported;
            document.getElementById('systemDateTime').disabled = !data.supported;
        })
        .catch(function (error) { console.error('Unable to get device time.', error); });
}

function SaveTimeSettings() {
    var value = document.getElementById('systemDateTime').value;
    var status = document.getElementById('timestatus');

    if (!value) {
        status.textContent = 'Pick a date and time!';
        return;
    }

    fetch('api/System/SetTime', {
        method: 'POST',
        headers: { 'Accept': 'application/json', 'Content-Type': 'application/json' },
        body: JSON.stringify({ dateTime: value.replace('T', ' ') })
    })
        .then(function (response) {
            if (!response.ok) {
                return response.text().then(function (text) {
                    throw new Error(text || response.statusText);
                });
            }
            return response.json();
        })
        .then(function () {
            status.textContent = 'Time set!';
            LoadTimeSettings();
        })
        .catch(function (error) {
            status.textContent = 'Unable to set time: ' + error.message.replace(/^"|"$/g, '');
        });
}
