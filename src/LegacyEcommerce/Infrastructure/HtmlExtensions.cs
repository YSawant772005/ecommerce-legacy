using System;
using System.IO;
using System.Web.Mvc;

namespace LegacyEcommerce.Infrastructure
{
    public static class PartialViewRenderer
    {
        public static string Render(ControllerContext context, string viewName, object model)
        {
            var viewResult = ViewEngines.Engines.FindPartialView(context, viewName);
            if (viewResult.View == null)
            {
                throw new InvalidOperationException("Partial view '" + viewName + "' not found.");
            }

            using (var writer = new StringWriter())
            {
                var viewContext = new ViewContext(context, viewResult.View, new ViewDataDictionary(model), new TempDataDictionary(), writer);
                viewResult.View.Render(viewContext, writer);
                return writer.ToString();
            }
        }
    }

    public static class HtmlExtensions
    {
        public static MvcHtmlString Price(this HtmlHelper html, decimal value, bool showStrike = false)
        {
            var formatted = value.ToString("N0");
            if (showStrike)
            {
                return MvcHtmlString.Create("<s class=\"price-old\">\u20B9" + formatted + "</s>");
            }
            return MvcHtmlString.Create("<span class=\"price-now\">\u20B9" + formatted + "</span>");
        }

        public static MvcHtmlString PriceFrom(this HtmlHelper html, decimal value)
        {
            return MvcHtmlString.Create("<span class=\"price-now\">\u20B9" + value.ToString("N0") + "</span>");
        }

        public static MvcHtmlString Stars(this HtmlHelper html, double rating, int count = -1)
        {
            var pct = Math.Max(0, Math.Min(100, Math.Round(rating / 5.0 * 100, 1)));
            var htmlStr = "<span class=\"stars\" title=\"" + rating.ToString("0.0") + " out of 5\">"
                + "<span class=\"stars-track\">\u2605\u2605\u2605\u2605\u2605</span>"
                + "<span class=\"stars-fill\" style=\"width:" + pct.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%\">\u2605\u2605\u2605\u2605\u2605</span>"
                + "</span>";
            if (count >= 0)
            {
                htmlStr += "<span class=\"stars-count\">(" + count.ToString("N0") + ")</span>";
            }
            return MvcHtmlString.Create(htmlStr);
        }

        public static MvcHtmlString Number(this HtmlHelper html, decimal value)
        {
            return MvcHtmlString.Create(value.ToString("N0"));
        }
    }
}
