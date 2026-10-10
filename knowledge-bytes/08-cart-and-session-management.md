# 08 - Cart & Session Management

**Goal of this topic:** understand how the cart works for guests and signed-in users, and how the app keeps session and database in sync.

---

### Byte 51: `SessionCart` — the guest cart

**Builds on:** [04 - Models & ViewModels](04-models-and-viewmodels.md).
**Source file(s):** `Infrastructure/SessionCart.cs:24`

**In plain terms:** A guest cart is a list of `{productId, qty, variant}` stored in session memory.

**The code:**

```csharp
// Infrastructure/SessionCart.cs:24 (excerpt)
public static class SessionCart
{
    private const string CartKey = "NK.Cart";
    private const string CouponKey = "NK.Coupon";

    public static List<CartLine> Lines(HttpSessionStateBase session)
    {
        var lines = session[CartKey] as List<CartLine>;
        if (lines == null) { lines = new List<CartLine>(); session[CartKey] = lines; }
        return lines;
    }

    public static void Add(HttpSessionStateBase session, int productId, int qty = 1, string variant = null)
    {
        if (qty < 1) qty = 1;
        var line = Lines(session).FirstOrDefault(l => l.ProductId == productId);
        if (line == null) Lines(session).Add(new CartLine { ProductId = productId, Qty = qty, Variant = variant });
        else line.Qty = Math.Min(10, line.Qty + qty);   // cap at 10
    }
}
```

**What's happening:** `CartLine` is a tiny class (`ProductId`, `Qty`, `Variant`). `SessionCart` reads/writes the session list, caps quantities at 10, and stores the applied coupon under a separate key.

**Why it matters:** `SessionCart` never touches the database; it only manages the session list. It takes `HttpSessionStateBase` as a parameter so it is testable and does not hard-code `HttpContext.Current` (except for the coupon convenience property). The quantity cap of 10 is enforced here.

**Trace it further:** Byte 53 (`CartStore`).

---

### Byte 52: The AJAX cart endpoints

**Builds on:** Byte 51.
**Source file(s):** `Controllers/CartController.cs`

**In plain terms:** Add, update, remove, clear and coupon actions all return small JSON payloads that JavaScript consumes.

**The code:**

```csharp
// Controllers/CartController.cs:76
[HttpPost]
public ActionResult Add(int id, int qty = 1, string variant = null)
{
    var product = db.Products.Find(id);
    if (product == null) return Json(new { ok = false, error = "Product not found." });
    if (product.Stock <= 0) return Json(new { ok = false, error = "Sorry, this item is out of stock." });

    SessionCart.Add(Session, id, qty, variant);
    Persist();
    return Json(new { ok = true, count = SessionCart.Count(Session), name = product.Name,
                      image = product.ImageUrl, price = product.Price });
}
```

**What's happening:** The controller checks the product exists and is in stock, adds to the session cart, persists (for signed-in users), and returns `{ ok, count, … }`. `Update`, `Remove`, `Clear`, `ApplyCoupon`, `RemoveCoupon`, `Totals` and `Count` follow the same shape, and `Totals` returns the full `CartTotals` breakdown.

**Why it matters:** The client (`site.js`, `cart.js`) relies on `ok`, `count` and `totals`. Note these POST endpoints have **no** `[ValidateAntiForgeryToken]` attribute — unlike the form-based POSTs elsewhere in the app (see [16](16-validation-security-and-error-handling.md)).

**Trace it further:** [15 - Frontend: Views, JavaScript & CSS](15-frontend-views-javascript-css.md) (`cart.js`).

---

### Byte 53: `CartStore` — persisting for signed-in users

**Builds on:** Byte 51.
**Source file(s):** `Infrastructure/CartStore.cs`

**In plain terms:** When someone is logged in, the session cart is mirrored into the database so it survives a logout.

**The code:**

```csharp
// Infrastructure/CartStore.cs:11
public static void SaveIfAuthenticated(StoreContext db, HttpSessionStateBase session)
{
    var user = Auth.FromSession(session);
    if (user == null) return;      // guests are not persisted
    Save(db, session, user.Id);
}

public static void Save(StoreContext db, HttpSessionStateBase session, int userId)
{
    // delete this user's existing CartItems, re-insert from session lines
    // upsert the UserCart header (coupon code)
}
```

