# 09 - Checkout & Coupons

**Goal of this topic:** understand the multi-step checkout, how pricing is computed, and how an order is created.

---

### Byte 57: A wizard held in session state

**Builds on:** [08 - Cart & Session Management](08-cart-and-session-management.md).
**Source file(s):** `Controllers/CheckoutController.cs:45`, `Controllers/CheckoutController.cs:120`

**In plain terms:** Checkout is three screens — Address, Payment, Review — with progress held in a `CheckoutState` object in session.

**The code:**

```csharp
// Controllers/CheckoutController.cs:45
private const string StateKey = "NK.Checkout";

private CheckoutState GetState()
{
    return Session[StateKey] as CheckoutState ?? new CheckoutState();
}

private void SetState(CheckoutState state) { Session[StateKey] = state; }
```

```csharp
// Controllers/CheckoutController.cs:174 — each later step guards on earlier ones
public ActionResult Payment()
{
    var cart = BuildCart();
    if (cart.IsEmpty) { TempData["Message"] = "Your cart is empty."; return RedirectToAction("Index", "Cart"); }
    var state = GetState();
    if (!state.AddressDone) return RedirectToAction("Index");    // cannot skip ahead
    ...
}
```

**What's happening:** `Address` (POST) validates and stores address fields, setting `AddressDone`. `Payment` requires `AddressDone`; `Review` requires both flags. `Confirm` requires both. Each step also redirects away when the cart is empty.

**Why it matters:** The flags prevent users from jumping straight to Confirm by typing a URL. They are not security controls, though — a user can still tamper with session state via the normal flow, but not forge an order without passing validation.

**Trace it further:** Byte 60 (Confirm).

---

### Byte 58: `Pricing.Compute` — the money math

**Builds on:** Byte 57.
**Source file(s):** `Models/ViewModels/ViewModels.cs:630`

**In plain terms:** One static method turns cart lines and a coupon code into a full totals breakdown.

**The code:**

```csharp
// Models/ViewModels/ViewModels.cs:630
public static class Pricing
{
    public const decimal FreeShippingThreshold = 999m;
    public const decimal ShippingFee = 49m;
    public const decimal TaxRate = 0.18m;

    public static CartTotals Compute(List<CartLineView> lines, string couponCode)
    {
        var totals = new CartTotals();
        totals.Subtotal = lines.Sum(l => l.LineTotal);
        totals.Count    = lines.Sum(l => l.Qty);

        if (!string.IsNullOrWhiteSpace(couponCode)) {
            var result = Coupons.Validate(couponCode, totals.Subtotal);
            totals.CouponCode = couponCode;
            totals.CouponMessage = result.Message;
            if (result.Valid) {
                totals.Discount = result.Amount;
                totals.FreeShipping = result.FreeShipping;
                totals.CouponValid = true;
            }
        }

        decimal afterDiscount = totals.Subtotal - totals.Discount;
        totals.Shipping = totals.FreeShipping ? 0
            : (afterDiscount >= FreeShippingThreshold ? 0 : ShippingFee);
        totals.Tax   = Math.Round(afterDiscount * TaxRate, 2);
        totals.Total = afterDiscount + totals.Shipping + totals.Tax;
        if (totals.Total < 0) totals.Total = 0;
        return totals;
    }
}
```

**What's happening:** Subtotal, then coupon discount, then shipping (free at ≥ ₹999 after discount, or when the coupon grants free shipping), then 18% tax on the discounted subtotal, then the grand total.

**Why it matters:** This single method is the authority for prices on the cart, mini-cart, checkout and the created order — so the numbers can never disagree between screens. `FreeShippingThreshold`, `ShippingFee` and `TaxRate` are named constants you can change in one place.

**Trace it further:** Byte 59 (coupons).

---

### Byte 59: Coupons

**Builds on:** Byte 58.
**Source file(s):** `Infrastructure/SessionCart.cs:106`, `Data/EcommerceSeeder.cs`

**In plain terms:** A coupon is validated against the database: active, inside its date window, meeting the minimum order, then a fixed or percentage discount.

**The code:**

```csharp
// Infrastructure/SessionCart.cs:138 — the discount computation
decimal amount = 0;
if (coupon.FixedAmount.HasValue)
{
    amount = coupon.FixedAmount.Value;
}
else if (coupon.PercentDiscount.HasValue)
{
    amount = Math.Round(subtotal * coupon.PercentDiscount.Value / 100m, 2);
    if (coupon.MaxDiscountAmount > 0 && amount > coupon.MaxDiscountAmount)
        amount = coupon.MaxDiscountAmount;   // cap the percentage discount
}
if (amount > subtotal) amount = subtotal;     // never discount below zero
```

**What's happening:** `Coupons.Validate` opens its own `StoreContext`, finds the coupon by code where `IsActive`, checks `StartsOn`/`EndsOn` and `MinOrderValue`, then computes the amount. It returns a `CouponResult` with `Valid`, `Amount`, `FreeShipping` and a human `Message`.

