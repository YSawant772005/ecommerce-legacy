# 14 - Admin Dashboard & Management

**Goal of this topic:** understand the back-office: dashboard, product/order/coupon management and message moderation.

---

### Byte 81: One guard for the whole admin area

**Builds on:** [10 - Authentication & Authorization](10-authentication-and-authorization.md).
**Source file(s):** `Controllers/AdminController.cs:12`, `Views/Admin/_AdminLayout.cshtml`

**In plain terms:** Every admin page sits behind a single class-level `[RequireAdmin]` attribute and its own layout.

**The code:**

```csharp
// Controllers/AdminController.cs:12
[RequireAdmin]
public class AdminController : Controller
{
    private readonly StoreContext db = new StoreContext();

    public ActionResult Index() { return RedirectToAction("Dashboard"); }
    ...
}
```

**What's happening:** The class-level attribute covers every action, so no admin endpoint is accidentally left open. `/admin` redirects to `/admin/dashboard`. Admin views use `_AdminLayout.cshtml`, a separate layout from the storefront.

**Why it matters:** A single class-level attribute is far safer than remembering it on each action. The separate layout lets the back-office have its own navigation and `admin.css` without affecting the customer site.

**Trace it further:** Byte 82.

---

### Byte 82: The dashboard metrics

**Builds on:** Byte 81.
**Source file(s):** `Controllers/AdminController.cs:28`

**In plain terms:** The dashboard shows counts, revenue, monthly revenue, recent orders, low stock and an order-status breakdown.

**The code:**

```csharp
// Controllers/AdminController.cs:32 (excerpt)
var model = new AdminDashboardViewModel
{
    ProductCount  = db.Products.Count(),
    OrderCount    = db.Orders.Count(),
    CustomerCount = db.Users.Count(u => u.Role == "Customer"),
    Revenue = db.Orders.Where(o => o.Status != OrderStatus.Cancelled)
                       .Select(o => (decimal?)o.Total).Sum() ?? 0,
    MonthlyRevenue = db.Orders
        .Where(o => o.Status != OrderStatus.Cancelled && o.CreatedOn >= monthCutoff)
        .Select(o => (decimal?)o.Total).Sum() ?? 0,
    RecentOrders = db.Orders.OrderByDescending(o => o.CreatedOn).Take(8).ToList(),
    LowStock = db.Products.Where(p => p.Stock <= 5).OrderBy(p => p.Stock).Take(8).ToList()
};

foreach (var s in Enum.GetValues(typeof(OrderStatus))) { /* count per status */ }
```

**What's happening:** Revenue excludes cancelled orders. `(decimal?)o.Total` is projected before `Sum` so an empty set yields `null`, then `?? 0` turns it into zero (avoiding an exception on an empty table). The status breakdown loops over `Enum.GetValues(typeof(OrderStatus))` so every status is represented even when its count is zero.

**Why it matters:** The `(decimal?)`/`?? 0` idiom is the correct way to sum money in EF6 without blowing up on empty results. "Customers" counts only `Role == "Customer"`, excluding the admin account.

**Trace it further:** Byte 86 (messages count).

---

### Byte 83: Managing products

**Builds on:** Byte 82.
**Source file(s):** `Controllers/AdminController.cs:88`, `Controllers/AdminController.cs:576`

**In plain terms:** Admins can create, edit and delete products, with auto-generated slugs, SKUs and a placeholder image.

**The code:**

