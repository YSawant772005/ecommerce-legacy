using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using LegacyEcommerce.Data;
using LegacyEcommerce.Infrastructure;
using LegacyEcommerce.Models;
using LegacyEcommerce.Models.ViewModels;

namespace LegacyEcommerce.Controllers
{
    public class ProductsController : Controller
    {
        private readonly StoreContext db = new StoreContext();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        private class FilterState
        {
            public string Slug;
            public string Q;
            public string Sort;
            public string Brand;
            public decimal? Min;
            public decimal? Max;
            public double? Rating;
            public int Page;
            public int PageSize;
            public string Mode;
        }

        public ActionResult Index(string slug = null, string q = null, string sort = null, int page = 1,
            int pageSize = 24, string mode = null, string brand = null, decimal? min = null,
            decimal? max = null, double? rating = null)
        {
            bool missing;
            var model = BuildCatalog(new FilterState
            {
                Slug = slug, Q = q, Sort = sort, Brand = brand, Min = min, Max = max,
                Rating = rating, Page = page, PageSize = pageSize, Mode = mode
            }, out missing);

            if (missing) return HttpNotFound();

            if (!string.IsNullOrWhiteSpace(slug))
            {
                var cat = CatalogCache.Categories.FirstOrDefault(c => c.Slug == slug);
                if (cat != null)
                {
                    ViewBag.MetaDescription = !string.IsNullOrEmpty(cat.Description) ? cat.Description : "Shop " + cat.Name + " at NovaKart.";
                }
            }
            ViewBag.RecentlyProducts = LoadRecentlyViewed();
            return View(model);
        }

