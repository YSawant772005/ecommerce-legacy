# 04 - Models & ViewModels

**Goal of this topic:** learn the data types — the entities EF persists and the view models the views render.

---

### Byte 27: Entities vs view models

**Builds on:** [03 - Data Layer & Database](03-data-layer-and-database.md).
**Source file(s):** `Models/*.cs`, `Models/ViewModels/ViewModels.cs`

**In plain terms:** Entities describe database rows; view models describe what a page needs.

**What's happening:**
- **Entities** (`Models/Product.cs`, `Order.cs`, `CartItem.cs`, `Category.cs`, `Coupon.cs`, `CustomerAddress.cs`, `ContactMessage.cs`, `PasswordReset.cs`) map to tables via `StoreContext` `DbSet`s.
- **View models** live in `Models/ViewModels/ViewModels.cs`. Some views use entities directly (for example `Wishlist/Index` renders a `List<Product>`), while others use purpose-built classes (`CatalogViewModel`, `CheckoutViewModel`, `Admin…ViewModel`).

**Why it matters:** Not every entity has a matching view model here — the project mixes both styles. When reading a view, check its `@model` line first: it tells you whether you are looking at an entity or a view model.

**Trace it further:** [05 - Product Catalog](05-product-catalog.md).

---

### Byte 28: `Product` and `Review`

**Builds on:** Byte 27.
**Source file(s):** `Models/Product.cs:8`

**In plain terms:** A product has pricing, stock, a rating, three images and variant lists; a review belongs to a product.

**The code:**

```csharp
// Models/Product.cs:8 (excerpt)
public class Product
{
    public int Id { get; set; }
    [Required, StringLength(200)] public string Name { get; set; }
    [StringLength(220)] public string Slug { get; set; }
    [StringLength(50)]  public string Sku { get; set; }
    public decimal Price { get; set; }
    public decimal? ComparePrice { get; set; }   // "was" price for discounts
    public int Stock { get; set; }
    public double Rating { get; set; }
    public int RatingCount { get; set; }
    public int ReviewCount { get; set; }
    public int SoldCount { get; set; }
    public int CategoryId { get; set; }
    public int BrandId { get; set; }
    public string ImageUrl { get; set; }
    public string ImageUrl2 { get; set; }
    public string ImageUrl3 { get; set; }
    [StringLength(240)] public string Colors { get; set; }  // comma-separated
    [StringLength(160)] public string Sizes  { get; set; }  // comma-separated
    public bool IsFeatured { get; set; }
    public bool IsDeal { get; set; }
    public bool IsNew { get; set; }
    public DateTime CreatedOn { get; set; }
    public virtual Category Category { get; set; }
    public virtual Brand Brand { get; set; }
    public virtual ICollection<Review> Reviews { get; set; }
}
```

**Computed (not stored) properties**, marked with `[NotMapped]`:
- `DiscountPercent` — `(ComparePrice - Price) / ComparePrice * 100`, or 0.
- `InStock` — `Stock > 0`.
- `ColorList` / `SizeList` — split `Colors` / `Sizes` on commas.

**What's happening:** Variants are stored as comma-separated strings, not separate tables. The card and detail views convert them to arrays via `ColorList`/`SizeList`.

**Why it matters:** `DiscountPercent` and `InStock` are derived, so they need no schema. But because `Colors`/`Sizes` are delimited strings, you cannot query variants in SQL — the app never needs to, since the picker is built in the view.

**Trace it further:** [06 - Product Details & Variants](06-product-details-and-variants.md).

---

### Byte 29: `User`, `Order`, `OrderItem`, `OrderStatus`

**Builds on:** Byte 27.
**Source file(s):** `Models/Order.cs`

**In plain terms:** A user places orders; each order has line items and a status.

**The code:**

```csharp
// Models/Order.cs (excerpt)
public enum OrderStatus { Placed = 0, Packed = 1, Shipped = 2, Delivered = 3, Cancelled = 4 }

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; }   // e.g. NK260101-1234
    public int? UserId { get; set; }          // null for guest checkout
    public string Email { get; set; }
    public string FullName { get; set; }
    public string Phone { get; set; }
    public string AddressLine { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string PostalCode { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public OrderStatus Status { get; set; }
    public string PaymentMethod { get; set; }
    public string PaymentRef { get; set; }
    public string CouponCode { get; set; }
    public DateTime CreatedOn { get; set; }
    public virtual User User { get; set; }
    public virtual ICollection<OrderItem> Items { get; set; }
}

public class OrderItem
{
    public int OrderId { get; set; }
    public int? ProductId { get; set; }
    public string ProductName { get; set; }
    public string ImageUrl { get; set; }
    public string Slug { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get { return UnitPrice * Quantity; } }
}
```

