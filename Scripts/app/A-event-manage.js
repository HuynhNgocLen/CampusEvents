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
       6. QR ĐIỂM DANH (Manage — token 1 phút · tần suất tự làm mới · toàn màn hình)
       ══════════════════════════════════════════════════════════ */
    var qrModalEl = document.getElementById('emQrDiemDanhModal');
    var qrModalBody = document.getElementById('emQrDiemDanhModalBody');
    var qrModalTitleLabel = document.getElementById('emQrDiemDanhModalLabel');
    var qrBsModal = null;
    if (qrModalEl && typeof bootstrap !== 'undefined' && bootstrap.Modal) {
        qrBsModal = new bootstrap.Modal(qrModalEl);
    }

    var qrExpiryTimeoutId = null;
    var qrRefreshIntervalId = null;
    var qrCountdownIntervalId = null;
    var qrLastExpiresAtIso = null;
    var qrLastUiPollAt = 0;
    var qrFsEscapeHandler = null;

    function clearQrTimers() {
        if (qrExpiryTimeoutId) {
            clearTimeout(qrExpiryTimeoutId);
            qrExpiryTimeoutId = null;
        }
        if (qrRefreshIntervalId) {
            clearInterval(qrRefreshIntervalId);
            qrRefreshIntervalId = null;
        }
        if (qrCountdownIntervalId) {
            clearInterval(qrCountdownIntervalId);
            qrCountdownIntervalId = null;
        }
    }

    function exitQrFullscreenIfOpen() {
        var el = document.fullscreenElement || document.webkitFullscreenElement;
        if (!el) return;
        var exit = document.exitFullscreen || document.webkitExitFullscreen || document.mozCancelFullScreen || document.msExitFullscreen;
        if (exit) {
            try {
                exit.call(document);
            } catch (e) { /* ignore */ }
        }
    }

    if (qrModalEl) {
        qrModalEl.addEventListener('hidden.bs.modal', function () {
            clearQrTimers();
            exitQrFullscreenIfOpen();
        });
    }

    document.addEventListener('fullscreenchange', onQrFullscreenChange);
    document.addEventListener('webkitfullscreenchange', onQrFullscreenChange);

    function onQrFullscreenChange() {
        var host = document.getElementById('emQrFsGlobal');
        var active = document.fullscreenElement || document.webkitFullscreenElement;
        if (!active && qrFsEscapeHandler) {
            document.removeEventListener('keydown', qrFsEscapeHandler);
            qrFsEscapeHandler = null;
            if (host) host.innerHTML = '';
        }
    }

    function getQrRefreshIntervalSec() {
        var sel = document.getElementById('emQrRefreshInterval');
        if (sel) return parseInt(sel.value, 10) || 0;
        var s = parseInt(sessionStorage.getItem('emQrRefreshSec') || '30', 10);
        if ([0, 30, 60, 90, 120].indexOf(s) < 0) return 30;
        return s;
    }

    function setQrModalLoading() {
        if (!qrModalBody) return;
        qrModalBody.innerHTML =
            '<div class="em-qr-modal__loading">' +
            '<span class="material-symbols-outlined em-spin">progress_activity</span>' +
            '<span>Đang tạo mã...</span></div>';
    }

    function downloadDataUrl(dataUrl, fileName) {
        if (!dataUrl) return;
        var a = document.createElement('a');
        a.href = dataUrl;
        a.download = fileName || 'DiemDanh-QR.png';
        a.rel = 'noopener';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
    }

    function wireQrDownloadBtn(dataUrl, fileName) {
        var dlBtn = document.getElementById('emQrDownloadBtn');
        if (!dlBtn || !dataUrl) return;
        dlBtn.onclick = function () {
            downloadDataUrl(dataUrl, fileName);
        };
    }

    function wireQrCopyBtn() {
        var copyBtn = document.getElementById('emQrCopyBtn');
        var fld = document.getElementById('emQrUrlField');
        if (!copyBtn || !fld) return;
        copyBtn.onclick = function () {
            fld.select();
            fld.setSelectionRange(0, 99999);
            try {
                navigator.clipboard.writeText(fld.value);
                copyBtn.innerHTML =
                    '<span class="material-symbols-outlined" style="font-size:18px">check</span>';
                copyBtn.title = 'Đã sao chép';
            } catch (e1) {
                try {
                    document.execCommand('copy');
                    copyBtn.innerHTML =
                        '<span class="material-symbols-outlined" style="font-size:18px">check</span>';
                    copyBtn.title = 'Đã sao chép';
                } catch (e2) { /* ignore */ }
            }
        };
    }

    function scheduleQrPoll(eventId, eventName, expiresAtIso) {
        clearQrTimers();
        qrLastExpiresAtIso = expiresAtIso || null;
        if (!expiresAtIso) return;

        var expMs = new Date(expiresAtIso).getTime();
        if (isNaN(expMs)) return;

        function tickQrClocks() {
            var sec = Math.max(0, Math.floor((expMs - Date.now()) / 1000));
            var m = Math.floor(sec / 60);
            var s = sec % 60;
            var timeStr = (m < 10 ? '0' : '') + m + ':' + (s < 10 ? '0' : '') + s;

            var el = document.getElementById('emQrCountdown');
            if (el) el.textContent = timeStr;
            var fsTokenEl = document.getElementById('emQrFsCountdown');
            if (fsTokenEl) fsTokenEl.textContent = timeStr;

            var ir = getQrRefreshIntervalSec();
            var remPoll = 0;
            if (ir > 0) {
                var nextUi = qrLastUiPollAt + ir * 1000;
                remPoll = Math.max(0, Math.ceil((nextUi - Date.now()) / 1000));
            }

            var pollEl = document.getElementById('emQrUiPollCountdown');
            var pollInline = document.getElementById('emQrUiPollInlineGroup');
            if (pollEl && pollInline) {
                if (ir <= 0) {
                    pollInline.style.display = 'none';
                } else {
                    pollInline.style.display = '';
                    pollEl.textContent = remPoll + 's';
                }
            }

            var fsPollEl = document.getElementById('emQrFsUiPollCountdown');
            var fsPollInline = document.getElementById('emQrFsPollInlineGroup');
            if (fsPollEl && fsPollInline) {
                if (ir <= 0) {
                    fsPollInline.style.display = 'none';
                } else {
                    fsPollInline.style.display = '';
                    fsPollEl.textContent = remPoll + 's';
                }
            }
        }

        tickQrClocks();
        qrCountdownIntervalId = setInterval(tickQrClocks, 1000);

        var intervalSec = getQrRefreshIntervalSec();
        if (intervalSec > 0) {
            qrRefreshIntervalId = setInterval(function () {
                loadQrPayload(eventId, eventName, true);
            }, intervalSec * 1000);
        }

        var msUntilExpiryRefresh = expMs - Date.now() - 2500;
        if (msUntilExpiryRefresh < 800) msUntilExpiryRefresh = 800;
        qrExpiryTimeoutId = setTimeout(function () {
            loadQrPayload(eventId, eventName, true);
        }, msUntilExpiryRefresh);
    }

    function wireQrRefreshToolbar(eventId, eventName) {
        var sel = document.getElementById('emQrRefreshInterval');
        if (!sel) return;
        sel.onchange = function () {
            sessionStorage.setItem('emQrRefreshSec', sel.value);
            qrLastUiPollAt = Date.now();
            clearQrTimers();
            scheduleQrPoll(eventId, eventName, qrLastExpiresAtIso);
        };
    }

    function exitQrFullscreen() {
        var ex = document.exitFullscreen || document.webkitExitFullscreen || document.mozCancelFullScreen || document.msExitFullscreen;
        if (ex) {
            try {
                ex.call(document);
            } catch (e) { /* ignore */ }
        }
    }

    function wireQrFullscreenEnter(qrSrc, titlePlain) {
        var btn = document.getElementById('emQrFsEnterBtn');
        var host = document.getElementById('emQrFsGlobal');
        if (!btn || !host || !qrSrc) return;

        btn.onclick = function () {
            if (qrFsEscapeHandler) {
                document.removeEventListener('keydown', qrFsEscapeHandler);
                qrFsEscapeHandler = null;
            }

            var titleEsc = escapeHtml(String(titlePlain || 'Điểm danh'));
            host.innerHTML =
                '<div class="em-qr-fs-global__inner">' +
                '<p class="em-qr-fs-global__title">' + titleEsc + '</p>' +
                '<div class="em-qr-fs-global__timers em-qr-fs-global__timers--unified" aria-live="polite">' +
                '<span class="em-qr-fs-global__timer em-qr-fs-global__timer--unified">' +
                '<span class="em-qr-fs-global__timer-part">Chữ ký QR còn <strong id="emQrFsCountdown" class="em-qr-fs-global__time">--:--</strong></span>' +
                '<span class="em-qr-fs-global__timer-sep" id="emQrFsPollInlineGroup">' +
                '<span class="em-qr-fs-global__timer-dot"> · </span>' +
                '<span class="em-qr-fs-global__timer-part">Làm mới ảnh sau <strong id="emQrFsUiPollCountdown" class="em-qr-fs-global__time em-qr-fs-global__time--poll">—</strong></span>' +
                '</span></span></div>' +
                '<img class="em-qr-fs-global__qr" src="' + qrSrc + '" alt="QR điểm danh"/>' +
                '<p class="em-qr-fs-global__sub">Nhấn <kbd>ESC</kbd> hoặc nút bên dưới để thoát</p>' +
                '<button type="button" class="ce-btn ce-btn-primary em-qr-fs-global__exit" id="emQrFsExitBtn">Thoát toàn màn hình</button>' +
                '</div>';

            setTimeout(function () {
                var srcT = document.getElementById('emQrCountdown');
                var dstT = document.getElementById('emQrFsCountdown');
                if (srcT && dstT) dstT.textContent = srcT.textContent;
                var srcP = document.getElementById('emQrUiPollCountdown');
                var dstP = document.getElementById('emQrFsUiPollCountdown');
                var fsInline = document.getElementById('emQrFsPollInlineGroup');
                var modalInline = document.getElementById('emQrUiPollInlineGroup');
                if (dstP && fsInline && modalInline) {
                    fsInline.style.display = modalInline.style.display || '';
                    if (fsInline.style.display !== 'none' && srcP) dstP.textContent = srcP.textContent;
                }
            }, 0);

            var exitBtn = document.getElementById('emQrFsExitBtn');
            if (exitBtn) exitBtn.onclick = exitQrFullscreen;

            qrFsEscapeHandler = function (e) {
                if (e.key === 'Escape') exitQrFullscreen();
            };
            document.addEventListener('keydown', qrFsEscapeHandler);

            var req = host.requestFullscreen || host.webkitRequestFullscreen || host.mozRequestFullScreen || host.msRequestFullscreen;
            if (req) {
                try {
                    req.call(host);
                } catch (e2) { /* ignore */ }
            }
        };
    }

    function renderQrModalContent(data, eventId, eventName, silent) {
        if (!qrModalBody) return;

        if (!silent) {
            exitQrFullscreenIfOpen();
            var fsHostClean = document.getElementById('emQrFsGlobal');
            if (fsHostClean && !(document.fullscreenElement || document.webkitFullscreenElement)) {
                fsHostClean.innerHTML = '';
            }
        }

        var titleDisplay = escapeHtml(String(data.eventTitle || eventName || ('Sự kiện #' + eventId)));

        var savedInt = parseInt(sessionStorage.getItem('emQrRefreshSec') || '30', 10);
        if ([0, 30, 60, 90, 120].indexOf(savedInt) < 0) savedInt = 30;

        var opt = function (v, t) {
            return '<option value="' + v + '"' + (savedInt === v ? ' selected' : '') + '>' + t + '</option>';
        };

        var toolbar =
            '<div class="em-qr-modal__toolbar">' +
            '<label class="em-qr-modal__toolbar-label" for="emQrRefreshInterval">Tự làm mới ảnh / liên kết (trình duyệt)</label>' +
            '<div class="em-qr-modal__toolbar-row">' +
            '<select id="emQrRefreshInterval" class="form-select form-select-sm em-qr-modal__toolbar-select">' +
            opt(0, 'Không tự làm mới (chỉ trước khi hết hạn)') +
            opt(30, '30 giây') +
            opt(60, '60 giây') +
            opt(90, '90 giây') +
            opt(120, '120 giây') +
            '</select>' +
            (data.qrSrc
                ? '<button type="button" class="ce-btn ce-btn-outline ce-btn-sm em-qr-modal__fs-btn" id="emQrFsEnterBtn">' +
                  '<span class="material-symbols-outlined" style="font-size:18px">open_in_full</span>Toàn màn hình</button>'
                : '') +
            '</div></div>';

        var hint =
            '<div class="em-qr-modal__hint">' +
            '<span class="material-symbols-outlined">lock_clock</span>' +
            '<span><strong>Chữ ký trên liên kết QR</strong> đổi theo chu kỳ <strong>1 phút</strong> trên máy chủ. ' +
            'Menu <strong>Tự làm mới ảnh</strong> là tần suất <em>tải lại ảnh trên trình duyệt</em> — hai mốc được gộp trên một dòng bên dưới.</span></div>';

        var countdown =
            '<div class="em-qr-modal__countdown-row">' +
            '<div class="em-qr-modal__countdown-wrap em-qr-modal__countdown-wrap--unified" title="Chữ ký URL (server) và lần tải lại ảnh tiếp theo (trình duyệt)">' +
            '<span class="material-symbols-outlined em-qr-modal__countdown-ico">schedule</span>' +
            '<span class="em-qr-modal__countdown-inline">' +
            '<span class="em-qr-modal__countdown-part">Chữ ký QR còn <span class="em-qr-modal__countdown" id="emQrCountdown">--:--</span></span>' +
            '<span class="em-qr-modal__countdown-inline-group" id="emQrUiPollInlineGroup">' +
            '<span class="em-qr-modal__countdown-sep" aria-hidden="true">·</span>' +
            '<span class="em-qr-modal__countdown-part">Làm mới ảnh sau <span class="em-qr-modal__countdown em-qr-modal__countdown--poll" id="emQrUiPollCountdown">—</span></span>' +
            '</span></span></div></div>';

        var qrBox = '';
        if (data.qrSrc) {
            var dlName = data.downloadFileName || ('Điểm Danh - (SuKien)-' + eventId + '.png');
            qrBox =
                '<div class="em-qr-modal__qr-wrap">' +
                '<img src="' + data.qrSrc + '" alt="QR điểm danh" class="em-qr-modal__qr-img"/>' +
                '<button type="button" class="ce-btn ce-btn-outline ce-btn-sm em-qr-modal__dl-btn" id="emQrDownloadBtn">' +
                '<span class="material-symbols-outlined" style="font-size:18px">download</span>Tải ảnh QR</button>' +
                '<p class="em-qr-modal__filename-hint">Tên file: <code>' + escapeHtml(dlName) + '</code></p>' +
                '</div>';
        } else {
            qrBox =
                '<div class="em-qr-modal__qr-wrap em-qr-modal__qr-wrap--empty">' +
                '<p class="mb-0 small" style="color:var(--warning,#d97706)">Không tạo được ảnh QR. Dùng liên kết bên dưới.</p>' +
                '</div>';
        }

        var linkBlock = '';
        if (data.checkInUrl) {
            linkBlock =
                '<div class="em-qr-modal__link-block">' +
                '<label class="em-qr-modal__link-label">Liên kết điểm danh</label>' +
                '<div class="em-qr-modal__link-row">' +
                '<input type="text" readonly class="form-control em-qr-modal__link-input" id="emQrUrlField" value="' +
                escapeHtml(data.checkInUrl) +
                '"/>' +
                '<button type="button" class="ce-btn ce-btn-outline ce-btn-sm em-qr-modal__copy-btn" id="emQrCopyBtn" title="Sao chép">' +
                '<span class="material-symbols-outlined" style="font-size:18px">content_copy</span>' +
                '</button></div></div>';
        }

        qrModalBody.innerHTML =
            '<div class="em-qr-modal__body">' +
            '<div class="em-qr-modal__event-title">' + titleDisplay + '</div>' +
            hint +
            toolbar +
            countdown +
            qrBox +
            linkBlock +
            '<p class="em-qr-modal__foot-note">' +
            '<span class="material-symbols-outlined" style="font-size:15px;vertical-align:text-bottom">autorenew</span> ' +
            'Dòng thời gian gộp <strong>chữ ký</strong> (server, ~1 phút) và <strong>làm mới ảnh</strong> (tuỳ chọn). Dùng <strong>Toàn màn hình</strong> khi chiếu cho sinh viên.</p>' +
            '</div>';

        if (data.qrSrc) {
            var saveName = data.downloadFileName || ('Điểm Danh - (SuKien)-' + eventId + '.png');
            wireQrDownloadBtn(data.qrSrc, saveName);
            wireQrFullscreenEnter(data.qrSrc, data.eventTitle || eventName || ('Sự kiện #' + eventId));
        }
        wireQrCopyBtn();
        wireQrRefreshToolbar(eventId, eventName);
        qrLastUiPollAt = Date.now();
        scheduleQrPoll(eventId, eventName, data.expiresAtUtc);

        if (silent && data.qrSrc) {
            var fsImg = document.querySelector('#emQrFsGlobal .em-qr-fs-global__qr');
            if (fsImg) fsImg.src = data.qrSrc;
        }
    }

    function loadQrPayload(eventId, eventName, silent) {
        if (!qrModalBody || !EM_CONFIG.getAttendanceQrUrl) return;

        if (!silent) {
            setQrModalLoading();
        } else {
            var bodyEl = qrModalBody.querySelector('.em-qr-modal__body');
            if (bodyEl) bodyEl.classList.add('em-qr-modal__body--refreshing');
        }

        var url = EM_CONFIG.getAttendanceQrUrl + '?id=' + encodeURIComponent(eventId);
        fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' }, cache: 'no-store' })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                var refEl = qrModalBody.querySelector('.em-qr-modal__body--refreshing');
                if (refEl) refEl.classList.remove('em-qr-modal__body--refreshing');

                if (!data || !data.success) {
                    if (!silent) {
                        qrModalBody.innerHTML =
                            '<div class="em-qr-modal__body"><p class="text-danger mb-0" style="font-size:.9rem">' +
                            escapeHtml(data && data.message ? data.message : 'Không tải được mã QR.') +
                            '</p></div>';
                    }
                    return;
                }
                renderQrModalContent(data, eventId, eventName, silent);
            })
            .catch(function () {
                var r2 = qrModalBody.querySelector('.em-qr-modal__body--refreshing');
                if (r2) r2.classList.remove('em-qr-modal__body--refreshing');
                if (!silent && qrModalBody) {
                    qrModalBody.innerHTML =
                        '<div class="em-qr-modal__body"><p class="text-danger mb-0" style="font-size:.9rem">Lỗi mạng khi tải mã QR.</p></div>';
                }
            });
    }

    function openQrModal(eventId, eventName) {
        if (!qrModalEl || !qrModalBody || !EM_CONFIG.getAttendanceQrUrl) return;

        clearQrTimers();

        if (qrModalTitleLabel) {
            qrModalTitleLabel.innerHTML =
                '<span class="material-symbols-outlined em-qr-modal__head-icon">qr_code_scanner</span>' +
                '<span>Điểm danh QR</span>';
        }

        setQrModalLoading();
        if (qrBsModal) {
            qrBsModal.show();
        } else {
            qrModalEl.classList.add('show');
            qrModalEl.style.display = 'block';
        }

        loadQrPayload(eventId, eventName, false);
    }

    window.emOpenQrModal = openQrModal;

    /* ══════════════════════════════════════════════════════════
       UTIL
       ══════════════════════════════════════════════════════════ */
    function escapeHtml(str) {
        var div = document.createElement('div');
        div.appendChild(document.createTextNode(str));
        return div.innerHTML;
    }

})();
