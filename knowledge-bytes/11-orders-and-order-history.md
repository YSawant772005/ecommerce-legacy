# 11 - Orders & Order History

**Goal of this topic:** understand how an order is modelled, how customers view and cancel orders, and how status changes flow.

---

### Byte 67: The order model & status enum

**Builds on:** [09 - Checkout & Coupons](09-checkout-and-coupons.md).
**Source file(s):** `Models/Order.cs:36`, `Models/Order.cs:45`

**In plain terms:** An `Order` owns a header (address, totals, payment) and a list of `OrderItem` lines.

**The code:**

```csharp
// Models/Order.cs:36
public enum OrderStatus { Placed = 0, Packed = 1, Shipped = 2, Delivered = 3, Cancelled = 4 }

// Models/Order.cs:45 (excerpt)
public class Order
{
    public int Id { get; set; }
    [Required, StringLength(40)]  public string OrderNumber { get; set; }
    public int? UserId { get; set; }                 // null for guest checkout
    [Required] public string Email { get; set; }
    ... AddressLine, City, State, PostalCode ...
    public decimal Subtotal, Discount, Shipping, Tax, Total;
    public OrderStatus Status { get; set; }
    public string PaymentMethod, PaymentRef, CouponCode;
    public DateTime CreatedOn { get; set; }
    public virtual User User { get; set; }
    public virtual ICollection<OrderItem> Items { get; set; }
}
```

**What's happening:** `Order.UserId` is nullable, so guests can place orders without an account. `OrderItem` stores a copy of `ProductName`, `ImageUrl`, `Slug` and `UnitPrice` (snapshot) plus `Quantity`, and exposes a computed `LineTotal`.

**Why it matters:** The snapshot design is why an order stays correct even if the product is later edited or deleted (`ProductId` is nullable for the same reason). `Status` is a stored `int`, so adding a new status means appending to the enum.

**Trace it further:** [03 - Data Layer & Database](03-data-layer-and-database.md) (relationships).

---

### Byte 68: Order history on the account page

**Builds on:** Byte 67.
**Source file(s):** `Controllers/AccountController.cs:190`

**In plain terms:** The account dashboard lists the signed-in user's most recent 20 orders.

**The code:**

```csharp
// Controllers/AccountController.cs:190
[RequireLogin]
[HttpGet]
public ActionResult Index()
{
    var me = Auth.FromSession(Session);
    var user = db.Users.Find(me.Id);
    var orders = db.Orders
        .Where(o => o.UserId == me.Id)
        .OrderByDescending(o => o.CreatedOn)
        .Take(20)
        .ToList();
    return View(new AccountOverview { User = user, Orders = orders });
}
```

**What's happening:** The orders are filtered by the session user's `Id` — never by anything from the URL — so one customer cannot see another's history. `Take(20)` caps the list on the dashboard.

**Why it matters:** Filtering by the session identity (not a route value) is the correct pattern and recurs in `Order`, `Invoice` and `CancelOrder`. Guest orders (null `UserId`) never appear here.

**Trace it further:** Byte 69.

---

### Byte 69: Order detail and invoice

**Builds on:** Byte 68.
**Source file(s):** `Controllers/AccountController.cs:206`, `Controllers/AccountController.cs:218`

**In plain terms:** A customer can open their own order and a printable invoice, both looked up by order number plus their user id.

**The code:**

```csharp
// Controllers/AccountController.cs:207
[RequireLogin]
public ActionResult Order(string id)
{
    var me = Auth.FromSession(Session);
    var order = db.Orders.Include("Items")
        .FirstOrDefault(o => o.OrderNumber == id && o.UserId == me.Id);
    if (order == null) return HttpNotFound();
    return View(order);
}
```

**What's happening:** Both `Order` and `Invoice` use the same double condition — matching `OrderNumber` **and** `UserId`. If the order belongs to someone else (or is a guest order), `FirstOrDefault` returns null and the action returns 404 rather than leaking the order.

