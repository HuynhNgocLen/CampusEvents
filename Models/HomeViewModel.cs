using shcool_event_management.Models;
using System.Collections.Generic;

namespace school_event_management.Models
{
    public class HomeViewModel
    {
        public IEnumerable<Vien> DanhSachVien { get; set; }
        public IEnumerable<EVENT> DanhSachSuKien { get; set; }

        public IEnumerable<DanhMuc> DanhMucs { get; set; }

    }
}