using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Web;

namespace IISRPWA
{
    public class BasicAuthentication : IHttpModule
    {
        public bool IsReusable
        {
            get { return true; }
        }

        protected bool IsHeaderPresent
        {
            get
            {
                HttpContext context = HttpContext.Current;
                string authHeader = context.Request.Headers["Authorization"];
                return (!string.IsNullOrEmpty(authHeader));
            }
        }

        public void Dispose()
        {
        }

        public void Init(HttpApplication context)
        {
            context.AuthenticateRequest += OnEnter;
            context.EndRequest += OnLeave;
        }

        void OnEnter(object sender, EventArgs e)
        {
            HttpContext context = HttpContext.Current;

            if (!context.Request.IsSecureConnection && !context.Request.IsLocal)
            {
                string redirectUrl = context.Request.Url.ToString().Replace("http:", "https:");
                context.Response.Redirect(redirectUrl, false);
                context.ApplicationInstance.CompleteRequest();
                return;
            }

            if (IsPathException())
                return;

            if (IsIPException())
                return;

            if (IsHeaderPresent)
            {
                if (!AuthenticateUser())
                {
                    DenyAccess();
                }
            }
            else
            {
                DenyAccess();
            }
        }

        void OnLeave(object sender, EventArgs e)
        {
            if (HttpContext.Current.Response.StatusCode == 401)
            {
                SendAuthenticationHeader();
            }
        }

        void SendAuthenticationHeader()
        {
            HttpContext context = HttpContext.Current;

            context.Response.StatusCode = 401;
            context.Response.AddHeader(
                "WWW-Authenticate",
                String.Format("Basic realm=\"{0}\"", context.Request.Url.Host));
        }

        bool IsPathException()
        {
            HttpContext context = HttpContext.Current;
            var currentPath = context.Request.AppRelativeCurrentExecutionFilePath;
            foreach (var pathException in Configuration.Instance.PathExceptions)
            {
                if (IsPathMatch(pathException.Pattern, currentPath))
                    return true;
            }
            return false;
        }

        bool IsIPException()
        {
            HttpContext context = HttpContext.Current;
            var clientIPAddress = context.Request.UserHostAddress;
            foreach (var ipException in Configuration.Instance.IPExceptions)
            {
                if (IsPathMatch(ipException.Pattern, clientIPAddress))
                    return true;
            }
            return false;

        }

        bool IsPathMatch(string pattern, string value)
        {
            if (pattern.Contains("*"))
            {
                var parts = pattern.Split('*');
                int? index = null;
                foreach (var part in parts)
                {
                    if (index == null)
                    {
                        if (!value.StartsWith(part, StringComparison.OrdinalIgnoreCase))
                            return false;
                        index = part.Length;
                    }
                    else
                    {
                        index = value.IndexOf(part, index.Value, StringComparison.OrdinalIgnoreCase);
                        if (index == -1)
                            return false;
                    }
                }
                return true;
            }
            else
            {
                if (pattern.Equals(value, StringComparison.OrdinalIgnoreCase))
                    return true;
                else
                    return false;
            }
        }

        bool AuthenticateUser()
        {
            string username = "", password = "";
            string authHeader = HttpContext.Current.Request.Headers["Authorization"];
            if (authHeader != null && authHeader.StartsWith("Basic"))
            {
                // extract credentials from header
                string[] credentials = ExtractCredentials(authHeader);
                username = credentials[0];
                password = credentials[1];
                var clientIPAddress = HttpContext.Current.Request.UserHostAddress;

                if (AuthenticationCookieManager.HasCookie &&
                    AuthenticationCookieManager.IsCookieValid(username, clientIPAddress))
                {
                    SetPrincipal(username);
                    return true;
                }

                if (ValidateUser(username, password))
                {
                    SetPrincipal(username);
                    AuthenticationCookieManager.AddCookie(username, DateTime.Now.AddMinutes(15), clientIPAddress);
                    return true;
                }
            }

            return false;
        }

        bool ValidateUser(string username, string password)
        {
            var user = Configuration.Instance.Users.FirstOrDefault((u) => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (user == null)
                return false;
            else
            {
                var passwordHash = Convert.FromBase64String(user.PasswordHash);
                var passwordSalt = Convert.FromBase64String(user.PasswordSalt);
                return HashHelper.VerifyPassword(user.Username, password, passwordHash, passwordSalt, user.HashIterations);
            }
        }

        void SetPrincipal(string username)
        {
            //Create Principal and set Context.User
            GenericIdentity id = new GenericIdentity(username, "CustomBasic");
            GenericPrincipal p = new GenericPrincipal(id, null);
            HttpContext.Current.User = p;
        }

        string[] ExtractCredentials(string authHeader)
        {
            // strip out the "basic"
            string encodedUserPass = authHeader.Substring(6).Trim();

            // that's the right encoding
            Encoding encoding = Encoding.GetEncoding("iso-8859-1");
            string userPass = encoding.GetString(Convert.FromBase64String(encodedUserPass));
            int separator = userPass.IndexOf(':');

            string[] credentials = new string[2];
            credentials[0] = userPass.Substring(0, separator);
            credentials[1] = userPass.Substring(separator + 1);

            return credentials;
        }

        void DenyAccess()
        {
            HttpContext context = HttpContext.Current;

            context.Response.StatusCode = 401;
            context.Response.End();
        }
    }
}