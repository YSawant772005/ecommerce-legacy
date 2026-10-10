# Knowledge Bytes — NovaKart

Welcome. **Knowledge Bytes** is a set of small, focused lessons that explain how the NovaKart (LegacyEcommerce) ASP.NET MVC 5 application works, one concept at a time.

Each lesson is built from **Bytes**. A Byte is a short, self-contained explanation (about 30–45 seconds to read) with a consistent shape:

- **Builds on:** prerequisite Bytes or topics.
- **Source file(s):** the real files the Byte is based on.
- **In plain terms:** a one-sentence analogy or summary.
- **The code:** a small, real snippet (trimmed, with a pointer to line numbers).
- **What's happening:** a step-by-step walk-through.
- **Why it matters:** the design/behavior consequence.
- **Trace it further:** where to look next.

Everything here is grounded in the actual repository at commit `d6fe96a`. Where something could not be verified, it is called out explicitly. Code snippets may be lightly trimmed for focus, but the behavior described matches the files under `src/LegacyEcommerce/`.

> New to the project? Read the [root README](../README.md) first, then follow the topics below **in order** — each topic builds on the previous ones.

---

## How to read this documentation

Pick the path that matches your goal:

- **"I want to understand the whole system."** Read topics 00 → 19 in order.
- **"I need to run it locally."** Read [README sections 8–12](../README.md), then topics [02](02-application-startup-and-request-flow.md) and [03](03-data-layer-and-database.md).
- **"I'm following the shopping flow."** Topics [05](05-product-catalog.md) → [06](06-product-details-and-variants.md) → [07](07-search-filtering-sorting-pagination.md) → [08](08-cart-and-session-management.md) → [09](09-checkout-and-coupons.md) → [11](11-orders-and-order-history.md).
- **"I care about accounts and roles."** Topic [10](10-authentication-and-authorization.md).
- **"I'm working on the admin area."** Topics [13](13-reviews-and-moderation.md) and [14](14-admin-dashboard-and-management.md).
- **"I need to deploy or debug."** Topics [16](16-validation-security-and-error-handling.md) and [19](19-iis-deployment-and-troubleshooting.md).

The suggested reading order is also outlined in the [root README section 18](../README.md).

---

## Topics

| # | Topic | What it covers |
|---|---|---|
| [00](00-project-architecture.md) | **Project Architecture** | The big picture: monolith structure, layers, request lifecycle, dependency map, architectural risks |
| [01](01-project-structure.md) | **Project Structure** | Every top-level folder and why it exists |
| [02](02-application-startup-and-request-flow.md) | **Application Startup & Request Flow** | `Global.asax`, culture, routing, filters, end-to-end request lifecycle |
| [03](03-data-layer-and-database.md) | **Data Layer & Database** | `StoreContext`, `EcommerceInitializer`, `EcommerceSeeder`, connection & seeding |
| [04](04-models-and-viewmodels.md) | **Models & ViewModels** | Entities, `OrderStatus`, view models, `Pricing`, `CatalogCache` |
| [05](05-product-catalog.md) | **Product Catalog** | Home page and product listing, `_ProductCard`/`_ProductGrid` partials |
| [06](06-product-details-and-variants.md) | **Product Details & Variants** | Gallery, color/size variants, related/recently-viewed, `pdp.js` |
| [07](07-search-filtering-sorting-pagination.md) | **Search, Filtering, Sorting & Pagination** | Live search suggest, filter facets, sorting, infinite scroll vs paging |
| [08](08-cart-and-session-management.md) | **Cart & Session Management** | `SessionCart`, `CartStore`, AJAX cart endpoints, guest vs signed-in carts |
| [09](09-checkout-and-coupons.md) | **Checkout & Coupons** | 3-step checkout, coupons, pricing math, mock payment, `checkout.js` |
| [10](10-authentication-and-authorization.md) | **Authentication & Authorization** | `Auth`, `SessionUser`, `PasswordHasher`, `RequireLogin`/`RequireAdmin` |
| [11](11-orders-and-order-history.md) | **Orders & Order History** | Order/OrderItem lifecycle, placing orders, status, invoice |
| [12](12-wishlist.md) | **Wishlist** | `WishlistStore` and AJAX toggling |
| [13](13-reviews-and-moderation.md) | **Reviews & Moderation** | Review submit, verified-purchase flag, admin approve/hide/delete |
| [14](14-admin-dashboard-and-management.md) | **Admin Dashboard & Management** | Metrics, product/order/coupon/customer management, cache refresh |
| [15](15-frontend-views-javascript-css.md) | **Frontend: Views, JavaScript & CSS** | Layouts, partials, `site.js`, `catalog.js`, `pdp.js`, CSS structure |
| [16](16-validation-security-and-error-handling.md) | **Validation, Security & Error Handling** | Model validation, antiforgery, XSS encoding, error pages, known weaknesses |
| [17](17-contact-newsletter-and-static-pages.md) | **Contact, Newsletter & Static Pages** | Contact form, newsletter, static content, SEO routes |
| [18](18-testing-and-verification.md) | **Testing & Verification** | Why there are no tests and how to verify manually |
| [19](19-iis-deployment-and-troubleshooting.md) | **IIS Deployment & Troubleshooting** | IIS Express vs IIS, pool identity, LocalDB access, common failures |

---

## Conventions used

- **File paths** are relative to the repository root, e.g. `src/LegacyEcommerce/Controllers/HomeController.cs`.
- **Line numbers** like `HomeController.cs:21` point at the file mentioned on the same line.
- **"Verified"** means the statement was checked against a file; **"not implemented"** / **"appears unused"** flags anything that looks present but does nothing (for example the missing `_AnnouncementBar.cshtml`).
- **Do not** copy real secrets. This codebase contains none (LocalDB uses Integrated Security); seeded demo credentials are shown in the app UI, not reproduced here.

---

*Start here → [00 - Project Architecture](00-project-architecture.md)*
