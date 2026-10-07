using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LegacyEcommerce.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string FullName { get; set; }

        [Required, StringLength(200), EmailAddress]
        public string Email { get; set; }

        [Required, StringLength(200)]
        public string PasswordHash { get; set; }

        [Required, StringLength(100)]
        public string PasswordSalt { get; set; }

        [StringLength(30)]
        public string Role { get; set; }

        [StringLength(30)]
        public string Phone { get; set; }

        public DateTime CreatedOn { get; set; }

        public virtual ICollection<Order> Orders { get; set; }

        public virtual ICollection<CustomerAddress> CustomerAddresses { get; set; }
    }

    public enum OrderStatus
    {
        Placed = 0,
        Packed = 1,
        Shipped = 2,
        Delivered = 3,
        Cancelled = 4
    }

    public class Order
    {
        public int Id { get; set; }

        [Required, StringLength(40)]
        public string OrderNumber { get; set; }

        public int? UserId { get; set; }

        [Required, StringLength(200)]
        public string Email { get; set; }

        [Required, StringLength(150)]
        public string FullName { get; set; }

        [StringLength(30)]
        public string Phone { get; set; }

        [Required, StringLength(400)]
        public string AddressLine { get; set; }

        [Required, StringLength(120)]
        public string City { get; set; }

        [Required, StringLength(120)]
        public string State { get; set; }

        [Required, StringLength(20)]
        public string PostalCode { get; set; }

        public decimal Subtotal { get; set; }

        public decimal Discount { get; set; }

        public decimal Shipping { get; set; }

        public decimal Tax { get; set; }

        public decimal Total { get; set; }

        public OrderStatus Status { get; set; }

        [StringLength(30)]
        public string PaymentMethod { get; set; }

        [StringLength(60)]
        public string PaymentRef { get; set; }

        [StringLength(30)]
        public string CouponCode { get; set; }

        public DateTime CreatedOn { get; set; }

        public virtual User User { get; set; }

        public virtual ICollection<OrderItem> Items { get; set; }
    }

    public class OrderItem
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        public int? ProductId { get; set; }

        [Required, StringLength(200)]
        public string ProductName { get; set; }

        [StringLength(300)]
        public string ImageUrl { get; set; }

        [StringLength(220)]
        public string Slug { get; set; }

        public decimal UnitPrice { get; set; }

        public int Quantity { get; set; }

        public decimal LineTotal
        {
            get { return UnitPrice * Quantity; }
        }

        public virtual Order Order { get; set; }

        public virtual Product Product { get; set; }
    }
}
