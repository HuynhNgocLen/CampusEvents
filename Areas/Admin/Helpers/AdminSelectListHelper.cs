using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using shcool_event_management.Models;

namespace shcool_event_management.Areas.Admin.Helpers
{
    public static class AdminSelectListHelper
    {
        public static void PopulateDropdowns(dynamic viewBag,
                                             school_event_managementEntities db,
                                             string selectedCat = null,
                                             int? selectedDiaDiem = null,
                                             string selectedVien = null,
                                             QuanTriVien currentAdmin = null,
                                             bool applyVienPermission = false)
        {
            viewBag.DanhMucList = db.DanhMucs
                .Select(d => new SelectListItem
                {
                    Value = d.MaDanhMuc,
                    Text = d.TenDanhMuc,
                    Selected = d.MaDanhMuc == selectedCat
                })
                .ToList();

            viewBag.DiaDiemList = db.DiaDiems
                .Select(d => new SelectListItem
                {
                    Value = d.MaDiaDiem.ToString(),
                    Text = d.TenDiaDiem + (d.DiaChiChiTiet != null ? " - " + d.DiaChiChiTiet : ""),
                    Selected = d.MaDiaDiem == selectedDiaDiem
                })
                .ToList();

            var vienFromDb = db.Viens
                .OrderBy(v => v.TenVien)
                .Select(v => new SelectListItem
                {
                    Value = v.MaVien,
                    Text = v.TenVien,
                    Selected = v.MaVien == selectedVien
                })
                .ToList();

            if (applyVienPermission && currentAdmin != null && (currentAdmin.Quyen == 1 || currentAdmin.Quyen == 2))
            {
                var forcedMaVien = ResolveAdminVienCode(db, currentAdmin);
                var forcedVien = vienFromDb.FirstOrDefault(v => v.Value == forcedMaVien);
                if (forcedVien == null)
                {
                    forcedVien = new SelectListItem
                    {
                        Value = forcedMaVien,
                        Text = "Viện " + forcedMaVien,
                        Selected = true
                    };
                    vienFromDb = new List<SelectListItem> { forcedVien };
                }
                else
                {
                    forcedVien.Selected = true;
                    vienFromDb = new List<SelectListItem> { forcedVien };
                }

                viewBag.IsVienLocked = true;
                viewBag.ForcedMaVien = forcedMaVien;
                viewBag.VienLockMessage = "Tài khoản quyền 2 chỉ được tạo sự kiện cho viện của mình.";
            }
            else
            {
                viewBag.IsVienLocked = false;
                viewBag.ForcedMaVien = null;
                viewBag.VienLockMessage = null;
            }

            viewBag.VienList = vienFromDb;
        }

        private static string ResolveAdminVienCode(school_event_managementEntities db, QuanTriVien admin)
        {
            if (admin == null) return null;

            var maVien = db.QuanTriViens
                .Where(q => q.TenDN == admin.TenDN)
                .Select(q => q.Vien.MaVien)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(maVien))
            {
                return maVien;
            }

            if (!string.IsNullOrWhiteSpace(admin.Vien?.MaVien))
            {
                return admin.Vien.MaVien;
            }

            return admin.MaQTV;
        }
    }
}
