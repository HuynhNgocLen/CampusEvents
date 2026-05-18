/* ==============================================================================
   SCHOOL EVENT MANAGEMENT - TRIGGERS + STORED PROCEDURE (v2 - 15/05/2026)
   ------------------------------------------------------------------------------
   Tạo:
       - trg_DangKySuKien_UpdateCount : tự động cập nhật SoLuongDaDangKy
       - trg_AutoCancel_KhiKetThuc    : tự động hủy đăng ký khi sự kiện kết thúc
       - sp_AutoUpdate_TrangThaiEvent : cập nhật trạng thái EVENT theo thời gian

   Yêu cầu: chạy 01_schema_and_data.sql trước.
============================================================================== */

USE [school_event_management];
GO

-- ============================================================
-- TRIGGER: Tự động cập nhật SoLuongDaDangKy trên bảng EVENT
-- Kích hoạt khi INSERT / UPDATE / DELETE trên DangKySuKien
-- ============================================================
CREATE OR ALTER TRIGGER dbo.trg_DangKySuKien_UpdateCount
ON dbo.DangKySuKien
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    -- Tăng số lượng: thêm mới hoặc đổi từ Hủy → đăng ký
    UPDATE e
    SET SoLuongDaDangKy = SoLuongDaDangKy + 1
    FROM dbo.[EVENT] e
    JOIN inserted i ON e.MaEvent = i.MaEvent
    WHERE i.TrangThai NOT LIKE N'%hủy%'
      AND i.TrangThai NOT LIKE N'%cancel%';

    -- Giảm số lượng: đổi sang Hủy
    UPDATE e
    SET SoLuongDaDangKy = SoLuongDaDangKy - 1
    FROM dbo.[EVENT] e
    JOIN inserted i ON e.MaEvent = i.MaEvent
    JOIN deleted  d ON e.MaEvent = d.MaEvent
    WHERE i.TrangThai LIKE N'%Hủy%'
      AND d.TrangThai NOT LIKE N'%Hủy%'
      AND d.TrangThai NOT LIKE N'%cancel%'
      AND e.SoLuongDaDangKy > 0;

    -- Giảm số lượng: xóa hẳn dòng đăng ký
    UPDATE e
    SET SoLuongDaDangKy = SoLuongDaDangKy - 1
    FROM dbo.[EVENT] e
    JOIN deleted d ON e.MaEvent = d.MaEvent
    WHERE e.SoLuongDaDangKy > 0;
END;
GO

-- ============================================================
-- TRIGGER: Tự động hủy đăng ký chưa hoàn thành
-- Kích hoạt khi EVENT chuyển sang trạng thái 'Đã kết thúc'
-- ============================================================
CREATE OR ALTER TRIGGER dbo.trg_AutoCancel_KhiKetThuc
ON dbo.[EVENT]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF UPDATE(TrangThai)
    BEGIN
        UPDATE dk
        SET dk.TrangThai = N'Hủy'
        FROM dbo.DangKySuKien dk
        JOIN inserted i   ON dk.MaEvent = i.MaEvent
        JOIN deleted  del ON dk.MaEvent = del.MaEvent
        WHERE i.TrangThai   = N'Đã kết thúc'
          AND del.TrangThai <> N'Đã kết thúc'
          AND dk.TrangThai NOT IN (N'Đã hoàn thành', N'Hủy');
    END;
END;
GO

-- ============================================================
-- STORED PROCEDURE: Tự động cập nhật trạng thái EVENT theo thời gian thực
-- Có thể gọi định kỳ bằng SQL Server Agent Job (1-5 phút/lần)
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.sp_AutoUpdate_TrangThaiEvent
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Now DATETIME = GETDATE();

    -- Sắp diễn ra → Đang diễn ra
    UPDATE dbo.[EVENT]
    SET TrangThai = N'Đang diễn ra'
    WHERE TrangThai = N'Sắp diễn ra'
      AND @Now >= NgayBatDau
      AND (NgayKetThuc IS NULL OR @Now <= NgayKetThuc);

    -- Đang diễn ra / Sắp diễn ra → Đã kết thúc (có NgayKetThuc)
    UPDATE dbo.[EVENT]
    SET TrangThai = N'Đã kết thúc'
    WHERE TrangThai IN (N'Sắp diễn ra', N'Đang diễn ra')
      AND NgayKetThuc IS NOT NULL
      AND @Now > NgayKetThuc;

    -- Đang diễn ra / Sắp diễn ra → Đã kết thúc (không có NgayKetThuc, qua ngày)
    UPDATE dbo.[EVENT]
    SET TrangThai = N'Đã kết thúc'
    WHERE TrangThai IN (N'Sắp diễn ra', N'Đang diễn ra')
      AND NgayKetThuc IS NULL
      AND CAST(@Now AS DATE) > CAST(NgayBatDau AS DATE);
END;
GO


PRINT N'✓ Đã tạo 2 triggers + 1 stored procedure.';
GO
