using System;
using System.Linq;
using System.Data.Entity.Validation;
using shcool_event_management.Models;

namespace shcool_event_management.Areas.Admin.Helpers
{
    public static class AdminEventCommonHelper
    {
        public static string BuildEntityValidationErrorMessage(DbEntityValidationException ex)
        {
            var errors = ex.EntityValidationErrors
                .SelectMany(e => e.ValidationErrors)
                .Select(v => string.Format("{0}: {1}", v.PropertyName, v.ErrorMessage))
                .Distinct()
                .ToList();

            return errors.Any()
                ? "Dữ liệu chưa hợp lệ - " + string.Join(" | ", errors)
                : "Dữ liệu chưa hợp lệ.";
        }

        public static int? ResolveOrCreateLocationId(school_event_managementEntities db, string locationName)
        {
            var normalized = (locationName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            var existing = db.DiaDiems.FirstOrDefault(d => d.TenDiaDiem == normalized);
            if (existing != null)
            {
                return existing.MaDiaDiem;
            }

            existing = db.DiaDiems
                .ToList()
                .FirstOrDefault(d => string.Equals((d.TenDiaDiem ?? string.Empty).Trim(), normalized, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing.MaDiaDiem;
            }

            var newLocation = new DiaDiem
            {
                TenDiaDiem = normalized,
                DiaChiChiTiet = normalized
            };

            db.DiaDiems.Add(newLocation);
            db.SaveChanges();
            return newLocation.MaDiaDiem;
        }

        public static int[] ResolveSemesterMonths(int? semester)
        {
            if (!semester.HasValue)
            {
                return null;
            }

            switch (semester.Value)
            {
                case 1: return new[] { 9, 10, 11, 12 };
                case 2: return new[] { 1, 2, 3, 4 };
                case 3: return new[] { 5, 6, 7, 8 };
                default: return null;
            }
        }

        public static IQueryable<DangKySuKien> ApplyRegistrationStatusFilter(IQueryable<DangKySuKien> query, string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return query;
            }

            if (status == "Đã đăng ký")
            {
                return query.Where(d => d.TrangThai == "Đã đăng ký" || d.TrangThai == "Đã xác nhận" || d.TrangThai == "Chờ xác nhận");
            }

            if (status == "Đã hoàn thành")
            {
                return query.Where(d => d.TrangThai == "Đã hoàn thành" || d.TrangThai == "Đã xác nhận");
            }

            return query.Where(d => d.TrangThai == status);
        }
    }
}
