using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using LegacyEcommerce.Data;
using LegacyEcommerce.Models;

namespace LegacyEcommerce.Controllers
{
    public class WishlistController : Controller
    {
        private const string Key = "NK.Wishlist";
        private readonly StoreContext db = new StoreContext();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        private List<int> Ids()
        {
            var list = Session[Key] as List<int>;
            if (list == null)
            {
                list = new List<int>();
                Session[Key] = list;
            }
            return list;
        }

        public ActionResult Index()
        {
            var ids = Ids();
            var products = ids.Count == 0
                ? new List<Product>()
                : db.Products.Include("Brand").Where(p => ids.Contains(p.Id)).ToList();
            ViewBag.Title = "My Wishlist";
            return View(products);
        }

        [HttpPost]
        public ActionResult Toggle(int id)
        {
            var ids = Ids();
            bool on;
            if (ids.Contains(id))
            {
                ids.Remove(id);
                on = false;
            }
            else
            {
                ids.Add(id);
                on = true;
            }
            return Json(new { ok = true, on, count = ids.Count });
        }

        [HttpGet]
        public ActionResult Count()
        {
            return Json(new { count = Ids().Count }, JsonRequestBehavior.AllowGet);
        }
    }
}
