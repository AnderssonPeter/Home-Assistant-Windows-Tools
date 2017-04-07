using System;
using System.Text;
using System.Web;
using System.Web.Security;

namespace IISRPWA
{
    public static class AuthenticationCookieManager
    {

        const string CookieName = "CookieMonster";
        const string Purpose = "Authentication";
        const string DateTimeFormat = "yyyyMMddHHmmss";

        public static bool HasCookie => (HttpContext.Current != null &&
                                         HttpContext.Current.Request != null &&
                                         HttpContext.Current.Request.Cookies != null &&
                                         HttpContext.Current.Request.Cookies[CookieName] != null);

        public static bool IsCookieValid(string username, string validFor)
        {
            if (!HasCookie)
                return false;
            try
            {
                var cookie = HttpContext.Current.Request.Cookies[CookieName];
                var protectedData = Convert.FromBase64String(cookie.Value);
                var cookieContent = Encoding.UTF8.GetString(MachineKey.Unprotect(protectedData, Purpose));
                var cookieParts = cookieContent.Split('|');
                var cookieUsername = cookieParts[0];
                var cookieValidUntil = DateTime.ParseExact(cookieParts[1], DateTimeFormat, null);
                var cookieValidFor = cookieParts[2];
                if (!cookieUsername.Equals(username, StringComparison.OrdinalIgnoreCase) ||
                    cookieValidUntil < DateTime.Now ||
                    !cookieValidFor.Equals(validFor, StringComparison.OrdinalIgnoreCase))
                    return false;
                else
                    return true;

            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                return false;
            }
        }

        public static void AddCookie(string username, DateTime validUntil, string validFor)
        {
            var cookieContent = username + "|" + validUntil.ToString(DateTimeFormat) + "|" + validFor;
            var protectedData = Convert.ToBase64String(MachineKey.Protect(Encoding.UTF8.GetBytes(cookieContent), Purpose));
            HttpContext.Current.Response.Cookies.Add(new HttpCookie(CookieName, protectedData) { Expires = validUntil, HttpOnly = true, Shareable = false, Secure = HttpContext.Current.Request.IsSecureConnection });
        }
    }
}