        [HttpGet]
        public ActionResult More(string slug = null, string q = null, string sort = null, int page = 2,
            int pageSize = 24, string brand = null, decimal? min = null,
            decimal? max = null, double? rating = null)
        {
            if (page < 1) page = 1;
            bool missing;
            var model = BuildCatalog(new FilterState
            {
                Slug = slug, Q = q, Sort = sort, Brand = brand, Min = min, Max = max,
                Rating = rating, Page = page, PageSize = pageSize, Mode = "scroll"
            }, out missing);

            var html = PartialViewRenderer.Render(ControllerContext, "_ProductGrid", model.Products);
            return Json(new
            {
                html,
                page = model.Page,
                hasMore = model.Page < model.TotalPages,
                total = model.TotalCount,
                from = model.From,
                to = model.To
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult Suggest(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            {
                return Json(new { items = new object[0] }, JsonRequestBehavior.AllowGet);
            }

            var term = q.Trim().ToLowerInvariant();
            var items = CatalogCache.Products
                .Where(p => (p.Name != null && p.Name.ToLowerInvariant().Contains(term))
                    || (p.Brand != null && p.Brand.Name.ToLowerInvariant().Contains(term))
                    || (p.Category != null && p.Category.Name.ToLowerInvariant().Contains(term)))
                .OrderByDescending(p => p.SoldCount)
                .Take(6)
                .Select(p => new { name = p.Name, slug = p.Slug, price = p.Price, image = p.ImageUrl, category = p.Category.Name })
                .ToList();

            return Json(new { items }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Details(string slug, string tab)
        {
            var product = db.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .FirstOrDefault(p => p.Slug == slug);

            if (product == null) return HttpNotFound();

            var related = db.Products
                .Include(p => p.Brand)
                .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id)
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.Rating)
                .ThenByDescending(p => p.SoldCount)
                .Take(8)
                .ToList();

            var reviews = db.Reviews
                .Where(r => r.ProductId == product.Id)
                .OrderByDescending(r => r.CreatedOn)
                .Take(12)
                .ToList();

            ViewBag.Title = product.Name;
            ViewBag.Breadcrumb = product.Category.Name;
            ViewBag.Tab = tab;
            ViewBag.MetaDescription = string.IsNullOrEmpty(product.ShortDescription) ? "Buy " + product.Name + " at NovaKart." : product.ShortDescription;
            ViewBag.Canonical = "/product/" + product.Slug;

            var recent = Session["NK.Recently"] as List<int> ?? new List<int>();
            recent.Remove(product.Id);
            recent.Insert(0, product.Id);
            if (recent.Count > 10)
            {
                recent = recent.Take(10).ToList();
            }
            Session["NK.Recently"] = recent;
            ViewBag.RecentlyProducts = LoadRecentlyViewed();

            return View(new ProductDetailViewModel
            {
                Product = product,
                Related = related,
                Reviews = reviews,
                ReviewCount = product.ReviewCount
            });
        }

        private List<Product> LoadRecentlyViewed()
        {
            var recent = Session["NK.Recently"] as List<int>;
            if (recent == null || recent.Count == 0) return new List<Product>();
            return db.Products
                .Include(p => p.Brand)
                .Where(p => recent.Contains(p.Id))
                .ToList()
                .OrderBy(p => recent.IndexOf(p.Id))
                .ToList();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireLogin]
        public ActionResult ReviewPost(ReviewViewModel model)
        {
            if (model.ProductId <= 0) return HttpNotFound();
            var product = db.Products.Find(model.ProductId);
            if (product == null) return HttpNotFound();

            if (!ModelState.IsValid)
            {
                TempData["Message"] = "Please pick a star rating and fill in your review.";
                return RedirectToAction("Details", new { slug = product.Slug, tab = "reviews" });
            }

            var me = Auth.FromSession(Session);
            var verified = db.OrderItems
                .Any(i => i.ProductId == product.Id && i.Order.UserId == me.Id && i.Order.Status == OrderStatus.Delivered);

            db.Reviews.Add(new Review
            {
                ProductId = product.Id,
                AuthorName = me != null ? me.Name : "Guest",
                Rating = model.Rating,
                Title = model.Title.Trim(),
                Body = model.Body.Trim(),
                VerifiedPurchase = verified,
                CreatedOn = DateTime.Now
            });

            var totalScore = product.Rating * product.RatingCount + model.Rating;
            product.RatingCount = product.RatingCount + 1;
            product.Rating = Math.Round(totalScore / product.RatingCount, 1);
            product.ReviewCount = product.ReviewCount + 1;

            db.SaveChanges();
            CatalogCache.RefreshProducts();

            TempData["Message"] = "Thanks! Your review has been published.";
            return RedirectToAction("Details", new { slug = product.Slug, tab = "reviews" });
        }

        private List<Product> ApplyFiltersInMemory(IEnumerable<Product> products, FilterState f, out Category category)
        {
            Category cat = null;
            if (!string.IsNullOrWhiteSpace(f.Slug))
            {
                cat = CatalogCache.Categories.FirstOrDefault(c => c.Slug == f.Slug);
                if (cat != null)
                {
                    products = products.Where(p => p.CategoryId == cat.Id);
                }
            }

            var query = products.ToList();

            if (!string.IsNullOrWhiteSpace(f.Q))
            {
                var term = f.Q.Trim();
                query = query.Where(p => p.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
                    || (p.ShortDescription != null && p.ShortDescription.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (p.Brand != null && p.Brand.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (p.Category != null && p.Category.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(f.Brand))
            {
                var list = f.Brand.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                if (list.Count > 0)
                {
                    query = query.Where(p => p.Brand != null && list.Contains(p.Brand.Slug)).ToList();
                }
            }

            if (f.Min.HasValue) query = query.Where(p => p.Price >= f.Min.Value).ToList();
            if (f.Max.HasValue) query = query.Where(p => p.Price <= f.Max.Value).ToList();
            if (f.Rating.HasValue && f.Rating.Value > 0) query = query.Where(p => p.Rating >= f.Rating.Value).ToList();

            category = cat;
            return query;
        }

        private CatalogViewModel BuildCatalog(FilterState f, out bool categoryMissing)
        {
            categoryMissing = false;
            var model = new CatalogViewModel
            {
                Q = f.Q,
                Sort = string.IsNullOrWhiteSpace(f.Sort) ? "popular" : f.Sort,
                Mode = f.Mode == "pages" ? "pages" : "scroll",
                MinPrice = f.Min,
                MaxPrice = f.Max,
                MinRating = f.Rating,
                BrandSlugs = (f.Brand ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToLowerInvariant()).ToList(),
                PageSize = f.PageSize == 12 || f.PageSize == 48 ? f.PageSize : 24,
                Page = f.Page < 1 ? 1 : f.Page
            };

            var products = CatalogCache.Products;

            Category resolved = null;
            if (!string.IsNullOrWhiteSpace(f.Slug))
            {
                resolved = CatalogCache.Categories.FirstOrDefault(c => c.Slug == f.Slug);
                if (resolved == null)
                {
                    categoryMissing = true;
                    model.CategorySlug = f.Slug;
                    model.Categories = CatalogCache.Categories;
                    return model;
                }
            }

            model.Categories = CatalogCache.Categories;
            model.CategorySlug = f.Slug;
            model.CategoryName = resolved != null ? resolved.Name : null;
            model.Title = !string.IsNullOrWhiteSpace(model.Q)
                ? "Results for \u201C" + model.Q + "\u201D"
                : resolved != null ? resolved.Name : "All Products";

            var all = products;
            model.CategoryCounts = all
                .GroupBy(p => p.CategoryId)
                .Select(g => new { Id = g.Key, N = g.Count() })
                .ToDictionary(g => g.Id, g => g.N)
                .Where(kv => kv.Key > 0)
                .ToDictionary(kv => CatalogCache.Categories.FirstOrDefault(c => c.Id == kv.Key) != null
                    ? CatalogCache.Categories.First(c => c.Id == kv.Key).Slug
                    : kv.Key.ToString(), kv => kv.Value);

            var brandCounts = all
                .GroupBy(p => p.BrandId)
                .Select(g => new { Id = g.Key, N = g.Count() })
                .ToList();
            model.Brands = CatalogCache.Brands
                .Where(b => brandCounts.Any(bc => bc.Id == b.Id))
                .OrderBy(b => b.Name)
                .ToList();

            model.PriceFloor = CatalogCache.PriceFloor;
            model.PriceCeil = CatalogCache.PriceCeil;

            var query = ApplyFiltersInMemory(products, f, out resolved);
            model.TotalCount = query.Count;

            switch (model.Sort)
            {
                case "price-asc":
                    query = query.OrderBy(p => p.Price).ToList();
                    break;
                case "price-desc":
                    query = query.OrderByDescending(p => p.Price).ToList();
                    break;
                case "rating":
                    query = query.OrderByDescending(p => p.Rating).ThenByDescending(p => p.RatingCount).ToList();
                    break;
                case "new":
                    query = query.OrderByDescending(p => p.CreatedOn).ToList();
                    break;
                case "discount":
                    query = query.OrderByDescending(p => p.ComparePrice == null ? 0 : p.ComparePrice - p.Price).ToList();
                    break;
                default:
                    query = query.OrderByDescending(p => p.SoldCount).ToList();
                    break;
            }

            var totalPages = (int)Math.Ceiling(model.TotalCount / (double)model.PageSize);
            if (totalPages < 1) totalPages = 1;
            if (model.Page > totalPages) model.Page = totalPages;

            model.Products = query
                .Skip((model.Page - 1) * model.PageSize)
                .Take(model.PageSize)
                .ToList();

            return model;
        }
    }
}
