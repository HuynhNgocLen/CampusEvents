using System.Collections.Generic;
using shcool_event_management.Models;

namespace shcool_event_management.Areas.Admin.ViewModels
{
    public class EventDashboardViewModel
    {
        public int TotalEvents { get; set; }
        public int UpcomingEvents { get; set; }
        public int OngoingEvents { get; set; }
        public int TotalRegistrations { get; set; }

        public List<EVENT> UpcomingList { get; set; }
        public List<CategoryStat> CategoryStats { get; set; }
    }

    public class CategoryStat
    {
        public string Category { get; set; }
        public int Count { get; set; }
        public int TotalReg { get; set; }
    }
}