**Why it matters:** Ownership is enforced in the query, which is more robust than an `if (order.UserId != me.Id)` check that could be forgotten. `Invoice` renders the same order data in a print-friendly layout.

**Trace it further:** [16 - Validation, Security & Error Handling](16-validation-security-and-error-handling.md).

---

### Byte 70: Cancelling an order (with stock restore)

**Builds on:** Byte 69.
**Source file(s):** `Controllers/AccountController.cs:230`

**In plain terms:** A customer can cancel an order that is still `Placed` or `Packed`; the stock returns and the status becomes `Cancelled`.

**The code:**

```csharp
// Controllers/AccountController.cs:240
if (order.Status == OrderStatus.Placed || order.Status == OrderStatus.Packed)
{
    foreach (var item in order.Items)
    {
        if (item.ProductId.HasValue)
        {
            var stock = db.Products.Find(item.ProductId.Value);
            if (stock != null) stock.Stock = stock.Stock + item.Quantity;
        }
    }
    order.Status = OrderStatus.Cancelled;
    db.SaveChanges();
    CatalogCache.RefreshProducts();
    TempData["Message"] = "Order " + order.OrderNumber + " has been cancelled. ...";
}
else
{
    TempData["Message"] = "This order can no longer be cancelled.";
}
```

**What's happening:** Only `Placed` and `Packed` orders are cancellable. The loop adds each line's quantity back to its product's stock (skipping lines whose product was deleted), then flips the status and refreshes the catalog cache.

**Why it matters:** This is the reverse of the checkout stock decrement, so restocking stays balanced. Because the cache is refreshed, the product immediately shows as available again. `Shipped` and `Delivered` orders cannot be cancelled here.

**Trace it further:** [09 - Checkout & Coupons](09-checkout-and-coupons.md) (stock decrement).

---

### Byte 71: Admin status changes

**Builds on:** Byte 70.
**Source file(s):** `Controllers/AdminController.cs:221`

**In plain terms:** Admins move an order forward by picking a status on the order-details page.

**The code:**

```csharp
// Controllers/AdminController.cs:221
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
```

**What's happening:** The posted status string is parsed with `Enum.TryParse` into the `OrderStatus` enum and saved. Only an exact enum name (`Placed`, `Packed`, `Shipped`, `Delivered`, `Cancelled`) is accepted; anything else is ignored.

**Why it matters:** There is **no transition validation** — an admin can jump straight to `Delivered` or back to `Placed`, and marking an order `Cancelled` this way does **not** restore stock (unlike the customer cancel). These are real behaviours worth documenting rather than assuming a strict workflow.

**Trace it further:** [14 - Admin Dashboard & Management](14-admin-dashboard-and-management.md).

---

### Byte 72: The order confirmation page

**Builds on:** Byte 67.
**Source file(s):** `Controllers/CheckoutController.cs:369`, `Views/Checkout/Success.cshtml`

**In plain terms:** After confirming, the buyer sees a receipt keyed by order number.

**The code:**

```csharp
// Controllers/CheckoutController.cs:364
Session["NK.LastOrder"] = order.OrderNumber;
return RedirectToAction("Success", new { id = order.OrderNumber });
```

**What's happening:** `Confirm` stores the new order number in `NK.LastOrder` and redirects to `Success`, which reloads the order (with items) and renders the receipt. The layout can surface `NK.LastOrder` for a lightweight "last order" link.

**Why it matters:** `Success` is a plain `GET` with no `[RequireLogin]` and no ownership check — anyone who knows the order number can view it. Order numbers are `NK{date}-{4 digits}`, so they are guessable. This is a documented limitation (see [16](16-validation-security-and-error-handling.md)), not something the docs should present as secure.

**Trace it further:** [16 - Validation, Security & Error Handling](16-validation-security-and-error-handling.md).

---

**Next topic →** [12 - Wishlist](12-wishlist.md)
