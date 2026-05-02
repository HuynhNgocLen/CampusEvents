using System;

namespace shcool_event_management.Areas.Admin.Models
{
    public class AdminDangNhapLogRow
    {
        public long Id { get; set; }
        public DateTime ThoiGian { get; set; }
        public string TenDN { get; set; }
        public string MaQTV { get; set; }
        public int? Quyen { get; set; }
        public string DiaChiIP { get; set; }
        public string ThietBi { get; set; }
    }
}