**What's happening:** `Order` stores a full snapshot of the shipping details (so it survives address edits) and money totals. `OrderItem` snapshots the product name, image and slug so the order remains readable even if the product is deleted. `UserId` is nullable, allowing guest orders.

**Why it matters:** The `OrderStatus` enum drives the admin status updates, the customer order timeline, and the "can I cancel?" rule (only `Placed` or `Packed` can be cancelled — see `AccountController.CancelOrder`). The value names (`Placed`, `Packed`, …) are used directly in CSS classes (`status-placed`) and admin `Enum.TryParse`.

**Trace it further:** [11 - Orders & Order History](11-orders-and-order-history.md).

---

### Byte 30: Cart, wishlist and address entities

**Builds on:** Byte 27.
**Source file(s):** `Models/CartItem.cs`, `Models/CustomerAddress.cs`

**In plain terms:** Signed-in users get persistent cart lines, a cart header (for the coupon), and wishlist rows.

**The code:**

```csharp
// Models/CartItem.cs (excerpt)
public class CartItem { public int UserId, ProductId, Qty; public string Variant; public DateTime UpdatedOn; }
public class UserCart { public int UserId; public string CouponCode; public DateTime UpdatedOn; }
public class WishlistItem { public int UserId, ProductId; public DateTime AddedOn; }
```

**What's happening:** `CartItem` is one persisted cart line per user/product; `UserCart` is a one-per-user header that stores the applied coupon code; `WishlistItem` is one persisted wishlist row per user/product. `CustomerAddress` (see `Models/CustomerAddress.cs`) holds saved shipping addresses with `IsDefault`, validated by regex (`Phone`, 6-digit `PostalCode`).

**Why it matters:** `CartStore` and `WishlistStore` keep these tables in sync with the session lists (see [08](08-cart-and-session-management.md)). The unique indexes `UX_CartItem_User_Product` and `UX_WishlistItem_User_Product` prevent duplicates.

**Trace it further:** [08 - Cart & Session Management](08-cart-and-session-management.md), [12 - Wishlist](12-wishlist.md).

---

### Byte 31: `Category`, `Brand`, `Coupon`, `ContactMessage`, `NewsletterSubscriber`, `PasswordReset`

**Builds on:** Byte 27.
**Source file(s):** `Models/Category.cs`, `Models/Coupon.cs`, `Models/ContactMessage.cs`, `Models/PasswordReset.cs`

**In plain terms:** The remaining tables cover navigation, discounts and messages.

**The code:**

```csharp
// Models/Category.cs (excerpt)
public class Category { public string Name { get; set; } public string Slug { get; set; }
                        public string Tagline { get; set; } public string Description { get; set; }
                        public string ImageUrl { get; set; }
                        public int DisplayOrder { get; set; } public bool IsActive { get; set; } }
public class Brand    { public string Name { get; set; } public string Slug { get; set; } public string Code { get; set; } }

// Models/Coupon.cs (excerpt)
public class Coupon { public string Code { get; set; } public string Description { get; set; }
    public decimal MinOrderValue { get; set; } public decimal? FixedAmount { get; set; }
    public decimal? PercentDiscount { get; set; } public decimal MaxDiscountAmount { get; set; }
    public bool FreeShipping { get; set; } public bool IsActive { get; set; }
    public int TotalUses { get; set; } public DateTime StartsOn { get; set; } public DateTime? EndsOn { get; set; } }

// Models/ContactMessage.cs (excerpt)
public class ContactMessage { public string Name { get; set; } public string Email { get; set; }
    public string Phone { get; set; } public string Topic { get; set; } public string Message { get; set; } public bool IsRead { get; set; } }
public class NewsletterSubscriber { public string Email { get; set; } public bool IsActive { get; set; } }

// Models/PasswordReset.cs (excerpt)
public class PasswordReset { public int UserId { get; set; } public string Token { get; set; }
    public DateTime ExpiresOn { get; set; } public DateTime? UsedOn { get; set; } }
```

**What's happening:**
- `Category` has `IsActive` and `DisplayOrder`; `CatalogCache.Categories` filters to active and orders by `DisplayOrder`.
- `Coupon` supports either a fixed amount or a percentage with a cap, plus optional free shipping and a validity window.
- `PasswordReset` stores a 64-char token with a 30-minute expiry and a `UsedOn` marker.

**Why it matters:** These types define the shape of the admin CRUD screens, coupon validation, the contact inbox, the newsletter list, and the password-reset flow.

**Trace it further:** [09 - Checkout & Coupons](09-checkout-and-coupons.md), [13 - Reviews & Moderation](13-reviews-and-moderation.md), [14 - Admin Dashboard & Management](14-admin-dashboard-and-management.md).

