# 17 - Contact, Newsletter & Static Pages

**Goal of this topic:** understand the home page, the informational pages, and the contact/newsletter forms, plus SEO files.

---

### Byte 100: The home page

**Builds on:** [05 - Product Catalog](05-product-catalog.md).
**Source file(s):** `Controllers/HomeController.cs:21`

**In plain terms:** The home page collects featured, deal and new-arrival products plus store-wide counts.

**The code:**

```csharp
// Controllers/HomeController.cs:21 (excerpt)
var model = new HomeViewModel
{
    Categories  = CatalogCache.Categories,
    Featured    = db.Products.Where(p => p.IsFeatured).OrderByDescending(p => p.SoldCount).Take(8).ToList(),
    Deals       = db.Products.Where(p => p.IsDeal && p.ComparePrice != null).OrderByDescending(p => p.SoldCount).Take(8).ToList(),
    NewArrivals = db.Products.Where(p => p.IsNew).OrderByDescending(p => p.CreatedOn).Take(8).ToList(),
    ProductCount = db.Products.Count(),
    BrandCount   = db.Brands.Count(),
    CustomerCount = db.Users.Count(u => u.Role == "Customer"),
    OrderCount   = db.Orders.Count()
};

if (model.Featured.Count < 8)
    model.Featured = db.Products.OrderByDescending(p => p.SoldCount).Take(8).ToList();
```

**What's happening:** Three curated rows (featured / deals / new) are pulled with `Take(8)`, and the page shows live totals. If fewer than 8 products are flagged featured, it falls back to the top-selling products so the row is never sparse.

**Why it matters:** The fallback keeps the homepage looking full even before an admin flags products. Deals require both `IsDeal` and a non-null `ComparePrice`, so the discount badge always has a real compare price to show.

**Trace it further:** [14 - Admin Dashboard & Management](14-admin-dashboard-and-management.md) (flags are set in product management).

---

### Byte 101: The informational pages

**Builds on:** Byte 100.
**Source file(s):** `Controllers/HomeController.cs:47`, `Controllers/HomeController.cs:83`

**In plain terms:** About, FAQ, Privacy, Terms, Shipping and Returns are simple "return a view" actions.

**The code:**

```csharp
// Controllers/HomeController.cs:47
public ActionResult About() { ViewBag.Title = "About NovaKart"; return View(); }
public ActionResult Faq()   { ViewBag.Title = "Frequently Asked Questions"; return View(); }
public ActionResult Privacy(){ ViewBag.Title = "Privacy Policy"; return View(); }
public ActionResult Terms() { ViewBag.Title = "Terms of Service"; return View(); }
public ActionResult Shipping(){ ViewBag.Title = "Shipping Policy"; return View(); }
public ActionResult Returns(){ ViewBag.Title = "Returns & Refunds"; return View(); }
```

**What's happening:** Each action sets a title and renders its own view under `Views/Home/`. The content is static Razor markup — no database access.

**Why it matters:** These pages are linked from the footer (see `_Layout`) and included in the sitemap. They are reached by the default `/{controller}/{action}` route as `/home/about`, `/home/faq`, etc.

**Trace it further:** Byte 104.

---

### Byte 102: The contact form

**Builds on:** Byte 101.
**Source file(s):** `Controllers/HomeController.cs:83`, `Models/ContactMessage.cs`

**In plain terms:** Contact submissions are validated and saved to the `ContactMessages` table for the admin inbox.

**The code:**

```csharp
// Controllers/HomeController.cs:91
[HttpPost]
[ValidateAntiForgeryToken]
public ActionResult Contact(ContactViewModel model)
{
    ViewBag.Title = "Contact Us";
    if (!ModelState.IsValid) return View(model);

    db.ContactMessages.Add(new ContactMessage
    {
        Name = model.Name.Trim(),
        Email = model.Email.Trim(),
        Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim(),
        Topic = string.IsNullOrWhiteSpace(model.Topic) ? "General enquiry" : model.Topic,
        Message = model.Message.Trim(),
        IsRead = false,
        CreatedOn = DateTime.Now
    });
    db.SaveChanges();

    TempData["ContactOk"] = "Thanks " + model.Name.Split(' ')[0] + "! ...";
    return RedirectToAction("Contact");
}
```

**What's happening:** After the anti-forgery token check and model validation, the message is stored with `IsRead = false` and a default topic when none is given. A `TempData` success message is set and the user is redirected back (redirect-after-POST).

**Why it matters:** Saved messages appear in the admin `Messages` inbox (Byte 85), where `MessageRead` flips `IsRead`. No email is sent — the message simply lands in the database.

**Trace it further:** [14 - Admin Dashboard & Management](14-admin-dashboard-and-management.md).

---

### Byte 103: Newsletter subscribe and unsubscribe

**Builds on:** Byte 102.
**Source file(s):** `Controllers/HomeController.cs:115`

