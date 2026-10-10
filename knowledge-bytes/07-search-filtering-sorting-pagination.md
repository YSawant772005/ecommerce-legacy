# 07 - Search, Filtering, Sorting & Pagination

**Goal of this topic:** understand how browsing, searching and paging work — and why the filtering is done in memory.

---

### Byte 45: The products index entry point

**Builds on:** [05 - Product Catalog](05-product-catalog.md).
**Source file(s):** `Controllers/ProductsController.cs:37`

**In plain terms:** One action receives all the query-string options for the listing.

**The code:**

```csharp
// Controllers/ProductsController.cs:37
public ActionResult Index(string slug = null, string q = null, string sort = null, int page = 1,
    int pageSize = 24, string mode = null, string brand = null, decimal? min = null,
    decimal? max = null, double? rating = null)
{
    bool missing;
    var model = BuildCatalog(new FilterState { Slug = slug, Q = q, Sort = sort, Brand = brand,
        Min = min, Max = max, Rating = rating, Page = page, PageSize = pageSize, Mode = mode },
        out missing);
    if (missing) return HttpNotFound();
    ...
    return View(model);
}
```

**What's happening:** The parameters mirror the query string. They are packed into a private `FilterState` object and passed to `BuildCatalog`. An unknown category slug sets `missing`, which returns a 404.

**Why it matters:** `pageSize` is validated inside `BuildCatalog` (only 12, 24 or 48 allowed), and `mode` only accepts "pages" (anything else becomes "scroll"). This is input sanitisation on a GET endpoint.

**Trace it further:** Byte 49 (pagination).

---

### Byte 46: Live search suggestions

**Builds on:** Byte 45.
**Source file(s):** `Controllers/ProductsController.cs:87`, `Scripts/site.js:140`

**In plain terms:** As you type in the search box, the site asks the server for up to six matching products.

**The code:**

```csharp
// Controllers/ProductsController.cs:87
public ActionResult Suggest(string q)
{
    if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        return Json(new { items = new object[0] }, JsonRequestBehavior.AllowGet);

    var term = q.Trim().ToLowerInvariant();
    var items = CatalogCache.Products
        .Where(p => (p.Name != null && p.Name.ToLowerInvariant().Contains(term))
                 || (p.Brand != null && p.Brand.Name.ToLowerInvariant().Contains(term))
                 || (p.Category != null && p.Category.Name.ToLowerInvariant().Contains(term)))
        .OrderByDescending(p => p.SoldCount).Take(6)
        .Select(p => new { name = p.Name, slug = p.Slug, price = p.Price, image = p.ImageUrl, category = p.Category.Name })
        .ToList();
    return Json(new { items }, JsonRequestBehavior.AllowGet);
}
```

**What's happening:** It searches the in-memory catalog by name, brand and category, returns the 6 best-selling matches as JSON, and `site.js` debounces input (160 ms) and renders the dropdown with keyboard navigation.

**Why it matters:** The suggestion endpoint does a linear scan over ~2,500 objects per keystroke. It works at this scale but would not for a large catalog — a good illustration of the trade-off made by `CatalogCache`.

**Trace it further:** [15 - Frontend: Views, JavaScript & CSS](15-frontend-views-javascript-css.md).

---

### Byte 47: Filtering — the `BuildCatalog` pipeline

**Builds on:** Byte 45.
**Source file(s):** `Controllers/ProductsController.cs:253`

**In plain terms:** BuildCatalog resolves the category, computes facet counts, filters, sorts and pages — all in memory.

**The code:**

```csharp
// Controllers/ProductsController.cs:253 (excerpt)
var products = CatalogCache.Products;              // full list in memory
model.CategoryCounts = products.GroupBy(p => p.CategoryId) ...;   // facet counts
model.Brands = CatalogCache.Brands.Where(...).OrderBy(b => b.Name).ToList();
model.PriceFloor = CatalogCache.PriceFloor;
model.PriceCeil  = CatalogCache.PriceCeil;

var query = ApplyFiltersInMemory(products, f, out resolved);       // WHERE
model.TotalCount = query.Count;

switch (model.Sort) { /* order by price/rating/created/discount/sold */ }

model.Products = query.Skip((model.Page - 1) * model.PageSize)
                      .Take(model.PageSize).ToList();              // paging
```

**What's happening:** It starts from the cached product list, computes category and brand facets, applies filters (`ApplyFiltersInMemory`), sorts with a `switch` on the sort key, then `Skip`/`Take` for the page.

**Why it matters:** Because everything is LINQ-to-Objects, adding a filter is easy but it scans the whole list each request. The sort `switch` supports `popular` (default), `new`, `rating`, `price-asc`, `price-desc` and `discount`.

