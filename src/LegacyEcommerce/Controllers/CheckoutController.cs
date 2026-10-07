using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using LegacyEcommerce.Data;
using LegacyEcommerce.Infrastructure;
using LegacyEcommerce.Models;
using LegacyEcommerce.Models.ViewModels;

namespace LegacyEcommerce.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly StoreContext db = new StoreContext();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        private CartViewModel BuildCart()
        {
            var model = new CartViewModel();
            var lines = SessionCart.Lines(Session);
            if (lines.Count == 0)
            {
                model.Totals = Pricing.Compute(model.Lines, SessionCart.GetCoupon(Session));
                return model;
            }

            var ids = lines.Select(l => l.ProductId).ToList();
            var products = db.Products.Include("Brand").Where(p => ids.Contains(p.Id)).ToList();
            var byId = products.ToDictionary(p => p.Id);
            foreach (var line in lines)
            {
                Product p;
                if (!byId.TryGetValue(line.ProductId, out p)) continue;
                model.Lines.Add(new CartLineView { Product = p, Qty = line.Qty, Variant = line.Variant });
            }
            model.Totals = Pricing.Compute(model.Lines, SessionCart.GetCoupon(Session));
            return model;
        }

        private const string StateKey = "NK.Checkout";

        private CheckoutState GetState()
        {
            return Session[StateKey] as CheckoutState ?? new CheckoutState();
        }

        private void SetState(CheckoutState state)
        {
            Session[StateKey] = state;
        }

        private CheckoutViewModel BuildAddressModel()
        {
            var state = GetState();
            var model = new CheckoutViewModel
            {
                FullName = state.FullName,
                Email = state.Email,
                Phone = state.Phone,
                AddressLine = state.AddressLine,
                City = state.City,
                State = state.State,
                PostalCode = state.PostalCode,
                SelectedAddressId = state.SelectedAddressId,
                SaveAddress = state.SaveAddress,
                Notes = state.Notes
            };
            PrefillFromRequest(model);
            return model;
        }

        private void PrefillFromRequest(CheckoutViewModel model)
        {
            if (!string.IsNullOrEmpty(model.FullName)) return;

            var user = Auth.FromSession(Session);
            if (user == null) return;

            var dbUser = db.Users.Find(user.Id);
            if (dbUser != null)
            {
                model.FullName = dbUser.FullName;
                model.Email = dbUser.Email;
                model.Phone = dbUser.Phone;
            }

            var preset = db.CustomerAddresses
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedOn)
                .FirstOrDefault();
            if (preset != null)
            {
                model.SelectedAddressId = preset.Id;
                model.FullName = preset.FullName;
                model.Phone = preset.Phone;
                model.AddressLine = preset.AddressLine;
                model.City = preset.City;
                model.State = preset.State;
                model.PostalCode = preset.PostalCode;
            }
        }

        private void LoadAddresses(CheckoutViewModel model)
        {
            var user = Auth.FromSession(Session);
            if (user == null) return;
            model.Addresses = db.CustomerAddresses
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedOn)
                .ToList();
        }

        [HttpGet]
        public ActionResult Index()
        {
            var cart = BuildCart();
            if (cart.IsEmpty)
            {
                TempData["Message"] = "Your cart is empty - add something you love first.";
                return RedirectToAction("Index", "Cart");
            }

            ViewBag.Title = "Checkout - Address";
            ViewBag.Step = 1;
            var model = BuildAddressModel();
            model.Cart = cart;
            LoadAddresses(model);
            return View("Address", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Address(CheckoutViewModel model)
        {
            model.Cart = BuildCart();
            if (model.Cart.IsEmpty)
            {
                TempData["Message"] = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Title = "Checkout - Address";
                ViewBag.Step = 1;
                LoadAddresses(model);
                return View("Address", model);
            }

            var state = GetState();
            state.AddressDone = true;
            state.SelectedAddressId = model.SelectedAddressId;
            state.SaveAddress = model.SaveAddress;
            state.FullName = model.FullName.Trim();
            state.Email = model.Email.Trim();
            state.Phone = model.Phone.Trim();
            state.AddressLine = model.AddressLine.Trim();
            state.City = model.City.Trim();
            state.State = model.State.Trim();
            state.PostalCode = model.PostalCode.Trim();
            state.Notes = model.Notes;
            SetState(state);

            return RedirectToAction("Payment");
        }

        [HttpGet]
        public ActionResult Payment()
        {
            var cart = BuildCart();
            if (cart.IsEmpty)
            {
                TempData["Message"] = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }
            var state = GetState();
            if (!state.AddressDone) return RedirectToAction("Index");

            ViewBag.Title = "Checkout - Payment";
            ViewBag.Step = 2;
            ViewBag.Cart = cart;
            var model = new PaymentStepViewModel { PaymentMethod = state.PaymentMethod };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Payment(PaymentStepViewModel model)
        {
            var cart = BuildCart();
            if (cart.IsEmpty)
            {
                TempData["Message"] = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }
            var state = GetState();
            if (!state.AddressDone) return RedirectToAction("Index");

            var method = (model.PaymentMethod ?? "").Trim();
            string detail = null;

            if (method == "Card")
            {
                var digits = new string((model.CardNumber ?? "").Where(char.IsDigit).ToArray());
                if (digits.Length != 16)
                {
                    ModelState.AddModelError("CardNumber", "Card number must be 16 digits.");
                }
                if (string.IsNullOrWhiteSpace(model.CardName) || model.CardName.Trim().Length < 3)
                {
                    ModelState.AddModelError("CardName", "Name on card is required.");
                }
                if (model.Expiry == null || !System.Text.RegularExpressions.Regex.IsMatch(model.Expiry, @"^(0[1-9]|1[0-2])\/[0-9]{2}$"))
                {
                    ModelState.AddModelError("Expiry", "Use MM/YY format.");
                }
                if (model.Cvv == null || !System.Text.RegularExpressions.Regex.IsMatch(model.Cvv, @"^[0-9]{3,4}$"))
                {
                    ModelState.AddModelError("Cvv", "CVV must be 3 or 4 digits.");
                }
                detail = "Card ending " + digits.Substring(Math.Max(0, digits.Length - 4));
            }
            else if (method == "UPI")
            {
                if (string.IsNullOrWhiteSpace(model.UpiId) || !System.Text.RegularExpressions.Regex.IsMatch(model.UpiId.Trim(), @"^[a-zA-Z0-9._\-]{2,}@[a-zA-Z]{2,}$"))
                {
                    ModelState.AddModelError("UpiId", "Enter a valid UPI ID (e.g. name@okhdfc).");
                }
                detail = "UPI: " + model.UpiId.Trim();
            }
            else if (method == "COD")
            {
                detail = "Cash on Delivery";
            }
            else
            {
                ModelState.AddModelError("PaymentMethod", "Choose a payment method.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Title = "Checkout - Payment";
                ViewBag.Step = 2;
                ViewBag.Cart = cart;
                return View(model);
            }

            state.PaymentDone = true;
            state.PaymentMethod = method;
            state.PaymentDetail = detail;
            SetState(state);

            return RedirectToAction("Review");
        }

        [HttpGet]
        public ActionResult Review()
        {
            var cart = BuildCart();
            if (cart.IsEmpty)
            {
                TempData["Message"] = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }
            var state = GetState();
            if (!state.AddressDone) return RedirectToAction("Index");
            if (!state.PaymentDone) return RedirectToAction("Payment");

            ViewBag.Title = "Checkout - Review";
            ViewBag.Step = 3;
            return View(new ReviewOrderViewModel { Cart = cart, State = state });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Confirm()
        {
            var cart = BuildCart();
            if (cart.IsEmpty)
            {
                TempData["Message"] = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }
            var state = GetState();
            if (!state.AddressDone) return RedirectToAction("Index");
            if (!state.PaymentDone) return RedirectToAction("Payment");

            var sessionUser = Auth.FromSession(Session);
            if (sessionUser != null && state.SaveAddress)
            {
                SaveAddressToBook(sessionUser.Id, state);
            }

            var totals = cart.Totals;
            var order = new Order
            {
                OrderNumber = NextOrderNumber(),
                UserId = sessionUser != null ? sessionUser.Id : (int?)null,
                Email = state.Email,
                FullName = state.FullName,
                Phone = state.Phone,
                AddressLine = state.AddressLine,
                City = state.City,
                State = state.State,
                PostalCode = state.PostalCode,
                Subtotal = totals.Subtotal,
                Discount = totals.Discount,
                Shipping = totals.Shipping,
                Tax = totals.Tax,
                Total = totals.Total,
                Status = OrderStatus.Placed,
                PaymentMethod = state.PaymentMethod,
                PaymentRef = state.PaymentMethod == "COD" ? null : "TXN" + DateTime.Now.ToString("yyMMddHHmm") + new Random().Next(100, 999),
                CouponCode = totals.CouponValid ? totals.CouponCode : null,
                CreatedOn = DateTime.Now,
                Items = new List<OrderItem>()
            };

            foreach (var line in cart.Lines)
            {
                order.Items.Add(new OrderItem
                {
                    ProductId = line.Product.Id,
                    ProductName = line.Product.Name + (string.IsNullOrEmpty(line.Variant) ? "" : " (" + line.Variant + ")"),
                    ImageUrl = line.Product.ImageUrl,
                    Slug = line.Product.Slug,
                    UnitPrice = line.Product.Price,
                    Quantity = line.Qty
                });

                var stock = db.Products.Find(line.Product.Id);
                if (stock != null)
                {
                    var left = stock.Stock - line.Qty;
                    stock.Stock = left < 0 ? 0 : left;
                }
            }

            if (totals.CouponValid && !string.IsNullOrEmpty(totals.CouponCode))
            {
                var coupon = db.Coupons.FirstOrDefault(c => c.Code == totals.CouponCode);
                if (coupon != null)
                {
                    coupon.TotalUses += 1;
                }
            }

            db.Orders.Add(order);
            db.SaveChanges();

            SessionCart.Clear(Session);
            if (sessionUser != null)
            {
                CartStore.Clear(db, sessionUser.Id);
            }
            Session[StateKey] = null;
            Session["NK.LastOrder"] = order.OrderNumber;

            return RedirectToAction("Success", new { id = order.OrderNumber });
        }

        public ActionResult Success(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return HttpNotFound();
            var order = db.Orders.Include("Items").FirstOrDefault(o => o.OrderNumber == id);
            if (order == null) return HttpNotFound();

            ViewBag.Title = "Order Confirmed";
            return View(order);
        }

        private void SaveAddressToBook(int userId, CheckoutState model)
        {
            if (model.SelectedAddressId.HasValue)
            {
                var existing = db.CustomerAddresses.FirstOrDefault(a => a.Id == model.SelectedAddressId.Value && a.UserId == userId);
                if (existing != null)
                {
                    existing.FullName = model.FullName.Trim();
                    existing.Phone = model.Phone.Trim();
                    existing.AddressLine = model.AddressLine.Trim();
                    existing.City = model.City.Trim();
                    existing.State = model.State.Trim();
                    existing.PostalCode = model.PostalCode.Trim();
                    db.SaveChanges();
                    return;
                }
            }

            var hasAny = db.CustomerAddresses.Any(a => a.UserId == userId);
            db.CustomerAddresses.Add(new CustomerAddress
            {
                UserId = userId,
                FullName = model.FullName.Trim(),
                Phone = model.Phone.Trim(),
                AddressLine = model.AddressLine.Trim(),
                City = model.City.Trim(),
                State = model.State.Trim(),
                PostalCode = model.PostalCode.Trim(),
                Country = "India",
                IsDefault = !hasAny,
                CreatedOn = DateTime.Now
            });
            db.SaveChanges();
        }

        private string NextOrderNumber()
        {
            var prefix = "NK" + DateTime.Now.ToString("yyMMdd") + "-";
            for (int i = 0; i < 40; i++)
            {
                var candidate = prefix + new Random().Next(1000, 9999);
                if (!db.Orders.Any(o => o.OrderNumber == candidate)) return candidate;
            }
            return prefix + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
        }
    }
}
