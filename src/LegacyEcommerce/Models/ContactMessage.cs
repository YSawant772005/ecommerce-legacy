using System;
using System.ComponentModel.DataAnnotations;

namespace LegacyEcommerce.Models
{
    public class ContactMessage
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; }

        [Required, StringLength(200)]
        public string Email { get; set; }

        [StringLength(30)]
        public string Phone { get; set; }

        [StringLength(60)]
        public string Topic { get; set; }

        [Required, StringLength(2000)]
        public string Message { get; set; }

        public bool IsRead { get; set; }

        public DateTime CreatedOn { get; set; }
    }

    public class NewsletterSubscriber
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Email { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedOn { get; set; }
    }
}