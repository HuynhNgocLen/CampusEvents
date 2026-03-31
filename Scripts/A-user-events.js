/* ============================================================
   Scripts/A-user-events.js
   ============================================================ */
function toggleFavorite(btn, maEvent) {
    if (btn.getAttribute('data-loading') === 'true') return;

    var isSaved = btn.classList.contains('saved');
    var url = isSaved ? '/Users/RemoveFavorite' : '/Users/AddFavorite';

    var token = document.querySelector('input[name="__RequestVerificationToken"]');

    if (!token) {
        CeToast.show('Vui lòng tải lại trang.', 'error');
        return;
    }

    btn.setAttribute('data-loading', 'true');
    btn.style.opacity = '0.5';

    $.ajax({
        url: url,
        type: 'POST',
        data: {
            maEvent: maEvent,
            __RequestVerificationToken: token.value
        },
        success: function (res) {
            if (res.success) {
                // Toggle class saved
                btn.classList.toggle('saved');
                var nowSaved = btn.classList.contains('saved');

                // Cập nhật Icon
                var icon = btn.querySelector('.material-symbols-outlined');
                if (icon) {
                    icon.textContent = nowSaved ? 'favorite' : 'favorite_border';
                }

                // Cập nhật Text (nếu có thẻ span bao quanh chữ)
                var textSpan = btn.querySelector('.btn-text');
                if (textSpan) {
                    textSpan.textContent = nowSaved ? 'Đã lưu sự kiện' : 'Lưu sự kiện';
                }

                CeToast.show(
                    nowSaved ? 'Đã lưu sự kiện' : 'Đã bỏ lưu sự kiện',
                    nowSaved ? 'success' : 'info'
                );
            } else {
                CeToast.show(res.message || 'Có lỗi xảy ra', 'error');
            }
        },
        error: function (xhr) {
            CeToast.show('Không thể kết nối máy chủ', 'error');
        },
        complete: function () {
            btn.removeAttribute('data-loading');
            btn.style.opacity = '1';
        }
    });
}

document.addEventListener('DOMContentLoaded', function () {
    setTimeout(function () {
        if (typeof ceCountUp === 'function') {
            var cntSv = document.getElementById('cnt-sv');
            var cntClb = document.getElementById('cnt-clb');
            if (cntSv) ceCountUp(cntSv, 8420);
            if (cntClb) ceCountUp(cntClb, 47);
        }
    }, 300);

    /* ── Chip filter */
    document.querySelectorAll('.ce-chip').forEach(function (chip) {
        chip.addEventListener('click', function () {
            document.querySelectorAll('.ce-chip').forEach(function (c) {
                c.classList.remove('active');
            });
            chip.classList.add('active');
            var cat = chip.dataset.value || chip.dataset.filter || 'all';
            var cards = document.querySelectorAll('#events-grid > [data-category]');

            cards.forEach(function (card) {
                if (cat === 'all' || card.dataset.category === cat) {
                    card.style.display = '';
                } else {
                    card.style.display = 'none';
                }
            });
        });
    });

    /* ── check-all toggle ─────────────────── */
    document.querySelectorAll('.ce-filter-group').forEach(function (group) {
        var allCb = group.querySelector('.check-all');
        var items = group.querySelectorAll('.check-item');
        if (!allCb) return;

        allCb.addEventListener('change', function () {
            if (this.checked) {
                items.forEach(function (i) { i.checked = false; });
            }
        });

        items.forEach(function (item) {
            item.addEventListener('change', function () {
                if (this.checked) allCb.checked = false;
                if (group.querySelectorAll('.check-item:checked').length === 0) {
                    allCb.checked = true;
                }
            });
        });
    });

    /* ── Nút xóa bộ lọc ─────────────────────────────────────── */
    var clearBtn = document.getElementById('btn-clear-filter');
    if (clearBtn) {
        clearBtn.addEventListener('click', function () {
            document.querySelectorAll('.check-item').forEach(function (i) {
                i.checked = false;
            });
            document.querySelectorAll('.check-all').forEach(function (i) {
                i.checked = true;
            });
            var firstTime = document.querySelector('input[name="time-filter"]');
            if (firstTime) firstTime.checked = true;

            // Reset
            document.querySelectorAll('.ce-chip').forEach(function (c) {
                c.classList.remove('active');
            });
            var allChip = document.querySelector('.ce-chip[data-value="all"]');
            if (allChip) allChip.classList.add('active');

            // Hiện lại tất cả card
            document.querySelectorAll('#events-grid > [data-category]').forEach(function (card) {
                card.style.display = '';
            });
        });
    }

    /* ── Hero search  ─────────────────────────── */
    var heroSearch = document.getElementById('hero-search');
    if (heroSearch) {
        heroSearch.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && this.value.trim()) {
                window.location.href = '/Users/Events?q=' + encodeURIComponent(this.value.trim());
            }
        });
    }

    /* ── Navbar search ──────────────────────────────────────── */
    var navSearch = document.getElementById('navbar-search');
    if (navSearch) {
        navSearch.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && this.value.trim()) {
                window.location.href = '/Users/Events?q=' + encodeURIComponent(this.value.trim());
            }
        });
    }
});