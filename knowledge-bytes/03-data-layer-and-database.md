# 03 - Data Layer & Database

**Goal of this topic:** understand how the app talks to SQL Server, how the database is created and seeded, and what the indexes are.

---

### Byte 20: `StoreContext` — the EF gateway

**Builds on:** [02 - Application Startup & Request Flow](02-application-startup-and-request-flow.md).
**Source file(s):** `Data/StoreContext.cs:6`, `Web.config:7`

**In plain terms:** `StoreContext` is the Entity Framework class that represents the database; each `DbSet` is a table.

**The code:**

```csharp
// Data/StoreContext.cs:6
public class StoreContext : DbContext
{
    public StoreContext() : base("name=StoreContext")
    {
        Database.CommandTimeout = 180;
    }

    public DbSet<Category> Categories { get; set; }
    public DbSet<Brand> Brands { get; set; }
    public DbSet<Product> Products { get; set; }
    // ... Reviews, Users, Orders, OrderItems, CustomerAddresses,
    //     Coupons, ContactMessages, NewsletterSubscribers,
    //     CartItems, UserCarts, WishlistItems, PasswordResets
}
```

**What's happening:** `base("name=StoreContext")` tells EF to use the connection string **named** `StoreContext` from `Web.config`. `CommandTimeout = 180` allows slow seeding queries. Each `DbSet<T>` maps to a table.

**Why it matters:** Controllers universally write `new StoreContext()` and dispose it in an overridden `Dispose`. Contexts are **per request**, short-lived, and not shared. If the connection string name changes, this constructor breaks.

**Trace it further:** [04 - Models & ViewModels](04-models-and-viewmodels.md) (the entity types).

---

### Byte 21: `OnModelCreating` — relationships and precision

**Builds on:** Byte 20.
**Source file(s):** `Data/StoreContext.cs:30`

**In plain terms:** This method tells EF how tables relate and how many decimal places money has.

**The code:**

```csharp
// Data/StoreContext.cs (excerpt)
modelBuilder.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);

modelBuilder.Entity<Product>()
    .HasRequired(p => p.Category)
    .WithMany(c => c.Products)
    .HasForeignKey(p => p.CategoryId)
    .WillCascadeOnDelete(true);          // deleting a category deletes its products

modelBuilder.Entity<Product>()
    .HasRequired(p => p.Brand)
    .WithMany(b => b.Products)
    .HasForeignKey(p => p.BrandId)
    .WillCascadeOnDelete(false);         // deleting a brand does NOT delete products
```

**What's happening:**
- All money columns (`Product.Price`/`ComparePrice`, `Order.*`, `OrderItem.UnitPrice`, `Coupon.*`) are configured as `decimal(18,2)`.
- `Product → Category` cascades on delete; `Product → Brand` does **not** (and `BrandId` is required).
- `Review → Product`, `OrderItem → Order`, `CustomerAddress`, `CartItem`, `UserCart`, `WishlistItem`, `PasswordReset` all cascade from their parents.
- `Order → User` is optional and does **not** cascade (a deleted user keeps their orders).

**Why it matters:** The brand cascade setting is deliberate: you cannot delete a brand that still has products (the FK blocks it), whereas deleting a category removes its products. `OrderItem.Product` is also optional and no-cascade, so an order line survives even if the product is deleted — which is why `OrderItem` stores its own `ProductName`/`Slug`/`ImageUrl` snapshots.

**Trace it further:** [11 - Orders & Order History](11-orders-and-order-history.md).

---

### Byte 22: `EcommerceInitializer` — create, seed, upgrade

**Builds on:** Byte 20.
**Source file(s):** `Data/EcommerceInitializer.cs:9`

**In plain terms:** A custom initializer that creates the database, seeds it, and adds any missing tables/indexes without dropping your data.

**The code:**

```csharp
// Data/EcommerceInitializer.cs:9
public void InitializeDatabase(StoreContext db)
{
    if (!db.Database.Exists())
    {
        db.Database.Create();
        EcommerceSeeder.Seed(db);
        CreateIndexes(db);
    }
    else if (!db.Products.Any())
    {
        EcommerceSeeder.Seed(db);
    }

    EnsureCoreTables(db);
}
```

**What's happening:**
- **Fresh database:** create it, seed it, create indexes.
- **Existing but empty:** seed it.
- **Always:** `EnsureCoreTables` runs guarded `CREATE TABLE IF NOT EXISTS` / `ALTER TABLE` statements to add later tables (`CustomerAddresses`, `Coupons`, `ContactMessages`, `NewsletterSubscribers`, `UserCarts`, `CartItems`, `WishlistItems`, `PasswordResets`) and the `Variant`/`IsApproved` columns.

**Why it matters:** This is a hand-rolled stand-in for EF Migrations. It never drops data, but it also does not alter existing columns in general — only the specific guarded statements listed. Each statement is wrapped in `try/catch` and swallowed, so a partially-failed upgrade is silent.

**Trace it further:** [19 - IIS Deployment & Troubleshooting](19-iis-deployment-and-troubleshooting.md) (empty-catalog issues).

---

### Byte 23: The indexes

**Builds on:** Byte 22.
**Source file(s):** `Data/EcommerceInitializer.cs:186`

**In plain terms:** Unique and lookup indexes are created for slugs, SKUs, emails, order numbers and common filters.

**The code:**

