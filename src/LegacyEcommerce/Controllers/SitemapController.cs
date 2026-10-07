using System;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using LegacyEcommerce.Infrastructure;
using LegacyEcommerce.Models.ViewModels;

namespace LegacyEcommerce.Controllers
{
    public class SitemapController : Controller
    {
        private string BaseUrl
        {
            get { return Request.Url.Scheme + "://" + Request.Url.Authority; }
        }

        public ActionResult Robots()
        {
            var sb = new StringBuilder();
            sb.AppendLine("User-agent: *");
            sb.AppendLine("Allow: /");
            sb.AppendLine("Disallow: /admin");
            sb.AppendLine("Disallow: /account");
            sb.AppendLine("Disallow: /cart");
            sb.AppendLine("Disallow: /checkout");
            sb.AppendLine("Disallow: /wishlist");
            sb.AppendLine();
            sb.AppendLine("Sitemap: " + BaseUrl + "/sitemap.xml");
            return Content(sb.ToString(), "text/plain", Encoding.UTF8);
        }

        [OutputCache(Duration = 3600, VaryByParam = "none", Location = System.Web.UI.OutputCacheLocation.Server)]
        public ActionResult Index()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
            sb.AppendLine("  <url><loc>" + BaseUrl + "/</loc><changefreq>daily</changefreq><priority>1.0</priority></url>");
            sb.AppendLine("  <url><loc>" + BaseUrl + "/products</loc><changefreq>daily</changefreq><priority>0.9</priority></url>");
            sb.AppendLine("  <url><loc>" + BaseUrl + "/home/about</loc><changefreq>monthly</changefreq><priority>0.4</priority></url>");
            sb.AppendLine("  <url><loc>" + BaseUrl + "/home/contact</loc><changefreq>monthly</changefreq><priority>0.4</priority></url>");

            var categories = CatalogCache.Categories;
            foreach (var c in categories)
            {
                sb.AppendLine("  <url><loc>" + BaseUrl + "/c/" + c.Slug + "</loc><changefreq>daily</changefreq><priority>0.8</priority></url>");
            }

            var products = CatalogCache.Products
                .Where(p => string.IsNullOrEmpty(Request.Params["full"]) ? p.SoldCount > 0 : p.Id > 0)
                .OrderByDescending(p => p.SoldCount)
                .Take(250);
            foreach (var p in products)
            {
                sb.AppendLine("  <url><loc>" + BaseUrl + "/product/" + p.Slug + "</loc><changefreq>weekly</changefreq><priority>0.7</priority></url>");
            }

            sb.AppendLine("</urlset>");
            return Content(sb.ToString(), "text/xml", Encoding.UTF8);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}