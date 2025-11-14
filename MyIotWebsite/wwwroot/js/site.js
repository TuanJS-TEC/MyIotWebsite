document.addEventListener('DOMContentLoaded', function() {
    const themeToggle = document.getElementById('theme-toggle');

    if (!themeToggle) {
        return;
    }

    const toggleIcon = themeToggle.querySelector('i');
    function updateIcon(theme) {
        if (theme === 'dark') {
            toggleIcon.classList.remove('fa-moon');
            toggleIcon.classList.add('fa-sun');
        } else {
            toggleIcon.classList.remove('fa-sun');
            toggleIcon.classList.add('fa-moon');
        }
    }

    let initialTheme = document.documentElement.getAttribute('data-theme');
    updateIcon(initialTheme);


    themeToggle.addEventListener('click', function() {

        let currentTheme = document.documentElement.getAttribute('data-theme');

        let newTheme = (currentTheme === 'dark') ? 'light' : 'dark';

        document.documentElement.setAttribute('data-theme', newTheme);

        updateIcon(newTheme);

        localStorage.setItem('theme', newTheme);
    });
});