using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using LegacyEcommerce.Data;
using LegacyEcommerce.Infrastructure;
using LegacyEcommerce.Models;
using LegacyEcommerce.Models.ViewModels;

namespace LegacyEcommerce.Controllers
{
    public class CartController : Controller
    {
        private readonly StoreContext db = new StoreContext();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        private void Persist()
        {
            CartStore.SaveIfAuthenticated(db, Session);
        }

        private CartViewModel BuildModel()
        {
            var model = new CartViewModel();
            var lines = SessionCart.Lines(Session);
            if (lines.Count == 0)
            {
                model.Totals = Pricing.Compute(model.Lines, SessionCart.GetCoupon(Session));
                return model;
            }

            var ids = lines.Select(l => l.ProductId).ToList();
            var products = db.Products.Include("Brand").Where(p => ids.Contains(p.Id)).ToList();
            var byId = products.ToDictionary(p => p.Id);

            foreach (var line in lines)
            {
                Product p;
                if (!byId.TryGetValue(line.ProductId, out p)) continue;
                model.Lines.Add(new CartLineView { Product = p, Qty = line.Qty });
            }

            model.Totals = Pricing.Compute(model.Lines, SessionCart.GetCoupon(Session));
            return model;
        }

        public ActionResult Index()
        {
            ViewBag.Title = "Your Shopping Cart";
            ViewBag.Coupons = db.Coupons
                .Where(x => x.IsActive && (!x.EndsOn.HasValue || x.EndsOn.Value >= DateTime.Now))
                .OrderBy(x => x.Code)
                .Take(6)
                .ToList();
            return View(BuildModel());
        }

        [ChildActionOnly]
        public ActionResult MiniCart()
        {
            var model = BuildModel();
            ViewBag.Coupons = db.Coupons
                .Where(x => x.IsActive && (!x.EndsOn.HasValue || x.EndsOn.Value >= DateTime.Now))
                .OrderBy(x => x.Code)
                .Take(3)
                .ToList();
            return PartialView("_MiniCart", model);
        }

        [HttpPost]
        public ActionResult Add(int id, int qty = 1)
        {
            var product = db.Products.Find(id);
            if (product == null)
            {
                return Json(new { ok = false, error = "Product not found." });
            }
            if (product.Stock <= 0)
            {
                return Json(new { ok = false, error = "Sorry, this item is out of stock." });
            }

            SessionCart.Add(Session, id, qty);
            Persist();
            return Json(new
            {
                ok = true,
                count = SessionCart.Count(Session),
                name = product.Name,
                image = product.ImageUrl,
                price = product.Price
            });
        }

        [HttpPost]
        public ActionResult Update(int id, int qty)
        {
            SessionCart.Update(Session, id, qty);
            Persist();
            var model = BuildModel();
            return Json(new
            {
                ok = true,
                count = model.Totals.Count,
                lineTotal = id > 0 && model.Lines.Any(l => l.Product.Id == id) ? model.Lines.First(l => l.Product.Id == id).LineTotal : 0m,
                totals = model.Totals,
                removed = !model.Lines.Any(l => l.Product.Id == id)
            });
        }

        [HttpPost]
        public ActionResult Remove(int id)
        {
            SessionCart.Remove(Session, id);
            Persist();
            var model = BuildModel();
            return Json(new { ok = true, count = model.Totals.Count, totals = model.Totals });
        }

        [HttpPost]
        public ActionResult Clear()
        {
            SessionCart.Clear(Session);
            Persist();
            return Json(new { ok = true, count = 0 });
        }

        [HttpPost]
        public ActionResult ApplyCoupon(string code)
        {
            var model = BuildModel();
            var result = Coupons.Validate(code, model.Totals.Subtotal);
            if (result.Valid)
            {
                SessionCart.SetCoupon(Session, result.Code);
            }
            Persist();
            var refreshed = BuildModel();
            return Json(new
            {
                ok = result.Valid,
                message = result.Message,
                totals = refreshed.Totals
            });
        }

        [HttpPost]
        public ActionResult RemoveCoupon()
        {
            SessionCart.SetCoupon(Session, null);
            Persist();
            var model = BuildModel();
            return Json(new { ok = true, totals = model.Totals });
        }

        [HttpGet]
        public ActionResult Totals()
        {
            var model = BuildModel();
            return Json(new { count = model.Totals.Count, totals = model.Totals }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult Count()
        {
            return Json(new { count = SessionCart.Count(Session) }, JsonRequestBehavior.AllowGet);
        }
    }
}
