# 16 - Validation, Security & Error Handling

**Goal of this topic:** understand how input is validated, how output is protected, how the app handles errors, and where its real security limits are.

---

### Byte 94: Model validation

**Builds on:** [04 - Models & ViewModels](04-models-and-viewmodels.md).
**Source file(s):** `Models/*.cs`, `Controllers/AccountController.cs:37`

**In plain terms:** Data annotations on models do the validation, and `ModelState.IsValid` decides what happens.

**The code:**

```csharp
// Models/Order.cs:11 — annotations on User
[Required, StringLength(150)]            public string FullName { get; set; }
[Required, StringLength(200), EmailAddress] public string Email { get; set; }
[Required, StringLength(200)]            public string PasswordHash { get; set; }
```

```csharp
// Controllers/AccountController.cs:37
if (!ModelState.IsValid) return View(model);
```

**What's happening:** Properties carry `[Required]`, `[StringLength]`, `[EmailAddress]`, `[Range]`, etc. MVC's model binder runs them before the action, and the action checks `ModelState.IsValid`, re-rendering the form with errors when it is false. `Web.config` has `ClientValidationEnabled` and `UnobtrusiveJavaScriptEnabled` set to true, so forms also validate in the browser.

**Why it matters:** Server-side validation is the authority (client validation is only a convenience and can be bypassed). The pattern "check `ModelState.IsValid`, else return the view" recurs in every POST action.

**Trace it further:** Byte 95.

---

### Byte 95: Output encoding and XSS

**Builds on:** Byte 94.
**Source file(s):** `Views/Products/Details.cshtml:27`, `Views/Products/Details.cshtml:55`

**In plain terms:** Razor HTML-encodes `@value` automatically; the only raw output is JSON-LD, and it is encoded first.

**The code:**

```cshtml
@* Details.cshtml:27 — every interpolated value is JSON-escaped first *@
Func<string, string> J = s => System.Web.HttpUtility.JavaScriptStringEncode(s == null ? "" : s);

var ld = "{\"@context\":\"https://schema.org\",\"@type\":\"Product\",…\"name\":\"" + J(p.Name) + "\"…}";
...
<script type="application/ld+json">@Html.Raw(ld)</script>
```

**What's happening:** By default Razor encodes text written with `@`, which is the primary XSS defence. The one `@Html.Raw` in the codebase (the product JSON-LD block) is only safe because each interpolated value passes through `JavaScriptStringEncode` via `J`.

**Why it matters:** This shows the correct way to emit JSON-LD without opening an XSS hole. A review `Body` or product `Name` containing `</script>` is escaped by `J`, so it cannot break out of the script tag. Any new `@Html.Raw` would need the same care.

**Trace it further:** [06 - Product Details & Variants](06-product-details-and-variants.md).

---

### Byte 96: Anti-forgery tokens (and the AJAX caveat)

**Builds on:** Byte 94.
**Source file(s):** `Views/Shared/_Layout.cshtml:160`, `Controllers/ProductsController.cs:170`

**In plain terms:** Form POSTs include an anti-forgery token; the JSON cart/wishlist endpoints do not.

**The code:**

```cshtml
@* _Layout.cshtml:159 — newsletter form carries a token *@
<form class="newsletter" action="@Url.Action("Newsletter", "Home")" method="post">
    @Html.AntiForgeryToken()
    <input type="email" name="Email" … />
</form>
```

```csharp
// Controllers/ProductsController.cs:169
[HttpPost]
[ValidateAntiForgeryToken]
[RequireLogin]
public ActionResult ReviewPost(ReviewViewModel model) { … }
```

**What's happening:** All the "real" form POSTs (login, register, review, checkout steps, admin actions) pair `@Html.AntiForgeryToken()` in the view with `[ValidateAntiForgeryToken]` on the action. The AJAX cart/wishlist endpoints (`/cart/add`, `/wishlist/toggle`, etc.) are **not** decorated and don't send a token, because `Nova.post` doesn't include one.

**Why it matters:** This is a genuine trade-off in the design: form-based state changes are CSRF-protected, but the JSON cart and wishlist mutations are not. Document it honestly — the endpoints are same-origin and only affect the caller's own session, which limits the impact, but it is not the same protection as the forms.

