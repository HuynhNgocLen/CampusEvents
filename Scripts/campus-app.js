'use strict';

/* Theme (light / dark) */
var CeTheme = (function () {
    var KEY = 'ce-theme';
    function set(t) {
        document.documentElement.setAttribute('data-theme', t);
        localStorage.setItem(KEY, t);
        _syncIcons(t);
    }
    function toggle() { set(get() === 'dark' ? 'light' : 'dark'); }
    function get() { return document.documentElement.getAttribute('data-theme') || 'light'; }
    function init() { set(localStorage.getItem(KEY) || 'light'); }
    function _syncIcons(t) {
        document.querySelectorAll('.ce-theme-icon').forEach(function (el) {
            el.textContent = t === 'dark' ? 'dark_mode' : 'light_mode';
        });
    }
    return { init: init, toggle: toggle, get: get };
})();

/* Toast */
var CeToast = (function () {
    var ICONS = { success: 'check_circle', error: 'cancel', info: 'info' };
    function show(msg, type, ms) {
        type = type || 'info'; ms = ms || 3400;
        var wrap = document.querySelector('.ce-toast-wrap');
        if (!wrap) {
            wrap = document.createElement('div');
            wrap.className = 'ce-toast-wrap';
            document.body.appendChild(wrap);
        }
        var el = document.createElement('div');
        el.className = 'ce-toast ce-toast-' + type;
        el.innerHTML = '<span class="material-symbols-outlined">' + (ICONS[type] || 'info') + '</span>'
            + '<span>' + msg + '</span>';
        wrap.appendChild(el);
        setTimeout(function () {
            el.style.cssText = 'opacity:0;transform:translateX(1rem);transition:.3s ease';
            setTimeout(function () { el.remove(); }, 320);
        }, ms);
    }
    return { show: show };
})();

/* Modal */
function ceOpenModal(id) {
    var el = document.getElementById(id);
    if (el) { el.classList.add('open'); document.body.style.overflow = 'hidden'; }
}
function ceCloseModal(id) {
    var el = document.getElementById(id);
    if (el) { el.classList.remove('open'); document.body.style.overflow = ''; }
}

/* Navbar mobile toggle */
function ceInitNavbar() {
    var btn = document.getElementById('ce-nav-toggler');
    var menu = document.getElementById('ce-mobile-menu');
    if (!btn || !menu) return;

    btn.addEventListener('click', function () {
        var open = menu.classList.toggle('open');
        btn.querySelector('.material-symbols-outlined').textContent = open ? 'close' : 'menu';
    });
    document.addEventListener('click', function (e) {
        if (!btn.contains(e.target) && !menu.contains(e.target)) {
            menu.classList.remove('open');
            if (btn.querySelector('.material-symbols-outlined'))
                btn.querySelector('.material-symbols-outlined').textContent = 'menu';
        }
    });
    menu.querySelectorAll('a').forEach(function (a) {
        a.addEventListener('click', function () {
            menu.classList.remove('open');
            if (btn.querySelector('.material-symbols-outlined'))
                btn.querySelector('.material-symbols-outlined').textContent = 'menu';
        });
    });
}

/* Tabs */
function ceInitTabs() {
    document.querySelectorAll('.ce-tab-btn').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var paneId = btn.dataset.tab;
            var scope = btn.closest('.ce-tabs-wrapper') || document;
            scope.querySelectorAll('.ce-tab-btn').forEach(function (b) { b.classList.remove('active'); });
            scope.querySelectorAll('.ce-tab-pane').forEach(function (p) { p.classList.remove('active'); });
            btn.classList.add('active');
            var pane = document.getElementById(paneId);
            if (pane) pane.classList.add('active');
        });
    });
}

/* Chips */
function ceInitChips() {
    document.querySelectorAll('.ce-chip').forEach(function (chip) {
        chip.addEventListener('click', function () {
            var group = chip.closest('.ce-chips');
            if (group) {
                group.querySelectorAll('.ce-chip').forEach(function (c) { c.classList.remove('active'); });
            }
            chip.classList.add('active');
        });
    });
}

/* Scroll reveal */
function ceInitReveal() {
    if (!window.IntersectionObserver) {
        document.querySelectorAll('.ce-reveal').forEach(function (el) { el.classList.add('visible'); });
        return;
    }
    var obs = new IntersectionObserver(function (entries) {
        entries.forEach(function (e) {
            if (e.isIntersecting) { e.target.classList.add('visible'); obs.unobserve(e.target); }
        });
    }, { threshold: 0.08 });
    document.querySelectorAll('.ce-reveal').forEach(function (el) { obs.observe(el); });
}

