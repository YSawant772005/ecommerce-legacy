# 01 - Project Structure

**Goal of this topic:** learn what every top-level folder is for, so you can navigate the codebase quickly.

---

### Byte 09: The repository root

**Builds on:** [00 - Project Architecture](00-project-architecture.md).
**Source file(s):** `.gitignore`, `LegacyEcommerce.sln`, `tools/nuget.exe`, `scripts/generate-images.ps1`

**In plain terms:** The root holds the solution, learning docs, and a couple of helper scripts — not application code.

**The structure:**

```text
ecommerce-legacy/
├── LegacyEcommerce.sln          # VS solution (references the one project)
├── README.md                    # Project overview
├── knowledge-bytes/             # These lessons
├── packages/                    # Restored NuGet packages (git-ignored)
├── scripts/generate-images.ps1  # Downloads the local photo pack
├── tools/nuget.exe              # NuGet CLI for restore
└── src/LegacyEcommerce/         # The web application itself
```

**What's happening:** All application code lives under `src/LegacyEcommerce/`. The solution is a single-project solution. `packages/` is a build artifact restored from NuGet and is git-ignored; `tools/nuget.exe` exists so you can restore without a system-wide NuGet.

**Why it matters:** When you add a file, you normally add it under `src/LegacyEcommerce/`. The `.csproj` uses wildcard includes (`**\*.cs`), so new source files compile without editing the project.

**Trace it further:** [02 - Application Startup & Request Flow](02-application-startup-and-request-flow.md).

---

### Byte 10: The application project folders

**Builds on:** Byte 09.
**Source file(s):** `src/LegacyEcommerce/`

**In plain terms:** Each folder maps to one concern of MVC.

**The structure (verified):**

```text
src/LegacyEcommerce/
├── App_Start/       RouteConfig.cs, FilterConfig.cs
├── Content/         site.css, admin.css, favicon.svg, images/ (253 JPEG photos)
├── Controllers/     Home, Products, Cart, Checkout, Account,
│                    Admin, Wishlist, Sitemap, Error
├── Data/            StoreContext.cs, EcommerceInitializer.cs, EcommerceSeeder.cs
├── Infrastructure/  Auth, CartStore, HtmlExtensions, PasswordHasher,
│                    SessionCart, WishlistStore
├── Models/          entity classes + ViewModels/ViewModels.cs
├── Properties/      AssemblyInfo.cs
├── Scripts/         site.js, catalog.js, cart.js, pdp.js, checkout.js
├── Views/           Razor views + Shared/ partials + Web.config
├── Global.asax(.cs) application lifecycle
├── Web.config       configuration + connection string
├── packages.config  NuGet list
└── LegacyEcommerce.csproj
```

**What's happening:** MVC conventions are followed literally: `Controllers/` holds one class per area, `Views/<Controller>/` mirrors controller names, `Models/` holds data types. `Infrastructure/` is a house-made folder for static helpers (not part of MVC's conventions).

**Why it matters:** New features usually touch three folders: a controller, a model/view model, and a view. Knowing the conventions means you can predict where a file should go.

**Trace it further:** [03 - Data Layer & Database](03-data-layer-and-database.md), [04 - Models & ViewModels](04-models-and-viewmodels.md).

---

### Byte 11: The web.config files

**Builds on:** Byte 10.
**Source file(s):** `src/LegacyEcommerce/Web.config`, `src/LegacyEcommerce/Views/Web.config`

**In plain terms:** There are two web.config files: one configures the app, the other registers the Razor view-engine namespaces and blocks direct access to the `.cshtml` files.

**The code:**

```xml
<!-- src/LegacyEcommerce/Web.config -->
<connectionStrings>
  <add name="StoreContext"
       connectionString="Data Source=(LocalDB)\MSSQLLocalDB;Initial Catalog=LegacyEcommerce;Integrated Security=True;MultipleActiveResultSets=True"
       providerName="System.Data.SqlClient" />
</connectionStrings>
```

**What's happening:**
- The root `Web.config` sets the connection string, `customErrors`, `sessionState` (InProc, 240 min), `globalization` (`en-IN`) and the entityFramework provider.
- `Views/Web.config` is the standard MVC file that registers the `System.Web.Mvc` and `System.Web.Razor` view-engine namespaces; it is not app configuration.