```csharp
// Controllers/AdminController.cs:576 — PrepareProduct
if (string.IsNullOrWhiteSpace(target.Slug))
    target.Slug = MakeUniqueSlug(EcommerceSeeder.Slugify(target.Name), 0);
else
    target.Slug = MakeUniqueSlug(target.Slug, target.Id);

if (string.IsNullOrWhiteSpace(target.Sku))
    target.Sku = "NK" + DateTime.Now.ToString("yyMMddHHmmss") + new Random().Next(10, 99);

if (target.Price <= 0) target.Price = 1;
if (target.ComparePrice.HasValue && target.ComparePrice.Value <= target.Price)
    target.ComparePrice = null;                 // no fake discounts

if (string.IsNullOrWhiteSpace(target.ImageUrl))
{
    var cat = db.Categories.Find(target.CategoryId);
    target.ImageUrl = "/Content/images/products/" + (cat != null ? cat.Slug : "electronics") + "-v0.svg";
}
```

**What's happening:** On save, `PrepareProduct` ensures a unique slug (`MakeUniqueSlug` appends `-2`, `-3`, …), generates a SKU when missing, floors the price at 1, drops a `ComparePrice` that isn't above the price (so `DiscountPercent` never goes negative), sets `CreatedOn`, and assigns a category placeholder image. Edit uses `TryUpdateModel` with an **explicit allow-list** of editable fields.

**Why it matters:** The `TryUpdateModel` allow-list prevents mass-assignment of fields like `Rating`, `SoldCount` or `Slug` from the form. Every write calls `CatalogCache.RefreshProducts()`, so admin changes appear on the storefront immediately.

**Trace it further:** [05 - Product Catalog](05-product-catalog.md) (`CatalogCache`), [03](03-data-layer-and-database.md) (`Slugify`).

---

### Byte 84: Managing orders

**Builds on:** Byte 82.
**Source file(s):** `Controllers/AdminController.cs:183`, `Controllers/AdminController.cs:212`

**In plain terms:** Admins search/filter orders, open an order, and change its status.

**The code:**

```csharp
// Controllers/AdminController.cs:183
public ActionResult Orders(string q, int page = 1, string status = null)
{
    var model = new AdminOrdersViewModel { Q = q, Status = status, Page = page < 1 ? 1 : page };
    var query = db.Orders.AsQueryable();

    if (!string.IsNullOrWhiteSpace(q))
    {
        var term = q.Trim();
        query = query.Where(o => o.OrderNumber.Contains(term)
            || o.FullName.Contains(term) || o.Email.Contains(term));
    }
    if (!string.IsNullOrWhiteSpace(status))
    {
        OrderStatus parsed;
        if (Enum.TryParse(status, out parsed)) query = query.Where(o => o.Status == parsed);
    }
    model.TotalCount = query.Count();
    model.Orders = query.OrderByDescending(o => o.CreatedOn)
        .Skip((model.Page - 1) * model.PageSize).Take(model.PageSize).ToList();
    return View(model);
}
```

**What's happening:** Search matches order number, name or email; the status filter uses `Enum.TryParse` so an invalid value is ignored rather than crashing. Results page 15 at a time. `OrderDetails` loads the order with `Include("Items").Include("User")`.

**Why it matters:** Stock is **not** adjusted here when an order is cancelled via `UpdateOrderStatus` (see Byte 71) — only the customer-facing cancel restores stock. This is a real behavioural gap worth stating.

**Trace it further:** [11 - Orders & Order History](11-orders-and-order-history.md).

---

### Byte 85: Customers and messages

**Builds on:** Byte 82.
**Source file(s):** `Controllers/AdminController.cs:237`, `Controllers/AdminController.cs:271`

**In plain terms:** A customer list with order counts and spend, and a contact-message inbox.

**The code:**

```csharp
// Controllers/AdminController.cs:254 — per-customer aggregates
model.Customers = rows.Select(u => new AdminCustomerRow
{
    Id = u.Id, FullName = u.FullName, Email = u.Email, Phone = u.Phone, CreatedOn = u.CreatedOn,
    OrderCount = db.Orders.Count(o => o.UserId == u.Id && o.Status != OrderStatus.Cancelled),
    TotalSpent = db.Orders.Where(o => o.UserId == u.Id && o.Status != OrderStatus.Cancelled)
                          .Select(o => (decimal?)o.Total).Sum() ?? 0
}).ToList();
```

