# 19 - IIS Deployment & Troubleshooting

**Goal of this topic:** run, publish and debug NovaKart — on IIS Express locally and on full IIS, and what to do when it breaks.

---

### Byte 111: Running on IIS Express (the intended path)

**Builds on:** [01 - Project Structure](01-project-structure.md).
**Source file(s):** `src/LegacyEcommerce/LegacyEcommerce.csproj:17`, `Global.asax.cs`

**In plain terms:** The project is configured for IIS Express; you press F5 in Visual Studio.

**The code:**

```xml
<!-- LegacyEcommerce.csproj -->
<TargetFrameworkVersion>v4.7</TargetFrameworkVersion>
<UseIISExpress>true</UseIISExpress>
<ProjectTypeGuids>{349C5851-65DF-11DA-9384-00065B846F21};…</ProjectTypeGuids>
```

**What's happening:** `UseIISExpress=true` and the web-application project-type GUID tell Visual Studio to launch IIS Express on a random `http://localhost:<port>/` when you start debugging. On `Application_Start` the app initializes the database and warms `CatalogCache`.

**Why it matters:** There is **no** `dotnet run` for .NET Framework MVC. The launch command is Visual Studio (or `msbuild` + IIS Express) only. IIS Express and LocalDB run as your user, so no Administrator rights are needed locally.

**Trace it further:** [02 - Application Startup & Request Flow](02-application-startup-and-request-flow.md).

---

### Byte 112: NuGet restore

**Builds on:** Byte 111.
**Source file(s):** `src/LegacyEcommerce/packages.config`, `.gitignore:9`, `tools/nuget.exe`

**In plain terms:** `packages/` is git-ignored, so you must restore NuGet packages before the first build.

**The code:**

```powershell
# Run from the repository root; tools\nuget.exe ships with the repo
& .\tools\nuget.exe restore .\LegacyEcommerce.sln
```

**What's happening:** `packages.config` lists the five packages, and `.gitignore` excludes `packages/` (and `bin/`, `obj/`). Visual Studio usually restores automatically on build; if not, the bundled NuGet CLI does it. The `.csproj` references packages via relative hints like `..\..\packages\...`.

**Why it matters:** A fresh clone will not build until packages are restored. If you see "The type or namespace 'EntityFramework' could not be found", the restore didn't run.

**Trace it further:** [18 - Testing & Verification](18-testing-and-verification.md).

---

### Byte 113: Publishing to full IIS

**Builds on:** Byte 111.
**Source file(s):** `src/LegacyEcommerce/LegacyEcommerce.csproj:23`, `Web.config`

**In plain terms:** You can publish the app to IIS, but LocalDB + Integrated Security needs care.

**What's happening:** In Visual Studio use **Build → Publish** (or `msbuild` with the `Microsoft.WebApplication.targets`), output the folder, then create an IIS site pointing at it with an application pool. Notable project settings: `MvcRazorCompileOnPublish=false`, which means Razor views are **compiled at runtime** on first request (a slower first hit, but no precompiled view assembly).

**Why it matters:** Because views compile at runtime, the `Views` folder and the Razor assemblies must be present in the published output. The `Global.asax` build action and `Web.config` must be deployed too. Test the first request carefully — that's when view compilation happens.

**Trace it further:** Byte 114.

---

### Byte 114: LocalDB, Integrated Security and the app-pool identity

**Builds on:** Byte 113.
**Source file(s):** `Web.config:7`

**In plain terms:** The connection string uses your Windows identity to talk to LocalDB, which a full IIS app pool usually cannot do.

**The code:**

```xml
<!-- Web.config:7 -->
<add name="StoreContext"
     connectionString="Data Source=(LocalDB)\MSSQLLocalDB;Initial Catalog=LegacyEcommerce;Integrated Security=True;MultipleActiveResultSets=True"
     providerName="System.Data.SqlClient" />
```

**What's happening:** `Integrated Security=True` means the process identity is used to authenticate. Under IIS Express that's your user, which owns the `(LocalDB)\MSSQLLocalDB` instance — so it works. Under full IIS, the app pool runs as `ApplicationPoolIdentity`, which by default cannot open your per-user LocalDB.