**Why it matters:** The three seeded coupons illustrate each rule: `SAVE10` (10% off), `WELCOME50` (₹50 off above a minimum), `FREESHIP` (free shipping). There is **no per-user usage tracking** — `TotalUses` is only incremented at order time, and a user can reuse a coupon on later orders. This is a documented limitation, not a bug to "fix" in the docs.

**Trace it further:** Byte 60 (order creation increments `TotalUses`).

---

### Byte 60: Creating the order in `Confirm`

**Builds on:** Byte 59.
**Source file(s):** `Controllers/CheckoutController.cs:281`, `Controllers/CheckoutController.cs:414`

**In plain terms:** Confirm snapshots the cart into an `Order` + `OrderItem` rows, decrements stock, clears the cart and redirects to the receipt.

**The code:**

```csharp
// Controllers/CheckoutController.cs:302 (excerpt)
var order = new Order {
    OrderNumber = NextOrderNumber(),
    UserId = sessionUser != null ? sessionUser.Id : (int?)null,
    ...
    Status = OrderStatus.Placed,
    PaymentMethod = state.PaymentMethod,
    PaymentRef = state.PaymentMethod == "COD" ? null
        : "TXN" + DateTime.Now.ToString("yyMMddHHmm") + new Random().Next(100, 999),
    CouponCode = totals.CouponValid ? totals.CouponCode : null,
    CreatedOn = DateTime.Now,
    Items = new List<OrderItem>()
};

foreach (var line in cart.Lines) {
    order.Items.Add(new OrderItem {
        ProductId = line.Product.Id,
        ProductName = line.Product.Name + (string.IsNullOrEmpty(line.Variant) ? "" : " (" + line.Variant + ")"),
        UnitPrice = line.Product.Price, Quantity = line.Qty, ...
    });
    var stock = db.Products.Find(line.Product.Id);
    if (stock != null) { var left = stock.Stock - line.Qty; stock.Stock = left < 0 ? 0 : left; }
}
```

**What's happening:** The order is built from the cart (copying name, image, slug and unit price so it is a permanent snapshot). Stock is decremented (never below zero). The coupon's `TotalUses` is incremented. `db.SaveChanges()` writes everything in one call, then the session cart, the stored user cart and the checkout state are cleared, and `NK.LastOrder` is set.

**Why it matters:** Because `OrderItem` copies the price and name, later product edits don't change past orders. `NextOrderNumber` (line 414) generates `NK{yyMMdd}-{4 digits}` and retries up to 40 times if the number already exists.

**Trace it further:** [11 - Orders & Order History](11-orders-and-order-history.md), Byte 61 (receipt).

---

### Byte 61: The payment step & the receipt

**Builds on:** Byte 60.
**Source file(s):** `Controllers/CheckoutController.cs:195`, `Controllers/CheckoutController.cs:369`, `Scripts/checkout.js`

**In plain terms:** Payment fields are validated with regexes (no real gateway), and Success looks the order up by number.

**The code:**

```csharp
// Controllers/CheckoutController.cs:220 — card expiry check
if (model.Expiry == null || !Regex.IsMatch(model.Expiry, @"^(0[1-9]|1[0-2])\/[0-9]{2}$"))
    ModelState.AddModelError("Expiry", "Use MM/YY format.");

// Controllers/CheckoutController.cs:232 — UPI check
if (string.IsNullOrWhiteSpace(model.UpiId) ||
    !Regex.IsMatch(model.UpiId.Trim(), @"^[a-zA-Z0-9._\-]{2,}@[a-zA-Z]{2,}$"))
    ModelState.AddModelError("UpiId", "Enter a valid UPI ID (e.g. name@okhdfc).");
```

```csharp
// Controllers/CheckoutController.cs:369
public ActionResult Success(string id)
{
    if (string.IsNullOrWhiteSpace(id)) return HttpNotFound();
    var order = db.Orders.Include("Items").FirstOrDefault(o => o.OrderNumber == id);
    if (order == null) return HttpNotFound();
    ViewBag.Title = "Order Confirmed";
    return View(order);
}
```

**What's happening:** Card numbers must be 16 digits (non-digits are stripped first), name ≥ 3 chars, expiry `MM/YY`, CVV 3–4 digits; UPI must look like `name@bank`. **No payment is actually processed** — a fake `PaymentRef` is generated. `Success` looks up the order by `OrderNumber` and renders the receipt; it is reachable only if you know the order number, and it never checks ownership (see [16](16-validation-security-and-error-handling.md)).

**Why it matters:** `checkout.js` only copies a saved address into the form when the customer picks one. The regexes are real input validation; the "payment" is a simulation and should be stated clearly to anyone reading this project.

**Trace it further:** [16 - Validation, Security & Error Handling](16-validation-security-and-error-handling.md).

---

**Next topic →** [10 - Authentication & Authorization](10-authentication-and-authorization.md)