---

### Byte 32: Page-shaping view models

**Builds on:** Byte 27.
**Source file(s):** `Models/ViewModels/ViewModels.cs:10`

**In plain terms:** Some view models exist purely to bundle everything a page needs.

**The code:**

```csharp
// Models/ViewModels/ViewModels.cs (excerpt)
public class CatalogViewModel {
    public List<Product> Products; public List<Category> Categories; public List<Brand> Brands;
    public Dictionary<string,int> CategoryCounts;
    public int Page, PageSize, TotalCount;
    public int TotalPages => ...; public int From => ...; public int To => ...;
    public string Sort, Q, CategorySlug, CategoryName, Mode, Title;
    public List<string> BrandSlugs; public decimal? MinPrice, MaxPrice; public double? MinRating;
    public decimal PriceFloor, PriceCeil; public List<int> PageNumbers => ...;
}
```

**What's happening:** `CatalogViewModel` carries products plus all the filter/sort/pagination state the listing view needs to rebuild the UI. Other page models include `ProductDetailViewModel`, `CartViewModel` + `CartTotals`, `CheckoutViewModel` + `CheckoutState` + `PaymentStepViewModel` + `ReviewOrderViewModel`, `HomeViewModel`, and the `Admin…ViewModel` family (Dashboard, Products, Orders, Customers, Messages, Newsletter, Reviews, Coupons).

**Why it matters:** `CatalogViewModel.PageNumbers` implements the "1 … 4 5 6 … 36" pager logic in one place; `Mode` ("scroll" or "pages") decides which control the view shows. These computed properties keep the Razor views logic-light.

**Trace it further:** [07 - Search, Filtering, Sorting & Pagination](07-search-filtering-sorting-pagination.md).

---

### Byte 33: `Pricing` — the single source of money math

**Builds on:** Byte 32.
**Source file(s):** `Models/ViewModels/ViewModels.cs:630`

**In plain terms:** One static class turns cart lines plus a coupon into a full totals breakdown.

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
        totals.Count = lines.Sum(l => l.Qty);
        // apply coupon -> Discount + FreeShipping
        // shipping free if afterDiscount >= 999 or coupon gives free shipping
        // totals.Tax = round(afterDiscount * 0.18, 2)
        // totals.Total = afterDiscount + Shipping + Tax
    }
}
```

**What's happening:** `Compute` is the single place subtotal, discount, shipping, GST and total are calculated. It calls `Coupons.Validate` for discounts and applies the free-shipping threshold to the *after-discount* amount.

**Why it matters:** Both the cart page and checkout call `Pricing.Compute`, so the numbers shown and the numbers charged agree. JavaScript never computes totals itself — it only displays the `totals` object the server returns.

**Trace it further:** [09 - Checkout & Coupons](09-checkout-and-coupons.md).

---

### Byte 34: `CatalogCache` — the in-memory product list

**Builds on:** Byte 32.
**Source file(s):** `Models/ViewModels/ViewModels.cs:672`

**In plain terms:** A static class that lazy-loads and caches categories, brands, products and price bounds for the whole process.

**The code:**

```csharp
// Models/ViewModels/ViewModels.cs:672 (excerpt)
public static class CatalogCache
{
    private static readonly object _lock = new object();
    private static List<Product> _products;

    public static List<Product> Products {
        get { if (_products == null) RefreshProducts(); return _products; }
    }

    public static void RefreshProducts() {
        using (var db = new Data.StoreContext()) {
            var list = db.Products.Include("Brand").Include("Category")
                .OrderBy(p => p.Id).ToList();
            lock (_lock) { _products = list; }
        }
    }

    public static decimal PriceFloor { get { /* MIN(Price), cached */ } }
    public static decimal PriceCeil  { get { /* MAX(Price), cached */ } }
    public static List<Category> Categories { get { /* active, ordered */ } }
    public static List<Brand> Brands { get { /* by name */ } }
}
```

**What's happening:** `Products`, `Categories` and `Brands` load on first access and stay in memory. `PriceFloor`/`PriceCeil` are computed with a `MIN`/`MAX` query and cached. `RefreshProducts()` reloads the product list under a lock and is called by admin product save/delete and by `ReviewPost`.

**Why it matters:** This makes reads fast and simple but introduces staleness and memory concerns. Note that only **products** are refreshed (`RefreshProducts`); if categories or brands change, the cached lists are not refreshed until the app restarts.

**Trace it further:** [07 - Search, Filtering, Sorting & Pagination](07-search-filtering-sorting-pagination.md), [14 - Admin Dashboard & Management](14-admin-dashboard-and-management.md).

---

**Next topic →** [05 - Product Catalog](05-product-catalog.md)