**Why it matters:** The connection string name `StoreContext` is referenced explicitly by `StoreContext`'s constructor. Rename one and you must rename the other.

**Trace it further:** [03 - Data Layer & Database](03-data-layer-and-database.md), [19 - IIS Deployment & Troubleshooting](19-iis-deployment-and-troubleshooting.md).

---

### Byte 12: Views and the shared folder

**Builds on:** Byte 10.
**Source file(s):** `src/LegacyEcommerce/Views/`

**In plain terms:** Views are grouped by controller and share a layout plus reusable partials.

**The structure (verified):**

```text
Views/
├── _ViewStart.cshtml     # sets Layout = ~/Views/Shared/_Layout.cshtml
├── Web.config
├── Shared/
│   ├── _Layout.cshtml        # storefront chrome (header, nav, footer)
│   ├── _ProductCard.cshtml   # one product tile
│   ├── _ProductGrid.cshtml   # list of tiles
│   └── _RecentlyViewed.cshtml
├── Admin/_AdminLayout.cshtml # back-office chrome
├── Account/ Admin/ Cart/ Checkout/ Error/ Home/ Products/ Wishlist/
```

**What's happening:** `_ViewStart.cshtml` applies the storefront layout to every view unless the view overrides it (the Admin views set `_AdminLayout`). Partial views (names starting with `_`) are rendered inside other views.

**Why it matters:** Changing `_Layout.cshtml` changes the header/footer of the entire storefront at once. Partials let the same product card render on the home page, catalog and wishlist.

**Trace it further:** [05 - Product Catalog](05-product-catalog.md), [15 - Frontend: Views, JavaScript & CSS](15-frontend-views-javascript-css.md).

---

### Byte 13: Content and Scripts

**Builds on:** Byte 10.
**Source file(s):** `Content/`, `Scripts/`

**In plain terms:** One CSS file for the storefront, one for admin, and five plain JavaScript files.

**The structure:**

```text
Content/
├── site.css      # storefront styles
├── admin.css     # admin styles
├── favicon.svg
└── images/       # 253 downloaded JPEG photos (categories/, products/, placeholder)
Scripts/
├── site.js       # shared: fmt, toast, fetch helpers, add-to-cart, wishlist, search suggest
├── catalog.js    # listing page: infinite scroll / paging, sort, mode toggle
├── cart.js       # cart page: qty, remove, coupon, clear
├── pdp.js        # product page: gallery, tabs, variant swatches
└── checkout.js   # checkout: address radio prefill
```

**What's happening:** There is no bundler/minifier and no external JS library. All scripts are loaded as-is. `site.js` defines a small global namespace `window.Nova` that the other scripts depend on.

**Why it matters:** Because `site.js` must load before `catalog.js`, `cart.js`, etc., views include it via `_Layout.cshtml` and append page-specific scripts through the `@RenderSection("scripts")` slot.

**Trace it further:** [15 - Frontend: Views, JavaScript & CSS](15-frontend-views-javascript-css.md).

---

### Byte 14: Where images come from

**Builds on:** Byte 13.
**Source file(s):** `scripts/generate-images.ps1`, `Content/images/`

**In plain terms:** Product and category pictures are real JPEG photographs downloaded once by a PowerShell script and cached on disk, so the app stays offline-friendly.

**The script's job:** `generate-images.ps1` downloads deterministic photographs from `picsum.photos` (seeded per file, via `curl.exe`) into `Content/images/categories/*.jpg` and `Content/images/products/*.jpg` — the paths the seeder references (for example `/Content/images/products/electronics-v0.jpg`). It also writes a `placeholder.jpg` fallback and the `favicon.svg` brand mark.

**Why it matters:** The seeder stores image *paths* derived from a category slug and a variant index (`-v0`…`-v19`); see `EcommerceSeeder.ImageVariants`, which must stay in sync with the script's `-Variants` parameter. There are **253** JPEG files under `Content/images/` (~12 MB). Pass `-Force` to re-download or `-Variants N` for more variety. If you delete them, product tiles lose their images (they do not crash, but the app needs internet to fetch them again).

**Trace it further:** [03 - Data Layer & Database](03-data-layer-and-database.md) (seeding writes these paths).

---

**Next topic →** [02 - Application Startup & Request Flow](02-application-startup-and-request-flow.md)
