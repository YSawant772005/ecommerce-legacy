using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LegacyEcommerce.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; }

        [StringLength(220)]
        public string Slug { get; set; }

        [StringLength(50)]
        public string Sku { get; set; }

        [StringLength(500)]
        public string ShortDescription { get; set; }

        [StringLength(4000)]
        public string Description { get; set; }

        public decimal Price { get; set; }

        public decimal? ComparePrice { get; set; }

        public int Stock { get; set; }

        public double Rating { get; set; }

        public int RatingCount { get; set; }

        public int ReviewCount { get; set; }

        public int SoldCount { get; set; }

        public int CategoryId { get; set; }

        public int BrandId { get; set; }

        [StringLength(300)]
        public string ImageUrl { get; set; }

        [StringLength(300)]
        public string ImageUrl2 { get; set; }

        [StringLength(300)]
        public string ImageUrl3 { get; set; }

        [StringLength(240)]
        public string Colors { get; set; }

        [StringLength(160)]
        public string Sizes { get; set; }

        [StringLength(200)]
        public string Highlights { get; set; }

        public bool IsFeatured { get; set; }

        public bool IsDeal { get; set; }

        public bool IsNew { get; set; }

        public DateTime CreatedOn { get; set; }

        public virtual Category Category { get; set; }

        public virtual Brand Brand { get; set; }

        public virtual ICollection<Review> Reviews { get; set; }

        [NotMapped]
        public int DiscountPercent
        {
            get
            {
                if (!ComparePrice.HasValue || ComparePrice.Value <= Price || ComparePrice.Value <= 0) return 0;
                return (int)Math.Round((ComparePrice.Value - Price) / ComparePrice.Value * 100);
            }
        }

        [NotMapped]
        public bool InStock
        {
            get { return Stock > 0; }
        }

        [NotMapped]
        public string[] ColorList
        {
            get { return string.IsNullOrWhiteSpace(Colors) ? new string[0] : Colors.Split(','); }
        }

        [NotMapped]
        public string[] SizeList
        {
            get { return string.IsNullOrWhiteSpace(Sizes) ? new string[0] : Sizes.Split(','); }
        }
    }

    public class Review
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        [Required, StringLength(120)]
        public string AuthorName { get; set; }

        public int Rating { get; set; }

        [StringLength(200)]
        public string Title { get; set; }

        [StringLength(2000)]
        public string Body { get; set; }

        public bool VerifiedPurchase { get; set; }

        public DateTime CreatedOn { get; set; }

        public virtual Product Product { get; set; }
    }
}
