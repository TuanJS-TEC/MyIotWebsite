const searchInput = document.getElementById('search-input');
const deviceSelect = document.getElementById('device-select');
const statusSelect = document.getElementById('status-select');
const pageSizeSelect = document.getElementById('pagesize-select');
const searchButton = document.getElementById('search-btn');
const tableBody = document.getElementById('history-table-body');
const paginationControls = document.getElementById('pagination-controls');
const debouncedSearch = debounce(() => loadHistoryData(1),500);

let currentPage = 1;

function sanitizeHTML(str) {
    return str.replace(/[&<>"']/g, function(m) {
        return {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#39;'
        }[m];
    });
}

function debounce(func, delay = 500) {
    let timeoutId;
    return (...args) => {
        clearTimeout(timeoutId);
        timeoutId = setTimeout(() => {
            func.apply(this, args);
        }, delay);
    };
}

function getDeviceIcon(deviceName) {
    const name = deviceName.toLowerCase();
    if (name.includes('fan')) return '<i class="fas fa-fan text-info me-2"></i>';
    if (name.includes('light') || name.includes('bulb')) return '<i class="fas fa-lightbulb text-warning me-2"></i>';
    if (name.includes('ac')) return '<i class="fas fa-snowflake text-primary me-2"></i>';
    return '<i class="fas fa-toggle-on me-2"></i>';
}

function formatDateTime(isoString) {
    const date = new Date(isoString);
    const day = String(date.getDate()).padStart(2, '0');
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const year = date.getFullYear();
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    const seconds = String(date.getSeconds()).padStart(2, '0');

    return `${day}/${month}/${year} ${hours}:${minutes}:${seconds}`;
}

const connection = new signalR.HubConnectionBuilder().withUrl("/sensorHub").build();
// connection.on("ReceiveActionHistory", function (record) {
//     if (currentPage === 1 && !searchInput.value && deviceSelect.value === 'all' && statusSelect.value === 'all') {
//         loadHistoryData(1);
//     }
// });
connection.on("ReceiveActionHistory", function (record) {
    if (currentPage !== 1 || searchInput.value || deviceSelect.value !== 'all' || statusSelect.value !== 'all') {
        return;
    }

    const noDataRow = tableBody.querySelector('td[colspan="4"]');
    if (noDataRow) {
        noDataRow.parentElement.remove();
    }

    const actionBadge = record.isOn
        ? '<span class="badge bg-success">Bật</span>'
        : '<span class="badge bg-danger">Tắt</span>';
    const safeDeviceName = sanitizeHTML(record.deviceName);
    const icon = getDeviceIcon(safeDeviceName);
    const formattedDate = formatDateTime(record.timestamp);

    const row = `<tr>
                    <td>${record.id}</td>
                    <td>${icon} ${safeDeviceName}</td>
                    <td>${actionBadge}</td>
                    <td>${formattedDate}</td>
                 </tr>`;

    tableBody.insertAdjacentHTML('afterbegin', row);

    const rows = tableBody.querySelectorAll('tr');
    const pageSize = parseInt(pageSizeSelect.value, 10);
    if (rows.length > pageSize) {
        rows[rows.length - 1].remove();
    }
});

async function loadHistoryData(page = 1) {
    currentPage = page;
    tableBody.innerHTML = `<tr><td colspan="4" class="text-center">Đang tải dữ liệu...</td></tr>`;

    try {
        const params = new URLSearchParams();
        params.append('deviceName', deviceSelect.value);
        params.append('status', statusSelect.value);
        params.append('searchTerm', searchInput.value.trim());
        params.append('pageNumber', currentPage);
        params.append('pageSize', pageSizeSelect.value);

        const apiUrl = `/api/IotApi/actionhistory?${params.toString()}`;
        const response = await fetch(apiUrl);

        if (response.status === 404) {
            const errorMessage = await response.text();
            tableBody.innerHTML = `<tr><td colspan="4" class="text-center text-danger fw-bold">${errorMessage}</td></tr>`;
            paginationControls.innerHTML = '';
            return;
        }

        if (!response.ok) throw new Error("Network response not ok");

        const paginatedResponse = await response.json();
        tableBody.innerHTML = '';

        if (!paginatedResponse.data || paginatedResponse.data.length === 0) {
            tableBody.innerHTML = `<tr><td colspan="4" class="text-center text-warning">Không có dữ liệu lịch sử nào.</td></tr>`;
            paginationControls.innerHTML = '';
            return;
        }

        let rowsHtml = '';


        paginatedResponse.data.forEach(record => {
            const actionBadge = record.isOn
                ? '<span class="badge bg-success">Bật</span>'
                : '<span class="badge bg-danger">Tắt</span>';
            const safeDeviceName = sanitizeHTML(record.deviceName);
            const icon = getDeviceIcon(record.deviceName);
            const formattedDate = formatDateTime(record.timestamp);

            rowsHtml += `<tr>
                        <td>${record.id}</td>
                        <td>${icon} ${safeDeviceName}</td>
                        <td>${actionBadge}</td>
                        <td>${formattedDate}</td>
                     </tr>`;
        });

        tableBody.innerHTML = rowsHtml;

        renderPagination(paginatedResponse.totalPages, paginatedResponse.pageNumber);

    } catch (error) {
        console.error("Lỗi khi tải lịch sử:", error);
        tableBody.innerHTML = `<tr><td colspan="4" class="text-center text-danger">Không thể tải dữ liệu. Vui lòng thử lại.</td></tr>`;
    }
}

