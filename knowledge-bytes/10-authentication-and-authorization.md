# 10 - Authentication & Authorization

**Goal of this topic:** understand how login works, how passwords are stored, and how admin pages are protected.

---

### Byte 62: A session-based login (not Forms auth)

**Builds on:** [04 - Models & ViewModels](04-models-and-viewmodels.md).
**Source file(s):** `Infrastructure/Auth.cs:15`, `Controllers/AccountController.cs:30`, `Web.config`

**In plain terms:** There is no ASP.NET Forms authentication here — "logged in" simply means a `SessionUser` object exists in the session.

**The code:**

```csharp
// Infrastructure/Auth.cs:15
public class SessionUser { public int Id; public string Name, Email, Role; }

public static class Auth
{
    private const string UserKey = "NK.User";
    public static SessionUser FromSession(HttpSessionStateBase session) => session[UserKey] as SessionUser;

    public static void Login(HttpSessionStateBase session, User user) =>
        session[UserKey] = new SessionUser { Id = user.Id, Name = user.FullName,
            Email = user.Email, Role = user.Role };

    public static bool IsAdmin(HttpSessionStateBase session) =>
        session[UserKey] is SessionUser u && u.Role == "Admin";
}
```

**What's happening:** `Web.config` sets `<authentication mode="None" />`. On a successful login the controller calls `Auth.Login(Session, user)`, storing a small snapshot (`Id`, `Name`, `Email`, `Role`) under the session key `NK.User`. Every page reads this via `Auth.FromSession(Session)`.

**Why it matters:** Because identity lives only in session (in-proc, cookie `ASP.NET_SessionId`), there is no persistent auth ticket. Editing `Web.config` or using Forms auth elsewhere would break this design. The `Role` string (`"Admin"` vs `"Customer"`) is the sole basis for admin checks.

**Trace it further:** Byte 65 (the admin guard).

---

### Byte 63: Password hashing with PBKDF2

**Builds on:** Byte 62.
**Source file(s):** `Infrastructure/PasswordHasher.cs`

**In plain terms:** Passwords are never stored in plain text — they are run through PBKDF2 with a random salt.

**The code:**

```csharp
// Infrastructure/PasswordHasher.cs
private const int Iterations = 120000;

public static PasswordHash Hash(string password)
{
    var saltBytes = new byte[16];
    using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(saltBytes);
    var salt = Convert.ToBase64String(saltBytes);
    return new PasswordHash { Salt = salt, Hash = Compute(password, salt) };
}

public static bool Verify(string password, string salt, string expectedHash)
{
    if (string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(expectedHash)) return false;
    try { return string.Equals(Compute(password, salt), expectedHash, StringComparison.Ordinal); }
    catch { return false; }
}

private static string Compute(string password, string salt) =>
    Convert.ToBase64String(new Rfc2898DeriveBytes(password, Convert.FromBase64String(salt), Iterations).GetBytes(32));
```

**What's happening:** A cryptographically random 16-byte salt is generated per user; the password+salt is stretched with `Rfc2898DeriveBytes` (PBKDF2-SHA1) for 120,000 iterations to produce a 32-byte hash. Both salt and hash are base64 strings stored on the `User` row. `Verify` recomputes and compares.

**Why it matters:** This is the correct approach for this era of .NET Framework and matches the seeded users (the seeder calls `PasswordHasher.Hash` too). `Verify` swallows exceptions (e.g. a malformed stored salt) and returns `false`, so a corrupt row can never authenticate.

**Trace it further:** [03 - Data Layer & Database](03-data-layer-and-database.md) (seeded accounts).

---

### Byte 64: `RequireLogin` and JSON-aware redirects

**Builds on:** Byte 62.
**Source file(s):** `Infrastructure/Auth.cs:57`

**In plain terms:** A custom action filter turns away anonymous users — sending AJAX calls a 401 JSON, and normal requests a redirect to login.

**The code:**

