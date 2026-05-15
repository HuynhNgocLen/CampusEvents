/* ==============================================================================
   SCHOOL EVENT MANAGEMENT - SCHEMA + DỮ LIỆU MẪU (v2 - 15/05/2026)
   ------------------------------------------------------------------------------
   File này tạo lại database từ đầu:
       1) Drop database cũ (nếu có) và tạo mới `school_event_management`
       2) Tạo schema (Vien, MaNghanh, SinhVien, DanhMuc, DiaDiem, QuanTriVien,
          EVENT, DangKySuKien, SuKienYeuThich, QTVAdminDangNhapLog,
          QTVHanhDongLog) + index
       3) Insert dữ liệu mẫu (Vien, MaNghanh, DanhMuc, DiaDiem, EVENT)

   Sau khi chạy file này, chạy tiếp:
       - 02_views.sql        (3 views thống kê)
       - 03_triggers.sql     (2 triggers + 1 stored procedure)

   Yêu cầu: SQL Server 2017 trở lên.
   Lưu ý: nếu IIS/ứng dụng đang giữ kết nối tới DB, hãy stop site trước khi chạy.
============================================================================== */

USE master;
GO

IF DB_ID(N'school_event_management') IS NOT NULL
BEGIN
    ALTER DATABASE [school_event_management] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [school_event_management];
END
GO

CREATE DATABASE [school_event_management];
GO

USE [school_event_management];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO


/* ==============================================================================
   PHẦN 1 - SCHEMA (TABLES + INDEXES)
============================================================================== */

-- ============================================================
-- VIỆN (phải tạo trước MaNghanh / SinhVien / EVENT / QuanTriVien)
-- ============================================================
CREATE TABLE dbo.Vien (
    MaVien  VARCHAR(12)   NOT NULL CONSTRAINT PK_Vien PRIMARY KEY,
    TenVien NVARCHAR(150) NOT NULL
);
GO

-- ============================================================
-- NGÀNH
-- ============================================================
CREATE TABLE dbo.MaNghanh (
    MaNghanh  VARCHAR(6)    NOT NULL CONSTRAINT PK_MaNghanh PRIMARY KEY,
    TenNghanh NVARCHAR(100) NOT NULL,
    THUOCVIEN VARCHAR(12)   NULL,
    CONSTRAINT FK_MaNghanh_Vien FOREIGN KEY (THUOCVIEN) REFERENCES dbo.Vien (MaVien)
);
GO

-- ============================================================
-- SINH VIÊN
-- ============================================================
CREATE TABLE dbo.SinhVien (
    ID               VARCHAR(13)   NOT NULL CONSTRAINT PK_SinhVien PRIMARY KEY,
    Ten              NVARCHAR(100) NOT NULL,
    NgayThangNamSinh DATE          NULL,
    SoDienThoai      VARCHAR(11)   NULL,
    Email            NVARCHAR(100) NULL,
    Lop              NVARCHAR(50)  NULL,
    MaNghanh         VARCHAR(6)    NULL,
    MaVien           VARCHAR(12)   NULL,
    MatKhau          VARCHAR(255)  NULL,
    CONSTRAINT FK_SinhVien_Nghanh FOREIGN KEY (MaNghanh) REFERENCES dbo.MaNghanh (MaNghanh),
    CONSTRAINT FK_SinhVien_Vien   FOREIGN KEY (MaVien)   REFERENCES dbo.Vien (MaVien)
);
GO

-- ============================================================
-- DANH MỤC / ĐỊA ĐIỂM (trước EVENT)
-- ============================================================
CREATE TABLE dbo.DanhMuc (
    MaDanhMuc  VARCHAR(5)    NOT NULL CONSTRAINT PK_DanhMuc PRIMARY KEY,
    TenDanhMuc NVARCHAR(100) NOT NULL,
    MauSac     NVARCHAR(50)  NULL,
    Icon       NVARCHAR(50)  NULL
);
GO

CREATE TABLE dbo.DiaDiem (
    MaDiaDiem     INT IDENTITY (1, 1) NOT NULL CONSTRAINT PK_DiaDiem PRIMARY KEY,
    TenDiaDiem    NVARCHAR(150)       NOT NULL,
    DiaChiChiTiet NVARCHAR(255)       NULL
);
GO

