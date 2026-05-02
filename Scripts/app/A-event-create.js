'use strict';

let detailEditor;
ClassicEditor.create(document.querySelector('#richContent'), {
    toolbar: ['heading', '|', 'bold', 'italic', 'underline', '|', 'bulletedList', 'numberedList', '|', 'link', 'blockQuote', '|', 'undo', 'redo']
}).then(function (editor) {
    detailEditor = editor;
}).catch(function (err) {
    console.warn('CKEditor:', err);
});

const input = document.getElementById('coverImageInput');
const zone = document.getElementById('uploadZone');
const previewWrap = document.getElementById('previewWrap');
const previewImg = document.getElementById('previewImg');
const removeBtn = document.getElementById('previewRemove');
const qualityInfo = document.getElementById('uploadQuality');

function showPreview(file) {
    const reader = new FileReader();
    reader.onload = function (e) {
        previewImg.src = e.target.result;
        zone.style.display = 'none';
        previewWrap.style.display = 'block';
        qualityInfo.style.display = 'flex';
    };
    reader.readAsDataURL(file);
}

input.addEventListener('change', function (e) { if (e.target.files[0]) showPreview(e.target.files[0]); });
removeBtn.addEventListener('click', function () {
    input.value = '';
    previewWrap.style.display = 'none';
    qualityInfo.style.display = 'none';
    zone.style.display = 'block';
    previewImg.src = '';
});

zone.addEventListener('dragover', function (e) { e.preventDefault(); zone.classList.add('drag-over'); });
zone.addEventListener('dragleave', function () { zone.classList.remove('drag-over'); });
zone.addEventListener('drop', function (e) {
    e.preventDefault();
    zone.classList.remove('drag-over');
    const f = e.dataTransfer.files[0];
    if (f && (f.type === 'image/jpeg' || f.type === 'image/png')) {
        const dt = new DataTransfer();
        dt.items.add(f);
        input.files = dt.files;
        showPreview(f);
    }
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

const createForm = document.getElementById('createForm');
if (createForm) {
    createForm.addEventListener('submit', function (e) {
        const ngayBatDauInput = document.getElementById('ngayBatDauInput');
        if (ngayBatDauInput && ngayBatDauInput.value) {
            const selectedDate = new Date(ngayBatDauInput.value + 'T00:00:00');
            const minDate = new Date();
            minDate.setHours(0, 0, 0, 0);
            minDate.setDate(minDate.getDate() - 20);

            if (selectedDate < minDate) {
                e.preventDefault();
                window.alert('Ngày bắt đầu không được nhỏ hơn ngày hiện tại quá 20 ngày.');
                ngayBatDauInput.focus();
                return;
            }
        }

        if (detailEditor) {
            document.querySelector('#richContent').value = detailEditor.getData();
        }
    });
}

const cancelBtn = document.querySelector('.js-cancel-event');
if (cancelBtn) {
    cancelBtn.addEventListener('click', function (e) {
        e.preventDefault();
        const ok = window.confirm('Bạn có chắc muốn hủy?\nCác thay đổi chưa lưu sẽ bị mất.');
        if (ok) window.location.href = cancelBtn.getAttribute('href');
    });
}

const createHelpModal = document.getElementById('modal-create-help');
const btnCreateHelp = document.getElementById('btnCreateHelp');
const btnCloseCreateHelp = document.getElementById('btnCloseCreateHelp');
const btnOkCreateHelp = document.getElementById('btnOkCreateHelp');

function openCreateHelpModal() {
    if (createHelpModal) createHelpModal.classList.add('open');
}

function closeCreateHelpModal() {
    if (createHelpModal) createHelpModal.classList.remove('open');
}

if (btnCreateHelp) btnCreateHelp.addEventListener('click', openCreateHelpModal);
if (btnCloseCreateHelp) btnCloseCreateHelp.addEventListener('click', closeCreateHelpModal);
if (btnOkCreateHelp) btnOkCreateHelp.addEventListener('click', closeCreateHelpModal);
if (createHelpModal) {
    createHelpModal.addEventListener('click', function (e) {
        if (e.target === createHelpModal) closeCreateHelpModal();
    });
}