```csharp
// Infrastructure/Auth.cs:57
public class RequireLoginAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        if (Auth.FromSession(filterContext.HttpContext.Session) == null)
        {
            var request = filterContext.HttpContext.Request;
            bool isAjax = string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest",
                                        StringComparison.OrdinalIgnoreCase);
            if (isAjax)
            {
                filterContext.Result = new JsonResult {
                    Data = new { ok = false, error = "login_required" },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet };
                filterContext.HttpContext.Response.StatusCode = 401;
            }
            else
            {
                filterContext.Result = new RedirectResult(
                    "~/account/login?returnUrl=" + HttpUtility.UrlEncode(request.RawUrl));
            }
        }
    }
}
```

**What's happening:** `[RequireLogin]` is applied to account, order and wishlist actions. For AJAX requests it returns `{ ok:false, error:"login_required" }` with HTTP 401; otherwise it redirects with an encoded `returnUrl`.

**Why it matters:** The `isAjax` branch is why JavaScript can detect "needs login" without following an HTML redirect to the login page. The `returnUrl` is checked with `Url.IsLocalUrl` at login (Byte 62) — an **open-redirect guard** that is easy to overlook.

**Trace it further:** [12 - Wishlist](12-wishlist.md) (uses this filter on its toggle endpoint).

---

### Byte 65: The admin guard

**Builds on:** Byte 64.
**Source file(s):** `Infrastructure/Auth.cs:84`, `Controllers/AdminController.cs:14`

**In plain terms:** `[RequireAdmin]` sends anonymous users to login and signed-in non-admins to the home page.

**The code:**

```csharp
// Infrastructure/Auth.cs:84
public class RequireAdminAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        var user = Auth.FromSession(filterContext.HttpContext.Session);
        if (user == null)
            filterContext.Result = new RedirectResult(
                "~/account/login?returnUrl=" + HttpUtility.UrlEncode(filterContext.HttpContext.Request.RawUrl));
        else if (!string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            filterContext.Result = new RedirectResult("~/");
    }
}
```

**What's happening:** The `AdminController` class-level `[RequireAdmin]` protects every admin action. Anonymous → login; wrong role → home page. The role comparison is case-insensitive.

**Why it matters:** This is a coarse but effective guard. It protects the whole admin surface including destructive POST actions. Note it is an MVC filter, not the older `AuthorizeAttribute`, because there is no Forms-auth principal to role-check against.

**Trace it further:** [14 - Admin Dashboard & Management](14-admin-dashboard-and-management.md).

---

### Byte 66: Register, forgot password and reset

**Builds on:** Byte 63.
**Source file(s):** `Controllers/AccountController.cs:67`, `Controllers/AccountController.cs:118`

**In plain terms:** Registration hashes and stores a user; "forgot password" creates a 30-minute, single-use reset token.

**The code:**

```csharp
// Controllers/AccountController.cs:127 — token creation
var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
db.PasswordResets.Add(new PasswordReset {
    UserId = user.Id, Token = token,
    CreatedOn = DateTime.Now, ExpiresOn = DateTime.Now.AddMinutes(30)
});
db.SaveChanges();
ViewBag.ResetLink = Url.Action("ResetPassword", "Account", new { token = token });
```

```csharp
// Controllers/AccountController.cs:183 — validity check
return db.PasswordResets.FirstOrDefault(p => p.Token == token && p.UsedOn == null && p.ExpiresOn > now);
```

**What's happening:** Registration lower-cases the email, rejects duplicates, stores the hash+salt and signs the new user in. Forgot-password issues a 128-hex-char token valid for 30 minutes and single-use (a non-null `UsedOn` invalidates it). Reset re-hashes the new password.

**Why it matters:** There is **no email delivery** — the controller places the reset link in `ViewBag.ResetLink` and renders it on the "check your email" page (`ForgotPasswordSent`). This is a demo shortcut; in production that link would be emailed and never shown. The docs should flag this clearly rather than imply email works.

**Trace it further:** [16 - Validation, Security & Error Handling](16-validation-security-and-error-handling.md).

---

**Next topic →** [11 - Orders & Order History](11-orders-and-order-history.md)