-- ============================================================
-- QUẢN TRỊ VIÊN
-- Quyen: 0=xem, 1=quản lý, 2=admin
-- MatKhau: Argon2id (SHA-256 legacy tự động nâng cấp khi đăng nhập)
-- ============================================================
CREATE TABLE dbo.QuanTriVien (
    TenDN         VARCHAR(64)  NOT NULL CONSTRAINT PK_QuanTriVien PRIMARY KEY,
    MatKhau       VARCHAR(255) NOT NULL,
    Quyen         INT          NOT NULL,
    MaQTV         VARCHAR(12)  NOT NULL,
    TrangThaiKhoa BIT          NOT NULL CONSTRAINT DF_QuanTriVien_Lock DEFAULT 0,
    CONSTRAINT CK_QuanTriVien_TenDN  CHECK (TenDN = LOWER(TenDN) AND TenDN NOT LIKE '% %'),
    CONSTRAINT CK_QuanTriVien_Quyen  CHECK (Quyen IN (0, 1, 2)),
    CONSTRAINT FK_QTV_Vien           FOREIGN KEY (MaQTV) REFERENCES dbo.Vien (MaVien)
);
GO

-- ============================================================
-- SỰ KIỆN
-- NguoiDang = VARCHAR(12) theo EDMX; SinhVien.ID = VARCHAR(13)
-- => không tạo FK NguoiDang vì khác kiểu/độ dài (EF store model cũng không map FK này).
-- ============================================================
CREATE TABLE dbo.[EVENT] (
    MaEvent           INT IDENTITY (1, 1) NOT NULL CONSTRAINT PK_EVENT PRIMARY KEY,
    MaVien            VARCHAR(12)    NOT NULL,
    TenEvent          NVARCHAR(250)  NOT NULL,
    MaDanhMuc         VARCHAR(5)     NOT NULL,
    Email             NVARCHAR(100)  NULL,
    MaDiaDiem         INT            NULL,
    NgayBatDau        DATETIME       NOT NULL,
    GioBatDau         TIME(0)        NULL,
    NgayKetThuc       DATETIME       NULL,
    GioKetThuc        TIME(0)        NULL,
    NgayBatDauDangKy  DATETIME       NULL,
    NgayHetHanDangKy  DATE           NULL,
    SoLuongToiDa      INT            NOT NULL CONSTRAINT DF_Event_SLTD DEFAULT 200,
    SoLuongDaDangKy   INT            NOT NULL CONSTRAINT DF_Event_SLDK DEFAULT 0,
    GiaVe             DECIMAL(12, 2) NOT NULL CONSTRAINT DF_Event_Gia DEFAULT 0,
    DRL               INT            NOT NULL CONSTRAINT DF_Event_DRL DEFAULT 0,
    MoTa              NVARCHAR(MAX)  NULL,
    ChiTiet           NVARCHAR(MAX)  NULL,
    NguoiDang         VARCHAR(12)    NULL,
    LuotXem           INT            NOT NULL CONSTRAINT DF_Event_View DEFAULT 0,
    TrangThai         NVARCHAR(50)   NOT NULL CONSTRAINT DF_Event_TT DEFAULT N'Sắp diễn ra',
    IsHidden          BIT            NOT NULL CONSTRAINT DF_Event_Hidden DEFAULT 0,
    AnhBia            NVARCHAR(255)  NULL,
    LinkZalo          VARCHAR(100)   NULL,
    NgayTao           DATETIME       NOT NULL CONSTRAINT DF_Event_Created DEFAULT GETDATE(),
    NgayCapNhat       DATETIME       NULL,
    CONSTRAINT FK_Event_Vien    FOREIGN KEY (MaVien)    REFERENCES dbo.Vien (MaVien),
    CONSTRAINT FK_Event_DanhMuc FOREIGN KEY (MaDanhMuc) REFERENCES dbo.DanhMuc (MaDanhMuc),
    CONSTRAINT FK_Event_DiaDiem FOREIGN KEY (MaDiaDiem) REFERENCES dbo.DiaDiem (MaDiaDiem)
);
GO

-- ============================================================
-- ĐĂNG KÝ / YÊU THÍCH
-- ============================================================
CREATE TABLE dbo.DangKySuKien (
    MaEvent    INT          NOT NULL,
    IDSinhVien VARCHAR(13)  NOT NULL,
    NgayDangKy DATETIME     NOT NULL CONSTRAINT DF_DK_Ngay DEFAULT GETDATE(),
    TrangThai  NVARCHAR(50) NOT NULL CONSTRAINT DF_DK_TT DEFAULT N'Đã đăng ký',
    CONSTRAINT PK_DangKySuKien PRIMARY KEY (MaEvent, IDSinhVien),
    CONSTRAINT FK_DangKy_Event    FOREIGN KEY (MaEvent)    REFERENCES dbo.[EVENT] (MaEvent),
    CONSTRAINT FK_DangKy_SinhVien FOREIGN KEY (IDSinhVien) REFERENCES dbo.SinhVien (ID)
);
GO