**In plain terms:** Subscribing adds or reactivates a subscriber; unsubscribing flips `IsActive` off.

**The code:**

```csharp
// Controllers/HomeController.cs:128
var email = model.Email.Trim();
var existing = db.NewsletterSubscribers.FirstOrDefault(n => n.Email == email);
if (existing == null)
{
    db.NewsletterSubscribers.Add(new NewsletterSubscriber { Email = email, IsActive = true, CreatedOn = DateTime.Now });
    db.SaveChanges();
    TempData["NewsletterMsg"] = "Thanks! You are on the list.";
}
else if (!existing.IsActive)
{
    existing.IsActive = true;   // re-subscribe
    db.SaveChanges();
    TempData["NewsletterMsg"] = "Welcome back! You are subscribed again.";
}
else
{
    TempData["NewsletterMsg"] = "You are already subscribed to our newsletter.";
}
```

**What's happening:** The subscribe action handles three cases — new, previously-unsubscribed (reactivate) and already subscribed (informational message). The footer form posts here with an anti-forgery token. Afterward it redirects to the `UrlReferrer` **only if the referrer host matches** the current host (a same-host guard).

**Why it matters:** Reactivating rather than duplicating keeps the subscriber table clean (`UX_Newsletter_Email` also enforces uniqueness). The referrer host check prevents an open redirect via a crafted `Referer` header. Unsubscribe is a two-step GET/POST on the same action.

**Trace it further:** [14 - Admin Dashboard & Management](14-admin-dashboard-and-management.md) (admin can toggle subscribers).

---

### Byte 104: `robots.txt` and `sitemap.xml`

**Builds on:** Byte 101.
**Source file(s):** `Controllers/SitemapController.cs`, `App_Start/RouteConfig.cs:12`, `Web.config:42`

**In plain terms:** SEO files are generated dynamically by a controller, not served as static files.

**The code:**

```csharp
// App_Start/RouteConfig.cs:12
routes.MapRoute(name: "Robots",  url: "robots.txt",  defaults: new { controller = "Sitemap", action = "Robots" });
routes.MapRoute(name: "Sitemap", url: "sitemap.xml", defaults: new { controller = "Sitemap", action = "Index" });
```

```csharp
// Controllers/SitemapController.cs:32
[OutputCache(Duration = 3600, VaryByParam = "none", Location = OutputCacheLocation.Server)]
public ActionResult Index()
{
    var sb = new StringBuilder();
    sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
    sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
    // home, /products, static pages, then each category (/c/{slug}) and product (/product/{slug})
    ...
    return Content(sb.ToString(), "text/xml", Encoding.UTF8);
}
```

**What's happening:** Custom routes map the `robots.txt` and `sitemap.xml` URLs to `SitemapController`. `Robots` disallows `/admin`, `/account`, `/cart`, `/checkout` and `/wishlist`, and points to the sitemap. `Index` builds XML for the home page, product list, static pages, every category and up to 45,000 products (the sitemap protocol limit), cached server-side for one hour. `Web.config` also registers handlers so IIS hands those paths to MVC.

**Why it matters:** `OutputCache` with `Location=Server` avoids regenerating the XML on every crawl. The `Take(45000)` cap is exactly the sitemap spec's per-file limit. The base URL is derived from the request, so it works on whatever host serves it.

**Trace it further:** [19 - IIS Deployment & Troubleshooting](19-iis-deployment-and-troubleshooting.md).

---

### Byte 105: The unused announcement bar

**Builds on:** Byte 100.
**Source file(s):** `Controllers/HomeController.cs:181`, `Views/Shared/_Layout.cshtml:32`

**In plain terms:** There is a child action for an announcement bar, but its partial view does not exist and the layout uses a hard-coded promo bar instead.

**The code:**

```csharp
// Controllers/HomeController.cs:181
[ChildActionOnly]
public ActionResult AnnouncementBar()
{
    return PartialView("_AnnouncementBar");
}
```

```cshtml
@* Views/Shared/_Layout.cshtml:32 — actual promo bar (hard-coded, no child action) *@
<div class="promo-bar">
    Free delivery above &#8377;999 <span>&bull;</span> Extra 10% off with code <b>SAVE10</b> <span>&bull;</span> Easy 15-day returns
</div>
```

**What's happening:** `AnnouncementBar` would render `_AnnouncementBar.cshtml`, but no such view exists in `Views/Shared/` or `Views/Home/`. Nothing calls the action — the layout renders a static `promo-bar` div instead.

**Why it matters:** This is dead code. Calling it would throw a "view not found" error. It's a good example of a leftover from an earlier design and is worth mentioning so readers don't assume the banner is data-driven.

**Trace it further:** [02 - Application Startup & Request Flow](02-application-startup-and-request-flow.md) (views that don't exist).

---

**Next topic →** [18 - Testing & Verification](18-testing-and-verification.md)
