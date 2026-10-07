using System.Web.Mvc;
using System.Web.Routing;

namespace LegacyEcommerce
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "Robots",
                url: "robots.txt",
                defaults: new { controller = "Sitemap", action = "Robots" }
            );

            routes.MapRoute(
                name: "Sitemap",
                url: "sitemap.xml",
                defaults: new { controller = "Sitemap", action = "Index" }
            );

            routes.MapRoute(
                name: "ProductDetails",
                url: "product/{slug}",
                defaults: new { controller = "Products", action = "Details" }
            );

            routes.MapRoute(
                name: "CategoryLanding",
                url: "c/{slug}",
                defaults: new { controller = "Products", action = "Index" }
            );

            routes.MapRoute(
                name: "OrderSuccess",
                url: "checkout/success/{id}",
                defaults: new { controller = "Checkout", action = "Success" }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