CREATE INDEX IX_DangKy_MaEvent    ON dbo.DangKySuKien (MaEvent);
CREATE INDEX IX_DangKy_IDSinhVien ON dbo.DangKySuKien (IDSinhVien);
GO

CREATE TABLE dbo.SuKienYeuThich (
    IDSinhVien VARCHAR(13) NOT NULL,
    MaEvent    INT         NOT NULL,
    NgayLuu    DATETIME    NOT NULL CONSTRAINT DF_YT_Ngay DEFAULT GETDATE(),
    TrangThai  VARCHAR(5)  NULL,
    CONSTRAINT PK_SuKienYeuThich PRIMARY KEY (IDSinhVien, MaEvent),
    CONSTRAINT FK_YeuThich_SinhVien FOREIGN KEY (IDSinhVien) REFERENCES dbo.SinhVien (ID),
    CONSTRAINT FK_YeuThich_Event    FOREIGN KEY (MaEvent)    REFERENCES dbo.[EVENT] (MaEvent)
);
GO

-- ============================================================
-- LOG (admin) - khớp với EF Model
-- ============================================================
CREATE TABLE dbo.QTVAdminDangNhapLog (
    Id       BIGINT        IDENTITY (1, 1) NOT NULL CONSTRAINT PK_QTVAdminDangNhapLog PRIMARY KEY,
    ThoiGian DATETIME2(7)  NOT NULL CONSTRAINT DF_QTVAdminDangNhapLog_ThoiGian DEFAULT (SYSDATETIME()),
    TenDN    VARCHAR(64)   NOT NULL,
    MaQTV    VARCHAR(32)   NULL,
    Quyen    INT           NULL,
    DiaChiIP NVARCHAR(45)  NULL,
    ThietBi  NVARCHAR(512) NULL
);
GO

CREATE INDEX IX_QTVAdminDangNhapLog_ThoiGian ON dbo.QTVAdminDangNhapLog (ThoiGian DESC);
CREATE INDEX IX_QTVAdminDangNhapLog_TenDN    ON dbo.QTVAdminDangNhapLog (TenDN);
GO

CREATE TABLE dbo.QTVHanhDongLog (
    Id             BIGINT         IDENTITY (1, 1) NOT NULL CONSTRAINT PK_QTVHanhDongLog PRIMARY KEY,
    ThoiGian       DATETIME2(7)   NOT NULL CONSTRAINT DF_QTVHanhDongLog_ThoiGian DEFAULT (SYSDATETIME()),
    TenDN          VARCHAR(64)    NOT NULL,
    MaQTV          VARCHAR(32)    NULL,
    PhuongThuc     VARCHAR(16)    NOT NULL,
    ControllerName VARCHAR(128)   NOT NULL,
    ActionName     VARCHAR(128)   NOT NULL,
    DuongDan       NVARCHAR(500)  NULL,
    MoTa           NVARCHAR(2000) NULL
);
GO

CREATE INDEX IX_QTVHanhDongLog_ThoiGian ON dbo.QTVHanhDongLog (ThoiGian DESC);
GO


/* ==============================================================================
   PHẦN 2 - DATASET MẪU
============================================================================== */

-- ============================================================
-- 2.1 Vien (insert TRƯỚC vì MaNghanh.THUOCVIEN tham chiếu Vien.MaVien)
-- ============================================================
INSERT INTO dbo.Vien (MaVien, TenVien) VALUES
    ('CNS',    N'Viện Công Nghệ Số'),
    ('KTCN',   N'Viện Kỹ Thuật Công Nghệ'),
    ('CNXBV',  N'Viện Công Nghệ Xanh Và Bền Vững'),
    ('KTTC',   N'Trường Kinh Tế Tài Chính'),
    ('LQL',    N'Trường Luật Và Quản Lý'),
    ('KSP',    N'Khoa Sư Phạm'),
    ('KNN',    N'Khoa Ngoại Ngữ'),
    ('KCNVH',  N'Khoa Công Nghiệp Văn Hóa'),
    ('KKXD',   N'Khoa Kiến Trúc - Xây Dựng'),
    ('YTDP',   N'Viện Đào Tạo Y Dược');
GO

