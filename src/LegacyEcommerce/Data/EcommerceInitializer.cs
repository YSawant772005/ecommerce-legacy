using System;
using System.Data.Entity;
using System.Linq;

namespace LegacyEcommerce.Data
{
    public class EcommerceInitializer : IDatabaseInitializer<StoreContext>
    {
        public void InitializeDatabase(StoreContext db)
        {
            if (!db.Database.Exists())
            {
                db.Database.Create();
                EcommerceSeeder.Seed(db);
                CreateIndexes(db);
            }
            else if (!db.Products.Any())
            {
                EcommerceSeeder.Seed(db);
            }

            EnsureCoreTables(db);
        }

        public static void EnsureCoreTables(StoreContext db)
        {
            var statements = new[]
            {
                "IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CustomerAddresses') " +
                "CREATE TABLE dbo.CustomerAddresses(" +
                "Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY, " +
                "UserId INT NOT NULL, " +
                "FullName NVARCHAR(150) NOT NULL, " +
                "Phone NVARCHAR(30) NOT NULL, " +
                "AddressLine NVARCHAR(400) NOT NULL, " +
                "City NVARCHAR(120) NOT NULL, " +
                "State NVARCHAR(120) NOT NULL, " +
                "PostalCode NVARCHAR(20) NOT NULL, " +
                "Country NVARCHAR(80) NOT NULL, " +
                "IsDefault BIT NOT NULL, " +
                "CreatedOn DATETIME NOT NULL, " +
                "CONSTRAINT FK_CustomerAddresses_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE)",
                "IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Coupons') " +
                "CREATE TABLE dbo.Coupons(" +
                "Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY, " +
                "Code NVARCHAR(30) NOT NULL, " +
                "Description NVARCHAR(140) NOT NULL, " +
                "MinOrderValue DECIMAL(18,2) NOT NULL, " +
                "FixedAmount DECIMAL(18,2) NULL, " +
                "PercentDiscount DECIMAL(18,2) NULL, " +
                "MaxDiscountAmount DECIMAL(18,2) NOT NULL, " +
                "FreeShipping BIT NOT NULL, " +
                "IsActive BIT NOT NULL, " +
                "TotalUses INT NOT NULL, " +
                "StartsOn DATETIME NOT NULL, " +
                "EndsOn DATETIME NULL, " +
                "CreatedOn DATETIME NOT NULL)",
                "IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ContactMessages') " +
                "CREATE TABLE dbo.ContactMessages(" +
                "Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY, " +
                "Name NVARCHAR(150) NOT NULL, " +
                "Email NVARCHAR(200) NOT NULL, " +
                "Phone NVARCHAR(30) NULL, " +
                "Topic NVARCHAR(60) NULL, " +
                "Message NVARCHAR(2000) NOT NULL, " +
                "IsRead BIT NOT NULL, " +
                "CreatedOn DATETIME NOT NULL)",
                "IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'NewsletterSubscribers') " +
                "CREATE TABLE dbo.NewsletterSubscribers(" +
                "Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY, " +
                "Email NVARCHAR(200) NOT NULL, " +
                "IsActive BIT NOT NULL, " +
                "CreatedOn DATETIME NOT NULL)",
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Newsletter_Email' AND object_id = OBJECT_ID('dbo.NewsletterSubscribers')) " +
                "CREATE UNIQUE INDEX UX_Newsletter_Email ON dbo.NewsletterSubscribers(Email)",
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Coupon_Code' AND object_id = OBJECT_ID('dbo.Coupons')) " +
                "CREATE UNIQUE INDEX UX_Coupon_Code ON dbo.Coupons(Code)",
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CustomerAddress_UserId' AND object_id = OBJECT_ID('dbo.CustomerAddresses')) " +
                "CREATE NONCLUSTERED INDEX IX_CustomerAddress_UserId ON dbo.CustomerAddresses(UserId)"
            };

            foreach (var sql in statements)
            {
                try
                {
                    db.Database.ExecuteSqlCommand(sql);
                }
                catch
                {
                    // table already exists or unsupported - safe to ignore
                }
            }
        }

        public static void EnsureDefaultCoupons(StoreContext db)
        {
            if (db.Coupons.Any()) return;
            var now = DateTime.Now;
            db.Coupons.Add(new Models.Coupon
            {
                Code = "SAVE10",
                Description = "10% off up to \u20B9500 on orders above \u20B9999",
                MinOrderValue = 999,
                PercentDiscount = 10m,
                MaxDiscountAmount = 500m,
                FreeShipping = false,
                IsActive = true,
                StartsOn = now,
                CreatedOn = now
            });
            db.Coupons.Add(new Models.Coupon
            {
                Code = "WELCOME50",
                Description = "\u20B950 off on orders above \u20B9499",
                MinOrderValue = 499,
                FixedAmount = 50m,
                MaxDiscountAmount = 50m,
                FreeShipping = false,
                IsActive = true,
                StartsOn = now,
                CreatedOn = now
            });
            db.Coupons.Add(new Models.Coupon
            {
                Code = "FREESHIP",
                Description = "Free standard delivery on any order",
                MinOrderValue = 0,
                MaxDiscountAmount = 0,
                FreeShipping = true,
                IsActive = true,
                StartsOn = now,
                CreatedOn = now
            });
            db.SaveChanges();
        }

        private static void CreateIndexes(StoreContext db)
        {
            var statements = new[]
            {
                "CREATE UNIQUE INDEX UX_Product_Slug ON dbo.Products(Slug)",
                "CREATE UNIQUE INDEX UX_Product_Sku ON dbo.Products(Sku)",
                "CREATE UNIQUE INDEX UX_Category_Slug ON dbo.Categories(Slug)",
                "CREATE UNIQUE INDEX UX_Brand_Slug ON dbo.Brands(Slug)",
                "CREATE UNIQUE INDEX UX_User_Email ON dbo.Users(Email)",
                "CREATE UNIQUE INDEX UX_Order_OrderNumber ON dbo.Orders(OrderNumber)",
                "CREATE NONCLUSTERED INDEX IX_Product_CategoryId ON dbo.Products(CategoryId)",
                "CREATE NONCLUSTERED INDEX IX_Product_BrandId ON dbo.Products(BrandId)",
                "CREATE NONCLUSTERED INDEX IX_Product_IsFeatured ON dbo.Products(IsFeatured)",
                "CREATE NONCLUSTERED INDEX IX_Product_IsDeal ON dbo.Products(IsDeal)",
                "CREATE NONCLUSTERED INDEX IX_Product_CreatedOn ON dbo.Products(CreatedOn)",
                "CREATE NONCLUSTERED INDEX IX_Product_Price ON dbo.Products(Price)",
                "CREATE NONCLUSTERED INDEX IX_Product_Rating ON dbo.Products(Rating)",
                "CREATE NONCLUSTERED INDEX IX_Review_ProductId ON dbo.Reviews(ProductId)",
                "CREATE NONCLUSTERED INDEX IX_Order_UserId ON dbo.Orders(UserId)",
                "CREATE NONCLUSTERED INDEX IX_Order_CreatedOn ON dbo.Orders(CreatedOn)",
                "CREATE NONCLUSTERED INDEX IX_OrderItem_OrderId ON dbo.OrderItems(OrderId)"
            };

            foreach (var sql in statements)
            {
                try
                {
                    db.Database.ExecuteSqlCommand(sql);
                }
                catch
                {
                    // index already exists or unsupported - safe to ignore
                }
            }
        }
    }
}
