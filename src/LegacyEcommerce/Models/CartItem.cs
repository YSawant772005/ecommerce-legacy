using System;
using System.ComponentModel.DataAnnotations;

namespace LegacyEcommerce.Models
{
    public class CartItem
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int ProductId { get; set; }

        public int Qty { get; set; }

        [StringLength(120)]
        public string Variant { get; set; }

        public DateTime UpdatedOn { get; set; }

        public virtual User User { get; set; }

        public virtual Product Product { get; set; }
    }

    public class UserCart
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        [StringLength(30)]
        public string CouponCode { get; set; }

        public DateTime UpdatedOn { get; set; }

        public virtual User User { get; set; }
    }

    public class WishlistItem
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int ProductId { get; set; }

        public DateTime AddedOn { get; set; }

        public virtual User User { get; set; }

        public virtual Product Product { get; set; }
    }
}