**Trace it further:** [08 - Cart & Session Management](08-cart-and-session-management.md), [15 - Frontend: Views, JavaScript & CSS](15-frontend-views-javascript-css.md).

---

### Byte 97: Error handling and custom error pages

**Builds on:** [02 - Application Startup & Request Flow](02-application-startup-and-request-flow.md).
**Source file(s):** `App_Start/FilterConfig.cs`, `Web.config:28`, `Controllers/ErrorController.cs`

**In plain terms:** HTTP 404/500 are redirected to friendly error pages by `customErrors`.

**The code:**

```xml
<!-- Web.config:28 -->
<customErrors mode="RemoteOnly">
  <error statusCode="404" redirect="~/error/notfound" />
  <error statusCode="500" redirect="~/error/index" />
</customErrors>
```

```csharp
// Controllers/ErrorController.cs:7
public ActionResult Index()
{
    Response.StatusCode = 500;
    Response.TrySkipIisCustomErrors = true;
    ViewBag.Title = "Something went wrong";
    return View("Error");
}
```

**What's happening:** `mode="RemoteOnly"` means developers on the local machine see the full stack trace while remote visitors see the friendly page. `ErrorController` sets the correct status code and `TrySkipIisCustomErrors = true` so IIS doesn't replace the page with its own. A global `HandleErrorAttribute` is registered in `FilterConfig`, but **there is no `Views/Shared/Error.cshtml`**, so it cannot render and the request falls through to the `customErrors` redirect instead.

**Why it matters:** The missing `Error.cshtml` is a real quirk: the global filter is effectively inert and `customErrors` does the work. This is why `~/error/index` is the page users actually see on a 500.

**Trace it further:** Byte 98.

---

### Byte 98: Headers and transport

**Builds on:** Byte 97.
**Source file(s):** `Web.config:47`, `Web.config:19`

**In plain terms:** The app sets one security header and runs in debug mode over plain HTTP.

**The code:**

```xml
<!-- Web.config:47 -->
<httpProtocol>
  <customHeaders>
    <add name="X-Content-Type-Options" value="nosniff" />
  </customHeaders>
</httpProtocol>
<!-- Web.config:19 -->
<compilation debug="true" targetFramework="4.7">
```

**What's happening:** `X-Content-Type-Options: nosniff` stops browsers from guessing content types. There is **no** HSTS, CSP or `X-Frame-Options` header, `debug="true"` is on, and the connection string uses SQL Server LocalDB with Integrated Security.

**Why it matters:** These are the concrete hardening items to note for a production deployment: turn off `debug`, add HSTS/`X-Frame-Options`, and enforce HTTPS. `debug="true"` also disables some request validation behaviour and shows detailed errors locally.

**Trace it further:** [19 - IIS Deployment & Troubleshooting](19-iis-deployment-and-troubleshooting.md).

---

### Byte 99: Known limitations (be honest about them)

**Builds on:** Byte 98.
**Source file(s):** whole app

**In plain terms:** A few things this prototype deliberately does *not* do.

**What's happening:**
- **No real payment** — checkout generates a fake `PaymentRef` and validates card/UPI formats only.
- **No email** — password reset links and newsletters are never actually sent; reset links are shown on screen.
- **Guessable order pages** — `Success` and order numbers (`NK{date}-{4 digits}`) have no ownership check.
- **No login throttling** — there is no lockout or rate limiting on `Login`.
- **Coupon reuse** — `TotalUses` is informational; there is no per-user limit.
- **Review averages can drift** — hiding/deleting a review does not recompute `Product.Rating`.
- **AJAX mutations lack anti-forgery tokens** (Byte 96).
- **Session-only auth** — restarting the app or the session cookie pool logs everyone out.

**Why it matters:** Knowing these limits is part of understanding the codebase. They are appropriate shortcuts for a class project but must be called out clearly so no one mistakes the prototype for production-ready commerce.

**Trace it further:** [18 - Testing & Verification](18-testing-and-verification.md).

---

**Next topic →** [17 - Contact, Newsletter & Static Pages](17-contact-newsletter-and-static-pages.md)