function renderPagination(totalPages, currentPage) {
    paginationControls.innerHTML = '';
    if (totalPages <= 1) return;

    let html = '';
    const maxPagesToShow = 5;
    let startPage, endPage;

    if (totalPages <= maxPagesToShow) {
        startPage = 1;
        endPage = totalPages;
    } else {
        const maxPagesBeforeCurrent = Math.floor(maxPagesToShow / 2);
        const maxPagesAfterCurrent = Math.ceil(maxPagesToShow / 2) - 1;
        if (currentPage <= maxPagesBeforeCurrent) {
            startPage = 1;
            endPage = maxPagesToShow;
        } else if (currentPage + maxPagesAfterCurrent >= totalPages) {
            startPage = totalPages - maxPagesToShow + 1;
            endPage = totalPages;
        } else {
            startPage = currentPage - maxPagesBeforeCurrent;
            endPage = currentPage + maxPagesAfterCurrent;
        }
    }

    html += `<li class="page-item ${currentPage === 1 ? 'disabled' : ''}">
                    <a class="page-link" href="#" onclick="event.preventDefault(); loadHistoryData(${currentPage - 1})">Previous</a>
                 </li>`;

    for (let i = startPage; i <= endPage; i++) {
        html += `<li class="page-item ${i === currentPage ? 'active' : ''}">
                        <a class="page-link" href="#" onclick="event.preventDefault(); loadHistoryData(${i})">${i}</a>
                     </li>`;
    }

    html += `<li class="page-item ${currentPage === totalPages ? 'disabled' : ''}">
                    <a class="page-link" href="#" onclick="event.preventDefault(); loadHistoryData(${currentPage + 1})">Next</a>
                 </li>`;
    paginationControls.innerHTML = html;
}

searchButton.addEventListener('click', () => loadHistoryData(1));

// searchInput.addEventListener('keyup', function(event) {
//     if (event.key === 'Enter') {
//         searchButton.click();
//     }
// });

searchInput.addEventListener('input', debouncedSearch);

deviceSelect.addEventListener('change', () => loadHistoryData(1));
statusSelect.addEventListener('change', () => loadHistoryData(1));
pageSizeSelect.addEventListener('change', () => loadHistoryData(1));

async function loadDeviceFilters() {
    try {
        const response = await fetch('/api/IotApi/devicestates');
        if (!response.ok) return;

        const devices = await response.json();
        deviceSelect.innerHTML = '<option value="all" selected>Tất cả</option>';

        const deviceNames = [...new Set(devices.map(d => d.deviceName))];

        deviceNames.forEach(name => {
            const safeName = sanitizeHTML(name);
            const capitalizedName = safeName.charAt(0).toUpperCase() + safeName.slice(1);
            deviceSelect.innerHTML += `<option value="${safeName.toLowerCase()}">${capitalizedName}</option>`;
        });

    } catch (error) {
        console.error("Lỗi khi tải danh sách thiết bị:", error);
    }
}

async function start() {
    try {
        await connection.start();
        console.log("SignalR Connected for History page.");

        await Promise.all([
            loadDeviceFilters(),
            loadHistoryData(1)
        ]);

    } catch (err) {
        console.error(err);
        setTimeout(start, 5000);
    }
};
start();