/* ==============================================================================
   SCHOOL EVENT MANAGEMENT - VIEWS (v2 - 15/05/2026)
   ------------------------------------------------------------------------------
   Tạo 3 views thống kê:
       - vw_SoChoConLai          : số chỗ còn lại của từng sự kiện
       - vw_UserStatsByYear      : thống kê đăng ký theo sinh viên - từng năm
       - vw_DiemRenLuyenTheoKy   : điểm rèn luyện theo học kỳ

   Yêu cầu: chạy 01_schema_and_data.sql trước.
============================================================================== */

USE [school_event_management];
GO

-- ============================================================
-- VIEW: Số chỗ còn lại của từng sự kiện
-- ============================================================
CREATE OR ALTER VIEW dbo.vw_SoChoConLai AS
SELECT
    MaEvent,
    TenEvent,
    SoLuongToiDa,
    SoLuongDaDangKy,
    (SoLuongToiDa - SoLuongDaDangKy) AS SoChoConLai,
    CASE
        WHEN SoLuongToiDa - SoLuongDaDangKy <= 0  THEN N'Hết chỗ'
        WHEN SoLuongToiDa - SoLuongDaDangKy <= 10 THEN N'Sắp đầy'
        ELSE N'Còn chỗ'
    END AS TrangThaiCho
FROM dbo.[EVENT];
GO

-- ============================================================
-- VIEW: Thống kê đăng ký theo sinh viên - từng năm
-- ============================================================
CREATE OR ALTER VIEW dbo.vw_UserStatsByYear AS
SELECT
    IDSinhVien,
    YEAR(NgayDangKy)                                          AS Nam,
    COUNT(CASE WHEN TrangThai <> N'Hủy' THEN 1 END)           AS TongDangKyNam,
    COUNT(CASE WHEN TrangThai = N'Đã hoàn thành' THEN 1 END)  AS TongHoanThanhNam,
    COUNT(CASE WHEN TrangThai = N'Hủy' THEN 1 END)            AS TongHuyNam
FROM dbo.DangKySuKien
GROUP BY IDSinhVien, YEAR(NgayDangKy);
GO

-- ============================================================
-- VIEW: Điểm rèn luyện theo học kỳ (HK1: tháng 1-5, HK2: tháng 6-12)
-- Chỉ tính các sự kiện có trạng thái 'Đã hoàn thành'
-- ============================================================
CREATE OR ALTER VIEW dbo.vw_DiemRenLuyenTheoKy AS
SELECT
    DK.IDSinhVien,
    YEAR(E.NgayBatDau)                                                AS Nam,
    CASE WHEN MONTH(E.NgayBatDau) BETWEEN 1 AND 5 THEN 1 ELSE 2 END   AS HocKy,
    SUM(ISNULL(E.DRL, 0))                                             AS TongDiemRenLuyen
FROM dbo.DangKySuKien DK
JOIN dbo.[EVENT] E ON DK.MaEvent = E.MaEvent
WHERE DK.TrangThai = N'Đã hoàn thành'
GROUP BY
    DK.IDSinhVien,
    YEAR(E.NgayBatDau),
    CASE WHEN MONTH(E.NgayBatDau) BETWEEN 1 AND 5 THEN 1 ELSE 2 END;
GO


PRINT N'✓ Đã tạo 3 views.';
GO
