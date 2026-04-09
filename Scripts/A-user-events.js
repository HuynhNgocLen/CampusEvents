/* Scripts/A-user-events.js */

/* ══════════════════════════════════════════════════════════
   1.  FILTER SIDEBAR  (open / close / toggle)
   ══════════════════════════════════════════════════════════ */
function openFilter() {
    var panel = document.getElementById('ce-filter-sidebar');
    var backdrop = document.getElementById('cef-backdrop');
    if (!panel) return;
    panel.classList.add('open');
    if (backdrop) backdrop.classList.add('show');
    document.body.style.overflow = 'hidden';
}

function closeFilter() {
    var panel = document.getElementById('ce-filter-sidebar');
    var backdrop = document.getElementById('cef-backdrop');
    if (!panel) return;
    panel.classList.remove('open');
    if (backdrop) backdrop.classList.remove('show');
    document.body.style.overflow = '';
}

function toggleFilterSidebar() {
    var panel = document.getElementById('ce-filter-sidebar');
    if (!panel) return;
    panel.classList.contains('open') ? closeFilter() : openFilter();
}

/* ══════════════════════════════════════════════════════════
   2.  LƯU / BỎ LƯU SỰ KIỆN  (Yêu thích)
   ══════════════════════════════════════════════════════════ */
function toggleFavorite(btn, maEvent) {
    if (btn.getAttribute('data-loading') === 'true') return;
    btn.setAttribute('data-loading', 'true');

    var iconSpan = btn.querySelector('.material-symbols-outlined');
    var textSpan = btn.querySelector('.btn-text');

    fetch('/Users/ToggleFavorite', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ maEvent: maEvent })
    })
        .then(res => res.json())
        .then(data => {
            btn.setAttribute('data-loading', 'false');
            if (data.success) {
                if (data.isFavorite) {
                    btn.classList.add('saved');
                    if (iconSpan) iconSpan.textContent = 'favorite';
                    if (textSpan) textSpan.textContent = 'Đã lưu sự kiện';

                    // ĐỔI TỪ showToast THÀNH CeToast.show
                    if (typeof CeToast !== 'undefined') {
                        CeToast.show('Đã lưu sự kiện', 'success');
                    }
                } else {
                    btn.classList.remove('saved');
                    if (iconSpan) iconSpan.textContent = 'favorite_border';
                    if (textSpan) textSpan.textContent = 'Lưu sự kiện';

                    // ĐỔI TỪ showToast THÀNH CeToast.show
                    if (typeof CeToast !== 'undefined') {
                        CeToast.show('Đã bỏ lưu sự kiện', 'info');
                    }
                }
            }
        })
        .catch(err => {
            btn.setAttribute('data-loading', 'false');
            console.error('Lỗi:', err);
        });
}

/* ══════════════════════════════════════════════════════════
   3. CÁC TƯƠNG TÁC GIAO DIỆN CHUNG & BỘ LỌC
   ══════════════════════════════════════════════════════════ */
document.addEventListener('DOMContentLoaded', function () {
    // Xử lý Category Chips
    document.querySelectorAll('.ce-chip').forEach(function (chip) {
        chip.addEventListener('click', function () {
            document.querySelectorAll('.ce-chip').forEach(function (c) { c.classList.remove('active'); });
            chip.classList.add('active');

            var cat = chip.dataset.value || 'all';
            var cards = document.querySelectorAll('#events-grid > [data-category]');
            cards.forEach(function (card) {
                card.style.display = (cat === 'all' || card.dataset.category === cat) ? '' : 'none';
            });
        });
    });

    // Mở sẵn danh sách trong bộ lọc
    document.querySelectorAll('.cef-group__body').forEach(function (body) {
        body.style.maxHeight = body.scrollHeight + 'px';
        body.style.opacity = '1';
    });
    updateFilterCount();
});

/* ── Xử lý Click Toàn cục (Đóng filter, Đóng/mở Accordion) ── */
document.addEventListener('click', function (e) {
    // 1. Đóng filter khi click backdrop
    var panel = document.getElementById('ce-filter-sidebar');
    var trigger = e.target.closest('.ce-filter-trigger');
    if (window.innerWidth <= 991.98 && panel && panel.classList.contains('open')) {
        if (!panel.contains(e.target) && !trigger && !e.target.closest('#cef-backdrop')) {
            closeFilter();
        }
    }

    // 2. Mở/đóng Accordion bộ lọc
    var toggleBtn = e.target.closest('.cef-group__toggle');
    if (toggleBtn) {
        var body = toggleBtn.closest('.cef-group').querySelector('.cef-group__body');
        var isOpen = toggleBtn.getAttribute('aria-expanded') === 'true';

        toggleBtn.setAttribute('aria-expanded', String(!isOpen));
        body.style.maxHeight = isOpen ? '0' : body.scrollHeight + 'px';
        body.style.opacity = isOpen ? '0' : '1';
    }
});

/* ── Xử lý thay đổi Checkbox/Radio trong Bộ lọc ── */
document.addEventListener('change', function (e) {
    // Làm sáng ô Thời gian
    if (e.target.matches('.cef-time-pill input[type="radio"]')) {
        document.querySelectorAll('.cef-time-pill').forEach(function (p) { p.classList.remove('active'); });
        if (e.target.checked) e.target.closest('.cef-time-pill').classList.add('active');
    }

    // Nút "Tất cả"
    if (e.target.matches('.check-all')) {
        var group = e.target.closest('.cef-group');
        var items = group.querySelectorAll('.check-item');
        if (e.target.checked) items.forEach(function (i) { i.checked = false; });
    }

    // Nút mục lẻ (VD: Còn chỗ, Khoa IT...)
    if (e.target.matches('.check-item')) {
        var group = e.target.closest('.cef-group');
        var allCb = group.querySelector('.check-all');
        var items = group.querySelectorAll('.check-item');
        var exclusive = group.dataset.exclusive === 'true';

        if (e.target.checked) {
            if (allCb) allCb.checked = false;
            if (exclusive) {
                items.forEach(function (i) { if (i !== e.target) i.checked = false; });
            }
        }
        var anyChecked = Array.from(items).some(function (i) { return i.checked; });
        if (!anyChecked && allCb) allCb.checked = true;
    }

    updateFilterCount();
});

/* ── Hàm cập nhật đếm số lượng bộ lọc ── */
function updateFilterCount() {
    var count = document.querySelectorAll('#filter-form .check-item:checked').length;
    var timeVal = document.querySelector('#filter-form input[name="time"]:checked');
    if (timeVal && timeVal.value !== 'all') count++;

    var badge = document.getElementById('filter-count');
    if (badge) {
        badge.textContent = count;
        badge.style.display = count > 0 ? 'inline-flex' : 'none';
    }
}

/* ── Tìm kiếm (Enter) ── */
var handleSearch = function (e) {
    if (e.key === 'Enter' && e.target.value.trim()) {
        window.location.href = '/Users/Events?q=' + encodeURIComponent(e.target.value.trim());
    }
};

var heroSearch = document.getElementById('hero-search');
var navSearch = document.getElementById('navbar-search');
if (heroSearch) heroSearch.addEventListener('keydown', handleSearch);
if (navSearch) navSearch.addEventListener('keydown', handleSearch);