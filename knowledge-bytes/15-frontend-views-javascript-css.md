# 15 - Frontend: Views, JavaScript & CSS

**Goal of this topic:** understand the Razor layout, the shared helpers, and the small amount of JavaScript that powers interactivity.

---

### Byte 88: The layout and `_ViewStart`

**Builds on:** [04 - Models & ViewModels](04-models-and-viewmodels.md).
**Source file(s):** `Views/_ViewStart.cshtml`, `Views/Shared/_Layout.cshtml`

**In plain terms:** Every storefront page is wrapped by `_Layout.cshtml`, which sets up the header, navigation, footer and scripts.

**The code:**

```cshtml
@* Views/Shared/_Layout.cshtml:1 — data the layout needs on every page *@
@{
    var me = Auth.FromSession(Context.Session);
    var cartCount = SessionCart.Count(Context.Session);
    var wishList = Context.Session["NK.Wishlist"] as List<int> ?? new List<int>();
    var categories = CatalogCache.Categories;
    var isAdmin = me != null && me.Role == "Admin";
}
...
<script>window.__CART_COUNT__ = @cartCount;</script>
<script src="@Url.Content("~/Scripts/site.js")"></script>
@RenderSection("scripts", required: false)
```

**What's happening:** `_ViewStart` points every view at `_Layout`. The layout reads the current user, cart count, wishlist count and categories, renders the header/footer, and defines a `scripts` section that each view can fill (`@section scripts { … }`). It also emits the initial cart count to JavaScript.

**Why it matters:** Because `site.js` is loaded in the layout and uses delegated event listeners, it works on every page without per-page wiring. `@RenderSection("scripts", required: false)` is how pages opt in to their own script (`catalog.js`, `pdp.js`, etc.).

**Trace it further:** Byte 90.

---

### Byte 89: HTML helpers

**Builds on:** Byte 88.
**Source file(s):** `Infrastructure/HtmlExtensions.cs`, `Views/Products/Details.cshtml:96`

**In plain terms:** Small extension methods render prices and star ratings consistently.

**The code:**

```cshtml
@* Views/Products/Details.cshtml *@
@Html.Stars(p.Rating, p.RatingCount)
@Html.Price(p.Price)
@Html.Price(p.ComparePrice.Value, true)   @* struck-through compare price *@
```

**What's happening:** `HtmlExtensions` adds `Stars` (draws filled/half/empty stars from a decimal rating) and `Price` (formats using the `₹` sign and Indian `3,2,3` digit grouping; the second argument renders a struck-through style). Both are used across cards, reviews and the product page.

**Why it matters:** Centralising money and star formatting means the whole site stays consistent and the Indian numbering grouping is defined once. `Html.Raw` inside these helpers must be used carefully — see [16](16-validation-security-and-error-handling.md).

**Trace it further:** Byte 91.

---

### Byte 90: The `Nova` JavaScript namespace

**Builds on:** Byte 88.
**Source file(s):** `Scripts/site.js:4`, `Scripts/site.js:24`

**In plain terms:** `site.js` exposes a tiny helper object (`Nova`) with `post`, `get`, `fmt`, `toast` and `setCount`.

**The code:**

```javascript
// Scripts/site.js:24
Nova.post = function (url, data) {
    var body = [];
    for (var k in data) {
        if (Object.prototype.hasOwnProperty.call(data, k)) {
            body.push(encodeURIComponent(k) + '=' + encodeURIComponent(data[k]));
        }
    }
    return fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
        body: body.join('&'),
        credentials: 'same-origin'
    }).then(function (r) { return r.json(); });
};

Nova.get = function (url) {
    return fetch(url, { credentials: 'same-origin' }).then(function (r) { return r.json(); });
};
```

**What's happening:** `Nova.post` URL-encodes a plain object and POSTs it form-encoded with `fetch` (same-origin credentials), returning parsed JSON. `Nova.get` does the equivalent for GET. `fmt` formats rupees, `toast` shows a transient message, and `setCount` updates a badge with a "bump" animation.

**Why it matters:** These AJAX POSTs do **not** send an anti-forgery token — so the JSON endpoints they call must not require one. This is a deliberate (if subtle) design constraint linking the client and server security choices. See [16](16-validation-security-and-error-handling.md).

