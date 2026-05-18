'use strict';

(function () {
    function sanitizeSegment(s) {
        if (!s) return '';
        return String(s).replace(/\|/g, '\uFF5C').trim();
    }

    function parseChiTiet(text) {
        if (!text || !String(text).trim()) {
            return [{ time: '', title: '', note: '' }];
        }
        var rows = String(text)
            .split(/\r\n|\n|\r/)
            .map(function (l) { return l.trim(); })
            .filter(Boolean);
        if (!rows.length) {
            return [{ time: '', title: '', note: '' }];
        }
        return rows.map(function (line) {
            var parts = line.split('|').map(function (p) { return p.trim(); });
            if (parts.length >= 2) {
                return {
                    time: parts[0],
                    title: parts[1],
                    note: parts.slice(2).join(' | ')
                };
            }
            return { time: '', title: line, note: '' };
        });
    }

    function serializeChiTiet(rowEls) {
        var lines = [];
        for (var i = 0; i < rowEls.length; i++) {
            var row = rowEls[i];
            var time = sanitizeSegment(row.querySelector('[data-tl="time"]').value);
            var title = sanitizeSegment(row.querySelector('[data-tl="title"]').value);
            var note = sanitizeSegment(row.querySelector('[data-tl="note"]').value);
            if (!time && !title && !note) continue;
            lines.push(time + ' | ' + title + ' | ' + note);
        }
        return lines.length ? lines.join('\n') : '';
    }

    function syncEventTimelineToHidden() {
        var hidden = document.getElementById('ChiTiet');
        var root = document.getElementById('timelineEditorRoot');
        if (!hidden || !root) return;
        var rows = root.querySelectorAll('.timeline-editor-row');
        hidden.value = serializeChiTiet(rows);
    }
    window.syncEventTimelineToHidden = syncEventTimelineToHidden;

    function removeRow(btn) {
        var wrap = document.getElementById('timelineRows');
        if (!wrap || wrap.querySelectorAll('.timeline-editor-row').length <= 1) return;
        var row = btn.closest('.timeline-editor-row');
        if (row) row.remove();
    }

    function addRow(wrap, inputClass, data) {
        data = data || { time: '', title: '', note: '' };
        var row = document.createElement('div');
        row.className = 'timeline-editor-row';
        row.innerHTML =
            '<div class="timeline-editor-field">' +
            '<label class="timeline-editor-label">Giờ</label>' +
            '<input type="text" class="' + inputClass + '" data-tl="time" value="" placeholder="VD: 09:00 hoặc 9h - 10h30" maxlength="80" />' +
            '</div>' +
            '<div class="timeline-editor-field">' +
            '<label class="timeline-editor-label">Tiêu đề</label>' +
            '<input type="text" class="' + inputClass + '" data-tl="title" value="" placeholder="VD: Khai mạc" maxlength="300" />' +
            '</div>' +
            '<div class="timeline-editor-field">' +
            '<label class="timeline-editor-label">Ghi chú</label>' +
            '<input type="text" class="' + inputClass + '" data-tl="note" value="" placeholder="VD: Hội trường A — Diễn giả" maxlength="400" />' +
            '</div>' +
            '<div class="timeline-editor-actions">' +
            '<button type="button" class="timeline-editor-remove" title="Xóa mốc" aria-label="Xóa mốc">' +
            '<span class="material-symbols-outlined">close</span>' +
            '</button>' +
            '</div>';
        row.querySelector('[data-tl="time"]').value = data.time || '';
        row.querySelector('[data-tl="title"]').value = data.title || '';
        row.querySelector('[data-tl="note"]').value = data.note || '';
        row.querySelector('.timeline-editor-remove').addEventListener('click', function () {
            removeRow(this);
        });
        wrap.appendChild(row);
    }

    function init() {
        var root = document.getElementById('timelineEditorRoot');
        var hidden = document.getElementById('ChiTiet');
        if (!root || !hidden) return;

        var wrap = document.getElementById('timelineRows');
        var inputClass = root.getAttribute('data-input-class') || 'ce-form-control';
        var items = parseChiTiet(hidden.value);
        wrap.innerHTML = '';
        for (var i = 0; i < items.length; i++) {
            addRow(wrap, inputClass, items[i]);
        }

        var addBtn = document.getElementById('timelineAddRow');
        if (addBtn) {
            addBtn.addEventListener('click', function () {
                addRow(wrap, inputClass, { time: '', title: '', note: '' });
            });
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
