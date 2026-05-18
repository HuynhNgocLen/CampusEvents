using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using shcool_event_management.Models;

namespace shcool_event_management.Areas.Admin.Helpers
{
    public static class AdminNotificationCenterHelper
    {
        public class AdminNotificationItem
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Message { get; set; }
            public string Time { get; set; }
            public bool Read { get; set; }
        }

        public static List<AdminNotificationItem> Build(
            HttpSessionStateBase session,
            school_event_managementEntities db,
            string adminMaQtv)
        {
            var items = new List<AdminNotificationItem>();
            if (session == null || db == null || string.IsNullOrWhiteSpace(adminMaQtv))
            {
                return items;
            }

            var now = DateTime.Now;

            if (IsEnabled(session, "Notif_NewReg"))
            {
                AddNewRegistrationNotifications(items, db, adminMaQtv, now);
            }

            if (IsEnabled(session, "Notif_Reminder"))
            {
                AddEventReminderNotifications(items, db, adminMaQtv, now);
            }

            if (IsEnabled(session, "Notif_WeekReport"))
            {
                AddWeeklySummaryNotification(items, db, adminMaQtv, now, session);
            }

            return items
                .OrderByDescending(x => x.Id)
                .Take(30)
                .ToList();
        }

        private static bool IsEnabled(HttpSessionStateBase session, string key)
        {
            if (session == null)
            {
                return true;
            }

            var raw = session[key];
            if (raw == null)
            {
                // Mặc định bật để tránh mất thông báo khi session mới.
                return true;
            }

            if (raw is bool boolValue)
            {
                return boolValue;
            }

            var text = raw as string;
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            return string.Equals(text, "on", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(text, "1", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddNewRegistrationNotifications(
            List<AdminNotificationItem> items,
            school_event_managementEntities db,
            string adminMaQtv,
            DateTime now)
        {
            var since = now.AddDays(-2);
            var registrations = db.DangKySuKiens
                .Where(d => d.NgayDangKy >= since && d.EVENT.NguoiDang == adminMaQtv)
                .OrderByDescending(d => d.NgayDangKy)
                .Take(12)
                .Select(d => new
                {
                    d.MaEvent,
                    EventName = d.EVENT.TenEvent,
                    d.IDSinhVien,
                    StudentName = d.SinhVien.Ten,
                    d.NgayDangKy
                })
                .ToList();

            foreach (var reg in registrations)
            {
                var studentDisplay = string.IsNullOrWhiteSpace(reg.StudentName) ? reg.IDSinhVien : reg.StudentName;
                items.Add(new AdminNotificationItem
                {
                    Id = string.Format("new-reg-{0}-{1}-{2}",
                        reg.MaEvent,
                        reg.IDSinhVien ?? "sv",
                        reg.NgayDangKy.ToString("yyyyMMddHHmmss")),
                    Title = "Đăng ký mới",
                    Message = string.Format("{0} vừa đăng ký sự kiện \"{1}\".", studentDisplay, reg.EventName),
                    Time = reg.NgayDangKy.ToString("dd/MM/yyyy HH:mm"),
                    Read = false
                });
            }
        }

        private static void AddEventReminderNotifications(
            List<AdminNotificationItem> items,
            school_event_managementEntities db,
            string adminMaQtv,
            DateTime now)
        {
            var start = now;
            var end = now.AddHours(24);
            var events = db.EVENTs
                .Where(e => e.NguoiDang == adminMaQtv
                            && e.NgayBatDau >= start
                            && e.NgayBatDau <= end
                            && e.IsHidden == false)
                .OrderBy(e => e.NgayBatDau)
                .Take(8)
                .Select(e => new
                {
                    e.MaEvent,
                    e.TenEvent,
                    e.NgayBatDau
                })
                .ToList();

            foreach (var ev in events)
            {
                items.Add(new AdminNotificationItem
                {
                    Id = string.Format("event-reminder-{0}-{1}", ev.MaEvent, ev.NgayBatDau.ToString("yyyyMMddHHmm")),
                    Title = "Nhắc nhở sự kiện",
                    Message = string.Format("Sự kiện \"{0}\" sẽ bắt đầu lúc {1}.", ev.TenEvent, ev.NgayBatDau.ToString("HH:mm dd/MM")),
                    Time = "Trong 24 giờ tới",
                    Read = false
                });
            }
        }

        private static void AddWeeklySummaryNotification(
            List<AdminNotificationItem> items,
            school_event_managementEntities db,
            string adminMaQtv,
            DateTime now,
            HttpSessionStateBase session)
        {
            if (now.DayOfWeek != DayOfWeek.Monday || now.TimeOfDay < new TimeSpan(8, 0, 0))
            {
                return;
            }

            var vi = CultureInfo.GetCultureInfo("vi-VN");
            var calendar = vi.Calendar;
            var week = calendar.GetWeekOfYear(now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            var weekKey = string.Format("{0}-W{1:00}", now.Year, week);
            var sentKey = "Notif_WeekReport_SentWeek";

            if (string.Equals(session[sentKey] as string, weekKey, StringComparison.Ordinal))
            {
                return;
            }

            var weekStart = now.Date.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday);
            var weekEnd = weekStart.AddDays(7);
            var eventIds = db.EVENTs
                .Where(e => e.NguoiDang == adminMaQtv)
                .Select(e => e.MaEvent)
                .ToList();

            var registrationCount = db.DangKySuKiens
                .Count(d => eventIds.Contains(d.MaEvent) && d.NgayDangKy >= weekStart && d.NgayDangKy < weekEnd);

            var upcomingEnd = now.AddDays(7);
            var upcomingCount = db.EVENTs
                .Count(e => e.NguoiDang == adminMaQtv && e.NgayBatDau >= now && e.NgayBatDau < upcomingEnd);

            items.Add(new AdminNotificationItem
            {
                Id = string.Format("weekly-report-{0}", weekKey),
                Title = "Báo cáo hàng tuần",
                Message = string.Format("Tuần này: {0} lượt đăng ký mới, {1} sự kiện sắp diễn ra.", registrationCount, upcomingCount),
                Time = string.Format("Thứ Hai 08:00 - tuần {0}", week),
                Read = false
            });

            session[sentKey] = weekKey;
        }
    }
}