```csharp
// Controllers/AdminController.cs:306
[HttpPost]
[ValidateAntiForgeryToken]
public ActionResult MessageRead(int id)
{
    var msg = db.ContactMessages.Find(id);
    if (msg != null) { msg.IsRead = true; db.SaveChanges(); }
    return RedirectToAction("Messages");
}
```

**What's happening:** `Customers` excludes the seeded admin (`u.Email != "admin@novakart.in"`), and computes order count and total spent per row (excluding cancelled). `Messages` lists contact-form submissions, unread first, with an unread count; `MessageRead` marks one as read.

**Why it matters:** The per-row aggregate runs a query per customer page member (an N+1 pattern) — acceptable for 20 rows but worth noting. The admin exclusion is hard-coded to a specific email.

**Trace it further:** [17 - Contact, Newsletter & Static Pages](17-contact-newsletter-and-static-pages.md).

---

### Byte 86: Coupons and newsletter management

**Builds on:** Byte 82.
**Source file(s):** `Controllers/AdminController.cs:430`, `Controllers/AdminController.cs:319`

**In plain terms:** Admins create/edit/delete coupons and toggle newsletter subscribers.

**The code:**

```csharp
// Controllers/AdminController.cs:465
[HttpPost]
[ValidateAntiForgeryToken]
public ActionResult CouponCreate(CouponViewModel model)
{
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
    ...
}
```

```csharp
// Controllers/AdminController.cs:352
[HttpPost] [ValidateAntiForgeryToken]
public ActionResult NewsletterToggle(int id)
{
    var sub = db.NewsletterSubscribers.Find(id);
    if (sub != null) { sub.IsActive = !sub.IsActive; db.SaveChanges(); }
    return RedirectToAction("Newsletter");
}
```

**What's happening:** Coupon codes are upper-cased and checked for duplicates. A coupon must have *either* a fixed amount *or* a positive percentage discount — otherwise a validation error is added. `Newsletter` lists subscribers with active/inactive filters; `NewsletterToggle` flips `IsActive`.

**Why it matters:** The duplicate-code and "must have a discount type" checks are the only business rules enforced on coupons. `CouponEdit` re-checks the code uniqueness while excluding the current coupon, and `ApplyCouponModel` copies the form onto the row.

**Trace it further:** [09 - Checkout & Coupons](09-checkout-and-coupons.md) (how coupons are consumed).

---

### Byte 87: The admin layout

**Builds on:** Byte 81.
**Source file(s):** `Views/Admin/_AdminLayout.cshtml`, `Content/admin.css`

**In plain terms:** Admin pages share their own header, sidebar and stylesheet.

**The code:**

```cshtml
@* Views/Admin/_AdminLayout.cshtml (conceptual) *@
<!DOCTYPE html>
<html>
<head>
    <link href="@Url.Content("~/Content/admin.css")" rel="stylesheet" />
</head>
<body>
    <header class="admin-top">…</header>
    <aside class="admin-nav">…Dashboard / Products / Orders / Customers / Messages / Reviews / Coupons…</aside>
    <main>@RenderBody()</main>
</body>
</html>
```

**What's happening:** `_AdminLayout` provides the back-office chrome; each admin view sets `Layout = "~/Views/Admin/_AdminLayout.cshtml"` (typically via a `_ViewStart`-style convention or explicitly). `admin.css` styles it independently of the storefront `site.css`.

**Why it matters:** Keeping admin and storefront layouts separate means a change to one cannot break the other. TempData messages (e.g. "Product updated") are surfaced once in this layout after redirects.

**Trace it further:** [15 - Frontend: Views, JavaScript & CSS](15-frontend-views-javascript-css.md).

---

**Next topic →** [15 - Frontend: Views, JavaScript & CSS](15-frontend-views-javascript-css.md)
