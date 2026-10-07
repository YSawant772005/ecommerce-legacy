using System;
using System.Linq;
using System.Web;
using LegacyEcommerce.Data;
using LegacyEcommerce.Models;

namespace LegacyEcommerce.Infrastructure
{
    public static class CartStore
    {
        public static void SaveIfAuthenticated(StoreContext db, HttpSessionStateBase session)
        {
            var user = Auth.FromSession(session);
            if (user == null) return;
            Save(db, session, user.Id);
        }

        public static void Save(StoreContext db, HttpSessionStateBase session, int userId)
        {
            var lines = SessionCart.Lines(session);
            var existing = db.CartItems.Where(c => c.UserId == userId).ToList();
            foreach (var e in existing)
            {
                db.CartItems.Remove(e);
            }

            if (lines.Count > 0)
            {
                var wanted = lines.Select(l => l.ProductId).Distinct().ToList();
                var available = db.Products.Where(p => wanted.Contains(p.Id)).Select(p => p.Id).ToList();
                foreach (var l in lines)
                {
                    if (!available.Contains(l.ProductId)) continue;
                    db.CartItems.Add(new CartItem
                    {
                        UserId = userId,
                        ProductId = l.ProductId,
                        Qty = l.Qty,
                        UpdatedOn = DateTime.Now
                    });
                }
            }

            var header = db.UserCarts.FirstOrDefault(c => c.UserId == userId);
            if (header == null)
            {
                header = new UserCart { UserId = userId };
                db.UserCarts.Add(header);
            }
            header.CouponCode = SessionCart.GetCoupon(session);
            header.UpdatedOn = DateTime.Now;
            db.SaveChanges();
        }

        public static void MergeFromDb(StoreContext db, HttpSessionStateBase session, int userId)
        {
            var lines = SessionCart.Lines(session);
            var dbLines = db.CartItems
                .Where(c => c.UserId == userId)
                .Select(c => new { c.ProductId, c.Qty })
                .ToList();

            foreach (var dl in dbLines)
            {
                var line = lines.FirstOrDefault(l => l.ProductId == dl.ProductId);
                if (line == null)
                {
                    lines.Add(new CartLine { ProductId = dl.ProductId, Qty = Math.Min(10, dl.Qty) });
                }
                else
                {
                    line.Qty = Math.Min(10, Math.Max(line.Qty, dl.Qty));
                }
            }

            var header = db.UserCarts.FirstOrDefault(c => c.UserId == userId);
            if (header != null && !string.IsNullOrEmpty(header.CouponCode) && string.IsNullOrEmpty(SessionCart.GetCoupon(session)))
            {
                SessionCart.SetCoupon(session, header.CouponCode);
            }

            Save(db, session, userId);
        }

        public static void Clear(StoreContext db, int userId)
        {
            var existing = db.CartItems.Where(c => c.UserId == userId).ToList();
            foreach (var e in existing)
            {
                db.CartItems.Remove(e);
            }
            var header = db.UserCarts.FirstOrDefault(c => c.UserId == userId);
            if (header != null)
            {
                header.CouponCode = null;
                header.UpdatedOn = DateTime.Now;
            }
            db.SaveChanges();
        }
    }
}
