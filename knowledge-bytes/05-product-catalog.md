# 05 - Product Catalog

**Goal of this topic:** understand how the home page and the product listing page are built.

---

### Byte 35: The Home page model

**Builds on:** [04 - Models & ViewModels](04-models-and-viewmodels.md).
**Source file(s):** `Controllers/HomeController.cs:21`, `Models/ViewModels/ViewModels.cs:380`

**In plain terms:** The home page gathers featured products, deals, new arrivals and some headline counts.

**The code:**

```csharp
// Controllers/HomeController.cs:21
public ActionResult Index()
{
    var model = new HomeViewModel
    {
        Categories = CatalogCache.Categories,
        Featured = db.Products.Where(p => p.IsFeatured)
            .OrderByDescending(p => p.SoldCount).Take(8).ToList(),
        Deals = db.Products.Where(p => p.IsDeal && p.ComparePrice != null)
            .OrderByDescending(p => p.SoldCount).Take(8).ToList(),
        NewArrivals = db.Products.Where(p => p.IsNew)
            .OrderByDescending(p => p.CreatedOn).Take(8).ToList(),
        ProductCount = db.Products.Count(),
        BrandCount = db.Brands.Count(),
        CustomerCount = db.Users.Count(u => u.Role == "Customer"),
        OrderCount = db.Orders.Count()
    };
    if (model.Featured.Count < 8)
        model.Featured = db.Products.OrderByDescending(p => p.SoldCount).Take(8).ToList();
    ViewBag.Title = "NovaKart - Online Shopping for Electronics, Fashion, Home & More";
    return View(model);
}
```

**What's happening:** It issues separate EF queries for featured/deal/new products (each `Take(8)`), falls back to bestsellers if fewer than 8 featured exist, and counts products, brands, customers and orders. Categories come from the cache.

**Why it matters:** Unlike the listing page, the home page queries the database directly (not `CatalogCache`). Each section is limited to 8 items, so it stays fast even with 2,500 products.

**Trace it further:** [15 - Frontend: Views, JavaScript & CSS](15-frontend-views-javascript-css.md) (the Home view).

---

### Byte 36: The product card partial

**Builds on:** Byte 35.
**Source file(s):** `Views/Shared/_ProductCard.cshtml`

**In plain terms:** One reusable tile renders a product everywhere: image, badges, rating, price, wishlist heart and Add to Cart.

**The code:**

```cshtml
@* Views/Shared/_ProductCard.cshtml *@
@model LegacyEcommerce.Models.Product
@{
    var wish = Context.Session["NK.Wishlist"] as List<int> ?? new List<int>();
    var wished = wish.Contains(Model.Id);
    var detailsUrl = Url.Action("Details", "Products", new { slug = Model.Slug });
}
<article class="card" data-id="@Model.Id">
    <div class="card-badges">
        @if (!Model.InStock) { <span class="chip chip-out">Sold out</span> }
        else if (Model.IsDeal && Model.DiscountPercent >= 15) { <span class="chip chip-deal">@Model.DiscountPercent% off</span> }
        @if (Model.InStock && !Model.IsDeal && Model.IsNew) { <span class="chip chip-new">New</span> }
        @if (Model.SoldCount > 6000) { <span class="chip chip-best">Bestseller</span> }
    </div>
    <button type="button" class="wish-btn @(wished ? "is-on" : "")" data-wish="@Model.Id">…</button>
    <a class="card-media" href="@detailsUrl"><img src="@Model.ImageUrl" alt="@Model.Name" loading="lazy" /></a>
    …
    <button type="button" class="btn btn-cart-add" data-add="@Model.Id">Add to Cart</button>
</article>
```

**What's happening:** The partial reads the wishlist session list to decide the heart state. It renders the `data-add` / `data-wish` attributes that `Scripts/site.js` listens for (event delegation). Price/rating come from the `@Html.Price` / `@Html.Stars` helpers.

**Why it matters:** Because `site.js` uses `document.addEventListener('click', …)` on the whole document, every card's buttons work without per-card script setup. The wishlist state is computed server-side, but toggled client-side by AJAX.

