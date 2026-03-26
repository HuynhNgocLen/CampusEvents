using Newtonsoft.Json.Linq;
using school_event_management;
using school_event_management.Models;
using shcool_event_management.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using System.Linq;
using System.Web.Mvc;
using System.Web.UI.WebControls;
using System.Windows.Ink;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace school_event_management.Controllers
{
    public class UsersController : Controller
    {

        SchoolEventManagementEntities db = new SchoolEventManagementEntities();

        //Users/Index 
        public ActionResult Index()
        {
            ViewBag.Title = "Khám phá Sự kiện";
            ViewBag.ActivePage = "home";
            ViewBag.UserName = "Sinh Viên";
            ViewBag.ListVien = db.Viens.OrderBy(v => v.TenVien).ToList();
            ViewBag.DanhMucs = db.DanhMucs.ToList();
            var events = db.EVENTs.ToList();
            return View(events);
        }

        //Users/Events
        public ActionResult Events(string[] vien, string[] danhmuc, string time, string[] status)
        {
            // Load dữ liệu cho Sidebar
            ViewBag.ListVien = db.Viens.OrderBy(v => v.TenVien).ToList();
            ViewBag.ListDanhMuc = db.DanhMucs.ToList();
            ViewBag.DanhMucs = db.DanhMucs.ToList();

            var query = db.EVENTs.AsQueryable();

            List<string> selectedVien = (vien ?? new string[0]).ToList();
            List<string> selectedDM = (danhmuc ?? new string[0]).ToList();
            List<string> selectedStatus = (status ?? new string[0]).ToList();

            // Lọc theo Viện
            if (selectedVien.Count > 0 && !selectedVien.Contains("all"))
            {
                query = query.Where(e => selectedVien.Contains(e.MaVien));
            }

            // Lọc theo Danh mục
            if (selectedDM.Count > 0 && !selectedDM.Contains("all"))
            {
                query = query.Where(e => selectedDM.Contains(e.MaDanhMuc));
            }

            // Lọc theo Thời gian
            var today = DateTime.Today;
            if (!string.IsNullOrEmpty(time) && time != "all")
            {
                if (time == "today")
                {
                    query = query.Where(e => DbFunctions.TruncateTime(e.NgayBatDau) == today);
                }
                else if (time == "week")
                {
                    var endWeek = today.AddDays(7);
                    query = query.Where(e => e.NgayBatDau >= today && e.NgayBatDau <= endWeek);
                }
                else if (time == "month")
                {
                    query = query.Where(e => e.NgayBatDau.Month == today.Month && e.NgayBatDau.Year == today.Year);
                }
            }

            // Lọc Trạng thái
            if (selectedStatus.Count > 0 && !selectedStatus.Contains("all"))
            {
                bool isFree = selectedStatus.Contains("free");
                bool isAvailable = selectedStatus.Contains("available");
                bool isAlmostFull = selectedStatus.Contains("almost");

                query = query.Where(e =>
                    (isFree && e.GiaVe == 0) ||
                    (isAvailable && (e.SoLuongToiDa - e.SoLuongDaDangKy) > 0) ||
                    (isAlmostFull && (e.SoLuongToiDa - e.SoLuongDaDangKy) <= 10 && (e.SoLuongToiDa - e.SoLuongDaDangKy) > 0)
                );
            }

            var data = query.OrderByDescending(e => e.NgayBatDau).ToList();
            return View(data);
        }

        public ActionResult EventDetail(int? id)
        {
            //So Cho Con Lai
             var tinhTrang = db.vw_SoChoConLai.FirstOrDefault(v => v.MaEvent == id);
             ViewBag.tinhTrang = tinhTrang;
            if (id == null) return RedirectToAction("Events");
            ViewBag.tinhTrang = db.vw_SoChoConLai.FirstOrDefault(v => v.MaEvent == id);
            //Phan Tram
            if (tinhTrang != null && tinhTrang.SoLuongToiDa > 0)
            {
                ViewBag.phanTram = (double)(tinhTrang.SoLuongDaDangKy * 100) / (double)tinhTrang.SoLuongToiDa;
            }
            else
            {
                ViewBag.phanTram = 0;
            }

            db.Configuration.ProxyCreationEnabled = false;

            var ev = db.EVENTs
                       .Include(e => e.DanhMuc)
                       .Include(e => e.Vien)
                       .Include(e => e.DiaDiem)
                       .FirstOrDefault(e => e.MaEvent == id);

            if (ev == null) return HttpNotFound();

            string currentStudentId = "SV001";

            // ĐÚNG tên bảng
            bool daDangKy = db.DangKySuKiens.Any(d => d.MaEvent == id && d.IDSinhVien == currentStudentId);
            ViewBag.DaDangKy = daDangKy;

            //Time 
            DateTime ngayHetHan = ev.NgayHetHanDangKy ?? DateTime.Now;
            DateTime ngayHienTai = DateTime.Now.Date;
            TimeSpan difference = ngayHetHan - ngayHienTai;
            int soNgayConLai = difference.Days;
            ViewBag.SoNgayConLai = soNgayConLai;

            if (daDangKy)
            {
                //Sử dụng QRCodeHelper mới của bạn
                ViewBag.QRCodeBase64 = QRCodeHelper.GenerateQRCode(ev.MaEvent, currentStudentId);
            }

            return View(ev);
        }

        //Registrations
        public ActionResult Registrations()
        {
            ViewBag.Title = "Đăng ký của tôi";
            ViewBag.ActivePage = "registrations";
            ViewBag.UserName = "Sinh Viên";
            return View();
        }

        //Users/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(int eventId)
        {
            TempData["Success"] = "Đăng ký thành công!";
            return RedirectToAction("Registrations");
        }

        //Users/Schedule
        public ActionResult Schedule()
        {
            ViewBag.Title = "Lịch của tôi";
            ViewBag.ActivePage = "schedule";
            ViewBag.UserName = "Sinh Viên";
            return View();
        }

        //Users/Filters
        public ActionResult Filter()
        {
            ViewBag.Title = "Bộ Lọc";
            ViewBag.ActivePage = "Filter";
            ViewBag.UserName = "Sinh Viên";
            var dsKhoaVien = db.Viens.OrderBy(v => v.TenVien).ToList();
            return View(dsKhoaVien);
        }

    }
}