**What's happening:** `Save` replaces the user's stored `CartItems` with the current session lines (delete-then-insert), and upserts the `UserCart` header row holding the coupon. `MergeFromDb` (used at login) does the reverse: it loads the stored lines into the session, taking the larger quantity on conflicts, and restores a stored coupon. `MergeFromDb` then **saves**, so the two sources are reconciled.

**Why it matters:** Delete-then-insert is simple but rewrites the whole cart each time. `CartController.Persist()` calls `SaveIfAuthenticated` after every mutation, so logged-in carts are always mirrored. Unique indexes (`UX_CartItem_User_Product`) guard against duplicates.

**Trace it further:** Byte 54 (login merge).

---

### Byte 54: Merging the cart at login

**Builds on:** Byte 53.
**Source file(s):** `Controllers/AccountController.cs:46`

**In plain terms:** Logging in combines whatever was in the guest cart with whatever was saved on the account.

**The code:**

```csharp
// Controllers/AccountController.cs:46
Auth.Login(Session, user);
CartStore.MergeFromDb(db, Session, user.Id);
WishlistStore.MergeFromDb(db, Session, user.Id);
```

**What's happening:** After a successful login, the app merges the database cart/wishlist into the session. The merge waits for no conflicts — for each product, it keeps the greater quantity (capped at 10) and prefers a stored variant if the session line had none.

**Why it matters:** This is why a guest can add items, sign in, and still find them in the cart. At logout (`Logout`), the session cart and wishlist session lists are cleared but the database copy remains until it is overwritten at the next save.

**Trace it further:** [10 - Authentication & Authorization](10-authentication-and-authorization.md).

---

### Byte 55: Building the cart model

**Builds on:** Byte 52.
**Source file(s):** `Controllers/CartController.cs:27`, `Models/ViewModels/ViewModels.cs:97`

**In plain terms:** The session lines are joined to product rows and run through `Pricing` to produce a display model.

**The code:**

```csharp
// Controllers/CartController.cs:27 (excerpt)
var lines = SessionCart.Lines(Session);
var ids = lines.Select(l => l.ProductId).ToList();
var products = db.Products.Include("Brand").Where(p => ids.Contains(p.Id)).ToList();
var byId = products.ToDictionary(p => p.Id);
foreach (var line in lines) {
    if (!byId.TryGetValue(line.ProductId, out p)) continue;      // product deleted -> drop line
    model.Lines.Add(new CartLineView { Product = p, Qty = line.Qty, Variant = line.Variant });
}
model.Totals = Pricing.Compute(model.Lines, SessionCart.GetCoupon(Session));
```

**What's happening:** It loads only the products in the cart, builds `CartLineView` objects (which expose `LineTotal = Product.Price * Qty`), and computes totals via `Pricing.Compute`. If a product no longer exists, its line is silently skipped.

**Why it matters:** Prices always come from the current product rows, so a price change is reflected in the cart immediately. `CartTotals` carries subtotal, discount, shipping, tax, total, count, the coupon code and its message.

**Trace it further:** [09 - Checkout & Coupons](09-checkout-and-coupons.md).

---

### Byte 56: The mini-cart

**Builds on:** Byte 55.
**Source file(s):** `Controllers/CartController.cs:63`, `Views/Cart/_MiniCart.cshtml`, `Views/Shared/_Layout.cshtml:91`

**In plain terms:** The header shows a small dropdown cart powered by a child action.

**The code:**

```cshtml
@* Views/Shared/_Layout.cshtml:91 *@
<div class="dropdown dd-cart">
    @Html.Action("MiniCart", "Cart")
</div>
```

```csharp
// Controllers/CartController.cs:63
[ChildActionOnly]
public ActionResult MiniCart()
{
    var model = BuildModel();
    ViewBag.Coupons = db.Coupons.Where(x => x.IsActive …).Take(3).ToList();
    return PartialView("_MiniCart", model);
}
```

**What's happening:** `Html.Action` renders the `MiniCart` child action inline inside the layout. It builds the same cart model and passes the active coupons for hints, then renders `_MiniCart.cshtml`.

**Why it matters:** Because the layout renders the mini-cart on every page, every page load queries the active coupons. `[ChildActionOnly]` prevents calling `MiniCart` directly as a URL.

**Trace it further:** [15 - Frontend: Views, JavaScript & CSS](15-frontend-views-javascript-css.md).

---

**Next topic →** [09 - Checkout & Coupons](09-checkout-and-coupons.md)
