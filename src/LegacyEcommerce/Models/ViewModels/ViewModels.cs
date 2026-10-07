using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using LegacyEcommerce.Infrastructure;
using LegacyEcommerce.Models;

namespace LegacyEcommerce.Models.ViewModels
{
    public class CatalogViewModel
    {
        public List<Product> Products { get; set; }
        public List<Category> Categories { get; set; }
        public List<Brand> Brands { get; set; }
        public Dictionary<string, int> CategoryCounts { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get { return PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize); } }
        public int From { get { return TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1; } }
        public int To { get { return Math.Min(Page * PageSize, TotalCount); } }
        public string Sort { get; set; }
        public string Q { get; set; }
        public string CategorySlug { get; set; }
        public string CategoryName { get; set; }
        public List<string> BrandSlugs { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public double? MinRating { get; set; }
        public string Mode { get; set; }
        public string Title { get; set; }
        public decimal PriceFloor { get; set; }
        public decimal PriceCeil { get; set; }

        public List<int> PageNumbers
        {
            get
            {
                var list = new List<int>();
                int totalPages = TotalPages;
                if (totalPages <= 9)
                {
                    for (int i = 1; i <= totalPages; i++)
                    {
                        list.Add(i);
                    }
                    return list;
                }

                list.Add(1);
                if (Page > 4)
                {
                    list.Add(0);
                }
                int from = Math.Max(2, Page - 2);
                int to = Math.Min(totalPages - 1, Page + 2);
                for (int i = from; i <= to; i++)
                {
                    list.Add(i);
                }
                if (Page < totalPages - 3)
                {
                    list.Add(0);
                }
                list.Add(totalPages);
                return list;
            }
        }

        public CatalogViewModel()
        {
            Products = new List<Product>();
            Categories = new List<Category>();
            Brands = new List<Brand>();
            CategoryCounts = new Dictionary<string, int>();
            BrandSlugs = new List<string>();
            Sort = "popular";
            PageSize = 24;
            Mode = "scroll";
        }
    }

    public class ProductDetailViewModel
    {
        public Product Product { get; set; }
        public List<Product> Related { get; set; }
        public List<Review> Reviews { get; set; }
        public int ReviewCount { get; set; }

        public ProductDetailViewModel()
        {
            Related = new List<Product>();
            Reviews = new List<Review>();
        }
    }

    public class CartLineView
    {
        public Product Product { get; set; }
        public int Qty { get; set; }
        public decimal LineTotal { get { return Product.Price * Qty; } }
    }

    public class CartTotals
    {
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal Shipping { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public int Count { get; set; }
        public string CouponCode { get; set; }
        public string CouponMessage { get; set; }
        public bool CouponValid { get; set; }
        public bool FreeShipping { get; set; }
    }

    public class CartViewModel
    {
        public List<CartLineView> Lines { get; set; }
        public CartTotals Totals { get; set; }
        public bool IsEmpty { get { return Lines == null || Lines.Count == 0; } }

        public CartViewModel()
        {
            Lines = new List<CartLineView>();
            Totals = new CartTotals();
        }
    }

    public class CheckoutViewModel
    {
        public CartViewModel Cart { get; set; }
        public List<CustomerAddress> Addresses { get; set; }
        public int? SelectedAddressId { get; set; }
        public bool SaveAddress { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Full name is required")]
        [StringLength(150)]
        public string FullName { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Enter a valid email")]
        [StringLength(200)]
        public string Email { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Phone is required")]
        [RegularExpression(@"[0-9+\-\s]{8,15}", ErrorMessage = "Enter a valid phone number")]
        public string Phone { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Address is required")]
        [StringLength(400, MinimumLength = 6, ErrorMessage = "Enter a complete address")]
        public string AddressLine { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "City is required")]
        [StringLength(120)]
        public string City { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "State is required")]
        [StringLength(120)]
        public string State { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "PIN code is required")]
        [RegularExpression(@"[0-9]{6}", ErrorMessage = "PIN code must be 6 digits")]
        public string PostalCode { get; set; }

        public string PaymentMethod { get; set; }

        public string Notes { get; set; }

        public CheckoutViewModel()
        {
            Cart = new CartViewModel();
            Addresses = new List<CustomerAddress>();
        }

        public string Country
        {
            get { return "India"; }
        }
    }

