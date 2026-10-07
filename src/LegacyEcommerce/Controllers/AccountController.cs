using System;
using System.Linq;
using System.Web.Mvc;
using LegacyEcommerce.Data;
using LegacyEcommerce.Infrastructure;
using LegacyEcommerce.Models;
using LegacyEcommerce.Models.ViewModels;

namespace LegacyEcommerce.Controllers
{
    public class AccountController : Controller
    {
        private readonly StoreContext db = new StoreContext();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        [HttpGet]
        public ActionResult Login(string returnUrl)
        {
            if (Auth.FromSession(Session) != null) return RedirectToAction("Index");
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.Title = "Sign In";
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model, string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.Title = "Sign In";

            if (!ModelState.IsValid) return View(model);

            var user = db.Users.FirstOrDefault(u => u.Email == model.Email);
            if (user == null || !PasswordHasher.Verify(model.Password, user.PasswordSalt, user.PasswordHash))
            {
                ModelState.AddModelError("", "Email or password is incorrect.");
                return View(model);
            }

            Auth.Login(Session, user);
            CartStore.MergeFromDb(db, Session, user.Id);
            WishlistStore.MergeFromDb(db, Session, user.Id);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(user.Role == "Admin" ? "Dashboard" : "Index", user.Role == "Admin" ? "Admin" : "Account");
        }

        [HttpGet]
        public ActionResult Register()
        {
            if (Auth.FromSession(Session) != null) return RedirectToAction("Index");
            ViewBag.Title = "Create Account";
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel model)
        {
            ViewBag.Title = "Create Account";
            if (!ModelState.IsValid) return View(model);

            var email = model.Email.Trim().ToLowerInvariant();
            if (db.Users.Any(u => u.Email == email))
            {
                ModelState.AddModelError("Email", "An account with this email already exists.");
                return View(model);
            }

            var hash = PasswordHasher.Hash(model.Password);
            var user = new User
            {
                FullName = model.FullName.Trim(),
                Email = email,
                PasswordHash = hash.Hash,
                PasswordSalt = hash.Salt,
                Role = "Customer",
                Phone = model.Phone,
                CreatedOn = DateTime.Now
            };
            db.Users.Add(user);
            db.SaveChanges();

            Auth.Login(Session, user);
            CartStore.SaveIfAuthenticated(db, Session);
            WishlistStore.SaveIfAuthenticated(db, Session);
            TempData["Message"] = "Welcome to NovaKart, " + user.FullName + "!";
            return RedirectToAction("Index");
        }