```csharp
// Data/EcommerceInitializer.cs — CreateIndexes (excerpt)
"CREATE UNIQUE INDEX UX_Product_Slug    ON dbo.Products(Slug)",
"CREATE UNIQUE INDEX UX_Product_Sku     ON dbo.Products(Sku)",
"CREATE UNIQUE INDEX UX_User_Email      ON dbo.Users(Email)",
"CREATE UNIQUE INDEX UX_Order_OrderNumber ON dbo.Orders(OrderNumber)",
"CREATE NONCLUSTERED INDEX IX_Product_Price ON dbo.Products(Price)",
"CREATE NONCLUSTERED INDEX IX_Product_CategoryId ON dbo.Products(CategoryId)",
// ... plus IsFeatured, IsDeal, CreatedOn, Rating, Review, Order lookups
```

**What's happening:** Uniqueness is enforced on URL slugs, SKUs, user emails and order numbers. Non-unique indexes support the common filters (category, brand, price, rating, featured/deal flags).

**Why it matters:** Because the app filters in memory via `CatalogCache`, many of these indexes are not actually used for the catalog queries — but they still enforce important uniqueness rules (you cannot create two products with the same slug). `EnsureCoreTables` adds a few more unique indexes (`NewsletterSubscribers.Email`, `Coupons.Code`, etc.).

**Trace it further:** [07 - Search, Filtering, Sorting & Pagination](07-search-filtering-sorting-pagination.md).

---

### Byte 24: The seeder — 2,500 products from a fixed seed

**Builds on:** Byte 22.
**Source file(s):** `Data/EcommerceSeeder.cs:31`

**In plain terms:** A one-shot program that fills an empty database with categories, brands, products, reviews, users and orders — deterministically.

**The code:**

```csharp
// Data/EcommerceSeeder.cs:31
public static void Seed(StoreContext db)
{
    if (db.Products.Any()) return;         // never double-seed

    var rnd = new Random(20261007);        // fixed seed => reproducible data
    var now = DateTime.Now;

    var defs = BuildCategories();
    var categories = SeedCategories(db, defs);
    var brands = SeedBrands(db, defs);
    var products = SeedProducts(db, categories, defs, brands, rnd, now);
    SeedReviews(db, products, rnd);
    var users = SeedUsers(db);
    SeedOrders(db, products, users, rnd);
    db.SaveChanges();
}
```

**What's happening:**
- **Guard:** returns immediately if any product exists.
- **`new Random(20261007)`:** the same seed produces the same product names, prices and stock every time.
- **12 categories** (Electronics, Phones & Tablets, Computers, Furniture, Kitchen, Men's, Women's, Footwear, Beauty, Sports, Toys, Books), each with defined brands/types/price ranges.
- **~2,500 products** (`TargetProductCount = 2500`) distributed across the categories.
- **Reviews** for about half the products, drawn from fixed review templates.
- **9 users** (1 admin + 8 customers).
- **60 orders** with realistic status histories, cities and line items.

**Why it matters:** Seeding is deterministic and idempotent. That makes bugs reproducible but also means the demo data is "fake" and dates are relative to `DateTime.Now` at seed time. Product images are paths like `/Content/images/products/electronics-v0.jpg`, derived from a category slug and a variant index (`0`…`ImageVariants-1`, currently 20), which must exist under `Content/images/products/`.

**Trace it further:** [05 - Product Catalog](05-product-catalog.md), [14 - Admin Dashboard & Management](14-admin-dashboard-and-management.md).

---

### Byte 25: Seeding users and passwords

**Builds on:** Byte 24.
**Source file(s):** `Data/EcommerceSeeder.cs:259`, `Infrastructure/PasswordHasher.cs`

**In plain terms:** Seed users get real hashed passwords created with the same hasher the login uses.

**The code:**

```csharp
// Data/EcommerceSeeder.cs — SeedUsers (excerpt)
var hash = PasswordHasher.Hash(password);
return new User {
    FullName = name, Email = email,
    PasswordHash = hash.Hash, PasswordSalt = hash.Salt,
    Role = role, Phone = phone, ...
};
```

**What's happening:** The seeder calls `PasswordHasher.Hash` for each seeded account, so the seeded passwords work with `PasswordHasher.Verify` at login. One admin account and a demo customer account are created; their demo credentials are displayed on the Sign In page (`Views/Account/Login.cshtml`), so they are intentionally public in this project.

**Why it matters:** Because hashing is salted per user, the stored hashes differ even when two users share the same password. Do **not** reuse these demo credentials in any real deployment.

**Trace it further:** [10 - Authentication & Authorization](10-authentication-and-authorization.md).

---

### Byte 26: Default coupons

**Builds on:** Byte 22.
**Source file(s):** `Data/EcommerceInitializer.cs:144`

**In plain terms:** On startup the app ensures a small set of usable coupons exists.

**The code:**

```csharp
// Data/EcommerceInitializer.cs:144
public static void EnsureDefaultCoupons(StoreContext db)
{
    if (db.Coupons.Any()) return;
    // SAVE10   : 10% off, max ₹500, min order ₹999
    // WELCOME50: ₹50 off, min order ₹499
    // FREESHIP : free shipping, min order ₹0
    db.SaveChanges();
}
```

**What's happening:** If the `Coupons` table is empty, three coupons are inserted. `Application_Start` calls this right after initializing the database.

**Why it matters:** The coupon rules live in the `Coupon` entity and are evaluated by the `Coupons` helper (see [09](09-checkout-and-coupons.md)). These three codes are the ones shown as hints on the cart page.

**Trace it further:** [09 - Checkout & Coupons](09-checkout-and-coupons.md).

---

**Next topic →** [04 - Models & ViewModels](04-models-and-viewmodels.md)
