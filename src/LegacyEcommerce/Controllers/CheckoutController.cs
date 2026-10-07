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
                model.Lines.Add(new CartLineView { Product = p, Qty = line.Qty });
            }
            model.Totals = Pricing.Compute(model.Lines, SessionCart.GetCoupon(Session));
            return model;
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

            var model = new CheckoutViewModel { Cart = cart };
            var user = Auth.FromSession(Session);
            if (user != null)
            {
                var dbUser = db.Users.Find(user.Id);
                if (dbUser != null)
                {
                    model.FullName = dbUser.FullName;
                    model.Email = dbUser.Email;
                    model.Phone = dbUser.Phone;
                }

                var addresses = db.CustomerAddresses
                    .Where(a => a.UserId == user.Id)
                    .OrderByDescending(a => a.IsDefault)
                    .ThenByDescending(a => a.CreatedOn)
                    .ToList();
                model.Addresses = addresses;

                var preset = addresses.FirstOrDefault();
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

            ViewBag.Title = "Checkout";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(CheckoutViewModel model)
        {
            model.Cart = BuildCart();
            if (model.Cart.IsEmpty)
            {
                TempData["Message"] = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Title = "Checkout";
                return View(model);
            }

            var sessionUser = Auth.FromSession(Session);
            if (sessionUser != null && model.SaveAddress)
            {
                SaveAddressToBook(sessionUser.Id, model);
            }

            var totals = model.Cart.Totals;
            var order = new Order
            {
                OrderNumber = NextOrderNumber(),
                UserId = sessionUser != null ? sessionUser.Id : (int?)null,
                Email = model.Email,
                FullName = model.FullName,
                Phone = model.Phone,
                AddressLine = model.AddressLine,
                City = model.City,
                State = model.State,
                PostalCode = model.PostalCode,
                Subtotal = totals.Subtotal,
                Discount = totals.Discount,
                Shipping = totals.Shipping,
                Tax = totals.Tax,
                Total = totals.Total,
                Status = OrderStatus.Placed,
                PaymentMethod = model.PaymentMethod,
                PaymentRef = model.PaymentMethod == "COD" ? null : "TXN" + DateTime.Now.ToString("yyMMddHHmm") + new Random().Next(100, 999),
                CouponCode = totals.CouponValid ? totals.CouponCode : null,
                CreatedOn = DateTime.Now,
                Items = new List<OrderItem>()
            };

            foreach (var line in model.Cart.Lines)
            {
                order.Items.Add(new OrderItem
                {
                    ProductId = line.Product.Id,
                    ProductName = line.Product.Name,
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

        private void SaveAddressToBook(int userId, CheckoutViewModel model)
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