    public class CheckoutState
    {
        public bool AddressDone { get; set; }
        public bool PaymentDone { get; set; }

        public int? SelectedAddressId { get; set; }
        public bool SaveAddress { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string AddressLine { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }

        public string PaymentMethod { get; set; }
        public string PaymentDetail { get; set; }

        public string Notes { get; set; }
    }

    public class PaymentStepViewModel
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Choose a payment method")]
        public string PaymentMethod { get; set; }

        [StringLength(25)]
        public string CardNumber { get; set; }

        [StringLength(60)]
        public string CardName { get; set; }

        [StringLength(5)]
        public string Expiry { get; set; }

        [StringLength(4)]
        public string Cvv { get; set; }

        [StringLength(80)]
        public string UpiId { get; set; }
    }

    public class ReviewOrderViewModel
    {
        public CartViewModel Cart { get; set; }
        public CheckoutState State { get; set; }

        public ReviewOrderViewModel()
        {
            Cart = new CartViewModel();
            State = new CheckoutState();
        }
    }

    public class ReviewViewModel
    {
        [System.ComponentModel.DataAnnotations.Required]
        public int ProductId { get; set; }

        [System.ComponentModel.DataAnnotations.Range(1, 5, ErrorMessage = "Choose a star rating")]
        public int Rating { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Give your review a title")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Title should be at least 3 characters")]
        public string Title { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Write a few words about the product")]
        [StringLength(2000, MinimumLength = 10, ErrorMessage = "Review should be at least 10 characters")]
        public string Body { get; set; }
    }

    public class AddressViewModel
    {
        public int Id { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Receiver name is required")]
        [StringLength(150)]
        public string FullName { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Phone is required")]
        [RegularExpression(@"[0-9+\-\s]{8,15}", ErrorMessage = "Enter a valid phone number")]
        public string Phone { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Address is required")]
        [StringLength(400, MinimumLength = 6, ErrorMessage = "Enter a complete address")]
        public string AddressLine { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "City is required")]
        [StringLength(120)]
        public string City { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "State is required")]
        [StringLength(120)]
        public string State { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "PIN code is required")]
        [RegularExpression(@"[0-9]{6}", ErrorMessage = "PIN code must be 6 digits")]
        public string PostalCode { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Country is required")]
        [StringLength(80)]
        public string Country { get; set; }

        public bool IsDefault { get; set; }
    }

    public class LoginViewModel
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string Email { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public string ReturnUrl { get; set; }
    }

    public class RegisterViewModel
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Name is required")]
        [StringLength(150, MinimumLength = 3, ErrorMessage = "Enter your full name")]
        public string FullName { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Enter a valid email")]
        [StringLength(200)]
        public string Email { get; set; }

        [RegularExpression(@"[0-9+\-\s]{8,15}", ErrorMessage = "Enter a valid phone number")]
        public string Phone { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [System.ComponentModel.DataAnnotations.Compare("Password", ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; }
    }

    public class EditProfileViewModel
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Name is required")]
        [StringLength(150, MinimumLength = 3, ErrorMessage = "Enter your full name")]
        public string FullName { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Enter a valid email")]
        [StringLength(200)]
        public string Email { get; set; }

        [RegularExpression(@"[0-9+\-\s]{8,15}", ErrorMessage = "Enter a valid phone number")]
        public string Phone { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Enter your current password")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Enter a new password")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }

        [System.ComponentModel.DataAnnotations.Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; }
    }

    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Enter your email address")]
        [EmailAddress(ErrorMessage = "Enter a valid email address")]
        public string Email { get; set; }
    }

    public class ResetPasswordViewModel
    {
        [Required]
        public string Token { get; set; }

        [Required(ErrorMessage = "Enter a new password")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }

        [System.ComponentModel.DataAnnotations.Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; }
    }

    public class HomeViewModel
    {
        public List<Category> Categories { get; set; }
        public List<Product> Featured { get; set; }
        public List<Product> Deals { get; set; }
        public List<Product> NewArrivals { get; set; }
        public int ProductCount { get; set; }
        public int BrandCount { get; set; }
        public int CustomerCount { get; set; }
        public int OrderCount { get; set; }

