using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using LegacyEcommerce.Data;
using LegacyEcommerce.Models;

namespace LegacyEcommerce.Infrastructure
{
    public static class WishlistStore
    {
        public const string Key = "NK.Wishlist";

        public static List<int> Ids(HttpSessionStateBase session)
        {
            var list = session[Key] as List<int>;
            if (list == null)
            {
                list = new List<int>();
                session[Key] = list;
            }
            return list;
        }

        public static void SaveIfAuthenticated(StoreContext db, HttpSessionStateBase session)
        {
            var user = Auth.FromSession(session);
            if (user == null) return;
            Save(db, session, user.Id);
        }

        public static void Save(StoreContext db, HttpSessionStateBase session, int userId)
        {
            var ids = Ids(session);
            var existing = db.WishlistItems.Where(w => w.UserId == userId).ToList();
            foreach (var e in existing)
            {
                db.WishlistItems.Remove(e);
            }

            if (ids.Count > 0)
            {
                var available = db.Products.Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToList();
                foreach (var id in ids)
                {
                    if (!available.Contains(id)) continue;
                    db.WishlistItems.Add(new WishlistItem
                    {
                        UserId = userId,
                        ProductId = id,
                        AddedOn = DateTime.Now
                    });
                }
            }
            db.SaveChanges();
        }

        public static void MergeFromDb(StoreContext db, HttpSessionStateBase session, int userId)
        {
            var ids = Ids(session);
            var dbIds = db.WishlistItems
                .Where(w => w.UserId == userId)
                .Select(w => w.ProductId)
                .ToList();
            foreach (var id in dbIds)
            {
                if (!ids.Contains(id)) ids.Add(id);
            }
            Save(db, session, userId);
        }

        public static void ClearSession(HttpSessionStateBase session)
        {
            session[Key] = new List<int>();
        }
    }
}
