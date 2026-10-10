# 06 - Product Details & Variants

**Goal of this topic:** understand the product page: gallery, variants, tabs, reviews, related products and SEO markup.

---

### Byte 40: The Details action

**Builds on:** [05 - Product Catalog](05-product-catalog.md).
**Source file(s):** `Controllers/ProductsController.cs:108`, `App_Start/RouteConfig.cs:24`

**In plain terms:** `/product/{slug}` loads one product plus its related products and approved reviews.

**The code:**

```csharp
// Controllers/ProductsController.cs:108
public ActionResult Details(string slug, string tab)
{
    var product = db.Products.Include(p => p.Brand).Include(p => p.Category)
        .FirstOrDefault(p => p.Slug == slug);
    if (product == null) return HttpNotFound();

    var related = db.Products.Include(p => p.Brand)
        .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id)
        .OrderByDescending(p => p.IsFeatured)
        .ThenByDescending(p => p.Rating)
        .ThenByDescending(p => p.SoldCount)
        .Take(8).ToList();

    var reviews = db.Reviews
        .Where(r => r.ProductId == product.Id && r.IsApproved)
        .OrderByDescending(r => r.CreatedOn).Take(12).ToList();
    ...
    return View(new ProductDetailViewModel { Product = product, Related = related,
        Reviews = reviews, ReviewCount = product.ReviewCount });
}
```

**What's happening:** It queries the product by slug, up to 8 related products from the same category (featured first), and up to 12 approved reviews. It also records the product in the "recently viewed" session list and sets `ViewBag.Tab` (to open the reviews tab when redirected after posting a review).

**Why it matters:** Only `IsApproved` reviews are shown, so moderation actually controls the storefront. Slug lookups use the unique `UX_Product_Slug` index.

**Trace it further:** [13 - Reviews & Moderation](13-reviews-and-moderation.md).

---

### Byte 41: The gallery and JSON-LD

**Builds on:** Byte 40.
**Source file(s):** `Views/Products/Details.cshtml:4`, `Views/Products/Details.cshtml:46`

**In plain terms:** The page builds a 3-image gallery and emits structured data for search engines.

**The code:**

```cshtml
@* Views/Products/Details.cshtml *@
@{
    var gallery = new List<string>();
    if (!string.IsNullOrWhiteSpace(p.ImageUrl))  gallery.Add(p.ImageUrl);
    if (!string.IsNullOrWhiteSpace(p.ImageUrl2)) gallery.Add(p.ImageUrl2);
    if (!string.IsNullOrWhiteSpace(p.ImageUrl3)) gallery.Add(p.ImageUrl3);
    if (gallery.Count == 0) gallery.Add(Url.Content(p.Category != null ? "~/Content/images/products/" + p.Category.Slug + "-v0.jpg" : "~/Content/images/placeholder.jpg"));

    var ld = "{\"@context\":\"https://schema.org\",\"@type\":\"Product\",…";
}
<script type="application/ld+json">@Html.Raw(ld)</script>
```

**What's happening:** Up to three images form the gallery (thumbnails plus a main image). A JSON-LD `Product` block is built with name, images, SKU, brand, category, `aggregateRating` (when `RatingCount > 0`) and an `offers` block with `priceCurrency: INR` and availability. It is emitted with `@Html.Raw` (because the string is already JSON-encoded) at line 55.

**Why it matters:** This is the app's structured-data/SEO feature. Note `@Html.Raw` is only safe here because every interpolated value is passed through `JavaScriptStringEncode` (the local `J` function) first — a good pattern to copy.

**Trace it further:** [17 - Contact, Newsletter & Static Pages](17-contact-newsletter-and-static-pages.md) (SEO routes).

---

### Byte 42: The variant picker

**Builds on:** Byte 40.
**Source file(s):** `Views/Products/Details.cshtml:123`, `Scripts/pdp.js:37`, `Scripts/site.js:54`

**In plain terms:** Some products have color swatches and size buttons; the selection is turned into a text label like "Black · L".

