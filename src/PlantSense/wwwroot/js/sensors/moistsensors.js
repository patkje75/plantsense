// Set new default font family and font color to mimic Bootstrap's default styling
Chart.defaults.global.defaultFontFamily = 'Nunito', '-apple-system,system-ui,BlinkMacSystemFont,"Segoe UI",Roboto,"Helvetica Neue",Arial,sans-serif';
Chart.defaults.global.defaultFontColor = '#858796';

// Store chart instances to allow data updates without destroy/recreate
var chartInstances = {};

// Initial fetch for all sensors on page load
for (let i = 0; i < 8; i++) {
    GetMoistData(i);
}

// Refresh sensor data every 30 seconds
setInterval(() => {
    for (let i = 0; i < 8; i++) {
        GetMoistData(i);
    }
}, 30000);

//Calls the GetSensorData API to get Sensor values
function GetMoistData(sensorid) {
    fetch('api/Sensor/GetSensorData/' + sensorid)
        .then(response => {
            if (!response.ok) throw new Error(response.statusText);
            return response.json();
        })
        .then(data => drawChart(data, sensorid))
        .catch(error => console.error('Unable to get Sensor data.', error));
}

// Draws the Charts
function drawChart(result, sensorid) {
    var sensorChart = document.getElementById("sensor" + sensorid + "PieChart");
    // Use textContent instead of innerHTML to prevent XSS
    document.getElementById("sensor" + sensorid).textContent = result.name;

    var dryness = result.dryness;
    var moist = 100 - dryness;

    document.getElementById("sensor" + sensorid + "MoistValue").textContent = moist + '%';
    document.getElementById("sensor" + sensorid + "DryValue").textContent = dryness + '%';

    if (chartInstances[sensorid]) {
        // Update existing chart data so Chart.js animates arc transitions
        chartInstances[sensorid].data.datasets[0].data = [moist, dryness];
        chartInstances[sensorid].update();
        return;
    }

    chartInstances[sensorid] = new Chart(sensorChart, {
        type: 'doughnut',
        data: {
            labels: ["Moisture", "Dry"],
            datasets: [{
                data: [moist, dryness],
                backgroundColor: ['#2d6a4f', '#c9a227'],
                hoverBackgroundColor: ['#1b4332', '#a07818'],
                hoverBorderColor: "rgba(234, 236, 244, 1)",
            }],
        },
        options: {
            maintainAspectRatio: false,
            tooltips: {
                backgroundColor: "rgb(255,255,255)",
                bodyFontColor: "#858796",
                borderColor: '#dddfeb',
                borderWidth: 1,
                xPadding: 15,
                yPadding: 15,
                displayColors: false,
                caretPadding: 10,
                callbacks: {
                    label: function (tooltipItem, data) {
                        var label = data.labels[tooltipItem.index] || '';
                        var value = data.datasets[tooltipItem.datasetIndex].data[tooltipItem.index];
                        return label + ': ' + value + '%';
                    }
                },
            },
            legend: {
                display: false
            },
            cutoutPercentage: 80,
        },
    });
}
