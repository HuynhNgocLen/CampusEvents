/* ============================================================
   A-event-manage.js
   Quản lí sự kiện — View toggle · Student modal · Close event
   ============================================================ */
'use strict';

(function () {

    /* ══════════════════════════════════════════════════════════
       1. CARD / LIST VIEW TOGGLE
       ══════════════════════════════════════════════════════════ */
    var KEY_VIEW = 'em-view-mode';

    var btnCardView = document.getElementById('emBtnCardView');
    var btnListView = document.getElementById('emBtnListView');
    var cardGrid = document.getElementById('emCardGrid');
    var listView = document.getElementById('emListView');

    function setView(mode) {
        if (!cardGrid || !listView) return;
        if (mode === 'list') {
            cardGrid.classList.remove('active');
            listView.classList.add('active');
            if (btnCardView) btnCardView.classList.remove('active');
            if (btnListView) btnListView.classList.add('active');
        } else {
            cardGrid.classList.add('active');
            listView.classList.remove('active');
            if (btnCardView) btnCardView.classList.add('active');
            if (btnListView) btnListView.classList.remove('active');
        }
        localStorage.setItem(KEY_VIEW, mode);
    }

    // Init view from saved preference
    var savedView = localStorage.getItem(KEY_VIEW) || 'card';
    setView(savedView);

    if (btnCardView) {
        btnCardView.addEventListener('click', function () { setView('card'); });
    }
    if (btnListView) {
        btnListView.addEventListener('click', function () { setView('list'); });
    }

    /* ══════════════════════════════════════════════════════════
       2. STUDENT LIST MODAL
       ══════════════════════════════════════════════════════════ */
    var modalOverlay = document.getElementById('emStudentModal');
    var modalTitle = document.getElementById('emModalTitle');
    var modalBody = document.getElementById('emModalBody');
    var modalCount = document.getElementById('emModalCount');
    var modalSearchInput = document.getElementById('emModalSearch');
    var modalStatusFilter = document.getElementById('emModalStatusFilter');
    var modalExcelBtn = document.getElementById('emModalExcel');
    var modalCloseBtn = document.getElementById('emModalClose');

    var currentEventId = null;
    var allStudents = []; // cached for client-side search
    var studentStatuses = ['Đã đăng ký', 'Đã hoàn thành', 'Đã hủy'];

    function openStudentModal(eventId, eventName) {
        if (!modalOverlay) return;
        currentEventId = eventId;

        // Set title
        if (modalTitle) {
            modalTitle.innerHTML =
                '<span class="material-symbols-outlined">group</span>' +
                'Danh sách sinh viên — ' + eventName;
        }

        // Show loading
        if (modalBody) {
            modalBody.innerHTML =
                '<div class="em-modal__loading">' +
                '<span class="material-symbols-outlined em-spin">progress_activity</span>' +
                '<span>Đang tải danh sách...</span>' +
                '</div>';
        }

        // Reset search
        if (modalSearchInput) modalSearchInput.value = '';
        if (modalStatusFilter) modalStatusFilter.value = '';
        if (modalCount) modalCount.textContent = '';
        syncModalExcelUrl();

        modalOverlay.classList.add('open');
        document.body.style.overflow = 'hidden';

        // Fetch students
        fetchStudents(eventId);
    }

    function fetchStudents(eventId) {
        fetch(EM_CONFIG.getStudentsUrl + '?id=' + eventId)
            .then(function (res) { return res.json(); })
            .then(function (data) {
                if (data.success) {
                    allStudents = data.data;
                    applyModalFilters();
                } else {
                    showModalError('Không thể tải dữ liệu.');
                }
            })
            .catch(function () {
                showModalError('Lỗi kết nối máy chủ.');
            });
    }

    function renderStudents(students) {
        if (!modalBody) return;

        if (!students || students.length === 0) {
            modalBody.innerHTML =
                '<div class="em-modal__empty">' +
                '<span class="material-symbols-outlined">person_off</span>' +
                '<span>Chưa có sinh viên đăng ký</span>' +
                '</div>';
            return;
        }

        var html = '<table class="em-student-table"><thead><tr>' +
            '<th style="width:40px">#</th>' +
            '<th>MSSV</th>' +
            '<th>Họ Tên</th>' +
            '<th>Lớp</th>' +
            '<th>Viện</th>' +
            '<th>Trạng thái</th>' +
            '</tr></thead><tbody>';

        for (var i = 0; i < students.length; i++) {
            var s = students[i];
            html += '<tr>' +
                '<td style="color:var(--text-muted);font-size:.78rem">' + (i + 1) + '</td>' +
                '<td class="td-mssv">' + escapeHtml(s.mssv || '') + '</td>' +
                '<td>' + escapeHtml(s.hoTen || '') + '</td>' +
                '<td>' + escapeHtml(s.lop || '') + '</td>' +
                '<td>' + escapeHtml(s.vien || '') + '</td>' +
                '<td>' +
                '<select class="em-status-select" data-student-id="' + escapeHtml(s.idSinhVien || s.mssv || '') + '" data-current-status="' + escapeHtml(s.trangThai || '') + '">' +
                buildStatusOptions(s.trangThai) +
                '</select>' +
                '</td>' +
                '</tr>';
        }

        html += '</tbody></table>';
        modalBody.innerHTML = html;
    }

    function showModalError(msg) {
        if (modalBody) {
            modalBody.innerHTML =
                '<div class="em-modal__empty">' +
                '<span class="material-symbols-outlined">error</span>' +
                '<span>' + msg + '</span>' +
                '</div>';
        }
    }

    function closeStudentModal() {
        if (!modalOverlay) return;
        modalOverlay.classList.remove('open');
        document.body.style.overflow = '';
        allStudents = [];
        currentEventId = null;
    }

    // Close button
    if (modalCloseBtn) {
        modalCloseBtn.addEventListener('click', closeStudentModal);
    }

    // Click overlay to close
    if (modalOverlay) {
        modalOverlay.addEventListener('click', function (e) {
            if (e.target === modalOverlay) closeStudentModal();
        });
    }

    // Escape key
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && modalOverlay && modalOverlay.classList.contains('open')) {
            closeStudentModal();
        }
        if (e.key === 'Escape' && deleteModalOverlay && deleteModalOverlay.classList.contains('open')) {
            closeDeleteModal();
        }
        if (e.key === 'Escape' && closeModalOverlay && closeModalOverlay.classList.contains('open')) {
            closeCloseModal();
        }
    });

    // Client-side search in modal
    if (modalSearchInput) {
        var searchTimeout;
        modalSearchInput.addEventListener('input', function () {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(function () {
                applyModalFilters();
            }, 250);
        });
    }

    if (modalStatusFilter) {
        modalStatusFilter.addEventListener('change', function () {
            applyModalFilters();
        });
    }

    // Expose openStudentModal globally
    window.emOpenStudentModal = openStudentModal;

    function buildStatusOptions(selectedStatus) {
        var selected = selectedStatus || '';
        var options = '';
        for (var i = 0; i < studentStatuses.length; i++) {
            var st = studentStatuses[i];
            var isSelected = st === selected ? ' selected' : '';
            options += '<option value="' + escapeHtml(st) + '"' + isSelected + '>' + escapeHtml(st) + '</option>';
        }
        return options;
    }

    function updateStudentStatus(studentId, nextStatus, selectEl) {
        if (!currentEventId || !studentId || !selectEl) return;

        var prevStatus = selectEl.getAttribute('data-current-status') || '';
        if (prevStatus === nextStatus) return;

        selectEl.disabled = true;
        selectEl.classList.add('is-updating');

        var token = document.querySelector('input[name="__RequestVerificationToken"]');
        var body = 'maEvent=' + encodeURIComponent(currentEventId) +
            '&idSinhVien=' + encodeURIComponent(studentId) +
            '&trangThai=' + encodeURIComponent(nextStatus) +
            (token ? '&__RequestVerificationToken=' + encodeURIComponent(token.value) : '');

        fetch(EM_CONFIG.updateStudentStatusUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: body
        })
            .then(function (res) { return res.json(); })
            .then(function (data) {
                if (!data || !data.success) {
                    selectEl.value = prevStatus;
                    return;
                }
                var finalStatus = data.status || nextStatus;
                selectEl.value = finalStatus;
                selectEl.setAttribute('data-current-status', finalStatus);

                for (var i = 0; i < allStudents.length; i++) {
                    if ((allStudents[i].idSinhVien || allStudents[i].mssv) === studentId) {
                        allStudents[i].trangThai = finalStatus;
                        break;
                    }
                }
                syncModalExcelUrl();
            })
            .catch(function () {
                selectEl.value = prevStatus;
            })
            .finally(function () {
                selectEl.disabled = false;
                selectEl.classList.remove('is-updating');
            });
    }

    if (modalBody) {
        modalBody.addEventListener('change', function (e) {
            var selectEl = e.target.closest('.em-status-select');
            if (!selectEl) return;
            var studentId = selectEl.getAttribute('data-student-id');
            updateStudentStatus(studentId, selectEl.value, selectEl);
        });
    }

    function applyModalFilters() {
        var q = modalSearchInput ? modalSearchInput.value.trim().toLowerCase() : '';
        var status = modalStatusFilter ? modalStatusFilter.value : '';

        var filtered = allStudents.filter(function (s) {
            var matchSearch = !q ||
                (s.hoTen && s.hoTen.toLowerCase().indexOf(q) >= 0) ||
                (s.mssv && s.mssv.toLowerCase().indexOf(q) >= 0);
            var matchStatus = !status || s.trangThai === status;
            return matchSearch && matchStatus;
        });

        renderStudents(filtered);
        if (modalCount) {
            modalCount.textContent = filtered.length + ' / ' + allStudents.length + ' sinh viên';
        }
        syncModalExcelUrl();
    }

    function syncModalExcelUrl() {
        if (!modalExcelBtn || !currentEventId) return;
        var url = EM_CONFIG.exportExcelUrl + '?id=' + encodeURIComponent(currentEventId);
        var q = modalSearchInput ? modalSearchInput.value.trim() : '';
        var status = modalStatusFilter ? modalStatusFilter.value : '';
        if (q) url += '&search=' + encodeURIComponent(q);
        if (status) url += '&status=' + encodeURIComponent(status);
        modalExcelBtn.href = url;
    }

    /* ══════════════════════════════════════════════════════════
       3. CLOSE EVENT (AJAX + CONFIRM MODAL)
       ══════════════════════════════════════════════════════════ */
    var closeModalOverlay = document.getElementById('emCloseModal');
    var closeModalEventName = document.getElementById('emCloseModalEventName');
    var closeModalTitle = document.getElementById('emCloseModalTitle');
    var closeModalDescAction = document.getElementById('emCloseModalDescAction');
    var closeCancelBtn = document.getElementById('emCloseCancelBtn');
    var closeConfirmBtn = document.getElementById('emCloseConfirmBtn');
    var pendingCloseEvent = null;

    function closeEventRequest(eventId, btn, action) {
        if (btn) {
            btn.disabled = true;
            btn.style.opacity = '.5';
        }

        var token = document.querySelector('input[name="__RequestVerificationToken"]');
        var targetUrl = action === 'reopen' ? EM_CONFIG.reopenEventUrl : EM_CONFIG.closeEventUrl;

        fetch(targetUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: 'id=' + eventId + (token ? '&__RequestVerificationToken=' + token.value : '')
        })
            .then(function (res) { return res.json(); })
            .then(function (data) {
                if (data.success) {
                    setTimeout(function () { location.reload(); }, 800);
                } else {
                    if (btn) { btn.disabled = false; btn.style.opacity = '1'; }
                }
            })
            .catch(function () {
                if (btn) { btn.disabled = false; btn.style.opacity = '1'; }
            });
    }

    window.emCloseEvent = function (eventId, btn) {
        closeEventRequest(eventId, btn, 'close');
    };

    function closeCloseModal() {
        if (!closeModalOverlay) return;
        closeModalOverlay.classList.remove('open');
        closeModalOverlay.setAttribute('aria-hidden', 'true');
        document.body.style.overflow = '';
        pendingCloseEvent = null;
    }

    function openCloseModal(eventId, eventName, btn, action) {
        if (!closeModalOverlay) {
            closeEventRequest(eventId, btn, action);
            return;
        }
        pendingCloseEvent = { id: eventId, name: eventName, btn: btn, action: action };
        if (closeModalEventName) {
            closeModalEventName.textContent = '"' + (eventName || 'không xác định') + '"';
        }
        if (action === 'reopen') {
            if (closeModalTitle) closeModalTitle.textContent = 'Bạn có chắc muốn mở lại sự kiện?';
            if (closeModalDescAction) closeModalDescAction.innerHTML = 'sẽ hiển thị lại trên hệ thống và tự động cập nhật trạng thái <strong>Sắp diễn ra</strong> hoặc <strong>Đang diễn ra</strong> theo thời gian đăng ký hiện tại.';
            if (closeCancelBtn) closeCancelBtn.textContent = 'Không mở lại';
            if (closeConfirmBtn) closeConfirmBtn.textContent = 'Xác nhận mở lại';
        } else {
            if (closeModalTitle) closeModalTitle.textContent = 'Bạn có chắc muốn đóng sự kiện?';
            if (closeModalDescAction) closeModalDescAction.innerHTML = 'sẽ được cập nhật trạng thái <strong>Đã hủy</strong> và tự động ẩn khỏi hệ thống.';
            if (closeCancelBtn) closeCancelBtn.textContent = 'Không đóng nữa';
            if (closeConfirmBtn) closeConfirmBtn.textContent = 'Xác nhận đóng';
        }
        closeModalOverlay.classList.add('open');
        closeModalOverlay.setAttribute('aria-hidden', 'false');
        document.body.style.overflow = 'hidden';
    }

    window.emOpenCloseModal = function (eventId, eventName, btn) {
        openCloseModal(eventId, eventName, btn, 'close');
    };
    window.emOpenReopenModal = function (eventId, eventName, btn) {
        openCloseModal(eventId, eventName, btn, 'reopen');
    };

    if (closeCancelBtn) {
        closeCancelBtn.addEventListener('click', closeCloseModal);
    }
    if (closeConfirmBtn) {
        closeConfirmBtn.addEventListener('click', function () {
            if (!pendingCloseEvent) return;
            var payload = pendingCloseEvent;
            closeCloseModal();
            closeEventRequest(payload.id, payload.btn, payload.action);
        });
    }
    if (closeModalOverlay) {
        closeModalOverlay.addEventListener('click', function (e) {
            if (e.target === closeModalOverlay) closeCloseModal();
        });
    }

    /* ══════════════════════════════════════════════════════════
       4. DELETE EVENT MODAL
       ══════════════════════════════════════════════════════════ */
    var deleteModalOverlay = document.getElementById('emDeleteModal');
    var deleteModalEventName = document.getElementById('emDeleteModalEventName');
    var deleteCancelBtn = document.getElementById('emDeleteCancelBtn');
    var deleteConfirmBtn = document.getElementById('emDeleteConfirmBtn');
    var pendingDeleteForm = null;

    function openDeleteModal(form) {
        if (!deleteModalOverlay || !form) return;
        pendingDeleteForm = form;

        if (deleteModalEventName) {
            var eventName = form.getAttribute('data-event-name') || 'không xác định';
            deleteModalEventName.textContent = '"' + eventName + '"';
        }

        deleteModalOverlay.classList.add('open');
        deleteModalOverlay.setAttribute('aria-hidden', 'false');
        document.body.style.overflow = 'hidden';
    }

    function closeDeleteModal() {
        if (!deleteModalOverlay) return;
        deleteModalOverlay.classList.remove('open');
        deleteModalOverlay.setAttribute('aria-hidden', 'true');
        document.body.style.overflow = '';
        pendingDeleteForm = null;
    }

    function initDeleteModal() {
        if (!deleteModalOverlay) return;

        var deleteForms = document.querySelectorAll('.em-delete-form');
        deleteForms.forEach(function (form) {
            form.addEventListener('submit', function (e) {
                e.preventDefault();
                openDeleteModal(form);
            });
        });

        if (deleteCancelBtn) {
            deleteCancelBtn.addEventListener('click', closeDeleteModal);
        }

        if (deleteConfirmBtn) {
            deleteConfirmBtn.addEventListener('click', function () {
                if (pendingDeleteForm) {
                    pendingDeleteForm.submit();
                }
            });
        }

        deleteModalOverlay.addEventListener('click', function (e) {
            if (e.target === deleteModalOverlay) closeDeleteModal();
        });
    }

    initDeleteModal();

    /* ══════════════════════════════════════════════════════════
       5. TOGGLE HIDDEN (reuse)
       ══════════════════════════════════════════════════════════ */
    window.emToggleHidden = function (id, btn) {
        if (btn) { btn.classList.add('toggling'); btn.disabled = true; }
        var token = document.querySelector('input[name="__RequestVerificationToken"]');

        fetch(EM_CONFIG.toggleHiddenUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: 'id=' + id + (token ? '&__RequestVerificationToken=' + token.value : '')
        })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (data.success) {
                    var ico = btn.querySelector('.material-symbols-outlined');
                    if (ico) ico.textContent = data.isHidden ? 'visibility_off' : 'visibility';
                    btn.title = data.isHidden ? 'Đang ẩn — Nhấn để hiện' : 'Đang hiện — Nhấn để ẩn';
                }
            })
            .finally(function () {
                if (btn) { btn.classList.remove('toggling'); btn.disabled = false; }
            });
    };

    /* ══════════════════════════════════════════════════════════
       UTIL
       ══════════════════════════════════════════════════════════ */
    function escapeHtml(str) {
        var div = document.createElement('div');
        div.appendChild(document.createTextNode(str));
        return div.innerHTML;
    }

})();
