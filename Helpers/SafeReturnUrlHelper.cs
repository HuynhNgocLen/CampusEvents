using System;
using System.Web;
using System.Web.Mvc;

namespace school_event_management.Helpers
{
    public static class SafeReturnUrlHelper
    {
        /// <summary>
        /// Chỉ cho phép đường dẫn nội bộ dạng /... để tránh open redirect.
        /// </summary>
        public static bool IsSafeRelativeAppPath(HttpRequestBase request, string url)
        {
            if (string.IsNullOrWhiteSpace(url) || request?.RequestContext == null) return false;
            var u = url.Trim();
            if (!u.StartsWith("/", StringComparison.Ordinal)) return false;
            if (u.StartsWith("//", StringComparison.Ordinal)) return false;
            return new UrlHelper(request.RequestContext).IsLocalUrl(u);
        }
    }
}
