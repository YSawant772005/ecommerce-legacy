using System.Web.Mvc;

namespace LegacyEcommerce.Controllers
{
    public class ErrorController : Controller
    {
        public ActionResult Index()
        {
            Response.StatusCode = 500;
            Response.TrySkipIisCustomErrors = true;
            ViewBag.Title = "Something went wrong";
            return View("Error");
        }

        public ActionResult NotFound()
        {
            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;
            ViewBag.Title = "Page not found";
            return View("NotFound");
        }

        public ActionResult Forbidden()
        {
            Response.StatusCode = 403;
            Response.TrySkipIisCustomErrors = true;
            ViewBag.Title = "Access denied";
            return View("Forbidden");
        }
    }
}