function ceInitNotificationPopup() {
    var listEl = document.getElementById('ce-notif-list');
    if (!listEl) return;

    var btnReadAll = document.getElementById('ce-notif-read-all');
    var btnClearAll = document.getElementById('ce-notif-clear-all');
    var dot = document.querySelector('.ce-notif-dot');
    var KEY = 'ce-user-notifications-v1';

    function seedData() {
        return [
            { id: 'n1', title: 'Workshop UI/UX', message: 'Sự kiện diễn ra vào 08:00 ngày mai.', time: 'Vừa xong', read: false },
            { id: 'n2', title: 'Đăng ký thành công', message: 'Bạn đã đăng ký Talkshow Công nghệ 2026.', time: '10 phút trước', read: false },
            { id: 'n3', title: 'Nhắc lịch', message: 'Bạn có sự kiện trong tuần này.', time: 'Hôm qua', read: true }
        ];
    }

    function loadItems() {
        try {
            var raw = localStorage.getItem(KEY);
            if (!raw) return seedData();
            var parsed = JSON.parse(raw);
            return Array.isArray(parsed) ? parsed : seedData();
        } catch (e) {
            return seedData();
        }
    }

    function saveItems(items) {
        localStorage.setItem(KEY, JSON.stringify(items));
    }

    function hasUnread(items) {
        return items.some(function (x) { return !x.read; });
    }

    function updateDot(items) {
        if (!dot) return;
        dot.style.display = hasUnread(items) ? 'inline-flex' : 'none';
    }

    function escapeHtml(text) {
        return String(text || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function render() {
        var items = loadItems();
        updateDot(items);
        if (!items.length) {
            listEl.innerHTML = '<div class="text-muted text-center py-4">Không có thông báo nào.</div>';
            return;
        }

        listEl.innerHTML = items.map(function (item) {
            return '<div class="border rounded-3 p-3 ' + (item.read ? '' : 'bg-primary-subtle') + '" data-id="' + escapeHtml(item.id) + '">'
                + '<div class="d-flex justify-content-between align-items-start gap-2">'
                + '<div>'
                + '<div class="fw-semibold">' + escapeHtml(item.title) + '</div>'
                + '<div class="text-muted small mt-1">' + escapeHtml(item.message) + '</div>'
                + '</div>'
                + (!item.read ? '<span class="badge text-bg-primary">Mới</span>' : '')
                + '</div>'
                + '<div class="small text-muted mt-2">' + escapeHtml(item.time) + '</div>'
                + '</div>';
        }).join('');
    }

    listEl.addEventListener('click', function (e) {
        var card = e.target.closest('[data-id]');
        if (!card) return;
        var id = card.getAttribute('data-id');
        var items = loadItems();
        var item = items.find(function (x) { return x.id === id; });
        if (!item || item.read) return;
        item.read = true;
        saveItems(items);
        render();
    });

    if (btnReadAll) {
        btnReadAll.addEventListener('click', function () {
            var items = loadItems();
            items.forEach(function (x) { x.read = true; });
            saveItems(items);
            render();
        });
    }

    if (btnClearAll) {
        btnClearAll.addEventListener('click', function () {
            saveItems([]);
            render();
        });
    }

    render();
}

/* ── Counter animation ────────────────────────────────────────── */
function ceCountUp(el, target, dur) {
    if (!el) return;
    dur = dur || 1200;
    var start = null;
    function step(ts) {
        if (!start) start = ts;
        var p = Math.min((ts - start) / dur, 1);
        var ease = 1 - Math.pow(1 - p, 3);
        el.textContent = Math.floor(ease * target).toLocaleString('vi-VN');
        if (p < 1) requestAnimationFrame(step);
    }
    requestAnimationFrame(step);
}

/* ── Overlay click closes modal ──────────────────────────────── */
document.addEventListener('click', function (e) {
    if (e.target.classList.contains('ce-modal-overlay')) {
        e.target.classList.remove('open');
        document.body.style.overflow = '';
    }
});

/* ── Init on DOMContentLoaded ─────────────────────────────────── */
document.addEventListener('DOMContentLoaded', function () {
    CeTheme.init();
    ceInitNavbar();
    ceInitTabs();
    ceInitChips();
    ceInitReveal();
    ceInitNotificationPopup();

    // Theme toggle buttons
    document.querySelectorAll('[data-action="toggle-theme"]').forEach(function (btn) {
        btn.addEventListener('click', CeTheme.toggle);
    });
});
/*======= Admin sidebar toggle (mobile + desktop) =======*/
(function () {
    var shell = document.getElementById('caShell');
    var sidebar = document.getElementById('adminSidebar');
    var overlay = document.getElementById('sidebarOverlay');
    var toggleBtn = document.getElementById('sidebarToggle');

    var MOBILE_BP = 992;

    var hasAdminSidebar = shell && sidebar && overlay && toggleBtn;

    if (hasAdminSidebar && window.innerWidth >= MOBILE_BP && localStorage.getItem('sidebarCollapsed') === '1') {
        shell.classList.add('sidebar-collapsed');
        toggleBtn.setAttribute('aria-expanded', 'false');
    }

    function isMobile() { return window.innerWidth < MOBILE_BP; }

    function openMobile() {
        sidebar.classList.add('open');
        overlay.classList.add('visible');
        toggleBtn.setAttribute('aria-expanded', 'true');
        document.body.style.overflow = 'hidden';
    }

    function closeMobile() {
        sidebar.classList.remove('open');
        overlay.classList.remove('visible');
        toggleBtn.setAttribute('aria-expanded', 'false');
        document.body.style.overflow = '';
    }

    function toggleDesktop() {
        var collapsed = shell.classList.toggle('sidebar-collapsed');
        toggleBtn.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
        localStorage.setItem('sidebarCollapsed', collapsed ? '1' : '0');
    }

    if (hasAdminSidebar) {
        toggleBtn.addEventListener('click', function () {
            if (isMobile()) {
                sidebar.classList.contains('open') ? closeMobile() : openMobile();
            } else {
                toggleDesktop();
            }
        });
        overlay.addEventListener('click', closeMobile);

        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && isMobile()) closeMobile();
        });

        window.addEventListener('resize', function () {
            if (!isMobile()) {
                closeMobile();
            }
        });

        sidebar.querySelectorAll('.ca-nav-item').forEach(function (item) {
            item.addEventListener('click', function () {
                if (isMobile()) closeMobile();
            });
        });
    }

    document.querySelectorAll('.ce-toast').forEach(function (el) {
        setTimeout(function () {
            el.style.transition = 'opacity .4s';
            el.style.opacity = '0';
            setTimeout(function () { el.remove(); }, 400);
        }, 4000);
    });

})();