**Why it matters:** This is the single most common deployment failure. Options: run the pool under your user (bad practice), share the LocalDB instance for "All Users", grant the pool identity access to the LocalDB instance, or switch the connection string to a SQL Server login (`User Id`/`Password`). Any of these is an environment change, not a code change.

**Trace it further:** Byte 116.

---

### Byte 115: IIS handlers, static files and the SEO routes

**Builds on:** Byte 113.
**Source file(s):** `Web.config:35`, `Web.config:42`, `App_Start/RouteConfig.cs:12`

**In plain terms:** `robots.txt` and `sitemap.xml` are routed to MVC while normal assets are served statically.

**The code:**

```xml
<!-- Web.config:42 -->
<add name="RobotsTxtHandler" path="robots.txt" verb="*"
     type="System.Web.Handlers.TransferRequestHandler" preCondition="integratedMode,runtimeVersionv4.0" />
<add name="SitemapXmlHandler" path="sitemap.xml" verb="*"
     type="System.Web.Handlers.TransferRequestHandler" preCondition="integratedMode,runtimeVersionv4.0" />
<add name="StaticFile" path="*" verb="*" type="System.Web.StaticFileHandler" … />
```

```csharp
// RouteConfig.cs:12 — the URLs still map to a controller action
routes.MapRoute(name: "Robots", url: "robots.txt", defaults: new { controller = "Sitemap", action = "Robots" });
```

**What's happening:** The `robots.txt`/`sitemap.xml` handler mappings route those paths through MVC (so the controller can generate them), while `StaticFile` serves CSS/JS/SVG. This requires **Integrated** pipeline mode, which the `preCondition="integratedMode"` reflects.

**Why it matters:** If the app pool is set to Classic mode, these handlers won't apply and `/robots.txt` or `/sitemap.xml` may 404 or be served as static files. Keep the pool in Integrated mode.

**Trace it further:** [17 - Contact, Newsletter & Static Pages](17-contact-newsletter-and-static-pages.md).

---

### Byte 116: Troubleshooting guide

**Builds on:** all deployment topics.
**Source file(s):** several

**In plain terms:** Symptom → likely cause → fix.

**What's happening:**

| Symptom | Likely cause | Fix / where to look |
|---|---|---|
| Build fails: "EntityFramework … not found" | NuGet not restored | Run `tools\nuget.exe restore` (Byte 112) |
| HTTP 500.19 (config error) | Missing IIS URL-rewrite/handler modules or Classic pool | Use Integrated mode; install ASP.NET/Web Application feature |
| HTTP 500.21 (bad handler) | ASP.NET 4.x not registered with IIS | `aspnet_regiis -i` or enable ASP.NET in Windows features |
| HTTP 500 only remotely | `customErrors mode="RemoteOnly"` | Debug locally; check event log; [16](16-validation-security-and-error-handling.md) |
| "Cannot open database" / SQL login failed | LocalDB missing or pool identity lacks access | Byte 114 |
| Catalog is empty / stale | DB not seeded, or `CatalogCache` not refreshed | Restart app (`Application_Start` seeds); [03](03-data-layer-and-database.md) |
| Products show old data after a DB edit | Static in-memory cache | Admin edit triggers refresh; [14](14-admin-dashboard-and-management.md) |
| Antiforgery error on POST | Token missing/expired or session lost | Ensure `@Html.AntiForgeryToken()` in the form; [16](16-validation-security-and-error-handling.md) |
| Everyone logged out / carts reset | InProc session recycled (app restart, pool recycle) | Expected with `sessionState mode="InProc"`; [10](10-authentication-and-authorization.md) |
| `/robots.txt` or `/sitemap.xml` 404 | Classic pipeline or handler removed | Byte 115 |
| Images missing | `Content/images` not deployed or SVGs not generated | Run `scripts/generate-images.ps1`; [01](01-project-structure.md) |

**Why it matters:** Three of these (LocalDB access, Integrated pipeline, InProc session) are environment-specific and account for most real failures when moving from IIS Express to full IIS.

**Trace it further:** [18 - Testing & Verification](18-testing-and-verification.md).

---

*End of the Knowledge Bytes series. Return to the [Knowledge Bytes index](README.md) or the [root README](../README.md).*
