using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LegacyEcommerce.Infrastructure
{
    public class CartLine
    {
        public int ProductId { get; set; }
        public int Qty { get; set; }
    }

    public class CouponResult
    {
        public bool Valid { get; set; }
        public bool FreeShipping { get; set; }
        public string Code { get; set; }
        public decimal Amount { get; set; }
        public string Message { get; set; }
    }

    public static class SessionCart
    {
        private const string CartKey = "NK.Cart";
        private const string CouponKey = "NK.Coupon";

        public static List<CartLine> Lines(HttpSessionStateBase session)
        {
            var lines = session[CartKey] as List<CartLine>;
            if (lines == null)
            {
                lines = new List<CartLine>();
                session[CartKey] = lines;
            }
            return lines;
        }

        public static int Count(HttpSessionStateBase session)
        {
            return Lines(session).Sum(l => l.Qty);
        }

        public static void Add(HttpSessionStateBase session, int productId, int qty = 1)
        {
            if (qty < 1) qty = 1;
            var lines = Lines(session);
            var line = lines.FirstOrDefault(l => l.ProductId == productId);
            if (line == null)
            {
                lines.Add(new CartLine { ProductId = productId, Qty = qty });
            }
            else
            {
                line.Qty = Math.Min(10, line.Qty + qty);
            }
        }

        public static void Update(HttpSessionStateBase session, int productId, int qty)
        {
            var lines = Lines(session);
            var line = lines.FirstOrDefault(l => l.ProductId == productId);
            if (line == null) return;
            if (qty <= 0) lines.Remove(line);
            else line.Qty = Math.Min(10, qty);
        }

        public static void Remove(HttpSessionStateBase session, int productId)
        {
            Lines(session).RemoveAll(l => l.ProductId == productId);
        }

        public static void Clear(HttpSessionStateBase session)
        {
            session[CartKey] = new List<CartLine>();
            session[CouponKey] = null;
        }

        public static string CouponCode
        {
            get { return HttpContext.Current.Session[CouponKey] as string; }
            set { HttpContext.Current.Session[CouponKey] = value; }
        }

        public static string GetCoupon(HttpSessionStateBase session)
        {
            return session[CouponKey] as string;
        }

        public static void SetCoupon(HttpSessionStateBase session, string code)
        {
            session[CouponKey] = string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
        }
    }

    public static class Coupons
    {
        public static CouponResult Validate(string code, decimal subtotal)
        {
            var c = (code ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(c))
            {
                return new CouponResult { Valid = false, Message = "Enter a coupon code." };
            }

            using (var db = new Data.StoreContext())
            {
                var coupon = db.Coupons.FirstOrDefault(x => x.Code == c && x.IsActive);
                if (coupon == null)
                {
                    return new CouponResult { Valid = false, Code = c, Message = "That coupon code is not valid." };
                }

                var now = DateTime.Now;
                if (now < coupon.StartsOn)
                {
                    return new CouponResult { Valid = false, Code = c, Message = coupon.Code + " is not active yet. Check the validity window." };
                }
                if (coupon.EndsOn.HasValue && now > coupon.EndsOn.Value)
                {
                    return new CouponResult { Valid = false, Code = c, Message = coupon.Code + " has expired." };
                }
                if (subtotal < coupon.MinOrderValue)
                {
                    return new CouponResult { Valid = false, Code = c, Message = coupon.Code + " needs a cart value of \u20B9" + coupon.MinOrderValue.ToString("N0") + " or more." };
                }

                decimal amount = 0;
                if (coupon.FixedAmount.HasValue)
                {
                    amount = coupon.FixedAmount.Value;
                }
                else if (coupon.PercentDiscount.HasValue)
                {
                    amount = Math.Round(subtotal * coupon.PercentDiscount.Value / 100m, 2);
                    if (coupon.MaxDiscountAmount > 0 && amount > coupon.MaxDiscountAmount)
                    {
                        amount = coupon.MaxDiscountAmount;
                    }
                }
                if (amount > subtotal) amount = subtotal;

                return new CouponResult
                {
                    Valid = true,
                    Code = coupon.Code,
                    FreeShipping = coupon.FreeShipping,
                    Amount = amount,
                    Message = coupon.Code + " applied." + (coupon.FreeShipping ? " Free delivery included." : "")
                };
            }
        }
    }
}