        public HomeViewModel()
        {
            Categories = new List<Category>();
            Featured = new List<Product>();
            Deals = new List<Product>();
            NewArrivals = new List<Product>();
        }
    }

    public class AdminDashboardViewModel
    {
        public int ProductCount { get; set; }
        public int OrderCount { get; set; }
        public int CustomerCount { get; set; }
        public decimal Revenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public List<Order> RecentOrders { get; set; }
        public List<Product> LowStock { get; set; }
        public Dictionary<string, int> OrdersByStatus { get; set; }

        public AdminDashboardViewModel()
        {
            RecentOrders = new List<Order>();
            LowStock = new List<Product>();
            OrdersByStatus = new Dictionary<string, int>();
        }
    }

    public class AdminCouponsViewModel
    {
        public List<Coupon> Coupons { get; set; }
        public string Q { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get { return PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize); } }

        public AdminCouponsViewModel()
        {
            Coupons = new List<Coupon>();
            Page = 1;
            PageSize = 20;
        }
    }

    public class CouponViewModel
    {
        public int Id { get; set; }
        public bool IsNew { get { return Id == 0; } }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Coupon code is required")]
        [StringLength(30, MinimumLength = 3, ErrorMessage = "Code must be at least 3 characters")]
        public string Code { get; set; }

        public string Description { get; set; }

        public decimal MinOrderValue { get; set; }

        public decimal? FixedAmount { get; set; }

        public decimal? PercentDiscount { get; set; }

        public decimal MaxDiscountAmount { get; set; }

        public bool FreeShipping { get; set; }

        public bool IsActive { get; set; }

        [System.ComponentModel.DataAnnotations.DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? StartsOn { get; set; }

        [System.ComponentModel.DataAnnotations.DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? EndsOn { get; set; }
    }

    public class ContactViewModel
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Your name is required")]
        [StringLength(150)]
        public string Name { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Email is required")]
        [StringLength(200)]
        [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Enter a valid email address")]
        public string Email { get; set; }

        [StringLength(30)]
        public string Phone { get; set; }

        public string Topic { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Message is required")]
        [StringLength(2000, ErrorMessage = "Message must be under 2000 characters")]
        public string Message { get; set; }
    }

    public class NewsletterViewModel
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Enter your email address")]
        [StringLength(200)]
        [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Enter a valid email address")]
        public string Email { get; set; }
    }

    public class AdminCustomersViewModel
    {
        public List<AdminCustomerRow> Customers { get; set; }
        public string Q { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get { return PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize); } }

        public AdminCustomersViewModel()
        {
            Customers = new List<AdminCustomerRow>();
            Page = 1;
            PageSize = 20;
        }
    }

    public class AdminCustomerRow
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public DateTime CreatedOn { get; set; }
        public int OrderCount { get; set; }
        public decimal TotalSpent { get; set; }
    }

    public class AdminMessagesViewModel
    {
        public List<AdminMessageRow> Messages { get; set; }
        public string Q { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public int TotalPages { get { return PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize); } }

        public AdminMessagesViewModel()
        {
            Messages = new List<AdminMessageRow>();
            Page = 1;
            PageSize = 20;
        }
    }

    public class AdminMessageRow
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Topic { get; set; }
        public string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedOn { get; set; }
    }

    public class AdminNewsletterViewModel
    {
        public List<AdminSubscriberRow> Subscribers { get; set; }
        public string Q { get; set; }
        public string Status { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public int TotalPages { get { return PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize); } }

        public AdminNewsletterViewModel()
        {
            Subscribers = new List<AdminSubscriberRow>();
            Page = 1;
            PageSize = 20;
        }
    }

    public class AdminSubscriberRow
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedOn { get; set; }
    }

    public class AdminReviewsViewModel
    {
        public List<AdminReviewRow> Reviews { get; set; }
        public string Q { get; set; }
        public string Status { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int HiddenCount { get; set; }
        public int TotalPages { get { return PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize); } }

        public AdminReviewsViewModel()
        {
            Reviews = new List<AdminReviewRow>();
            Page = 1;
            PageSize = 20;
        }
    }