**Trace it further:** [12 - Wishlist](12-wishlist.md), [15 - Frontend: Views, JavaScript & CSS](15-frontend-views-javascript-css.md).

---

### Byte 37: The product grid partial

**Builds on:** Byte 36.
**Source file(s):** `Views/Shared/_ProductGrid.cshtml`, `Controllers/ProductsController.cs:75`

**In plain terms:** `_ProductGrid` renders a list of cards and is reused both on first load and by the "load more" AJAX endpoint.

**The code:**

```csharp
// Controllers/ProductsController.cs:75 — the More endpoint renders the partial to a string
var html = PartialViewRenderer.Render(ControllerContext, "_ProductGrid", model.Products);
return Json(new { html, page = model.Page, hasMore = model.Page < model.TotalPages,
                  total = model.TotalCount, from = model.From, to = model.To },
            JsonRequestBehavior.AllowGet);
```

**What's happening:** `More` builds the same catalog model for page N and renders `_ProductGrid` to an HTML string using `PartialViewRenderer` (`Infrastructure/HtmlExtensions.cs`), then returns that HTML as JSON. The browser appends it to the grid.

**Why it matters:** This is a "server renders HTML fragments" pattern (no client templating). It also means the partial must render correctly on its own — a common source of bugs when someone adds a `@section` or depends on `ViewBag`.

**Trace it further:** [07 - Search, Filtering, Sorting & Pagination](07-search-filtering-sorting-pagination.md).

---

### Byte 38: Category landing pages

**Builds on:** [02 - Application Startup & Request Flow](02-application-startup-and-request-flow.md).
**Source file(s):** `App_Start/RouteConfig.cs:30`, `Controllers/ProductsController.cs:37`

**In plain terms:** `/c/{slug}` is just the products index filtered to a category, with its own title and description.

**The code:**

```csharp
// Controllers/ProductsController.cs:50
if (!string.IsNullOrWhiteSpace(slug))
{
    var cat = CatalogCache.Categories.FirstOrDefault(c => c.Slug == slug);
    if (cat != null)
        ViewBag.MetaDescription = !string.IsNullOrEmpty(cat.Description)
            ? cat.Description : "Shop " + cat.Name + " at NovaKart.";
}
```

**What's happening:** The route `c/{slug}` maps to `ProductsController.Index` with the `slug` parameter. `BuildCatalog` resolves the category from `CatalogCache.Categories`; if the slug is unknown, `HttpNotFound()` is returned. The layout's nav links (`_Layout.cshtml`) build these URLs for every active category.

**Why it matters:** One controller action serves "all products", search and "category", which keeps the browsing logic in a single place — but also makes that method the most complex in the project.

**Trace it further:** [07 - Search, Filtering, Sorting & Pagination](07-search-filtering-sorting-pagination.md).

---

### Byte 39: Recently viewed

**Builds on:** Byte 36.
**Source file(s):** `Controllers/ProductsController.cs:138`, `Views/Shared/_RecentlyViewed.cshtml`

**In plain terms:** The app remembers the last ten products you opened (in session) and shows them again.

**The code:**

```csharp
// Controllers/ProductsController.cs:138 (inside Details)
var recent = Session["NK.Recently"] as List<int> ?? new List<int>();
recent.Remove(product.Id);
recent.Insert(0, product.Id);
if (recent.Count > 10) recent = recent.Take(10).ToList();
Session["NK.Recently"] = recent;
ViewBag.RecentlyProducts = LoadRecentlyViewed();
```

**What's happening:** Each product visit pushes the product id to the front of a session list capped at 10. `LoadRecentlyViewed` loads the matching products and re-orders them by the list order, then `_RecentlyViewed.cshtml` renders them.

**Why it matters:** "Recently viewed" is session-only, so it is lost when the session ends. It is a good example of the app's session state pattern alongside the cart and wishlist.

**Trace it further:** [08 - Cart & Session Management](08-cart-and-session-management.md).

---

**Next topic →** [06 - Product Details & Variants](06-product-details-and-variants.md)