**The code:**

```cshtml
@* Details.cshtml — color swatches and size buttons *@
<button type="button" class="swatch-opt is-selected" data-swatch="@colors[ci]" data-label="@colors[ci]"></button>
...
<button type="button" class="size-opt is-selected" data-label="@sizes[si]">@sizes[si]</button>
```

```javascript
// Scripts/site.js:54 — read the chosen variant
var readVariant = function () {
    var picker = document.getElementById('variantPicker');
    if (!picker) return null;
    var c = picker.querySelector('.swatch-opt.is-selected');
    var s = picker.querySelector('.size-opt.is-selected');
    var parts = [];
    if (c) parts.push(c.getAttribute('data-label'));
    if (s) parts.push(s.getAttribute('data-label'));
    return parts.join(' \u00B7 ');   // e.g. "Black · L"
};
```

**What's happening:** The view renders buttons from the comma-separated `Colors`/`Sizes` strings. `pdp.js` colors the swatches (a hex map keyed by color name) and toggles `.is-selected` when clicked. When Add to Cart is clicked, `site.js` reads the selected labels and sends `variant` with the request.

**Why it matters:** Variants are cosmetic only — the price is the same regardless of variant, and stock is tracked per product, not per variant. The chosen variant text is stored on the cart line and appended to the product name on the order.

**Trace it further:** [08 - Cart & Session Management](08-cart-and-session-management.md).

---

### Byte 43: Tabs and reviews

**Builds on:** Byte 40.
**Source file(s):** `Views/Products/Details.cshtml:178`, `Scripts/pdp.js:21`

**In plain terms:** Description/Specifications/Reviews are client-side tabs; the review form posts to the server.

**The code:**

```javascript
// Scripts/pdp.js:21 — tab switching
tabs.addEventListener('click', function (e) {
    var btn = e.target.closest('[data-tab]');
    if (!btn) return;
    var name = btn.getAttribute('data-tab');
    // toggle .is-on on the tab heads and show #tab-<name>
});
```

**What's happening:** Tabs are pure CSS/JS (no page reload). The review form (rendered only for signed-in users) posts to `ProductsController.ReviewPost`, which validates, computes a verified-purchase flag, saves the review and refreshes `CatalogCache`.

**Why it matters:** Because the review tab is client-side, after posting the controller redirects to `Details` with `tab=reviews`, and a small inline script at the end of `Details.cshtml` clicks the reviews tab. This is how the app preserves tab state across the POST.

**Trace it further:** [13 - Reviews & Moderation](13-reviews-and-moderation.md).

---

### Byte 44: Buy Now versus Add to Cart

**Builds on:** Byte 42.
**Source file(s):** `Views/Products/Details.cshtml:79`, `Scripts/site.js:66`

**In plain terms:** Both buttons add to the cart, but "Buy Now" then jumps straight to checkout.

**The code:**

```javascript
// Scripts/site.js — Add to Cart vs Buy Now
Nova.post('/cart/add', { id: id, qty: 1, variant: variant }).then(function (res) {
    setCount('cartCount', res.count);
    Nova.toast('Added to cart: ' + res.name);
});

// data-buy:
Nova.post('/cart/add', { id: buyId, qty: 1, variant: variantB }).then(function (res) {
    if (!res.ok) { … }
    setCount('cartCount', res.count);
    window.location.href = '/checkout';
});
```

**What's happening:** `site.js` handles click events on `[data-add]` and `[data-buy]` via one delegated listener. Both call `/cart/add`; the buy branch then redirects to `/checkout`.

**Why it matters:** The buttons work even on dynamically-loaded content because the listener is on `document`, not on individual buttons. This is the same pattern as the wishlist toggle.

**Trace it further:** [08 - Cart & Session Management](08-cart-and-session-management.md).

---

**Next topic →** [07 - Search, Filtering, Sorting & Pagination](07-search-filtering-sorting-pagination.md)
