using System.Data.Entity;
using LegacyEcommerce.Models;

namespace LegacyEcommerce.Data
{
    public class StoreContext : DbContext
    {
        public StoreContext() : base("name=StoreContext")
        {
            Database.CommandTimeout = 180;
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<CustomerAddress> CustomerAddresses { get; set; }
        public DbSet<Coupon> Coupons { get; set; }
        public DbSet<ContactMessage> ContactMessages { get; set; }
        public DbSet<NewsletterSubscriber> NewsletterSubscribers { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<UserCart> UserCarts { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }

        public DbSet<PasswordReset> PasswordResets { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);
            modelBuilder.Entity<Product>().Property(p => p.ComparePrice).HasPrecision(18, 2);
            modelBuilder.Entity<Order>().Property(o => o.Subtotal).HasPrecision(18, 2);
            modelBuilder.Entity<Order>().Property(o => o.Discount).HasPrecision(18, 2);
            modelBuilder.Entity<Order>().Property(o => o.Shipping).HasPrecision(18, 2);
            modelBuilder.Entity<Order>().Property(o => o.Tax).HasPrecision(18, 2);
            modelBuilder.Entity<Order>().Property(o => o.Total).HasPrecision(18, 2);
            modelBuilder.Entity<OrderItem>().Property(i => i.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<Coupon>().Property(c => c.MinOrderValue).HasPrecision(18, 2);
            modelBuilder.Entity<Coupon>().Property(c => c.FixedAmount).HasPrecision(18, 2);
            modelBuilder.Entity<Coupon>().Property(c => c.PercentDiscount).HasPrecision(18, 2);
            modelBuilder.Entity<Coupon>().Property(c => c.MaxDiscountAmount).HasPrecision(18, 2);

            modelBuilder.Entity<Product>()
                .HasRequired(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<Product>()
                .HasRequired(p => p.Brand)
                .WithMany(b => b.Products)
                .HasForeignKey(p => p.BrandId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Review>()
                .HasRequired(r => r.Product)
                .WithMany(p => p.Reviews)
                .HasForeignKey(r => r.ProductId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<OrderItem>()
                .HasRequired(i => i.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(i => i.OrderId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<OrderItem>()
                .HasOptional(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Order>()
                .HasOptional(o => o.User)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.UserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<CustomerAddress>()
                .HasRequired(a => a.User)
                .WithMany(u => u.CustomerAddresses)
                .HasForeignKey(a => a.UserId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<CartItem>()
                .HasRequired(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<CartItem>()
                .HasRequired(c => c.Product)
                .WithMany()
                .HasForeignKey(c => c.ProductId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<UserCart>()
                .HasRequired(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<WishlistItem>()
                .HasRequired(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<WishlistItem>()
                .HasRequired(c => c.Product)
                .WithMany()
                .HasForeignKey(c => c.ProductId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<PasswordReset>()
                .HasRequired(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .WillCascadeOnDelete(true);
        }
    }
}
