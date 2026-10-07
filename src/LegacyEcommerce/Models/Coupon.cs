using System;
using System.ComponentModel.DataAnnotations;

namespace LegacyEcommerce.Models
{
    public class Coupon
    {
        public int Id { get; set; }

        [Required, StringLength(30)]
        public string Code { get; set; }

        [Required, StringLength(140)]
        public string Description { get; set; }

        public decimal MinOrderValue { get; set; }

        public decimal? FixedAmount { get; set; }

        public decimal? PercentDiscount { get; set; }

        public decimal MaxDiscountAmount { get; set; }

        public bool FreeShipping { get; set; }

        public bool IsActive { get; set; }

        public int TotalUses { get; set; }

        public DateTime StartsOn { get; set; }

        public DateTime? EndsOn { get; set; }

        public DateTime CreatedOn { get; set; }
    }
}