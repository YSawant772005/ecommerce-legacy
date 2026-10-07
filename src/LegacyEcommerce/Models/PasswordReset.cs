using System;
using System.ComponentModel.DataAnnotations;

namespace LegacyEcommerce.Models
{
    public class PasswordReset
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        [Required]
        [StringLength(64)]
        public string Token { get; set; }

        public DateTime CreatedOn { get; set; }

        public DateTime ExpiresOn { get; set; }

        public DateTime? UsedOn { get; set; }

        public virtual User User { get; set; }
    }
}
