let sensorChart;
let dustCo2Chart;
const connection = new signalR.HubConnectionBuilder().withUrl("/sensorHub").build();

async function updateLatestData() {
    try {
        const response = await fetch('/api/IotApi/sensordata/latest');
        if (!response.ok) return;
        const data = await response.json();

        if (data && data.temperature != null) {
            document.getElementById('temp-value').innerText = data.temperature.toFixed(1) + ' °C';
        }
        if (data && data.humidity != null) {
            document.getElementById('hum-value').innerText = data.humidity.toFixed(1) + ' %';
        }
        if (data && data.light != null) {
            document.getElementById('light-value').innerText = data.light.toFixed(1) + ' %';
        }

        if (data && data.dust != null) {
            document.getElementById('dust-value').innerText = data.dust.toFixed(0);
        }
        if (data && data.co2 != null) {
            document.getElementById('co2-value').innerText = data.co2.toFixed(0);
        }

    } catch (error) {
        console.error('Error fetching latest data:', error);
    }
}

async function updateChart() {
    try {
        const response = await fetch('/api/IotApi/sensordata/history');
        if (!response.ok) return;
        const historyData = await response.json();

        const labels = historyData.map(d => new Date(d.timestamp).toLocaleTimeString('vi-VN'));
        const tempData = historyData.map(d => d.temperature);
        const humidityData = historyData.map(d => d.humidity);
        const lightData = historyData.map(d => d.light);

        const textColor = '#000B58';

        const chartData = {
            labels: labels,
            datasets: [
                {
                    label: 'Nhiệt độ (°C)',
                    data: tempData,
                    borderColor: 'rgba(255, 99, 132, 1)',
                    yAxisID: 'yTemp',
                    tension: 0.4
                },
                {
                    label: 'Độ ẩm (%)',
                    data: humidityData,
                    borderColor: 'rgba(54, 162, 235, 1)',
                    yAxisID: 'yHum',
                    tension: 0.4
                },
                {
                    label:  'Ánh sáng (%)',
                    data: lightData,
                    borderColor: 'rgba(255, 206, 86, 1)',
                    yAxisID: 'yLight',
                    tension: 0.4
                }
            ]
        };

        const chartOptions = {
            responsive: true,
            maintainAspectRatio: false,
            interaction: {
                mode: 'index',
                intersect: false,
            },
            plugins: {
                legend: {
                    position: 'top',
                    labels: {
                        color: textColor,
                        font: { size: 14 }
                    }
                }
            },
            scales: {
                x: {
                    ticks: { color: textColor },
                    grid: { color: 'rgba(0, 11, 88, 0.1)' }
                },
                yTemp: {
                    type: 'linear',
                    display: true,
                    position: 'left',
                    title: { display: true, text: 'Nhiệt độ (°C)', color: 'rgb(255, 99, 132)' },
                    ticks: { color: 'rgb(255, 99, 132)' }
                },
                yHum: {
                    type: 'linear',
                    display: true,
                    position: 'right',
                    title: { display: true, text: 'Độ ẩm (%)', color: 'rgb(54, 162, 235)' },
                    ticks: { color: 'rgb(54, 162, 235)' },
                    grid: { drawOnChartArea: false }
                },
                yLight: {
                    type: 'linear',
                    display: true,
                    position: 'right',
                    title: { display: true, text: 'Ánh sáng (%)', color: 'rgb(255, 206, 86)' },
                    ticks: { color: 'rgb(255, 206, 86)' },
                    grid: { drawOnChartArea: false }
                }
            }
        };

        if (!sensorChart) {
            const canvas = document.getElementById('sensorChart');
            const ctx = canvas.getContext('2d');
            sensorChart = new Chart(ctx, {
                type: 'line',
                data: chartData,
                options: chartOptions
            });
        } else {
            sensorChart.data = chartData;
            sensorChart.options = chartOptions;
            sensorChart.update();
        }
    } catch (error) {
        console.log("Error updating sensor chart:", error);
    }
}

async function updateDustCo2Chart() {
    try {
        const response = await fetch('/api/IotApi/sensordata/history');
        if (!response.ok) return;

        let historyData = await response.json();

        if (historyData.length > 10) {
            historyData = historyData.slice(historyData.length - 10);
        }

        const labels = historyData.map(d => new Date(d.timestamp).toLocaleTimeString('vi-VN'));
        const dustData = historyData.map(d => d.dust);
        const co2Data = historyData.map(d => d.co2);

        const textColor = '#000B58';

        const chartData = {
            labels: labels,
            datasets: [
                {
                    label: 'Độ Bụi (0-1000)',
                    data: dustData,
                    borderColor: 'rgba(108, 117, 125, 1)',
                    backgroundColor: 'rgba(108, 117, 125, 0.5)',
                    yAxisID: 'yDust',
                    tension: 0.3,
                    fill: true
                },
                {
                    label: 'CO2 (0-100)',
                    data: co2Data,
                    borderColor: 'rgba(25, 135, 84, 1)',
                    backgroundColor: 'rgba(25, 135, 84, 0.5)',
                    yAxisID: 'yCo2',
                    tension: 0.3,
                    fill: true
                }
            ]
        };

        const chartOptions = {
            responsive: true,
            maintainAspectRatio: false,
            interaction: {
                mode: 'index',
                intersect: false,
            },
            plugins: {
                legend: {
                    position: 'top',
                    labels: {
                        color: textColor,
                        font: { size: 14 }
                    }
                }
            },
            scales: {
                x: {
                    ticks: { color: textColor },
                    grid: { color: 'rgba(0, 11, 88, 0.1)' }
                },
                yDust: {
                    type: 'linear',
                    display: true,
                    position: 'left',
                    title: { display: true, text: 'Bụi (PM2.5)', color: 'rgb(108, 117, 125)' },
                    ticks: { color: 'rgb(108, 117, 125)' },
                    max: 1000
                },
                yCo2: {
                    type: 'linear',
                    display: true,
                    position: 'right',
                    title: { display: true, text: 'CO2', color: 'rgb(25, 135, 84)' },
                    ticks: { color: 'rgb(25, 135, 84)' },
                    grid: { drawOnChartArea: false },
                    max: 100
                }
            }
        };

        if (!dustCo2Chart) {
            const canvas = document.getElementById('dustCo2Chart');
            const ctx = canvas.getContext('2d');
            dustCo2Chart = new Chart(ctx, {
                type: 'line',
                data: chartData,
                options: chartOptions
            });
        } else {
            dustCo2Chart.data = chartData;
            dustCo2Chart.options = chartOptions;
            dustCo2Chart.update();
        }
    } catch (error) {
        console.log("Error updating Dust/CO2 chart:", error);
    }
}

