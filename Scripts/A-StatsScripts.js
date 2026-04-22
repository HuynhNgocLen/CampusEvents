const getGrid = () => getComputedStyle(document.documentElement).getPropertyValue('--border').trim() || '#e2e8f0';
const getTick = () => getComputedStyle(document.documentElement).getPropertyValue('--text-muted').trim() || '#64748b';

const BASE_OPTS = () => ({
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: {
        x: {
            grid: { color: 'transparent' },
            ticks: { color: getTick(), font: { family: "'Lexend', sans-serif", size: 11 } }
        },
        y: {
            grid: { color: getGrid(), borderDash: [3, 3] },
            ticks: { color: getTick(), font: { family: "'Lexend', sans-serif", size: 11 } }
        }
    }
});


const ctx1 = document.getElementById('monthlyChart').getContext('2d');
const grad1 = ctx1.createLinearGradient(0, 0, 0, 250);
grad1.addColorStop(0, '#137fec');
grad1.addColorStop(1, 'rgba(19, 127, 236, .2)');

const monthlyChart = new Chart(ctx1, {
    type: 'bar',
    data: {
        labels: MONTH_LABELS,
        datasets: [{
            data: MONTH_DATA,
            backgroundColor: grad1,
            borderRadius: 6,
            borderSkipped: false
        }]
    },
    options: BASE_OPTS()
});


const ctx2 = document.getElementById('statusChart');
if (ctx2 && typeof STATUS_LABELS !== 'undefined' && STATUS_LABELS.length > 0) {
    const ctx2Canvas = ctx2.getContext('2d');

    new Chart(ctx2Canvas, {
        type: 'doughnut',
        data: {
            labels: STATUS_LABELS,
            datasets: [{
                data: STATUS_DATA,
                backgroundColor: ['#137fec', '#16a34a', '#64748b', '#dc2626', '#d97706'],
                borderWidth: 0,
                hoverOffset: 8
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: true,
            plugins: { legend: { display: false } },
            cutout: '70%'
        }
    });
}


if (WEEK_LABELS.length > 0) {
    const ctx3 = document.getElementById('weeklyChart').getContext('2d');
    const grad3 = ctx3.createLinearGradient(0, 0, 0, 180);
    grad3.addColorStop(0, 'rgba(124, 58, 237, 0.2)');
    grad3.addColorStop(1, 'rgba(124, 58, 237, 0)');

    new Chart(ctx3, {
        type: 'line',
        data: {
            labels: WEEK_LABELS,
            datasets: [{
                data: WEEK_DATA,
                borderColor: '#7c3aed',
                backgroundColor: grad3,
                tension: 0.4,
                pointRadius: 4,
                pointBackgroundColor: '#7c3aed',
                pointBorderColor: '#fff',
                pointBorderWidth: 2,
                fill: true
            }]
        },
        options: { ...BASE_OPTS(), maintainAspectRatio: false }
    });
}

function loadChartData(yr) {
    const url = `${CHART_DATA_URL}?year=${encodeURIComponent(yr)}`;

    fetch(url)
        .then(response => {
            if (!response.ok) throw new Error('Network response was not ok');
            return response.json();
        })
        .then(data => {
            monthlyChart.data.labels = data.labels;
            monthlyChart.data.datasets[0].data = data.data;
            monthlyChart.update();

            const titleEl = document.querySelector('.ca-card__title--monthly');
            if (titleEl) titleEl.innerText = `Đăng ký theo tháng — ${yr}`;
        })
        .catch(error => console.error('Error fetching chart data:', error));
}