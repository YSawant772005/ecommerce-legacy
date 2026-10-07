using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using LegacyEcommerce.Data;
using LegacyEcommerce.Models.ViewModels;

namespace LegacyEcommerce
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            Database.SetInitializer(new EcommerceInitializer());

            using (var db = new StoreContext())
            {
                db.Database.Initialize(false);
                var warm = db.Products.Count();
                EcommerceInitializer.EnsureDefaultCoupons(db);
            }

            var cats = CatalogCache.Categories;
            var brands = CatalogCache.Brands;
            var prods = CatalogCache.Products;
            var pf = CatalogCache.PriceFloor;
            var pc = CatalogCache.PriceCeil;
        }

        protected void Application_BeginRequest()
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.CurrencySymbol = "\u20B9";
            culture.NumberFormat.CurrencyPositivePattern = 0;
            culture.NumberFormat.NumberGroupSizes = new[] { 3, 2, 3 };
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }
    }
}
