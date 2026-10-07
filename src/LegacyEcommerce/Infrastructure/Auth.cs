using System;
using System.Web;
using LegacyEcommerce.Models;

namespace LegacyEcommerce.Infrastructure
{
    public class SessionUser
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
    }

    public static class Auth
    {
        private const string UserKey = "NK.User";

        public static SessionUser FromSession(HttpSessionStateBase session)
        {
            return session[UserKey] as SessionUser;
        }

        public static SessionUser Current
        {
            get
            {
                var ctx = HttpContext.Current;
                if (ctx == null || ctx.Session == null) return null;
                return ctx.Session[UserKey] as SessionUser;
            }
        }

        public static void Login(HttpSessionStateBase session, User user)
        {
            session[UserKey] = new SessionUser
            {
                Id = user.Id,
                Name = user.FullName,
                Email = user.Email,
                Role = user.Role
            };
        }

        public static void Logout(HttpSessionStateBase session)
        {
            session[UserKey] = null;
        }

        public static bool IsAdmin(HttpSessionStateBase session)
        {
            var u = FromSession(session);
            return u != null && string.Equals(u.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class RequireLoginAttribute : System.Web.Mvc.ActionFilterAttribute
    {
        public override void OnActionExecuting(System.Web.Mvc.ActionExecutingContext filterContext)
        {
            if (Auth.FromSession(filterContext.HttpContext.Session) == null)
            {
                var request = filterContext.HttpContext.Request;
                var isAjax = string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
                if (isAjax)
                {
                    filterContext.Result = new System.Web.Mvc.JsonResult
                    {
                        Data = new { ok = false, error = "login_required" },
                        JsonRequestBehavior = System.Web.Mvc.JsonRequestBehavior.AllowGet,
                        ContentEncoding = System.Text.Encoding.UTF8
                    };
                    filterContext.HttpContext.Response.StatusCode = 401;
                }
                else
                {
                    var url = filterContext.HttpContext.Request.RawUrl;
                    filterContext.Result = new System.Web.Mvc.RedirectResult("~/account/login?returnUrl=" + HttpUtility.UrlEncode(url));
                }
            }
        }
    }

    public class RequireAdminAttribute : System.Web.Mvc.ActionFilterAttribute
    {
        public override void OnActionExecuting(System.Web.Mvc.ActionExecutingContext filterContext)
        {
            var user = Auth.FromSession(filterContext.HttpContext.Session);
            if (user == null)
            {
                filterContext.Result = new System.Web.Mvc.RedirectResult("~/account/login?returnUrl=" + HttpUtility.UrlEncode(filterContext.HttpContext.Request.RawUrl));
            }
            else if (!string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                filterContext.Result = new System.Web.Mvc.RedirectResult("~/");
            }
        }
    }
}

