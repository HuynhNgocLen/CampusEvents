using System;

namespace shcool_event_management.Areas.Admin.Models
{
    public class QtvHanhDongLogRow
    {
        public long Id { get; set; }
        public DateTime ThoiGian { get; set; }
        public string TenDN { get; set; }
        public string MaQTV { get; set; }
        public string PhuongThuc { get; set; }
        public string ControllerName { get; set; }
        public string ActionName { get; set; }
        public string DuongDan { get; set; }
        public string MoTa { get; set; }
    }
}
