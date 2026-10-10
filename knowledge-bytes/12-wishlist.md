# 12 - Wishlist

**Goal of this topic:** understand how "saved for later" works for guests and signed-in users.

---

### Byte 73: A wishlist is just a list of product ids

**Builds on:** [08 - Cart & Session Management](08-cart-and-session-management.md).
**Source file(s):** `Infrastructure/WishlistStore.cs:12`

**In plain terms:** The session wishlist is a `List<int>` of product ids under the key `NK.Wishlist`.

**The code:**

```csharp
// Infrastructure/WishlistStore.cs:12
public const string Key = "NK.Wishlist";

public static List<int> Ids(HttpSessionStateBase session)
{
    var list = session[Key] as List<int>;
    if (list == null) { list = new List<int>(); session[Key] = list; }
    return list;
}
```

**What's happening:** `Ids` lazily creates and returns the list. The whole wishlist is then built by querying products whose id is in that list (`WishlistController.Index`).

**Why it matters:** Like the cart, the session wishlist works without login. But unlike the cart, the wishlist controller has **no `[RequireLogin]`** — guests can wishlist freely, and it is only persisted to the database if/when they sign in.

**Trace it further:** Byte 74.

---

### Byte 74: Toggling from anywhere on the site

**Builds on:** Byte 73.
**Source file(s):** `Controllers/WishlistController.cs:41`, `Scripts/site.js:108`

**In plain terms:** Clicking a heart posts a toggle and returns the new state and count.

**The code:**

```csharp
// Controllers/WishlistController.cs:42
[HttpPost]
public ActionResult Toggle(int id)
{
    if (!db.Products.Any(p => p.Id == id))
        return Json(new { ok = false, error = "Product not found." });

    var ids = Ids();
    bool on;
    if (ids.Contains(id)) { ids.Remove(id); on = false; }
    else                  { ids.Add(id);     on = true; }
    Persist();
    return Json(new { ok = true, on, count = ids.Count });
}
```

```javascript
// Scripts/site.js:108 — one delegated listener handles every heart
document.addEventListener('click', function (e) {
    var btn = e.target.closest('[data-wish]');
    if (!btn) return;
    e.preventDefault();
    var id = btn.getAttribute('data-wish');
    Nova.post('/wishlist/toggle', { id: id }).then(function (res) {
        if (res && res.ok) {
            btn.classList.toggle('is-on', res.on);
            setCount('wishCount', res.count);
        }
    });
});
```

**What's happening:** `Toggle` validates the product exists, flips membership in the session list, persists for logged-in users, and returns `{ ok, on, count }`. `site.js` updates the heart's `is-on` class and the header's wishlist badge.

**Why it matters:** Because both the product cards and the product page render hearts with `data-wish`, a single document-level listener keeps them all working — including cards added later by infinite scroll. The controller checks the product exists to avoid saving junk ids.

**Trace it further:** [15 - Frontend: Views, JavaScript & CSS](15-frontend-views-javascript-css.md).

---

### Byte 75: Persisting and merging the wishlist

**Builds on:** Byte 73.
**Source file(s):** `Infrastructure/WishlistStore.cs:32`

**In plain terms:** On login, database wishlist ids are merged into the session, then the union is written back.

**The code:**

```csharp
// Infrastructure/WishlistStore.cs:58
public static void MergeFromDb(StoreContext db, HttpSessionStateBase session, int userId)
{
    var ids = Ids(session);
    var dbIds = db.WishlistItems.Where(w => w.UserId == userId)
                                .Select(w => w.ProductId).ToList();
    foreach (var id in dbIds)
        if (!ids.Contains(id)) ids.Add(id);

    Save(db, session, userId);
}

public static void Save(StoreContext db, HttpSessionStateBase session, int userId)
{
    foreach (var e in db.WishlistItems.Where(w => w.UserId == userId).ToList())
        db.WishlistItems.Remove(e);          // delete-then-insert
    var available = db.Products.Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToList();
    // only re-insert ids that still exist
    db.SaveChanges();
}
```

**What's happening:** `MergeFromDb` unions the guest ids with the stored ids, then `Save` replaces the user's `WishlistItems` rows with the session list, skipping ids of deleted products. `SaveIfAuthenticated` is the no-op-for-guests wrapper the controller calls.

**Why it matters:** The "skip deleted products" step prevents foreign-key failures when a wishlisted product was removed. Note there is **no toggle "off" on merge** — union means logging in can only add to your wishlist, never remove.

**Trace it further:** [10 - Authentication & Authorization](10-authentication-and-authorization.md) (login calls `MergeFromDb`).

---

### Byte 76: The wishlist page

**Builds on:** Byte 75.
**Source file(s):** `Controllers/WishlistController.cs:31`, `Views/Wishlist/Index.cshtml`

**In plain terms:** `/wishlist` loads the products in the list and renders them as cards.

**The code:**

```csharp
// Controllers/WishlistController.cs:31
public ActionResult Index()
{
    var ids = Ids();
    var products = ids.Count == 0
        ? new List<Product>()
        : db.Products.Include("Brand").Where(p => ids.Contains(p.Id)).ToList();
    ViewBag.Title = "My Wishlist";
    return View(products);
}
```

**What's happening:** If the list is empty it skips the query. Otherwise it loads the products and passes them to the view, which reuses the same `_ProductCard` partial as the catalog.

**Why it matters:** Reusing `_ProductCard` means the wishlist looks and behaves like the catalog for free (add-to-cart, heart toggle). A `Count` GET endpoint (`/wishlist/count`) additionally lets scripts refresh the header badge on demand.

**Trace it further:** [05 - Product Catalog](05-product-catalog.md) (`_ProductCard`).

---

**Next topic →** [13 - Reviews & Moderation](13-reviews-and-moderation.md)