    public class AdminReviewRow
    {
        public int Id { get; set; }
        public string AuthorName { get; set; }
        public int Rating { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public bool VerifiedPurchase { get; set; }
        public bool IsApproved { get; set; }
        public DateTime CreatedOn { get; set; }
        public string ProductName { get; set; }
        public string ProductSlug { get; set; }
    }

    public class AdminProductsViewModel
    {
        public List<Product> Products { get; set; }
        public string Q { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get { return PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize); } }

        public AdminProductsViewModel()
        {
            Products = new List<Product>();
            Page = 1;
            PageSize = 20;
        }
    }

    public static class Pricing
    {
        public const decimal FreeShippingThreshold = 999m;
        public const decimal ShippingFee = 49m;
        public const decimal TaxRate = 0.18m;

        public static CartTotals Compute(List<CartLineView> lines, string couponCode)
        {
            var totals = new CartTotals();
            totals.Subtotal = lines.Sum(l => l.LineTotal);
            totals.Count = lines.Sum(l => l.Qty);

            if (!string.IsNullOrWhiteSpace(couponCode))
            {
                var result = Coupons.Validate(couponCode, totals.Subtotal);
                totals.CouponCode = couponCode;
                totals.CouponMessage = result.Message;
                if (result.Valid)
                {
                    totals.Discount = result.Amount;
                    totals.FreeShipping = result.FreeShipping;
                    totals.CouponValid = true;
                }
            }

            decimal afterDiscount = totals.Subtotal - totals.Discount;
            if (totals.FreeShipping)
            {
                totals.Shipping = 0;
            }
            else
            {
                totals.Shipping = afterDiscount >= FreeShippingThreshold ? 0 : ShippingFee;
            }

            totals.Tax = Math.Round(afterDiscount * TaxRate, 2);
            totals.Total = afterDiscount + totals.Shipping + totals.Tax;
            if (totals.Total < 0) totals.Total = 0;
            return totals;
        }
    }

    public static class CatalogCache
    {
        private static readonly object _lock = new object();
        private static List<Category> _categories;
        private static List<Brand> _brands;
        private static List<Product> _products;
        private static decimal _priceFloor;
        private static decimal _priceCeil;

        public static List<Product> Products
        {
            get
            {
                if (_products == null)
                {
                    RefreshProducts();
                }
                return _products;
            }
        }

        public static void RefreshProducts()
        {
            using (var db = new Data.StoreContext())
            {
                var list = db.Products
                    .Include("Brand")
                    .Include("Category")
                    .OrderBy(p => p.Id)
                    .ToList();
                lock (_lock)
                {
                    _products = list;
                }
            }
        }

        public static decimal PriceFloor
        {
            get
            {
                if (_priceFloor == 0)
                {
                    lock (_lock)
                    {
                        if (_priceFloor == 0)
                        {
                            using (var db = new Data.StoreContext())
                            {
                                _priceFloor = db.Products.Min(p => p.Price);
                            }
                        }
                    }
                }
                return _priceFloor;
            }
        }

        public static decimal PriceCeil
        {
            get
            {
                if (_priceCeil == 0)
                {
                    lock (_lock)
                    {
                        if (_priceCeil == 0)
                        {
                            using (var db = new Data.StoreContext())
                            {
                                _priceCeil = Math.Ceiling(db.Products.Max(p => p.Price));
                            }
                        }
                    }
                }
                return _priceCeil;
            }
        }

        public static List<Category> Categories
        {
            get
            {
                if (_categories == null)
                {
                    lock (_lock)
                    {
                        if (_categories == null)
                        {
                            using (var db = new Data.StoreContext())
                            {
                                _categories = db.Categories.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ToList();
                            }
                        }
                    }
                }
                return _categories;
            }
        }

        public static List<Brand> Brands
        {
            get
            {
                if (_brands == null)
                {
                    lock (_lock)
                    {
                        if (_brands == null)
                        {
                            using (var db = new Data.StoreContext())
                            {
                                _brands = db.Brands.OrderBy(b => b.Name).ToList();
                            }
                        }
                    }
                }
                return _brands;
            }
        }
    }
}
