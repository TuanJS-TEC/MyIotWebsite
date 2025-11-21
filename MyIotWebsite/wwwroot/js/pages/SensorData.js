document.addEventListener('DOMContentLoaded', function () {
    // ==========================================
    // 1. KHAI BÁO BIẾN (DOM ELEMENTS & STATE)
    // ==========================================
    const searchInput = document.getElementById('search-input');
    const searchTypeSelect = document.getElementById('search-type-select');
    const pageSizeSelect = document.getElementById('pagesize-select');
    const searchButton = document.getElementById('search-btn');
    const paginationControls = document.getElementById('pagination-controls');
    const tableBody = document.getElementById('sensor-data-table-body');
    const tableHeaders = document.querySelectorAll('.sortable');

    const deleteStartDateInput = document.getElementById('delete-start-date');
    const deleteEndDateInput = document.getElementById('delete-end-date');
    const deleteButton = document.getElementById('delete-btn');

    let currentPage = 1;
    let currentSortBy = 'timestamp';
    let currentSortOrder = 'desc';

    // ==========================================
    // 2. HÀM TIỆN ÍCH (UTILS & HELPERS)
    // ==========================================
    function debounce(func, delay = 500) {
        let timeoutId;
        return (...args) => {
            clearTimeout(timeoutId);
            timeoutId = setTimeout(() => {
                func.apply(this, args);
            }, delay);
        };
    }

    function formatDateTime(dateString) {
        const date = new Date(dateString);
        const day = String(date.getDate()).padStart(2, '0');
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const year = date.getFullYear().toString().slice(-2);
        const hours = String(date.getHours()).padStart(2, '0');
        const minutes = String(date.getMinutes()).padStart(2, '0');
        const seconds = String(date.getSeconds()).padStart(2, '0');
        return `${day}/${month}/${year} ${hours}:${minutes}:${seconds}`;
    }

    function formatFriendlyDate(dateStr) {
        if (!dateStr) return '';
        const [year, month, day] = dateStr.split('-');
        return `${day}/${month}/${year}`;
    }

    function getValueClass(type, value) {
        if (type === 'temp') {
            if (value > 35) return 'text-value-high';
            if (value < 20) return 'text-value-low';
        }
        if (type === 'hum') {
            if (value > 80) return 'text-value-high';
            if (value < 40) return 'text-value-low';
        }
        if (type === 'dust') {
            if (value > 500) return 'text-value-dust';
        }
        if (type === 'co2') {
            if (value > 50) return 'text-value-co2';
        }
        return '';
    }

    function updateSortUI() {
        tableHeaders.forEach(header => {
            const sortBy = header.getAttribute('data-sortby');
            header.classList.remove('sorted', 'asc', 'desc');
            if (sortBy === currentSortBy) {
                header.classList.add('sorted', currentSortOrder);
            }
        });
    }

    // ==========================================
    // 3. LOGIC CHÍNH (API & RENDERING)
    // ==========================================
    async function loadSensorData(page = 1) {
        currentPage = page;
        tableBody.innerHTML = `<tr><td colspan="7" class="text-center">Đang tải dữ liệu...</td></tr>`;
        try {
            const params = new URLSearchParams();
            if (searchInput.value) {
                params.append('searchTerm', searchInput.value.trim());
            }
            params.append('searchType', searchTypeSelect.value);
            params.append('pageNumber', currentPage);
            params.append('pageSize', pageSizeSelect.value);
            params.append('sortBy', currentSortBy);
            params.append('sortOrder', currentSortOrder);

            const apiUrl = `/api/IotApi/sensordata/search?${params.toString()}`;
            const response = await fetch(apiUrl);

            if (!response.ok){
                if (response.status === 404) {
                    const errorMessage = await response.text();
                    tableBody.innerHTML = `<tr><td colspan="7" class="text-center text-danger p-3">${errorMessage}</td></tr>`;
                } else {
                    throw new Error(`Lỗi máy chủ hoặc API: ${response.status}`);
                }
                paginationControls.innerHTML = '';
                return;
            }
            const paginatedResponse = await response.json();

            tableBody.innerHTML = '';

            if (paginatedResponse.data.length === 0) {
                const message = searchInput.value
                    ? "Không tồn tại bất kỳ giá trị nào như bạn yêu cầu."
                    : "Chưa có dữ liệu nào.";
                tableBody.innerHTML = `<tr><td colspan="7" class="text-center text-warning p-3">${message}</td></tr>`;
                paginationControls.innerHTML = '';
                return;
            }

            paginatedResponse.data.forEach(record => {
                const row = document.createElement('tr');

                row.insertCell(0).textContent = record.id;

                const tempCell = row.insertCell(1);
                tempCell.textContent = record.temperature.toFixed(1);
                tempCell.className = getValueClass('temp', record.temperature);

                const humCell = row.insertCell(2);
                humCell.textContent = record.humidity.toFixed(1);
                humCell.className = getValueClass('hum', record.humidity);

                row.insertCell(3).textContent = record.light;

                const dustCell = row.insertCell(4);
                dustCell.textContent = record.dust.toFixed(0);
                dustCell.className = getValueClass('dust', record.dust);

                const co2Cell = row.insertCell(5);
                co2Cell.textContent = record.co2.toFixed(0);
                co2Cell.className = getValueClass('co2', record.co2);

                row.insertCell(6).textContent = formatDateTime(record.timestamp);

                tableBody.appendChild(row);
            });
            renderPagination(paginatedResponse.totalPages, paginatedResponse.pageNumber);
        } catch (error) {
            console.error("Lỗi khi tải dữ liệu:", error);
            tableBody.innerHTML = '<tr><td colspan="7" class="text-center text-danger p-3">Không thể tải dữ liệu. Vui lòng thử lại.</td></tr>';
        }
    }

    const debouncedLoadData = debounce(() => loadSensorData(1), 500);

    function renderPagination(totalPages, currentPage) {
        paginationControls.innerHTML = '';
        if (totalPages <= 1) return;

        const createPageItem = (page, text, isDisabled = false, isActive = false) => {
            const li = document.createElement('li');
            li.className = `page-item ${isDisabled ? 'disabled' : ''} ${isActive ? 'active' : ''}`;
            const a = document.createElement('a');
            a.className = 'page-link';
            a.href = '#';
            a.innerText = text;
            if (!isDisabled) {
                a.onclick = (e) => {
                    e.preventDefault();
                    loadSensorData(page);
                };
            }
            li.appendChild(a);
            return li;
        };

        paginationControls.appendChild(createPageItem(currentPage - 1, 'Previous', currentPage === 1));

        const pageToShow = 5;
        let startPage, endPage;

        if (totalPages <= pageToShow) {
            startPage = 1;
            endPage = totalPages;
        } else {
            const maxPagesBeforeCurrentPage = Math.floor(pageToShow / 2);
            const maxPagesAfterCurrentPage = Math.ceil(pageToShow / 2) - 1;
            if (currentPage <= maxPagesBeforeCurrentPage) {
                startPage = 1;
                endPage = pageToShow;
            } else if (currentPage + maxPagesAfterCurrentPage >= totalPages) {
                startPage = totalPages - pageToShow + 1;
                endPage = totalPages;
            } else {
                startPage = currentPage - maxPagesBeforeCurrentPage;
                endPage = currentPage + maxPagesAfterCurrentPage;
            }
        }

        if (startPage > 1) {
            paginationControls.appendChild(createPageItem(1, '1'));
            if (startPage > 2) {
                const li = document.createElement('li');
                li.className = 'page-item disabled';
                li.innerHTML = `<span class="page-link">...</span>`;
                paginationControls.appendChild(li);
            }
        }

        for (let i = startPage; i <= endPage; i++) {
            paginationControls.appendChild(createPageItem(i, i, false, i === currentPage));
        }

        if (endPage < totalPages) {
            if (endPage < totalPages - 1) {
                const li = document.createElement('li');
                li.className = 'page-item disabled';
                li.innerHTML = `<span class="page-link">...</span>`;
                paginationControls.appendChild(li);
            }
            paginationControls.appendChild(createPageItem(totalPages, totalPages));
        }

        paginationControls.appendChild(createPageItem(currentPage + 1, 'Next', currentPage === totalPages));
    }

    // ==========================================
    // 4. SIGNALR (REAL-TIME UPDATES)
    // ==========================================
    const connection = new signalR.HubConnectionBuilder().withUrl("/sensorHub").build();

    connection.on("ReceiveSensorData", function (record) {
        if (currentPage === 1 && !searchInput.value && currentSortBy === 'timestamp' && currentSortOrder === 'desc') {
            const existingRows = tableBody.getElementsByTagName('tr');
            if (existingRows.length === 1 && existingRows[0].getElementsByTagName('td').length > 1) {
                if (existingRows[0].querySelector('.text-warning, .text-danger, .text-center')) {
                    tableBody.innerHTML = '';
                }
            }

            const row = document.createElement('tr');
            row.className = 'new-row-highlight';

            row.innerHTML = `<td>${record.id}</td>
                                    <td class="${getValueClass('temp', record.temperature)}">${record.temperature.toFixed(1)}</td>
                                    <td class="${getValueClass('hum', record.humidity)}">${record.humidity.toFixed(1)}</td>
                                    <td>${record.light}</td>
                                    <td class="${getValueClass('dust', record.dust)}">${record.dust.toFixed(0)}</td>
                                    <td class="${getValueClass('co2', record.co2)}">${record.co2.toFixed(0)}</td>
                                    <td>${formatDateTime(record.timestamp)}</td>`;
            tableBody.prepend(row);

            const pageSize = parseInt(pageSizeSelect.value, 10);

            if (tableBody.rows.length > pageSize){
                tableBody.deleteRow(tableBody.rows.length - 1);
            }
        }
    });

    // ==========================================
    // 5. GẮN SỰ KIỆN (EVENT LISTENERS)
    // ==========================================

    // Search & Filter Events
    searchButton.addEventListener('click', () => loadSensorData(1));
    pageSizeSelect.addEventListener('change', () => loadSensorData(1));
    searchTypeSelect.addEventListener('change', () => loadSensorData(1));
    searchInput.addEventListener('input', debouncedLoadData);
    searchInput.addEventListener('keyup', (event) => {
        if (event.key === 'Enter') {
            loadSensorData(1);
        }
    });

    // Sort Events
    tableHeaders.forEach(header => {
        header.addEventListener('click', () => {
            const sortBy = header.getAttribute('data-sortby');
            if (!sortBy) return;

            if (currentSortBy === sortBy) {
                currentSortOrder = currentSortOrder === 'asc' ? 'desc' : 'asc';
            } else {
                currentSortBy = sortBy;
                currentSortOrder = (sortBy === 'timestamp') ? 'desc' : 'asc';
            }
            updateSortUI();
            loadSensorData(1);
        });
    });

    deleteButton.addEventListener('click', () => {
        const startDate = deleteStartDateInput.value;
        const endDate = deleteEndDateInput.value;

        if (!startDate || !endDate) {
            alert("Vui lòng chọn cả 'Từ ngày' và 'Đến ngày'.");
            return;
        }
        alert("Chức năng này đã bị tắt. Dữ liệu được bảo toàn.");
        deleteStartDateInput.value = '';
        deleteEndDateInput.value = '';
    });
    // ==========================================
    // 6. KHỞI TẠO (INITIALIZATION)
    // ==========================================
    async function start() {
        try {
            await connection.start();
            console.log("SignalR Connected.");
            updateSortUI();
            await loadSensorData(1);
        } catch (err) {
            console.error("Lỗi SignalR: ", err);
            setTimeout(start, 5000);
        }
    }

    start();
});