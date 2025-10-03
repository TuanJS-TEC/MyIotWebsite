document.addEventListener('DOMContentLoaded', function() {
    const themeToggle = document.getElementById('theme-toggle');
    const toggleIcon = themeToggle.querySelector('i');

    // Hàm để cập nhật icon
    function updateIcon(theme) {
        if (theme === 'dark') {
            toggleIcon.classList.remove('fa-moon');
            toggleIcon.classList.add('fa-sun');
        } else {
            toggleIcon.classList.remove('fa-sun');
            toggleIcon.classList.add('fa-moon');
        }
    }

    // Lấy theme đã lưu và cập nhật icon khi tải trang
    let currentTheme = localStorage.getItem('theme') || 'light';
    document.body.setAttribute('data-theme', currentTheme);
    updateIcon(currentTheme);

    // Xử lý sự kiện click
    themeToggle.addEventListener('click', function() {
        let theme = document.body.getAttribute('data-theme');

        if (theme === 'dark') {
            theme = 'light';
        } else {
            theme = 'dark';
        }

        // Cập nhật thuộc tính trên body
        document.body.setAttribute('data-theme', theme);
        // Cập nhật icon
        updateIcon(theme);
        // Lưu lựa chọn vào localStorage
        localStorage.setItem('theme', theme);
    });
});