-- ============================================================
-- 2.2 MaNghanh (kèm sẵn THUOCVIEN, không cần UPDATE riêng)
-- 49 ngành theo danh sách TDMU
-- ============================================================
INSERT INTO dbo.MaNghanh (MaNghanh, TenNghanh, THUOCVIEN) VALUES
    ('SPTH',   N'Sư Phạm Toán Học',                          'KSP'),
    ('SPNV',   N'Sư Phạm Ngữ Văn',                           'KSP'),
    ('SPLS',   N'Sư Phạm Lịch Sử',                           'KSP'),
    ('GDTH',   N'Giáo Dục Tiểu Học',                         'KSP'),
    ('GDMN',   N'Giáo Dục Mầm Non',                          'KSP'),
    ('GDHOC',  N'Giáo Dục Học',                              'KSP'),
    ('CNGD',   N'Công Nghệ Giáo Dục',                        'KSP'),
    ('TLH',    N'Tâm Lý Học',                                'KSP'),
    ('TOAN',   N'Toán Học',                                  'KSP'),

    ('KDQT',   N'Kinh Doanh Quốc Tế',                        'KTTC'),
    ('KTO',    N'Kiểm Toán',                                 'KTTC'),
    ('MKT',    N'Marketing',                                 'KTTC'),
    ('LOG',    N'Logistics Và Quản Lý Chuỗi Cung Ứng',      'KTTC'),
    ('QTKD',   N'Quản Trị Kinh Doanh',                       'KTTC'),
    ('TCNH',   N'Tài Chính - Ngân Hàng',                     'KTTC'),
    ('KET',    N'Kế Toán',                                   'KTTC'),

    ('NNHQ',   N'Ngôn Ngữ Hàn Quốc',                         'KNN'),
    ('NNTQ',   N'Ngôn Ngữ Trung Quốc',                       'KNN'),
    ('NNA',    N'Ngôn Ngữ Anh',                              'KNN'),

    ('TTDPT',  N'Truyền Thông Đa Phương Tiện',               'CNS'),
    ('TKHD',   N'Thiết Kế Đồ Họa',                           'CNS'),
    ('CNTT',   N'Công Nghệ Thông Tin',                       'CNS'),
    ('KTPM',   N'Kỹ Thuật Phần Mềm',                         'CNS'),
    ('TMDT',   N'Thương Mại Điện Tử',                        'CNS'),

    ('AM',     N'Âm Nhạc',                                   'KCNVH'),
    ('TLHGD',  N'Tâm Lý Học Giáo Dục',                       'KCNVH'),

    ('LUAT',   N'Luật',                                      'LQL'),
    ('QLCN',   N'Quản Lý Công Nghiệp',                       'LQL'),
    ('QLNN',   N'Quản Lý Nhà Nước',                          'LQL'),
    ('QLTNMT', N'Quản Lý Tài Nguyên Và Môi Trường',          'LQL'),
    ('QHQT',   N'Quan Hệ Quốc Tế',                           'LQL'),
    ('CTXH',   N'Công Tác Xã Hội',                           'LQL'),
    ('DL',     N'Du Lịch',                                   'LQL'),

    ('QLDD',   N'Quản Lý Đất Đai',                           'KKXD'),
    ('KTR',    N'Kiến Trúc',                                 'KKXD'),
    ('KTXD',   N'Kỹ Thuật Xây Dựng',                         'KKXD'),
    ('KTXDGT', N'Kỹ Thuật Xây Dựng Công Trình Giao Thông',  'KKXD'),

    ('KTMT',   N'Kỹ Thuật Môi Trường',                       'CNXBV'),
    ('CNVL',   N'Công Nghệ Vật Liệu',                        'CNXBV'),
    ('CNSH',   N'Công Nghệ Sinh Học',                        'CNXBV'),
    ('CNTP',   N'Công Nghệ Thực Phẩm',                       'CNXBV'),
    ('CNCBLS', N'Công Nghệ Chế Biến Lâm Sản',                'CNXBV'),

    ('KTCK',   N'Kỹ Thuật Cơ Khí',                           'KTCN'),
    ('TTNT',   N'Trí Tuệ Nhân Tạo Và Khoa Học Dữ Liệu',     'KTCN'),
    ('CNKTO',  N'Công Nghệ Kỹ Thuật Ô Tô',                  'KTCN'),
    ('KTDKDH', N'Kỹ Thuật Điều Khiển Và Tự Động Hóa',       'KTCN'),
    ('KTCDT',  N'Kỹ Thuật Cơ Điện Tử',                       'KTCN'),
    ('KTD',    N'Kỹ Thuật Điện',                             'KTCN'),

    ('DUOC',   N'Dược Học',                                  'YTDP'),
    ('YHOC',   N'Y Học',                                     'YTDP');
