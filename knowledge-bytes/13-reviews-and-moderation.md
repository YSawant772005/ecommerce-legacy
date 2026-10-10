# 13 - Reviews & Moderation

**Goal of this topic:** understand how reviews are posted, how the product rating is recalculated, and how admins moderate them.

---

### Byte 77: Posting a review

**Builds on:** [06 - Product Details & Variants](06-product-details-and-variants.md).
**Source file(s):** `Controllers/ProductsController.cs:169`

**In plain terms:** A signed-in customer submits a rating (1–5), a headline and a body; it is saved immediately.

**The code:**

```csharp
// Controllers/ProductsController.cs:169
[HttpPost]
[ValidateAntiForgeryToken]
[RequireLogin]
public ActionResult ReviewPost(ReviewViewModel model)
{
    if (model.ProductId <= 0) return HttpNotFound();
    var product = db.Products.Find(model.ProductId);
    if (product == null) return HttpNotFound();

    if (!ModelState.IsValid)
    {
        TempData["Message"] = "Please pick a star rating and fill in your review.";
        return RedirectToAction("Details", new { slug = product.Slug, tab = "reviews" });
    }
    ...
}
```

**What's happening:** The action is protected by `[ValidateAntiForgeryToken]` **and** `[RequireLogin]` (so anonymous posts are rejected — the AJAX variant returns the `login_required` JSON if the request body isn't right). Invalid input redirects back to the reviews tab with a `TempData` message rather than re-rendering.

**Why it matters:** This is a good example of stacking attributes: CSRF protection, authentication, and model validation all in one action. Redirect-after-POST avoids duplicate submissions on refresh.

**Trace it further:** Byte 78, and [16 - Validation, Security & Error Handling](16-validation-security-and-error-handling.md).

---

### Byte 78: Verified purchases and the running average

**Builds on:** Byte 77.
**Source file(s):** `Controllers/ProductsController.cs:184`

**In plain terms:** The app marks a review "verified" only if the user has a delivered order for that product, and updates the product's average rating.

**The code:**

```csharp
// Controllers/ProductsController.cs:184
var me = Auth.FromSession(Session);
var verified = db.OrderItems
    .Any(i => i.ProductId == product.Id && i.Order.UserId == me.Id
              && i.Order.Status == OrderStatus.Delivered);

db.Reviews.Add(new Review {
    ProductId = product.Id, AuthorName = me != null ? me.Name : "Guest",
    Rating = model.Rating, Title = model.Title.Trim(), Body = model.Body.Trim(),
    VerifiedPurchase = verified,
    IsApproved = true,                 // auto-approved on submit
    CreatedOn = DateTime.Now
});

var totalScore = product.Rating * product.RatingCount + model.Rating;
product.RatingCount = product.RatingCount + 1;
product.Rating = Math.Round(totalScore / product.RatingCount, 1);
product.ReviewCount = product.ReviewCount + 1;
db.SaveChanges();
CatalogCache.RefreshProducts();
```

**What's happening:** `verified` is true when the reviewer has a **Delivered** order containing that product. The product's rating is recomputed with a true running average — `(oldRating * oldCount + newRating) / newCount`, rounded to 1 decimal — and `ReviewCount` is incremented. The cached product list is refreshed so the new rating shows everywhere.

**Why it matters:** The running-average update means the stored `Rating` never drifts from the sum of reviews. Because `CatalogCache.RefreshProducts` is called, the new rating appears on cards immediately. New reviews are **auto-approved** (`IsApproved = true`) — moderation is post-hoc, not pre-publication.

**Trace it further:** Byte 79.

---

### Byte 79: Moderation — hide and delete

**Builds on:** Byte 78.
**Source file(s):** `Controllers/AdminController.cs:364`, `Controllers/AdminController.cs:402`

**In plain terms:** Admins can hide a review (toggle approval) or delete it permanently.

**The code:**

```csharp
// Controllers/AdminController.cs:402
[HttpPost]
[ValidateAntiForgeryToken]
public ActionResult ReviewToggle(int id)
{
    var review = db.Reviews.Find(id);
    if (review != null)
    {
        review.IsApproved = !review.IsApproved;
        db.SaveChanges();
        TempData["Message"] = review.IsApproved
            ? "Review approved and visible on the product page."
            : "Review hidden from the product page.";
    }
    return RedirectToAction("Reviews");
}
```

```csharp
// Controllers/AdminController.cs:418
[HttpPost]
[ValidateAntiForgeryToken]
public ActionResult ReviewDelete(int id)
{
    var review = db.Reviews.Find(id);
    if (review != null) { db.Reviews.Remove(review); db.SaveChanges(); }
    return RedirectToAction("Reviews");
}
```

**What's happening:** `ReviewToggle` flips `IsApproved`; `ReviewDelete` removes the row. The moderation page (`Reviews`) lists reviews, with filters for `visible`/`hidden`, shows the hidden count, and includes the product name.

**Why it matters:** Only `IsApproved` reviews render on the product page, so hiding is an effective soft-delete. Note that toggling/deleting **does not recompute** the product's `Rating`/`ReviewCount` — the average can therefore drift from the visible reviews. This is a documented limitation.

**Trace it further:** [14 - Admin Dashboard & Management](14-admin-dashboard-and-management.md).

---

### Byte 80: Displaying reviews

**Builds on:** Byte 78.
**Source file(s):** `Controllers/ProductsController.cs:127`, `Views/Products/Details.cshtml:213`

**In plain terms:** The product page shows the average, the star breakdown and the latest approved reviews.

**The code:**

```csharp
// Controllers/ProductsController.cs:127
var reviews = db.Reviews
    .Where(r => r.ProductId == product.Id && r.IsApproved)
    .OrderByDescending(r => r.CreatedOn)
    .Take(12).ToList();
```

```cshtml
@* Views/Products/Details.cshtml:259 *@
@foreach (var r in Model.Reviews.Take(8))
{
    <div class="review">
        <span class="r-avatar">@(r.AuthorName == null ? "?" : r.AuthorName.Substring(0, 1).ToUpper())</span>
        <b>@r.AuthorName</b>
        <span class="r-rating">@Html.Stars(r.Rating)</span>
        <div class="r-title">@r.Title</div>
        <p class="r-text">@r.Body</p>
    </div>
}
```

**What's happening:** The controller loads up to 12 approved reviews; the view renders the first 8 (the rest are available to the tab counter). The `Html.Stars` helper draws filled/half stars from a decimal rating.

**Why it matters:** The `Take(8)` in the view and `Take(12)` in the controller are independent caps — a small quirk, but harmless. `Html.Stars` centralises the star markup so cards and reviews stay consistent.

**Trace it further:** [04 - Models & ViewModels](04-models-and-viewmodels.md) (`Html.Stars` helper lives in `Infrastructure/HtmlExtensions.cs`).

---

**Next topic →** [14 - Admin Dashboard & Management](14-admin-dashboard-and-management.md)