async function toggleDevice(deviceName) {
    const cardElement = document.getElementById(`${deviceName}-toggle`).closest('.control-card');

    cardElement.classList.add('pending');

    try {
        await fetch(`/api/IotApi/devices/${deviceName}/toggle`, { method: 'POST' });
    } catch (error) {
        console.error(`Error toggling ${deviceName}:`, error);
        cardElement.classList.remove('pending');
    }
}

async function updateDeviceStates() {
    try {
        const response = await fetch('/api/IotApi/devicestates');
        if (!response.ok) {
            console.error(`Error fetching device states: ${response.status} ${response.statusText}`);
            const errorText = await response.text();
            console.error(errorText);
            return;
        }

        const devices = await response.json();

        devices.forEach(deviceState => {
            const deviceName = deviceState.deviceName.toLowerCase().trim();
            const statusElement = document.getElementById(`${deviceName}-status`);
            const toggleElement = document.getElementById(`${deviceName}-toggle`);

            if (statusElement && toggleElement) {
                const cardElement = toggleElement.closest('.control-card');
                cardElement.classList.remove('pending');

                if (deviceState.isOn) {
                    statusElement.innerText = 'Đang Bật';
                    statusElement.className = 'status fw-bold text-success';
                } else {
                    statusElement.innerText = 'Đang Tắt';
                    statusElement.className = 'status fw-bold text-secondary';
                }
                toggleElement.checked = deviceState.isOn;
            }
        });
    } catch (error) {
        console.error("Error fetching device states:", error);
    }
}


function updateSingleDevice(deviceState) {
    const deviceName = deviceState.deviceName.toLowerCase().trim();

    const statusElement = document.getElementById(`${deviceName}-status`);
    const toggleElement = document.getElementById(`${deviceName}-toggle`);

    const cardElement = toggleElement?.closest('.control-card');

    if (!statusElement || !toggleElement || !cardElement) {
        console.warn(`Không tìm thấy elements cho thiết bị: ${deviceName}`);
        return;
    }

    cardElement.classList.remove('pending');

    if (deviceState.isOn) {
        statusElement.innerText = 'Đang Bật';
        statusElement.className = 'status fw-bold text-success';
    } else {
        statusElement.innerText = 'Đang Tắt';
        statusElement.className = 'status fw-bold text-secondary';
    }

    toggleElement.checked = deviceState.isOn;
}

document.addEventListener('DOMContentLoaded', function() {

    function handleToggleClick(event, deviceName) {

        event.preventDefault();

        const cardElement = event.target.closest('.control-card');

        if (cardElement.classList.contains('pending')) {
            return;
        }

        toggleDevice(deviceName);
    }

    document.getElementById('fan-toggle').addEventListener('click', (e) => handleToggleClick(e, 'fan'));
    document.getElementById('light-toggle').addEventListener('click', (e) => handleToggleClick(e, 'light'));
    document.getElementById('ac-toggle').addEventListener('click', (e) => handleToggleClick(e, 'ac'));

    connection.on("ReceiveActionHistory", function (newAction) {
        console.log("Received action confirmation:", newAction);
        updateSingleDevice(newAction);
    });

    connection.on("ReceiveSensorData", function (newData) {
        updateLatestData();
        updateChart();
        updateDustCo2Chart();
    });

    async function start() {
        try {
            await connection.start();
            console.log("SignalR Connected.");
        } catch (err) {
            console.error("SignalR connection error: ", err);
            setTimeout(start, 5000);
            return;
        }

        try {
            await updateLatestData();
        } catch (err) {
            console.error("Error during initial updateLatestData:", err);
        }

        try {
            await updateChart();
        } catch (err) {
            console.error("Error during initial updateChart:", err);
        }

        try {
            await updateDustCo2Chart();
        } catch (err) {
            console.error("Error during initial updateDustCo2Chart:", err);
        }

        try {
            await updateDeviceStates();
        } catch (err) {
            console.error("Error during initial updateDeviceStates:", err);
        }
    }

    start();
});