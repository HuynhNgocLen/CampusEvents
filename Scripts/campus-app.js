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

/* ── Counter animation ────────────────────────────────────────── */
function ceCountUp(el, target, dur) {
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

    if (window.innerWidth >= MOBILE_BP && localStorage.getItem('sidebarCollapsed') === '1') {
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

    document.querySelectorAll('.ce-toast').forEach(function (el) {
        setTimeout(function () {
            el.style.transition = 'opacity .4s';
            el.style.opacity = '0';
            setTimeout(function () { el.remove(); }, 400);
        }, 4000);
    });

})();