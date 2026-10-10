# 18 - Testing & Verification

**Goal of this topic:** know how to verify the app by hand, what it does not do, and how to extend it safely.

---

### Byte 106: There are no automated tests

**Builds on:** the whole app.
**Source file(s):** `LegacyEcommerce.sln`, `src/LegacyEcommerce/packages.config`

**In plain terms:** The solution contains exactly one project and no test project.

**What's happening:** `LegacyEcommerce.sln` references a single project (`src\LegacyEcommerce\LegacyEcommerce.csproj`). There is no xUnit/NUnit/MSTest package in `packages.config`, and no `*Tests` project anywhere in the repository.

**Why it matters:** There is no `dotnet test`/`vstest` command to run. Verification is manual, by exercising the running site. This is expected for a small class project but it means changes need manual re-checking.

**Trace it further:** Byte 107.

---

### Byte 107: A manual smoke-test checklist

**Builds on:** Byte 106.
**Source file(s):** the running site

**In plain terms:** A repeatable click-path that touches every major feature.

**What's happening:** With the app running (see [01 - Project Structure](01-project-structure.md) and [19 - IIS Deployment](19-iis-deployment-and-troubleshooting.md)), verify:

1. **Home** loads categories, featured/deals/new rows and counters.
2. **Catalog** `/products`: sort, page size, mode toggle (scroll vs pages), filters (category, price, brand, rating), and infinite scroll loads more.
3. **Search** box shows suggestions after 2 characters; Enter navigates.
4. **Product page**: gallery thumbnails, tabs, variant selection, Add to Cart / Buy Now.
5. **Cart** as a guest: add, change qty (caps at 10), remove, apply a coupon, see totals.
6. **Sign in** as the seeded demo customer, confirm the guest cart/wishlist merge.
7. **Checkout**: Address → Payment (try invalid card/UPI to see validation) → Review → Confirm; check the receipt and that stock dropped.
8. **Account**: order history, invoice, cancel an order (stock returns).
9. **Wishlist**: toggle hearts, view `/wishlist`.
10. **Review**: post a review and see the average update.
11. **Admin** (as the seeded admin): dashboard numbers, product create/edit/delete, order status change, coupon create, message/review moderation.
12. **SEO**: open `/robots.txt` and `/sitemap.xml`.
13. **Errors**: visit a bad URL to see the custom 404 page.

**Why it matters:** Steps 6, 7 and 11 are the ones most likely to regress, because they cross controller/session/database boundaries. Run them after any change to pricing, cart, checkout or auth.

**Trace it further:** each linked topic.

---

### Byte 108: Known limitations

**Builds on:** [16 - Validation, Security & Error Handling](16-validation-security-and-error-handling.md).
**Source file(s):** whole app

**In plain terms:** The prototype intentionally skips several production concerns.

**What's happening:**
- **No real payment gateway** — card/UPI are format-validated only; `PaymentRef` is fabricated.
- **No email** — password-reset links and newsletters are never sent; reset links render on screen.
- **Session-only auth** — no Forms-auth ticket; restarting the app/session logs users out.
- **In-memory catalog** — `CatalogCache` holds all products; filtering is in-memory (fine for ~2,500 rows, not for millions).
- **Guessable receipts** — `Success`/order numbers aren't ownership-checked.
- **No `Views/Shared/Error.cshtml`** — the global `HandleErrorAttribute` is inert; `customErrors` does the work.
- **Dead code** — `HomeController.AnnouncementBar` targets a missing partial.
- **Coupon reuse and rating drift** — as noted in [09](09-checkout-and-coupons.md) and [13](13-reviews-and-moderation.md).

**Why it matters:** These are documented on purpose. When extending the app, decide which of these you actually need to fix (usually payment, email and auth first) rather than assuming they work.

**Trace it further:** Byte 109.

---

### Byte 109: Extension recipes

**Builds on:** the whole app.
**Source file(s):** several

**In plain terms:** The common "how would I add X?" answers.

**What's happening:**

**Add a product field** (e.g. `WarrantyMonths`):
1. Add the property to `Models/Product.cs` (with annotations).
2. EF6 will add the column on next `Initialize` (or update the schema manually).
3. Show it in `Views/Admin/ProductForm.cshtml` and add it to the `TryUpdateModel` allow-list in `AdminController.ProductEdit`.
4. Use it in `Views/Products/Details.cshtml` if needed, then `CatalogCache.RefreshProducts()`.

**Add a storefront page** (e.g. `/home/careers`):
1. Add `public ActionResult Careers()` to `HomeController` (set `ViewBag.Title`).
2. Create `Views/Home/Careers.cshtml`.
3. Link it from the footer in `Views/Shared/_Layout.cshtml`; add it to the sitemap if it should be indexed.

**Add a clean URL** (e.g. `/brand/{slug}`):
1. Add a `MapRoute` in `App_Start/RouteConfig.cs` **above** the `Default` route.
2. Point it at a controller/action and read the route value.
3. Generate links with `Url.RouteUrl("RouteName", new { slug = … })`.

**Change shipping/tax/discounts**:
- Edit the constants in `Pricing` (`Infrastructure`… actually `Models/ViewModels/ViewModels.cs`): `FreeShippingThreshold`, `ShippingFee`, `TaxRate`. Everything downstream updates automatically.

**Why it matters:** Following these mirror existing patterns (annotations, allow-list, cache refresh, route ordering). The `TryUpdateModel` allow-list and `CatalogCache.RefreshProducts()` steps are the two that are easy to forget.

**Trace it further:** [04 - Models & ViewModels](04-models-and-viewmodels.md), [14 - Admin Dashboard & Management](14-admin-dashboard-and-management.md).

---

### Byte 110: Dependencies at a glance

**Builds on:** Byte 106.
**Source file(s):** `src/LegacyEcommerce/packages.config`, `Web.config`

**In plain terms:** The whole app runs on five NuGet packages.

**The code:**

```xml
<!-- packages.config -->
<package id="EntityFramework"                version="6.4.4"  targetFramework="net47" />
<package id="Microsoft.AspNet.Mvc"           version="5.2.9"  targetFramework="net47" />
<package id="Microsoft.AspNet.Razor"         version="3.2.9"  targetFramework="net47" />
<package id="Microsoft.AspNet.WebPages"      version="3.2.9"  targetFramework="net47" />
<package id="Microsoft.Web.Infrastructure"   version="2.0.0"  targetFramework="net47" />
```

**What's happening:** EF6 provides the ORM; MVC/Razor/WebPages provide the web framework; Web.Infrastructure is the assembly-registration helper those packages depend on. `Web.config` pins matching assembly versions and adds binding redirects.

**Why it matters:** The dependency list is deliberately tiny — no jQuery, no Bootstrap, no bundling/minification package. All interactivity is hand-written vanilla JavaScript (`site.js`, etc.). Any new client library would need to be added to `packages.config` and referenced explicitly.

**Trace it further:** [01 - Project Structure](01-project-structure.md).

---

**Next topic →** [19 - IIS Deployment & Troubleshooting](19-iis-deployment-and-troubleshooting.md)
