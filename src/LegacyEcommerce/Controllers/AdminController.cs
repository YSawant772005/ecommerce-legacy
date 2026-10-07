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
    [RequireAdmin]
    public class AdminController : Controller
    {
        private readonly StoreContext db = new StoreContext();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        public ActionResult Index()
        {
            return RedirectToAction("Dashboard");
        }

        public ActionResult Dashboard()
        {
            var now = DateTime.Now;
            var monthCutoff = now.AddMonths(-1);
            var model = new AdminDashboardViewModel
            {
                ProductCount = db.Products.Count(),
                OrderCount = db.Orders.Count(),
                CustomerCount = db.Users.Count(u => u.Role == "Customer"),
                Revenue = db.Orders.Where(o => o.Status != OrderStatus.Cancelled).Select(o => (decimal?)o.Total).Sum() ?? 0,
                MonthlyRevenue = db.Orders
                    .Where(o => o.Status != OrderStatus.Cancelled && o.CreatedOn >= monthCutoff)
                    .Select(o => (decimal?)o.Total).Sum() ?? 0,
                RecentOrders = db.Orders.OrderByDescending(o => o.CreatedOn).Take(8).ToList(),
                LowStock = db.Products.Where(p => p.Stock <= 5).OrderBy(p => p.Stock).Take(8).ToList()
            };

            var statusCounts = db.Orders.GroupBy(o => o.Status).Select(g => new { g.Key, N = g.Count() }).ToList();
            foreach (var s in Enum.GetValues(typeof(OrderStatus)))
            {
                var key = (OrderStatus)s;
                model.OrdersByStatus[key.ToString()] = statusCounts.Where(x => x.Key == key).Select(x => x.N).DefaultIfEmpty(0).First();
            }

            ViewBag.Title = "Admin Dashboard";
            return View(model);
        }

        public ActionResult Products(string q, int page = 1)
        {
            var model = new AdminProductsViewModel { Q = q, Page = page < 1 ? 1 : page };
            IEnumerable<Product> query = CatalogCache.Products;

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(p => (p.Name != null && p.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (p.Sku != null && p.Sku.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (p.Brand != null && p.Brand.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
            }
            var list = query.ToList();
            model.TotalCount = list.Count;
            model.Products = list
                .OrderByDescending(p => p.Id)
                .Skip((model.Page - 1) * model.PageSize)
                .Take(model.PageSize)
                .ToList();

            ViewBag.Title = "Manage Products";
            return View(model);
        }

        [HttpGet]
        public ActionResult ProductCreate()
        {
            FillProductForm();
            ViewBag.Title = "Add Product";
            return View("ProductForm", new Product());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProductCreate(Product model)
        {
            if (!ModelState.IsValid)
            {
                FillProductForm();
                ViewBag.Title = "Add Product";
                return View("ProductForm", model);
            }

            PrepareProduct(model, null);
            db.Products.Add(model);
            try
            {
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Could not save product: " + ex.Message);
                FillProductForm();
                ViewBag.Title = "Add Product";
                return View("ProductForm", model);
            }

            TempData["Message"] = "Product created: " + model.Name;
            CatalogCache.RefreshProducts();
            return RedirectToAction("Products");
        }

        [HttpGet]
        public ActionResult ProductEdit(int id)
        {
            var product = db.Products.Find(id);
            if (product == null) return HttpNotFound();
            FillProductForm();
            ViewBag.Title = "Edit Product";
            return View("ProductForm", product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProductEdit(Product model)
        {
            var existing = db.Products.Find(model.Id);
            if (existing == null) return HttpNotFound();

            if (!ModelState.IsValid)
            {
                FillProductForm();
                ViewBag.Title = "Edit Product";
                return View("ProductForm", model);
            }

            TryUpdateModel(existing, new[]
            {
                "Name", "ShortDescription", "Description", "Price", "ComparePrice", "Stock",
                "CategoryId", "BrandId", "ImageUrl", "ImageUrl2", "ImageUrl3", "Colors",
                "Sizes", "Highlights", "IsFeatured", "IsDeal", "IsNew"
            });

            PrepareProduct(existing, model.Id);
            try
            {
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Could not save product: " + ex.Message);
                FillProductForm();
                ViewBag.Title = "Edit Product";
                return View("ProductForm", model);
            }

            TempData["Message"] = "Product updated: " + existing.Name;
            CatalogCache.RefreshProducts();
            return RedirectToAction("Products");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProductDelete(int id)
        {
            var product = db.Products.Find(id);
            if (product != null)
            {
                var name = product.Name;
                db.Products.Remove(product);
                db.SaveChanges();
                TempData["Message"] = "Deleted: " + name;
            }
            CatalogCache.RefreshProducts();
            return RedirectToAction("Products");
        }

        public ActionResult Orders(string q, int page = 1, string status = null)
        {
            var model = new AdminOrdersViewModel { Q = q, Status = status, Page = page < 1 ? 1 : page };
            var query = db.Orders.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(o => o.OrderNumber.Contains(term) || o.FullName.Contains(term) || o.Email.Contains(term));
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                OrderStatus parsed;
                if (Enum.TryParse(status, out parsed))
                {
                    query = query.Where(o => o.Status == parsed);
                }
            }

            model.TotalCount = query.Count();
            model.Orders = query.OrderByDescending(o => o.CreatedOn)
                .Skip((model.Page - 1) * model.PageSize)
                .Take(model.PageSize)
                .ToList();

            ViewBag.Title = "Manage Orders";
            return View(model);
        }

        [HttpGet]
        public ActionResult OrderDetails(int id)
        {
            var order = db.Orders.Include("Items").Include("User").FirstOrDefault(o => o.Id == id);
            if (order == null) return HttpNotFound();
            ViewBag.Title = "Order " + order.OrderNumber;
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateOrderStatus(int id, string status)
        {
            var order = db.Orders.Find(id);
            if (order == null) return HttpNotFound();
            OrderStatus parsed;
            if (Enum.TryParse(status, out parsed))
            {
                order.Status = parsed;
                db.SaveChanges();
                TempData["Message"] = "Order " + order.OrderNumber + " marked as " + parsed + ".";
            }
            return RedirectToAction("OrderDetails", new { id });
        }

        public ActionResult Customers(string q, int page = 1)
        {
            var model = new AdminCustomersViewModel { Q = q, Page = page < 1 ? 1 : page };
            var query = db.Users.Where(u => u.Email != "admin@novakart.in").AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(u => u.Email.Contains(term) || u.FullName.Contains(term) || (u.Phone != null && u.Phone.Contains(term)));
            }

            model.TotalCount = query.Count();
            var rows = query
                .OrderBy(u => u.Id)
                .Skip((model.Page - 1) * model.PageSize)
                .Take(model.PageSize)
                .ToList();

            model.Customers = rows.Select(u => new AdminCustomerRow
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                CreatedOn = u.CreatedOn,
                OrderCount = db.Orders.Count(o => o.UserId == u.Id && o.Status != OrderStatus.Cancelled),
                TotalSpent = db.Orders
                    .Where(o => o.UserId == u.Id && o.Status != OrderStatus.Cancelled)
                    .Select(o => (decimal?)o.Total).Sum() ?? 0
            }).ToList();

            ViewBag.Title = "Customers";
            return View(model);
        }

        public ActionResult Coupons(string q, int page = 1)
        {
            var model = new AdminCouponsViewModel { Q = q, Page = page < 1 ? 1 : page };
            var query = db.Coupons.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(c => c.Code.Contains(term) || c.Description.Contains(term));
            }

            model.TotalCount = query.Count();
            model.Coupons = query.OrderBy(c => c.Code)
                .Skip((model.Page - 1) * model.PageSize)
                .Take(model.PageSize)
                .ToList();

            ViewBag.Title = "Manage Coupons";
            return View(model);
        }

        [HttpGet]
        public ActionResult CouponCreate()
        {
            ViewBag.Title = "Add Coupon";
            return View("CouponForm", new CouponViewModel
            {
                IsActive = true,
                MinOrderValue = 0,
                MaxDiscountAmount = 0,
                StartsOn = DateTime.Now
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CouponCreate(CouponViewModel model)
        {
            ViewBag.Title = "Add Coupon";
            if (!ModelState.IsValid) return View("CouponForm", model);

            var code = model.Code.Trim().ToUpperInvariant();
            if (db.Coupons.Any(c => c.Code == code))
            {
                ModelState.AddModelError("Code", "A coupon with this code already exists.");
                return View("CouponForm", model);
            }
            if (!model.FixedAmount.HasValue && (!model.PercentDiscount.HasValue || model.PercentDiscount.Value <= 0))
            {
                ModelState.AddModelError("", "Set either a fixed discount amount or a percentage discount.");
                return View("CouponForm", model);
            }

            var coupon = new Coupon { CreatedOn = DateTime.Now };
            ApplyCouponModel(coupon, model);
            db.Coupons.Add(coupon);
            db.SaveChanges();

            TempData["Message"] = "Coupon created: " + coupon.Code;
            return RedirectToAction("Coupons");
        }

        [HttpGet]
        public ActionResult CouponEdit(int id)
        {
            var coupon = db.Coupons.Find(id);
            if (coupon == null) return HttpNotFound();
            ViewBag.Title = "Edit Coupon";
            return View("CouponForm", new CouponViewModel
            {
                Id = coupon.Id,
                Code = coupon.Code,
                Description = coupon.Description,
                MinOrderValue = coupon.MinOrderValue,
                FixedAmount = coupon.FixedAmount,
                PercentDiscount = coupon.PercentDiscount,
                MaxDiscountAmount = coupon.MaxDiscountAmount,
                FreeShipping = coupon.FreeShipping,
                IsActive = coupon.IsActive,
                StartsOn = coupon.StartsOn,
                EndsOn = coupon.EndsOn
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CouponEdit(CouponViewModel model)
        {
            ViewBag.Title = "Edit Coupon";
            var coupon = db.Coupons.Find(model.Id);
            if (coupon == null) return HttpNotFound();
            if (!ModelState.IsValid) return View("CouponForm", model);

            var code = model.Code.Trim().ToUpperInvariant();
            if (db.Coupons.Any(c => c.Code == code && c.Id != coupon.Id))
            {
                ModelState.AddModelError("Code", "A coupon with this code already exists.");
                return View("CouponForm", model);
            }
            if (!model.FixedAmount.HasValue && (!model.PercentDiscount.HasValue || model.PercentDiscount.Value <= 0))
            {
                ModelState.AddModelError("", "Set either a fixed discount amount or a percentage discount.");
                return View("CouponForm", model);
            }

            ApplyCouponModel(coupon, model);
            db.SaveChanges();

            TempData["Message"] = "Coupon updated: " + coupon.Code;
            return RedirectToAction("Coupons");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CouponDelete(int id)
        {
            var coupon = db.Coupons.Find(id);
            if (coupon != null)
            {
                var code = coupon.Code;
                db.Coupons.Remove(coupon);
                db.SaveChanges();
                TempData["Message"] = "Coupon deleted: " + code;
            }
            return RedirectToAction("Coupons");
        }

        private void ApplyCouponModel(Coupon target, CouponViewModel m)
        {
            target.Code = m.Code.Trim().ToUpperInvariant();
            target.Description = (m.Description ?? "").Trim();
            target.MinOrderValue = m.MinOrderValue;
            target.FixedAmount = m.FixedAmount;
            target.PercentDiscount = m.PercentDiscount;
            target.MaxDiscountAmount = m.MaxDiscountAmount;
            target.FreeShipping = m.FreeShipping;
            target.IsActive = m.IsActive;
            target.StartsOn = m.StartsOn ?? DateTime.Now;
            target.EndsOn = m.EndsOn;
        }

        private void FillProductForm()
        {
            ViewBag.Categories = new SelectList(db.Categories.OrderBy(c => c.DisplayOrder).ToList(), "Id", "Name");
            ViewBag.Brands = new SelectList(db.Brands.OrderBy(b => b.Name).ToList(), "Id", "Name");
        }

        private void PrepareProduct(Product target, int? incomingId)
        {
            if (string.IsNullOrWhiteSpace(target.Slug))
            {
                target.Slug = MakeUniqueSlug(EcommerceSeeder.Slugify(target.Name), 0);
            }
            else
            {
                target.Slug = MakeUniqueSlug(target.Slug, target.Id);
            }

            if (string.IsNullOrWhiteSpace(target.Sku))
            {
                target.Sku = "NK" + DateTime.Now.ToString("yyMMddHHmmss") + new Random().Next(10, 99);
            }
            else if (db.Products.Any(p => p.Sku == target.Sku && p.Id != target.Id))
            {
                target.Sku = target.Sku + "-" + new Random().Next(100, 999);
            }

            if (target.Price <= 0) target.Price = 1;
            if (target.ComparePrice.HasValue && target.ComparePrice.Value <= target.Price)
            {
                target.ComparePrice = null;
            }
            if (target.Id == 0)
            {
                target.CreatedOn = DateTime.Now;
            }
            if (string.IsNullOrWhiteSpace(target.ImageUrl))
            {
                var cat = db.Categories.Find(target.CategoryId);
                var slug = cat != null ? cat.Slug : "electronics";
                target.ImageUrl = "/Content/images/products/" + slug + "-v0.svg";
            }
        }

        private string MakeUniqueSlug(string baseSlug, int currentId)
        {
            var slug = baseSlug;
            int n = 2;
            while (db.Products.Any(p => p.Slug == slug && p.Id != currentId))
            {
                slug = baseSlug + "-" + n++;
            }
            return slug;
        }
    }

    public class AdminOrdersViewModel
    {
        public List<Order> Orders { get; set; }
        public string Q { get; set; }
        public string Status { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get { return PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize); } }

        public AdminOrdersViewModel()
        {
            Orders = new List<Order>();
            Page = 1;
            PageSize = 15;
        }
    }
}