GO

-- ============================================================
-- 2.3 DanhMuc
-- ============================================================
INSERT INTO dbo.DanhMuc (MaDanhMuc, TenDanhMuc, MauSac, Icon) VALUES
    ('ACAD',  N'Học thuật',    '#3b82f6', 'school'),
    ('SPOR',  N'Thể thao',     '#ef4444', 'sports_soccer'),
    ('WORK',  N'Workshop',     '#10b981', 'build'),
    ('CLUB',  N'Câu lạc bộ',  '#8b5cf6', 'groups'),
    ('CULT',  N'Văn hóa',      '#f59e0b', 'palette'),
    ('VOLUN', N'Tình nguyện',  '#06b6d4', 'volunteer_activism');
GO

-- ============================================================
-- 2.4 DiaDiem
-- ============================================================
INSERT INTO dbo.DiaDiem (TenDiaDiem, DiaChiChiTiet) VALUES
    (N'Hội trường 2',             N'Tòa A, tầng 2'),
    (N'Sân vận động trường',      N'Khu thể thao - Khu B'),
    (N'Phòng hội thảo 1',         N'Sau Lưng Tòa B'),
    (N'Sân khấu ngoài trời',      N'Khu trung tâm, trước thư viện'),
    (N'Lab Máy tính 201',         N'Tòa B, tầng 2, phòng 201'),
    (N'Phòng họp Ban Giám Hiệu',  N'Tòa E1, tầng 1');
GO

-- ============================================================
-- 2.5 EVENT (4 sự kiện mẫu)
-- Lưu ý: NguoiDang để NULL vì chưa có QTV trong dataset mặc định
-- ============================================================
SET IDENTITY_INSERT dbo.[EVENT] ON;

INSERT INTO dbo.[EVENT]
    (MaEvent, MaVien, TenEvent, MaDanhMuc, Email, MaDiaDiem,
     NgayBatDau, GioBatDau, NgayKetThuc, GioKetThuc,
     NgayBatDauDangKy, NgayHetHanDangKy,
     SoLuongToiDa, SoLuongDaDangKy, GiaVe, DRL,
     MoTa, NguoiDang, LuotXem, TrangThai, IsHidden, AnhBia, LinkZalo)
VALUES
    (1, 'CNS',  N'Olympic Tin học TDMU 2026',        'ACAD',  'viencns@tdmu.edu.vn',   6, '2026-05-10 08:00', '08:00', '2026-05-10 17:00', '17:00', '2026-04-20', '2026-05-08', 100, 0, 0, 10, N'Cuộc thi lập trình thuật toán dành cho sinh viên.', NULL, 500,  N'Đang mở đăng ký', 0, 'cns_event.jpg', 'zalo.me/g/cns26'),
    (2, 'KTTC', N'Ngày hội Tuyển dụng Kinh tế 2026', 'WORK',  'truongkttc@tdmu.edu.vn', 1, '2026-06-15 07:30', '07:30', '2026-06-15 16:30', '16:30', '2026-05-01', '2026-06-10', 500, 0, 0,  5, N'Cơ hội việc làm tại các tập đoàn đa quốc gia.',         NULL, 1200, N'Sắp diễn ra',     0, 'kttc_job.jpg',  'zalo.me/g/kttc26'),
    (3, 'KSP',  N'Hội thi Nghiệp vụ Sư phạm',        'ACAD',  'khoasp@tdmu.edu.vn',     4, '2026-05-20 13:00', '13:00', '2026-05-20 17:00', '17:00', '2026-05-01', '2026-05-18', 200, 0, 0,  8, N'Rèn luyện kỹ năng đứng lớp cho giáo viên tương lai.',  NULL, 350,  N'Đang mở đăng ký', 0, 'sp_event.jpg',  NULL),
    (4, 'YTDP', N'Chiến dịch Khám sức khỏe cộng đồng','VOLUN','vienyduoc@tdmu.edu.vn',  5, '2026-07-01 07:00', '07:00', '2026-07-02 17:00', '17:00', '2026-06-01', '2026-06-25',  50, 0, 0, 15, N'Hỗ trợ y tế cho người dân vùng xa.',                   NULL, 800,  N'Sắp diễn ra',     0, 'yt_volun.jpg',  'zalo.me/g/ytdmu');

SET IDENTITY_INSERT dbo.[EVENT] OFF;
GO


PRINT N'✓ Đã tạo schema + dataset mẫu. Tiếp tục chạy 02_views.sql và 03_triggers.sql.';
GO