**Trace it further:** [04 - Models & ViewModels](04-models-and-viewmodels.md) (`CatalogViewModel`).

---

### Byte 48: `ApplyFiltersInMemory`

**Builds on:** Byte 47.
**Source file(s):** `Controllers/ProductsController.cs:212`

**In plain terms:** The actual filter rules: category, keyword, brand list, price range and minimum rating.

**The code:**

```csharp
// Controllers/ProductsController.cs:212 (excerpt)
if (!string.IsNullOrWhiteSpace(f.Q)) {
    var term = f.Q.Trim();
    query = query.Where(p => p.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
        || (p.ShortDescription != null && p.ShortDescription.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
        || (p.Brand != null && p.Brand.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
        || (p.Category != null && p.Category.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
}
if (!string.IsNullOrWhiteSpace(f.Brand)) { /* list.Contains(p.Brand.Slug) */ }
if (f.Min.HasValue) query = query.Where(p => p.Price >= f.Min.Value).ToList();
if (f.Max.HasValue) query = query.Where(p => p.Price <= f.Max.Value).ToList();
if (f.Rating.HasValue && f.Rating.Value > 0) query = query.Where(p => p.Rating >= f.Rating.Value).ToList();
```

**What's happening:** Keyword search is a case-insensitive `IndexOf` across name, short description, brand and category. Brands come in as a comma-separated slug list. Price and rating are simple numeric comparisons. Each step calls `.ToList()` (materialising the sequence).

**Why it matters:** Calling `.ToList()` between steps is unnecessary and slightly wasteful, but correct. Note the search is "contains", so `q=phone` matches "Headphone" too.

**Trace it further:** Byte 46.

---

### Byte 49: Dual pagination — infinite scroll vs pages

**Builds on:** Byte 47.
**Source file(s):** `Controllers/ProductsController.cs:63`, `Scripts/catalog.js:45`, `Views/Products/Index.cshtml:139`

**In plain terms:** You can browse by scrolling forever or by clicking page numbers; the server supports both.

**The code:**

```cshtml
@* Views/Products/Index.cshtml — the more-URL is baked into the DOM *@
<div class="results" id="catalog"
     data-more-url="@Url.Action("More", "Products", new { slug = Model.CategorySlug, q = Model.Q,
        sort = Model.Sort, brand = brandParam, min = Model.MinPrice, max = Model.MaxPrice,
        rating = Model.MinRating, pageSize = Model.PageSize })"
     data-mode="@Model.Mode" data-page="@Model.Page" data-totalpages="@totalPages">
```

```javascript
// Scripts/catalog.js:45 — fetch the next page and append it
var moreUrl = catalog.getAttribute('data-more-url');
...
Nova.get(moreUrl + '&page=' + (page + 1)).then(function (res) {
    var frag = document.createElement('div');
    frag.innerHTML = res.html;
    while (frag.firstChild) grid.appendChild(frag.firstChild);
    ...
});
```

**What's happening:**
- **Scroll mode:** an `IntersectionObserver` watches `#scrollSentinel`; when it nears the viewport, `catalog.js` calls `More` and appends the returned `_ProductGrid` HTML.
- **Pages mode:** the normal `?page=N` links are shown; `More` is not used.
- The chosen mode is remembered in `localStorage` and re-applied on the next visit.

**Why it matters:** Both modes ultimately render the same `_ProductGrid` partial, so the markup never diverges. `More` returns `hasMore`, which lets the client stop cleanly at the last page.

**Trace it further:** [05 - Product Catalog](05-product-catalog.md) (`_ProductGrid`).

---

### Byte 50: Sorting and page size controls

**Builds on:** Byte 49.
**Source file(s):** `Scripts/catalog.js:91`, `Views/Products/Index.cshtml:39`

**In plain terms:** The toolbar's dropdowns rewrite the URL's query string to re-filter.

**The code:**

```javascript
// Scripts/catalog.js:105
function nav(patch) {
    var params = new URLSearchParams(window.location.search);
    for (var k in patch) {
        if (patch[k] === null || patch[k] === '') params.delete(k);
        else params.set(k, patch[k]);
    }
    window.location.href = window.location.pathname + '?' + params.toString();
}
```

**What's happening:** Changing "Sort by" or "Show" calls `nav`, which merges the new values into the current query string and navigates. Switching mode also calls `nav({ mode: next, page: 1 })`. The server reads those parameters in `Index`.

**Why it matters:** This keeps the server as the single source of truth for filtering — the browser only manipulates query-string values and reloads. It also means every filter/sort/page state is bookmarkable and shareable.

**Trace it further:** [15 - Frontend: Views, JavaScript & CSS](15-frontend-views-javascript-css.md).

---

**Next topic →** [08 - Cart & Session Management](08-cart-and-session-management.md)
