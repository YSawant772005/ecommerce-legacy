using System;
using System.Linq;
using System.Web.Mvc;
using LegacyEcommerce.Data;
using LegacyEcommerce.Infrastructure;
using LegacyEcommerce.Models;
using LegacyEcommerce.Models.ViewModels;

namespace LegacyEcommerce.Controllers
{
    public class HomeController : Controller
    {
        private readonly StoreContext db = new StoreContext();

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        public ActionResult Index()
        {
            var model = new HomeViewModel
            {
                Categories = CatalogCache.Categories,
                Featured = db.Products.Where(p => p.IsFeatured)
                    .OrderByDescending(p => p.SoldCount).Take(8).ToList(),
                Deals = db.Products.Where(p => p.IsDeal && p.ComparePrice != null)
                    .OrderByDescending(p => p.SoldCount).Take(8).ToList(),
                NewArrivals = db.Products.Where(p => p.IsNew)
                    .OrderByDescending(p => p.CreatedOn).Take(8).ToList(),
                ProductCount = db.Products.Count(),
                BrandCount = db.Brands.Count(),
                CustomerCount = db.Users.Count(u => u.Role == "Customer"),
                OrderCount = db.Orders.Count()
            };

            if (model.Featured.Count < 8)
            {
                model.Featured = db.Products.OrderByDescending(p => p.SoldCount).Take(8).ToList();
            }

            ViewBag.Title = "NovaKart - Online Shopping for Electronics, Fashion, Home & More";
            return View(model);
        }

        public ActionResult About()
        {
            ViewBag.Title = "About NovaKart";
            return View();
        }

        public ActionResult Faq()
        {
            ViewBag.Title = "Frequently Asked Questions";
            return View();
        }

        public ActionResult Privacy()
        {
            ViewBag.Title = "Privacy Policy";
            return View();
        }

        public ActionResult Terms()
        {
            ViewBag.Title = "Terms of Service";
            return View();
        }

        public ActionResult Shipping()
        {
            ViewBag.Title = "Shipping Policy";
            return View();
        }

        public ActionResult Returns()
        {
            ViewBag.Title = "Returns & Refunds";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Title = "Contact Us";
            return View(new ContactViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Contact(ContactViewModel model)
        {
            ViewBag.Title = "Contact Us";
            if (!ModelState.IsValid)
            {
                return View(model);
            }

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

            TempData["ContactOk"] = "Thanks " + model.Name.Split(' ')[0] + "! Your message has been received and our team will get back to you within 24 hours.";
            return RedirectToAction("Contact");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Newsletter(NewsletterViewModel model)
        {
            var referer = Request.UrlReferrer;
            var fallback = Url.Action("Index", "Home");

            if (!ModelState.IsValid)
            {
                TempData["NewsletterMsg"] = "Please enter a valid email address to subscribe.";
                return Redirect(referer != null && referer.Host == Request.Url.Host ? referer.PathAndQuery : fallback);
            }

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
                existing.IsActive = true;
                db.SaveChanges();
                TempData["NewsletterMsg"] = "Welcome back! You are subscribed again.";
            }
            else
            {
                TempData["NewsletterMsg"] = "You are already subscribed to our newsletter.";
            }

            return Redirect(referer != null && referer.Host == Request.Url.Host ? referer.PathAndQuery : fallback);
        }

        public ActionResult Unsubscribe(string email)
        {
            ViewBag.Title = "Unsubscribe";
            ViewBag.Email = email == null ? "" : email.Trim();
            var sub = string.IsNullOrWhiteSpace(email) ? null : db.NewsletterSubscribers.FirstOrDefault(n => n.Email == email.Trim());
            ViewBag.IsSubscribed = sub != null && sub.IsActive;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Unsubscribe(NewsletterViewModel model)
        {
            var email = model.Email == null ? "" : model.Email.Trim();
            var sub = db.NewsletterSubscribers.FirstOrDefault(n => n.Email == email);
            if (sub != null && sub.IsActive)
            {
                sub.IsActive = false;
                db.SaveChanges();
                TempData["Unsubscribed"] = "You have been unsubscribed from NovaKart emails.";
            }
            else
            {
                TempData["Unsubscribed"] = "This email is not on our newsletter list.";
            }
            ViewBag.Title = "Unsubscribe";
            ViewBag.Email = email;
            ViewBag.IsSubscribed = false;
            return View();
        }

        [ChildActionOnly]
        public ActionResult AnnouncementBar()
        {
            return PartialView("_AnnouncementBar");
        }
    }
}
