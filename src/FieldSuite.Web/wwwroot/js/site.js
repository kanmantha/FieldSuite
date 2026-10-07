(function () {
    "use strict";

    var sidebar = document.getElementById('appSidebar');
    var overlay = document.getElementById('sidebarOverlay');

    function closeSidebar() {
        if (!sidebar) return;
        sidebar.classList.remove('open');
        if (overlay) overlay.classList.remove('show');
    }

    var toggle = document.getElementById('sidebarToggle');
    if (toggle) {
        toggle.addEventListener('click', function () {
            sidebar.classList.toggle('open');
            if (overlay) overlay.classList.toggle('show', sidebar.classList.contains('open'));
        });
    }
    if (overlay) {
        overlay.addEventListener('click', closeSidebar);
    }

    // Auto-dismiss alerts after 6s
    setTimeout(function () {
        document.querySelectorAll('.alert.show').forEach(function (a) {
            if (window.bootstrap && bootstrap.Alert) {
                bootstrap.Alert.getOrCreateInstance(a).close();
            }
        });
    }, 6000);

    // Clicking a nav link on mobile closes the sidebar
    if (sidebar) {
        sidebar.querySelectorAll('.nav-link').forEach(function (link) {
            link.addEventListener('click', closeSidebar);
        });
    }
})();