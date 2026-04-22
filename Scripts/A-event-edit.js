'use strict';

let detailEditor;
ClassicEditor.create(document.querySelector('#richContent'))
    .then(function (editor) { detailEditor = editor; })
    .catch(function (e) { console.warn(e); });

const input = document.getElementById('coverImageInput');
const zone = document.getElementById('uploadZone');
const wrap = document.getElementById('previewWrap');
const img = document.getElementById('previewImg');
const rem = document.getElementById('previewRemove');

input?.addEventListener('change', function (e) {
    const f = e.target.files[0];
    if (!f) return;
    const r = new FileReader();
    r.onload = function (ev) {
        img.src = ev.target.result;
        wrap.style.display = 'block';
        zone.style.display = 'none';
    };
    r.readAsDataURL(f);
});

rem?.addEventListener('click', function () {
    input.value = '';
    img.src = '';
    wrap.style.display = 'none';
    zone.style.display = 'block';
});

const chk = document.getElementById('chkHidden');
const track = document.getElementById('switchTrack');
const thumb = document.getElementById('switchThumb');
function toggleSwitch() {
    chk.checked = !chk.checked;
    track.style.background = chk.checked ? 'var(--primary)' : 'var(--border)';
    thumb.style.transform = chk.checked ? 'translateX(18px)' : 'translateX(0)';
}
window.toggleSwitch = toggleSwitch;
track.style.background = chk.checked ? 'var(--primary)' : 'var(--border)';
thumb.style.transform = chk.checked ? 'translateX(18px)' : 'translateX(0)';

const cancelBtn = document.querySelector('.js-cancel-event');
if (cancelBtn) {
    cancelBtn.addEventListener('click', function (e) {
        e.preventDefault();
        const ok = window.confirm('Bạn có chắc muốn hủy?\nCác thay đổi chưa lưu sẽ bị mất.');
        if (ok) window.location.href = cancelBtn.getAttribute('href');
    });
}

const editForm = document.getElementById('editForm');
if (editForm) {
    editForm.addEventListener('submit', function () {
        if (detailEditor) {
            document.querySelector('#richContent').value = detailEditor.getData();
        }
    });
}
