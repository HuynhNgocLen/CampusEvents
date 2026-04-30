-- Bảng quản trị viên (chạy trên DB school_event_management đã có sẵn schema CHINH)
-- TenDN: chữ thường, không dấu, không khoảng trắng (ứng dụng chuẩn hóa khi đăng nhập)
-- MatKhau: SHA-256 hex (64 ký tự), giống AdminSettingsController.HashPassword
-- Quyen: 0, 1 hoặc 2

IF OBJECT_ID(N'dbo.QuanTriVien', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.QuanTriVien (
        TenDN   VARCHAR(64)  NOT NULL
            CONSTRAINT PK_QuanTriVien PRIMARY KEY
            CONSTRAINT CK_QuanTriVien_TenDN CHECK (TenDN = LOWER(TenDN) AND TenDN NOT LIKE '% %'),
        MatKhau VARCHAR(255) NOT NULL,
        Quyen   INT          NOT NULL
            CONSTRAINT CK_QuanTriVien_Quyen CHECK (Quyen IN (0, 1, 2))
    );
END
GO

-- Mẫu: tdmu / 1234556 (SHA-256 UTF-8)
IF NOT EXISTS (SELECT 1 FROM dbo.QuanTriVien WHERE TenDN = 'tdmu')
    INSERT INTO dbo.QuanTriVien (TenDN, MatKhau, Quyen)
    VALUES (
        'tdmu',
        N'822f2935cd87313ab4900483eb4a24003633efc487241c5ca7f1f5e9dbb76e10',
        2
    );
GO
