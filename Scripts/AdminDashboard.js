document.addEventListener('DOMContentLoaded', function () {
    let editorInstance = null;

    const CK = window.CKEDITOR || window.ClassicEditor;
    if (typeof CK === 'undefined' || typeof CK.ClassicEditor === 'undefined') {
        console.error('❌ CKEditor 5 chưa tải. Vui lòng kiểm tra kết nối mạng hoặc CDN.');
        return;
    }

    const ClassicEditor = CK.ClassicEditor || CK;

    ClassicEditor
        .create(document.querySelector('#ChiTietEditor'), {
            language: {
                ui: 'vi',
                content: 'vi'
            },
            toolbar: {
                items: [
                    'heading', '|',
                    'bold', 'italic', 'underline', '|',
                    'fontSize', 'fontColor', 'fontBackgroundColor', '|',
                    'bulletedList', 'numberedList', '|',
                    'alignment', 'link', 'blockQuote', 'insertTable', '|',
                    'undo', 'redo'
                ]
            },
            placeholder: 'Nhập nội dung chi tiết sự kiện tại đây...',
            height: '420px'
        })
        .then(editor => {
            editorInstance = editor;
            console.log('✅ CKEditor 5 đã khởi tạo thành công!');
        })
        .catch(error => {
            console.error('❌ Lỗi khởi tạo CKEditor:', error);
        });

    // Copy dữ liệu từ CKEditor vào textarea trước khi submit
    const form = document.getElementById('createEventForm');
    if (form) {
        form.addEventListener('submit', function () {
            if (editorInstance) {
                document.getElementById('ChiTietEditor').value = editorInstance.getData();
            }
        });
    }

    // Preview ảnh bìa
    const fileInput = document.getElementById('coverImageInput');
    const uploadContent = document.getElementById('uploadContent');
    const previewArea = document.getElementById('previewArea');
    const previewImg = document.getElementById('previewImg');
    const removeBtn = document.getElementById('removePreviewBtn');

    fileInput.addEventListener('change', function () {
        if (this.files && this.files[0]) {
            const reader = new FileReader();
            reader.onload = function (e) {
                previewImg.src = e.target.result;
                uploadContent.style.display = 'none';
                previewArea.style.display = 'block';
            };
            reader.readAsDataURL(this.files[0]);
        }
    });

    removeBtn.addEventListener('click', function (e) {
        e.preventDefault();
        fileInput.value = '';
        previewArea.style.display = 'none';
        uploadContent.style.display = 'block';
    });
});