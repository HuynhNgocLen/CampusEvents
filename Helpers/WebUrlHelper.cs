using System;

namespace school_event_management.Helpers
{
    public static class WebUrlHelper
    {
        /// <summary>
        /// Ensures external links work in href: values like "zalo.me/g/x" become "https://zalo.me/g/x".
        /// Empty input returns null.
        /// </summary>
        public static string NormalizeExternalHref(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            url = url.Trim();
            if (url.Length == 0)
            {
                return null;
            }

            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            if (url.StartsWith("//", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            int colon = url.IndexOf(':');
            if (colon > 0 && colon < 12)
            {
                var scheme = url.Substring(0, colon).ToLowerInvariant();
                if (scheme == "mailto" || scheme == "tel" || scheme == "sms")
                {
                    return url;
                }
            }

            return "https://" + url.TrimStart('/');
        }
    }
}