        public ActionResult Logout()
        {
            Auth.Logout(Session);
            SessionCart.Clear(Session);
            WishlistStore.ClearSession(Session);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public ActionResult ForgotPassword()
        {
            if (Auth.FromSession(Session) != null) return RedirectToAction("Index");
            ViewBag.Title = "Forgot Password";
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            ViewBag.Title = "Forgot Password";
            if (!ModelState.IsValid) return View(model);

            var email = model.Email.Trim().ToLowerInvariant();
            var user = db.Users.FirstOrDefault(u => u.Email == email);
            if (user != null)
            {
                var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                db.PasswordResets.Add(new PasswordReset
                {
                    UserId = user.Id,
                    Token = token,
                    CreatedOn = DateTime.Now,
                    ExpiresOn = DateTime.Now.AddMinutes(30)
                });
                db.SaveChanges();
                ViewBag.ResetLink = Url.Action("ResetPassword", "Account", new { token = token });
            }

            ViewBag.SubmittedEmail = model.Email;
            ViewBag.Title = "Check Your Email";
            return View("ForgotPasswordSent");
        }

        [HttpGet]
        public ActionResult ResetPassword(string token)
        {
            ViewBag.Title = "Reset Password";
            if (FindValidReset(token) == null)
            {
                TempData["Message"] = "This reset link is invalid or has expired. Request a new one below.";
                return RedirectToAction("ForgotPassword");
            }
            return View(new ResetPasswordViewModel { Token = token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(ResetPasswordViewModel model)
        {
            ViewBag.Title = "Reset Password";
            if (!ModelState.IsValid) return View(model);

            var reset = FindValidReset(model.Token);
            if (reset == null)
            {
                TempData["Message"] = "This reset link is invalid or has expired. Request a new one below.";
                return RedirectToAction("ForgotPassword");
            }

            var user = db.Users.Find(reset.UserId);
            if (user == null) return HttpNotFound();

            var hash = PasswordHasher.Hash(model.NewPassword);
            user.PasswordHash = hash.Hash;
            user.PasswordSalt = hash.Salt;
            reset.UsedOn = DateTime.Now;
            db.SaveChanges();

            TempData["Message"] = "Password reset successfully. Sign in with your new password.";
            return RedirectToAction("Login");
        }

        private PasswordReset FindValidReset(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            var now = DateTime.Now;
            return db.PasswordResets.FirstOrDefault(p => p.Token == token && p.UsedOn == null && p.ExpiresOn > now);
        }

        [RequireLogin]
        [HttpGet]
        public ActionResult Index()
        {
            var me = Auth.FromSession(Session);
            var user = db.Users.Find(me.Id);
            var orders = db.Orders
                .Where(o => o.UserId == me.Id)
                .OrderByDescending(o => o.CreatedOn)
                .Take(20)
                .ToList();

            ViewBag.Title = "My Account";
            return View(new AccountOverview { User = user, Orders = orders });
        }

        [RequireLogin]
        public ActionResult Order(string id)
        {
            var me = Auth.FromSession(Session);
            var order = db.Orders.Include("Items")
                .FirstOrDefault(o => o.OrderNumber == id && o.UserId == me.Id);
            if (order == null) return HttpNotFound();

            ViewBag.Title = "Order " + order.OrderNumber;
            return View(order);
        }

        [RequireLogin]
        public ActionResult Invoice(string id)
        {
            var me = Auth.FromSession(Session);
            var order = db.Orders.Include("Items")
                .FirstOrDefault(o => o.OrderNumber == id && o.UserId == me.Id);
            if (order == null) return HttpNotFound();

            ViewBag.Title = "Invoice " + order.OrderNumber;
            return View(order);
        }

        [RequireLogin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelOrder(string id)
        {
            var me = Auth.FromSession(Session);
            var order = db.Orders.Include("Items")
                .FirstOrDefault(o => o.OrderNumber == id && o.UserId == me.Id);
            if (order == null) return HttpNotFound();

            if (order.Status == OrderStatus.Placed || order.Status == OrderStatus.Packed)
            {
                foreach (var item in order.Items)
                {
                    if (item.ProductId.HasValue)
                    {
                        var stock = db.Products.Find(item.ProductId.Value);
                        if (stock != null)
                        {
                            stock.Stock = stock.Stock + item.Quantity;
                        }
                    }
                }

                order.Status = OrderStatus.Cancelled;
                db.SaveChanges();
                CatalogCache.RefreshProducts();
                TempData["Message"] = "Order " + order.OrderNumber + " has been cancelled. Any payment made will be refunded to your original payment method.";
            }
            else
            {
                TempData["Message"] = "This order can no longer be cancelled.";
            }

            return RedirectToAction("Order", new { id = order.OrderNumber });
        }

        [RequireLogin]
        public ActionResult Addresses()
        {
            var me = Auth.FromSession(Session);
            var addresses = db.CustomerAddresses
                .Where(a => a.UserId == me.Id)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedOn)
                .ToList();

            ViewBag.Title = "My Addresses";
            AccountView("addresses");
            return View(addresses);
        }

        [RequireLogin]
        [HttpGet]
        public ActionResult AddressCreate()
        {
            ViewBag.Title = "Add Address";
            ViewBag.FormAction = "AddressCreate";
            AccountView("addresses");
            return View("AddressForm", new AddressViewModel { Country = "India" });
        }

        [RequireLogin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddressCreate(AddressViewModel model)
        {
            ViewBag.Title = "Add Address";
            ViewBag.FormAction = "AddressCreate";
            if (!ModelState.IsValid) return View("AddressForm", model);

            var me = Auth.FromSession(Session);
            var hasAny = db.CustomerAddresses.Any(a => a.UserId == me.Id);
            var address = new CustomerAddress
            {
                UserId = me.Id,
                FullName = model.FullName.Trim(),
                Phone = model.Phone.Trim(),
                AddressLine = model.AddressLine.Trim(),
                City = model.City.Trim(),
                State = model.State.Trim(),
                PostalCode = model.PostalCode.Trim(),
                Country = string.IsNullOrWhiteSpace(model.Country) ? "India" : model.Country.Trim(),
                IsDefault = model.IsDefault || !hasAny,
                CreatedOn = DateTime.Now
            };

            if (address.IsDefault) ClearAddressDefaults(me.Id);
            db.CustomerAddresses.Add(address);
            db.SaveChanges();

            TempData["Message"] = "Address saved.";
            return RedirectToAction("Addresses");
        }

        [RequireLogin]
        [HttpGet]
        public ActionResult AddressEdit(int id)
        {
            var me = Auth.FromSession(Session);
            var address = db.CustomerAddresses.FirstOrDefault(a => a.Id == id && a.UserId == me.Id);
            if (address == null) return HttpNotFound();

            ViewBag.Title = "Edit Address";
            ViewBag.FormAction = "AddressEdit";
            AccountView("addresses");
            return View("AddressForm", new AddressViewModel
            {
                Id = address.Id,
                FullName = address.FullName,
                Phone = address.Phone,
                AddressLine = address.AddressLine,
                City = address.City,
                State = address.State,
                PostalCode = address.PostalCode,
                Country = address.Country,
                IsDefault = address.IsDefault
            });
        }

        [RequireLogin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddressEdit(int id, AddressViewModel model)
        {
            ViewBag.Title = "Edit Address";
            ViewBag.FormAction = "AddressEdit";
            if (!ModelState.IsValid) return View("AddressForm", model);

            var me = Auth.FromSession(Session);
            var address = db.CustomerAddresses.FirstOrDefault(a => a.Id == id && a.UserId == me.Id);
            if (address == null) return HttpNotFound();

            if (model.IsDefault && !address.IsDefault) ClearAddressDefaults(me.Id);

            address.FullName = model.FullName.Trim();
            address.Phone = model.Phone.Trim();
            address.AddressLine = model.AddressLine.Trim();
            address.City = model.City.Trim();
            address.State = model.State.Trim();
            address.PostalCode = model.PostalCode.Trim();
            address.Country = string.IsNullOrWhiteSpace(model.Country) ? "India" : model.Country.Trim();
            address.IsDefault = model.IsDefault;

            db.SaveChanges();

            TempData["Message"] = "Address updated.";
            return RedirectToAction("Addresses");
        }

        [RequireLogin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddressDelete(int id)
        {
            var me = Auth.FromSession(Session);
            var address = db.CustomerAddresses.FirstOrDefault(a => a.Id == id && a.UserId == me.Id);
            if (address != null)
            {
                db.CustomerAddresses.Remove(address);
                db.SaveChanges();
                TempData["Message"] = "Address removed.";
            }
            return RedirectToAction("Addresses");
        }

        [RequireLogin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddressDefault(int id)
        {
            var me = Auth.FromSession(Session);
            var address = db.CustomerAddresses.FirstOrDefault(a => a.Id == id && a.UserId == me.Id);
            if (address != null)
            {
                ClearAddressDefaults(me.Id);
                address.IsDefault = true;
                db.SaveChanges();
            }
            return RedirectToAction("Addresses");
        }

        private void ClearAddressDefaults(int userId)
        {
            foreach (var a in db.CustomerAddresses.Where(x => x.UserId == userId && x.IsDefault).ToList())
            {
                a.IsDefault = false;
            }
        }

        private void AccountView(string section)
        {
            var me = Auth.FromSession(Session);
            ViewData["User"] = me != null ? db.Users.Find(me.Id) : null;
            ViewData["Section"] = section;
        }

        [RequireLogin]
        [HttpGet]
        public ActionResult EditProfile()
        {
            var me = Auth.FromSession(Session);
            var user = db.Users.Find(me.Id);
            if (user == null) return HttpNotFound();

            ViewBag.Title = "Edit Profile";
            AccountView("profile");
            return View(new EditProfileViewModel { FullName = user.FullName, Email = user.Email, Phone = user.Phone });
        }

        [RequireLogin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditProfile(EditProfileViewModel model)
        {
            ViewBag.Title = "Edit Profile";
            AccountView("profile");

            var me = Auth.FromSession(Session);
            var user = db.Users.Find(me.Id);
            if (user == null) return HttpNotFound();

            if (!ModelState.IsValid) return View(model);

            var email = model.Email.Trim().ToLowerInvariant();
            if (db.Users.Any(u => u.Email == email && u.Id != user.Id))
            {
                ModelState.AddModelError("Email", "An account with this email already exists.");
                return View(model);
            }

            user.FullName = model.FullName.Trim();
            user.Email = email;
            user.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
            db.SaveChanges();

            Auth.Login(Session, user);
            TempData["Message"] = "Profile updated.";
            return RedirectToAction("Index");
        }

        [RequireLogin]
        [HttpGet]
        public ActionResult ChangePassword()
        {
            ViewBag.Title = "Change Password";
            AccountView("profile");
            return View(new ChangePasswordViewModel());
        }

        [RequireLogin]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordViewModel model)
        {
            ViewBag.Title = "Change Password";
            AccountView("profile");

            var me = Auth.FromSession(Session);
            var user = db.Users.Find(me.Id);
            if (user == null) return HttpNotFound();

            if (!ModelState.IsValid) return View(model);

            if (!PasswordHasher.Verify(model.CurrentPassword, user.PasswordSalt, user.PasswordHash))
            {
                ModelState.AddModelError("CurrentPassword", "Your current password is incorrect.");
                return View(model);
            }

            var hash = PasswordHasher.Hash(model.NewPassword);
            user.PasswordHash = hash.Hash;
            user.PasswordSalt = hash.Salt;
            db.SaveChanges();

            TempData["Message"] = "Password updated successfully.";
            return RedirectToAction("Index");
        }
    }

    public class AccountOverview
    {
        public User User { get; set; }
        public System.Collections.Generic.List<Order> Orders { get; set; }
    }
}
