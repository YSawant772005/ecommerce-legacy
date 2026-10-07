using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using LegacyEcommerce.Data;
using LegacyEcommerce.Infrastructure;
using LegacyEcommerce.Models;

namespace LegacyEcommerce.Controllers
{
    public class WishlistController : Controller
    {
        private readonly StoreContext db = new StoreContext();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        private List<int> Ids()
        {
            return WishlistStore.Ids(Session);
        }

        private void Persist()
        {
            WishlistStore.SaveIfAuthenticated(db, Session);
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
            if (!db.Products.Any(p => p.Id == id))
            {
                return Json(new { ok = false, error = "Product not found." });
            }
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
            Persist();
            return Json(new { ok = true, on, count = ids.Count });
        }

        [HttpGet]
        public ActionResult Count()
        {
            return Json(new { count = Ids().Count }, JsonRequestBehavior.AllowGet);
        }
    }
}
