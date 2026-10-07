using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LegacyEcommerce.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; }

        [Required, StringLength(140)]
        public string Slug { get; set; }

        [StringLength(240)]
        public string Tagline { get; set; }

        [StringLength(1000)]
        public string Description { get; set; }

        [StringLength(300)]
        public string ImageUrl { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; }

        public virtual ICollection<Product> Products { get; set; }
    }

    public class Brand
    {
        public int Id { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; }

        [Required, StringLength(140)]
        public string Slug { get; set; }

        [StringLength(6)]
        public string Code { get; set; }

        public virtual ICollection<Product> Products { get; set; }
    }
}