**Trace it further:** [07 - Search, Filtering, Sorting & Pagination](07-search-filtering-sorting-pagination.md).

---

### Byte 91: Delegated events and live updates

**Builds on:** Byte 90.
**Source file(s):** `Scripts/site.js:66`

**In plain terms:** One click listener at the document level handles every Add to Cart, Buy Now and wishlist heart.

**The code:**

```javascript
// Scripts/site.js:66
document.addEventListener('click', function (e) {
    var addBtn = e.target.closest ? e.target.closest('[data-add]') : null;
    if (addBtn && !addBtn.disabled) {
        e.preventDefault();
        var variant = readVariant();
        if (variant === '') { Nova.toast('Please choose a variant first', 'error'); return; }
        Nova.post('/cart/add', { id: addBtn.getAttribute('data-add'), qty: 1, variant: variant })
            .then(function (res) { setCount('cartCount', res.count); Nova.toast('Added to cart: ' + res.name); });
        return;
    }
    // ... [data-buy] and [data-wish] handled the same way ...
});
```

**What's happening:** Because the listener is attached to `document` and filters by `data-add`/`data-buy`/`data-wish`, any button matching those attributes works — including cards injected later by infinite scroll. The wishlist branch updates **all** hearts for that product id at once (`querySelectorAll('[data-wish="…"]')`).

**Why it matters:** Event delegation is the reason the same markup works on the catalog, product page and wishlist without extra wiring. When the selected variant is empty, the click is blocked with a toast — enforcing "choose a variant first" in the UI.

**Trace it further:** Byte 92.

---

### Byte 92: One script per page

**Builds on:** Byte 91.
**Source file(s):** `Scripts/cart.js`, `Scripts/catalog.js`, `Scripts/pdp.js`, `Scripts/checkout.js`

**In plain terms:** Each page loads exactly the script it needs through the `scripts` section.

**The code:**

```cshtml
@* Views/Products/Index.cshtml:192 *@
@section scripts {
    <script src="@Url.Content("~/Scripts/catalog.js")"></script>
}
```

| Script | Loaded by | Responsibility |
|--------|-----------|----------------|
| `catalog.js` | Products list | sort/size/mode controls, infinite scroll, facets |
| `pdp.js` | Product page | gallery thumbnails, tabs, swatch colours, variant state |
| `cart.js` | Cart page | quantity update, remove, move-to-wishlist |
| `checkout.js` | Address step | copy a saved address into the form |
| `site.js` | Layout (all pages) | cart/wishlist AJAX, search suggestions, toasts, header |

**What's happening:** `site.js` always runs; the page-specific script is added only by the page that needs it. This keeps other pages lighter (the product page never downloads `catalog.js`, for example).

**Why it matters:** Because `site.js` owns the generic interactions and page scripts own the specific ones, there are no duplicate IDs to conflict — each view only ships the code relevant to it.

**Trace it further:** [07](07-search-filtering-sorting-pagination.md), [08](08-cart-and-session-management.md), [09](09-checkout-and-coupons.md).

---

### Byte 93: CSS organisation

**Builds on:** Byte 88.
**Source file(s):** `Content/site.css`, `Content/admin.css`

**In plain terms:** The storefront is styled by one large `site.css`; the admin panel uses a separate `admin.css`.

**The code:**

```css
/* Content/site.css (excerpt) — CSS custom properties drive the theme */
:root {
    --brand: #6d28d9;
    --muted: #6b7280;
    /* ... */
}

.btn.btn-primary { background: var(--brand); color: #fff; }
```

**What's happening:** `site.css` (~2,000 lines) defines the theme through CSS custom properties (`:root { --brand: … }`) and uses utility/component classes (`.btn`, `.product-grid`, `.pdp-layout`, `.toast`, …). `admin.css` is loaded only by `_AdminLayout`.

**Why it matters:** Centralising colours as custom properties means the palette can be changed in one place. The two stylesheets never collide because admin pages don't load `site.css` and vice-versa. Layout is responsive; the header collapses for small screens.

**Trace it further:** [14 - Admin Dashboard & Management](14-admin-dashboard-and-management.md) (`_AdminLayout`).

---

**Next topic →** [16 - Validation, Security & Error Handling](16-validation-security-and-error-handling.md)
