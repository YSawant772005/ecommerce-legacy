using System;
using System.Collections.Generic;
using System.Linq;
using LegacyEcommerce.Infrastructure;
using LegacyEcommerce.Models;

namespace LegacyEcommerce.Data
{
    public static class EcommerceSeeder
    {
        private const int TargetProductCount = 2500;

        private class CatDef
        {
            public string Name;
            public string Slug;
            public string Code;
            public string Tagline;
            public string Description;
            public string[] Brands;
            public string[] Types;
            public string[] Descs;
            public string[] Colors;
            public string[] Sizes;
            public decimal Min;
            public decimal Max;
            public string Highlights;
            public bool BookMode;
        }

        public static void Seed(StoreContext db)
        {
            if (db.Products.Any()) return;

            var rnd = new Random(20261007);
            var now = DateTime.Now;

            var defs = BuildCategories();
            var categories = SeedCategories(db, defs);
            var brands = SeedBrands(db, defs);
            var products = SeedProducts(db, categories, defs, brands, rnd, now);
            SeedReviews(db, products, rnd);
            var users = SeedUsers(db);
            SeedOrders(db, products, users, rnd);

            db.SaveChanges();
        }

        private static List<Category> SeedCategories(StoreContext db, List<CatDef> defs)
        {
            var list = new List<Category>();
            int order = 1;
            foreach (var d in defs)
            {
                list.Add(new Category
                {
                    Name = d.Name,
                    Slug = d.Slug,
                    Tagline = d.Tagline,
                    Description = d.Description,
                    ImageUrl = "/Content/images/categories/" + d.Slug + ".svg",
                    DisplayOrder = order++,
                    IsActive = true
                });
            }
            db.Categories.AddRange(list);
            db.SaveChanges();
            return list;
        }

        private static Dictionary<string, Brand> SeedBrands(StoreContext db, List<CatDef> defs)
        {
            var map = new Dictionary<string, Brand>(StringComparer.OrdinalIgnoreCase);
            var rows = new List<Brand>();
            foreach (var name in defs.SelectMany(d => d.Brands).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var slug = Slugify(name);
                var b = new Brand
                {
                    Name = name,
                    Slug = slug,
                    Code = new string(name.Where(char.IsLetterOrDigit).Take(2).ToArray()).ToUpperInvariant()
                };
                rows.Add(b);
                map[name] = b;
            }
            db.Brands.AddRange(rows);
            db.SaveChanges();
            return map;
        }

        private static List<Product> SeedProducts(StoreContext db, List<Category> categories, List<CatDef> defs, Dictionary<string, Brand> brands, Random rnd, DateTime now)
        {
            var products = new List<Product>();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usedSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int globalIndex = 0;
            int remaining = TargetProductCount;

            for (int c = 0; c < defs.Count; c++)
            {
                var d = defs[c];
                int count = (c == defs.Count - 1) ? remaining : TargetProductCount / defs.Count;
                remaining -= count;

                var categoryBrands = d.Brands.Select(b => brands[b]).ToList();
                int catProducts = 0;

                for (int i = 0; i < count; i++)
                {
                    globalIndex++;
                    catProducts++;
                    var code = d.Code + (1000 + globalIndex);

                    string baseName = d.BookMode ? BuildBookName(d, rnd) : BuildStandardName(d, categoryBrands, rnd);
                    string name = baseName;
                    int guard = 0;
                    while (!usedNames.Add(name) && guard++ < 20)
                    {
                        name = baseName + " " + code;
                    }
                    var slug = Slugify(name);
                    int s = 2;
                    while (!usedSlugs.Add(slug))
                    {
                        slug = Slugify(name) + "-" + s++;
                    }

                    var price = NicePrice(rnd, d.Min, d.Max);
                    decimal? compare = rnd.NextDouble() < 0.72
                        ? NicePrice(rnd, price * 1.08m, Math.Max(price * 1.08m, price * 1.6m))
                        : (decimal?)null;
                    if (compare.HasValue && compare.Value <= price) compare = price + Math.Max(99m, price * 0.15m);

                    double rating = Math.Min(5.0, Math.Round(3.3 + Math.Pow(rnd.NextDouble(), 0.55) * 1.8, 1));
                    int ratingCount = Skew(rnd, 8, 9000);
                    int reviewCount = Math.Max(1, Math.Min(ratingCount, ratingCount / 9 + rnd.Next(1, 6)));
                    int sold = ratingCount * rnd.Next(3, 18) + rnd.Next(0, 500);

                    int stock = rnd.NextDouble() < 0.03 ? 0 : rnd.Next(12, 600);
                    var v = catProducts % 10;
                    var img = "/Content/images/products/" + d.Slug + "-v" + v + ".svg";
                    var img2 = "/Content/images/products/" + d.Slug + "-v" + ((v + 3) % 10) + ".svg";
                    var img3 = "/Content/images/products/" + d.Slug + "-v" + ((v + 6) % 10) + ".svg";

                    var created = now.AddDays(-rnd.Next(0, 540)).AddMinutes(-rnd.Next(0, 1440));
                    bool isNew = (i >= count - Math.Max(6, count / 14)) || (now - created).TotalDays < 45;

                    var p = new Product
                    {
                        Name = name,
                        Slug = slug,
                        Sku = code + "-" + rnd.Next(10, 99),
                        ShortDescription = BuildShort(d, name, rnd),
                        Description = BuildDescription(d, name, rnd),
                        Price = price,
                        ComparePrice = compare,
                        Stock = stock,
                        Rating = rating,
                        RatingCount = ratingCount,
                        ReviewCount = reviewCount,
                        SoldCount = sold,
                        CategoryId = 0,
                        BrandId = 0,
                        ImageUrl = img,
                        ImageUrl2 = img2,
                        ImageUrl3 = img3,
                        Colors = d.Colors != null && d.Colors.Length > 0 && !d.BookMode
                            ? string.Join(", ", Pick(rnd, d.Colors, rnd.Next(2, Math.Min(4, d.Colors.Length) + 1)))
                            : null,
                        Sizes = d.Sizes != null && d.Sizes.Length > 0 ? string.Join(",", d.Sizes) : null,
                        Highlights = d.Highlights,
                        IsFeatured = rnd.NextDouble() < 0.05,
                        IsDeal = compare.HasValue && rnd.NextDouble() < 0.45,
                        IsNew = isNew,
                        CreatedOn = created
                    };

                    p.CategoryId = categories[c].Id;
                    p.BrandId = categoryBrands[rnd.Next(categoryBrands.Count)].Id;
                    products.Add(p);
                }
            }

            db.Products.AddRange(products);
            db.SaveChanges();
            return products;
        }

        private static void SeedReviews(StoreContext db, List<Product> products, Random rnd)
        {
            var names = new[]
            {
                "Aarav Sharma","Priya Patel","Rohan Deshmukh","Sneha Iyer","Vikram Reddy","Ananya Rao",
                "Karthik Nair","Meera Joshi","Aditya Banerjee","Nisha Verma","Suresh Menon","Divya Krishnan",
                "Amit Kulkarni","Ritu Malhotra","Sandeep Chauhan","Pooja Bhatt","Arjun Saxena","Kavya Menon",
                "John D'Souza","Emily Carter","Rahul Gupta","Tanvi Shah","Imran Sheikh","Sara Khan",
                "Neha Kapse","Omkar Patil","Shreya Ghosh","Varun Thakur","Ishita Bose","Manish Yadav"
            };

            var good = new[]
            {
                new { T = "Absolutely fantastic", B = "Honestly exceeded what I expected for the price. Packaging was neat and delivery was quick. Using it daily for a few weeks now and zero complaints." },
                new { T = "Worth every rupee", B = "Build quality feels premium and it works exactly as described on the page. Would happily buy again from this brand." },
                new { T = "Great value for money", B = "Did a lot of comparison before buying this one. Glad I went with it - performance is solid and it looks even better in person." },
                new { T = "Highly recommended", B = "Received a genuine product with warranty card. Setup was easy and customer support responded quickly to my query." },
                new { T = "Perfect for everyday use", B = "Exactly what I was looking for. The finish is clean, and after three weeks of use I have no regrets at all." }
            };

            var ok = new[]
            {
                new { T = "Good, few niggles", B = "Overall a decent buy. Works as advertised but the packaging could have been better. Everything functions fine though." },
                new { T = "Does the job", B = "Nothing fancy but it gets the work done. For this price point I cannot really complain. Four stars from me." },
                new { T = "Pretty good", B = "Happy with the purchase. Took a couple of days to get used to it, after that it has been smooth sailing." },
                new { T = "Solid purchase", B = "Quality is above average and delivery was on time. One minor issue with instructions, otherwise all good." }
            };

            var bad = new[]
            {
                new { T = "Expected better", B = "It is not bad exactly, but the photos on the listing made it look better than it is. Keeping it since returning felt like a hassle." },
                new { T = "Average product", B = "Works, but I have seen better at this price. Build feels a bit light and the manual was not very helpful." },
                new { T = "Not for me", B = "Maybe I had too much expectation. It is usable but I would probably pick a different model next time." }
            };

            var reviews = new List<Review>();
            foreach (var p in products)
            {
                if (rnd.NextDouble() > 0.5) continue;
                int count = rnd.Next(1, p.Rating > 4.3 ? 6 : 4);
                var usedT = new HashSet<string>();
                for (int i = 0; i < count; i++)
                {
                    int bucket = rnd.Next(100);
                    var pool = bucket < 62 ? good : bucket < 88 ? ok : bad;
                    var item = pool[rnd.Next(pool.Length)];
                    if (!usedT.Add(item.T)) continue;
                    int stars = pool == good ? rnd.Next(4, 6) : pool == ok ? rnd.Next(3, 5) : rnd.Next(1, 3);
                    reviews.Add(new Review
                    {
                        ProductId = p.Id,
                        AuthorName = names[rnd.Next(names.Length)],
                        Rating = stars,
                        Title = item.T,
                        Body = item.B,
                        VerifiedPurchase = rnd.NextDouble() < 0.72,
                        CreatedOn = p.CreatedOn.AddDays(rnd.Next(1, 240)).AddMinutes(rnd.Next(0, 1440))
                    });
                }
            }

            for (int i = 0; i < reviews.Count; i += 400)
            {
                db.Reviews.AddRange(reviews.Skip(i).Take(400));
                db.SaveChanges();
            }
        }

        private static List<User> SeedUsers(StoreContext db)
        {
            User Make(string name, string email, string password, string role, string phone)
            {
                var hash = PasswordHasher.Hash(password);
                return new User
                {
                    FullName = name,
                    Email = email,
                    PasswordHash = hash.Hash,
                    PasswordSalt = hash.Salt,
                    Role = role,
                    Phone = phone,
                    CreatedOn = DateTime.Now.AddDays(-new Random(email.Length * 7).Next(30, 500))
                };
            }

            var users = new List<User>
            {
                Make("Store Admin", "admin@novakart.in", "Admin@123", "Admin", "9876500001"),
                Make("Aarav Sharma", "demo@novakart.in", "Demo@123", "Customer", "9876500002"),
                Make("Priya Patel", "priya@example.in", "Demo@123", "Customer", "9876500003"),
                Make("Rohan Deshmukh", "rohan@example.in", "Demo@123", "Customer", "9876500004"),
                Make("Sneha Iyer", "sneha@example.in", "Demo@123", "Customer", "9876500005"),
                Make("Vikram Reddy", "vikram@example.in", "Demo@123", "Customer", "9876500006"),
                Make("Ananya Rao", "ananya@example.in", "Demo@123", "Customer", "9876500007"),
                Make("Karthik Nair", "karthik@example.in", "Demo@123", "Customer", "9876500008"),
                Make("Meera Joshi", "meera@example.in", "Demo@123", "Customer", "9876500009")
            };
            db.Users.AddRange(users);
            db.SaveChanges();
            return users;
        }

        private static void SeedOrders(StoreContext db, List<Product> products, List<User> users, Random rnd)
        {
            var customers = users.Where(u => u.Role == "Customer").ToList();
            var cities = new[]
            {
                new { City = "Pune", State = "Maharashtra", Zip = "411001" },
                new { City = "Mumbai", State = "Maharashtra", Zip = "400001" },
                new { City = "Bengaluru", State = "Karnataka", Zip = "560001" },
                new { City = "Hyderabad", State = "Telangana", Zip = "500001" },
                new { City = "Delhi", State = "Delhi", Zip = "110001" },
                new { City = "Chennai", State = "Tamil Nadu", Zip = "600001" },
                new { City = "Ahmedabad", State = "Gujarat", Zip = "380001" },
                new { City = "Kolkata", State = "West Bengal", Zip = "700001" }
            };
            var payments = new[] { "UPI", "Card", "COD" };
            var addresses = new[] { "Flat 402, Sunrise Residency, MG Road", "B-12, Green Park Colony", "House 7, Lake View Road", "301, Silver Heights, Station Road", "22, Rose Villa, Civil Lines" };

            var orders = new List<Order>();
            for (int i = 0; i < 60; i++)
            {
                int daysAgo = rnd.Next(1, 60);
                var created = DateTime.Now.AddDays(-daysAgo).AddMinutes(-rnd.Next(0, 1400));
                var cust = customers[rnd.Next(customers.Count)];
                var city = cities[rnd.Next(cities.Length)];
                int lineCount = rnd.Next(1, 5);

                var lines = new List<OrderItem>();
                var picked = new HashSet<int>();
                decimal subtotal = 0;
                for (int j = 0; j < lineCount; j++)
                {
                    var prod = products[rnd.Next(products.Count)];
                    if (!picked.Add(prod.Id)) continue;
                    int qty = rnd.Next(1, 4);
                    subtotal += prod.Price * qty;
                    lines.Add(new OrderItem
                    {
                        ProductId = prod.Id,
                        ProductName = prod.Name,
                        ImageUrl = prod.ImageUrl,
                        Slug = prod.Slug,
                        UnitPrice = prod.Price,
                        Quantity = qty
                    });
                }
                if (lines.Count == 0) continue;

                decimal discount = 0;
                string coupon = null;
                if (subtotal >= 999 && rnd.NextDouble() < 0.3)
                {
                    coupon = "SAVE10";
                    discount = Math.Min(Math.Round(subtotal * 0.10m, 2), 500);
                }

                decimal shipping = (subtotal - discount) >= 999 ? 0 : 49;
                decimal tax = Math.Round((subtotal - discount) * 0.18m, 2);
                decimal total = subtotal - discount + shipping + tax;

                OrderStatus status;
                if (daysAgo > 25) status = rnd.NextDouble() < 0.9 ? OrderStatus.Delivered : OrderStatus.Cancelled;
                else if (daysAgo > 12) status = rnd.NextDouble() < 0.75 ? OrderStatus.Delivered : OrderStatus.Shipped;
                else if (daysAgo > 5) status = rnd.NextDouble() < 0.5 ? OrderStatus.Shipped : OrderStatus.Delivered;
                else status = daysAgo > 2 ? OrderStatus.Packed : OrderStatus.Placed;

                var order = new Order
                {
                    OrderNumber = "NK" + created.ToString("yyMMdd") + "-" + (1000 + i).ToString(),
                    UserId = cust.Id,
                    Email = cust.Email,
                    FullName = cust.FullName,
                    Phone = cust.Phone,
                    AddressLine = addresses[rnd.Next(addresses.Length)],
                    City = city.City,
                    State = city.State,
                    PostalCode = city.Zip,
                    Subtotal = subtotal,
                    Discount = discount,
                    Shipping = shipping,
                    Tax = tax,
                    Total = total,
                    Status = status,
                    PaymentMethod = payments[rnd.Next(payments.Length)],
                    PaymentRef = status == OrderStatus.Cancelled ? null : "TXN" + rnd.Next(10000000, 99999999),
                    CouponCode = coupon,
                    CreatedOn = created,
                    Items = lines
                };
                orders.Add(order);
            }

            for (int i = 0; i < orders.Count; i += 20)
            {
                db.Orders.AddRange(orders.Skip(i).Take(20));
                db.SaveChanges();
            }
        }

        private static string BuildStandardName(CatDef d, List<Brand> brandPool, Random rnd)
        {
            var brand = brandPool[rnd.Next(brandPool.Count)].Name;
            var desc = d.Descs[rnd.Next(d.Descs.Length)];
            var type = d.Types[rnd.Next(d.Types.Length)];
            int pattern = rnd.Next(3);
            if (pattern == 0) return brand + " " + desc + " " + type;
            if (pattern == 1) return brand + " " + type + " " + desc;
            return brand + " " + desc + " " + type;
        }

        private static string[] BookAdjs = { "Silent", "Crimson", "Hidden", "Eternal", "Fractured", "Midnight", "Paper", "Golden", "Broken", "Wandering", "Last", "Iron", "Glass", "Shifting", "Secret", "Burning" };
        private static string[] BookNouns = { "Harbor", "Kingdom", "Signal", "Garden", "Cipher", "River", "Empire", "Letter", "Horizon", "Archive", "Valley", "Crown", "Tide", "Bridge", "Forest", "Milestone" };

        private static string BuildBookName(CatDef d, Random rnd)
        {
            var title = "The " + BookAdjs[rnd.Next(BookAdjs.Length)] + " " + BookNouns[rnd.Next(BookNouns.Length)];
            if (rnd.NextDouble() < 0.3)
            {
                title += ", Book " + rnd.Next(2, 6);
            }
            return title;
        }

        private static string BuildShort(CatDef d, string name, Random rnd)
        {
            if (d.BookMode)
            {
                var genres = d.Types;
                return genres[rnd.Next(genres.Length)] + " | A " + (rnd.NextDouble() < 0.5 ? "paperback" : "e-reader favourite") + " pick loved by readers across India.";
            }
            var f = d.Highlights.Split('|');
            return "Premium " + d.Types[rnd.Next(d.Types.Length)].ToLowerInvariant()
                + " with " + f[0].ToLowerInvariant() + ", " + f[1].ToLowerInvariant()
                + " and " + f[2].ToLowerInvariant() + ".";
        }

        private static string BuildDescription(CatDef d, string name, Random rnd)
        {
            var f = d.Highlights.Split('|');
            if (d.BookMode)
            {
                return "About the book\n\n" + name + " is a " + d.Types[rnd.Next(d.Types.Length)].ToLowerInvariant()
                    + " title that has kept readers turning pages long past midnight. Written in clear, accessible prose, it balances depth with pace, making it a great pick for both seasoned readers and newcomers.\n\n"
                    + "Highlights\n"
                    + "\u2022 Clean, readable print on quality paper\n"
                    + "\u2022 Durable binding built for re-reading\n"
                    + "\u2022 Ships in protective packaging\n"
                    + "\u2022 " + f[0] + " | " + f[1] + "\n\n"
                    + "Whether it is for your shelf or a thoughtful gift, this one earns its place. Easy 7-day replacement if it arrives damaged.";
            }

            return "About the product\n\n"
                + "The " + name + " is built for people who want dependable performance without the fuss. It combines " + f[0].ToLowerInvariant()
                + " with " + f[1].ToLowerInvariant() + " so it fits naturally into your daily routine, whether at home, at work or on the move.\n\n"
                + "Highlights\n"
                + "\u2022 " + f[0] + "\n"
                + "\u2022 " + f[1] + "\n"
                + "\u2022 " + f[2] + "\n"
                + "\u2022 " + f[3] + "\n"
                + "\u2022 Quality checked and ready to use out of the box\n\n"
                + "Backed by a hassle-free warranty and easy returns, this is one of those buys you can make without second-guessing. Free delivery on eligible orders and cash on delivery available.";
        }

        private static decimal NicePrice(Random rnd, decimal min, decimal max)
        {
            if (max < min) max = min;
            var raw = min + (decimal)rnd.NextDouble() * (max - min);
            var step = raw > 20000 ? 100m : raw > 2000 ? 50m : 10m;
            var rounded = Math.Round(raw / step) * step;
            if (rounded < min) rounded = min;
            return Math.Max(49m, rounded - 1m);
        }

        private static int Skew(Random rnd, int min, int max)
        {
            double t = Math.Pow(rnd.NextDouble(), 2.2);
            return (int)(min + t * (max - min));
        }

        private static List<T> Pick<T>(Random rnd, T[] source, int count)
        {
            var pool = source.ToList();
            var result = new List<T>();
            while (pool.Count > 0 && result.Count < count)
            {
                var idx = rnd.Next(pool.Count);
                result.Add(pool[idx]);
                pool.RemoveAt(idx);
            }
            return result;
        }

        public static string Slugify(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "item";
            var sb = new System.Text.StringBuilder();
            bool lastDash = false;
            foreach (var ch in value.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(ch);
                    lastDash = false;
                }
                else if (!lastDash && sb.Length > 0)
                {
                    sb.Append('-');
                    lastDash = true;
                }
            }
            var s = sb.ToString().Trim('-');
            return s.Length == 0 ? "item" : s;
        }

        private static List<CatDef> BuildCategories()
        {
            return new List<CatDef>
            {
                new CatDef
                {
                    Name = "Electronics", Slug = "electronics", Code = "ELC",
                    Tagline = "Sound, vision and smart living",
                    Description = "Televisions, audio gear, cameras and wearables from brands you can trust - all quality checked and warranty backed.",
                    Brands = new[] { "NovaWave", "VoltEdge", "EchoLine", "Orbit", "Lumen", "Vortex", "Cobalt", "Hyperion" },
                    Types = new[] { "4K Ultra HD Smart TV", "Wireless Soundbar", "Bluetooth Speaker", "Over-Ear Headphones", "True Wireless Earbuds", "Action Camera", "Home Theater System", "Digital Camera", "Smart Watch", "HD Projector", "Streaming Media Player", "Power Bank" },
                    Descs = new[] { "Pro", "Max", "Ultra", "Prime", "Elite", "Air", "Neo", "Plus" },
                    Colors = new[] { "Black", "Graphite", "Silver", "White", "Navy" },
                    Sizes = new string[0],
                    Min = 1499, Max = 149999,
                    Highlights = "1-Year Brand Warranty|Free Delivery|7-Day Replacement|Cash on Delivery"
                },
                new CatDef
                {
                    Name = "Phones & Tablets", Slug = "phones-tablets", Code = "PHN",
                    Tagline = "Stay connected, stay ahead",
                    Description = "5G smartphones, tablets and accessories with big displays, big batteries and bigger value.",
                    Brands = new[] { "NovaWave", "ZenithX", "Quantum", "Apex", "Orbit", "Cobalt" },
                    Types = new[] { "5G Smartphone", "Android Phone", "Phablet", "Android Tablet", "2-in-1 Tablet", "Feature Phone", "Stylus Tablet" },
                    Descs = new[] { "Pro", "Max", "Ultra", "Lite", "Plus", "Neo", "Air", "Prime" },
                    Colors = new[] { "Midnight Black", "Aurora Blue", "Silver", "Forest Green", "Coral" },
                    Sizes = new string[0],
                    Min = 4999, Max = 129999,
                    Highlights = "1-Year Warranty|Fast Charging|7-Day Replacement|Cash on Delivery"
                },
                new CatDef
                {
                    Name = "Computers & Laptops", Slug = "computers", Code = "CMP",
                    Tagline = "Power for work and play",
                    Description = "Laptops, desktops, monitors and peripherals configured for students, creators and gamers alike.",
                    Brands = new[] { "Skyline", "Quantum", "Pinnacle", "Cobalt", "Hyperion", "Meridian" },
                    Types = new[] { "Ultrabook Laptop", "Gaming Laptop", "Business Laptop", "All-in-One Desktop", "Gaming Desktop PC", "Mechanical Keyboard", "Wireless Mouse", "27-inch Monitor", "USB-C Docking Station", "External SSD" },
                    Descs = new[] { "Pro", "Max", "Turbo", "Prime", "Elite", "Slim", "Neo", "Plus" },
                    Colors = new[] { "Space Grey", "Silver", "Black", "Midnight Blue" },
                    Sizes = new string[0],
                    Min = 2499, Max = 249999,
                    Highlights = "2-Year Warranty|Free Installation|7-Day Replacement|Cash on Delivery"
                },
                new CatDef
                {
                    Name = "Home & Furniture", Slug = "furniture", Code = "FNT",
                    Tagline = "Make every corner yours",
                    Description = "Sofas, beds, desks and storage that turn empty rooms into places you actually want to be.",
                    Brands = new[] { "Sterling", "Hearthwood", "UrbanNest", "Cascade", "Timberline" },
                    Types = new[] { "3-Seater Sofa", "Lounge Chair", "Coffee Table", "Study Desk", "Bookshelf", "Queen Bed Frame", "Wardrobe", "Shoe Rack", "TV Unit", "Dining Table Set", "Recliner", "Side Table" },
                    Descs = new[] { "Classic", "Signature", "Nordic", "Urban", "Craft", "Essential", "Premium", "Compact" },
                    Colors = new[] { "Walnut", "Oak Finish", "Ivory", "Charcoal", "Wenge" },
                    Sizes = new string[0],
                    Min = 1999, Max = 89999,
                    Highlights = "Termite-Resistant Wood|Free Assembly|7-Day Replacement|5-Year Frame Warranty"
                },
                new CatDef
                {
                    Name = "Kitchen & Appliances", Slug = "kitchen", Code = "KTN",
                    Tagline = "Cook faster, live easier",
                    Description = "Appliances and cookware that earn their counter space - from morning chai to midnight snacks.",
                    Brands = new[] { "Hearthwood", "Culinaire", "Everfresh", "Sterling", "VoltEdge" },
                    Types = new[] { "Mixer Grinder", "Air Fryer", "Induction Cooktop", "Pressure Cooker", "Electric Kettle", "Rice Cooker", "Microwave Oven", "Dishwasher", "Juicer Blender", "Water Purifier", "Coffee Maker", "Vegetable Chopper" },
                    Descs = new[] { "Pro", "Max", "Chef", "Prime", "Turbo", "Essential", "Neo", "Plus" },
                    Colors = new[] { "Black", "Silver", "White", "Red", "Grey" },
                    Sizes = new string[0],
                    Min = 799, Max = 49999,
                    Highlights = "1-Year Warranty|Energy Efficient|7-Day Replacement|Free Delivery"
                },
                new CatDef
                {
                    Name = "Men's Fashion", Slug = "mens-fashion", Code = "MNS",
                    Tagline = "Sharp looks, everyday comfort",
                    Description = "Shirts, denim and essentials cut for real life - office, weekend and everything between.",
                    Brands = new[] { "UrbanWeave", "DenimHaus", "MetroThread", "Apex", "LunarCo" },
                    Types = new[] { "Slim Fit Shirt", "Casual T-Shirt", "Denim Jeans", "Chinos", "Polo T-Shirt", "Bomber Jacket", "Hoodie", "Formal Blazer", "Track Pants", "Linen Shirt" },
                    Descs = new[] { "Classic", "Signature", "Urban", "Essentials", "Premium", "Relaxed", "Streetside", "Formal" },
                    Colors = new[] { "Black", "White", "Navy", "Olive", "Beige", "Indigo" },
                    Sizes = new[] { "S", "M", "L", "XL", "XXL" },
                    Min = 399, Max = 5999,
                    Highlights = "Skin-Friendly Fabric|Easy 15-Day Returns|Free Delivery|Cash on Delivery"
                },
                new CatDef
                {
                    Name = "Women's Fashion", Slug = "womens-fashion", Code = "WMS",
                    Tagline = "Ethnic, western, all of you",
                    Description = "Kurtis, dresses, sarees and fusion wear in fabrics that feel as good as they look.",
                    Brands = new[] { "Velvet & Co", "MilanThread", "Bloom & Lace", "Serein", "PetalRow" },
                    Types = new[] { "Kurti", "Anarkali Suit", "Maxi Dress", "Palazzo Set", "Saree", "Crop Top", "Wrap Blouse", "Jeggings", "Ethnic Lehenga", "Denim Skirt" },
                    Descs = new[] { "Signature", "Everyday", "Festive", "Premium", "Boho", "Essentials", "Editorial", "Handloom" },
                    Colors = new[] { "Rose", "Black", "Mustard", "Teal", "Ivory", "Wine" },
                    Sizes = new[] { "XS", "S", "M", "L", "XL" },
                    Min = 499, Max = 8999,
                    Highlights = "Breathable Fabric|Easy 15-Day Returns|Free Delivery|Cash on Delivery"
                },
                new CatDef
                {
                    Name = "Footwear", Slug = "footwear", Code = "FTW",
                    Tagline = "Every step, sorted",
                    Description = "Running shoes, sneakers and formals that go the distance in comfort and style.",
                    Brands = new[] { "SoleMate", "StrideCo", "TrailBlaz", "Pacer", "AeroStep" },
                    Types = new[] { "Running Shoes", "Casual Sneakers", "Formal Leather Shoes", "Sports Sandals", "Canvas Shoes", "Hiking Boots", "Slides", "Ballet Flats" },
                    Descs = new[] { "Pro", "Air", "Street", "Classic", "Elite", "Grip", "Flex", "Urban" },
                    Colors = new[] { "Black", "White", "Grey", "Red", "Blue" },
                    Sizes = new[] { "6", "7", "8", "9", "10", "11", "12" },
                    Min = 799, Max = 12999,
                    Highlights = "Cushioned Sole|Easy 15-Day Returns|Free Delivery|Anti-Skid Grip"
                },
                new CatDef
                {
                    Name = "Beauty & Personal Care", Slug = "beauty", Code = "BTY",
                    Tagline = "Look good, feel better",
                    Description = "Skin care, grooming tools and daily essentials with ingredients you can pronounce.",
                    Brands = new[] { "GlowLab", "Naturale", "LushLeaf", "AuraBotanics", "VelvetSkin" },
                    Types = new[] { "Face Serum", "Vitamin C Cream", "Hair Dryer", "Beard Trimmer", "Electric Shaver", "Eau de Parfum", "Sunscreen SPF 50", "Shower Gel Set", "Hair Straightener", "Massage Gun" },
                    Descs = new[] { "Pure", "Daily", "Advanced", "Botanics", "Pro", "Glow", "Essential", "Rich" },
                    Colors = new[] { "White", "Green", "Gold", "Pink", "Amber" },
                    Sizes = new string[0],
                    Min = 249, Max = 8999,
                    Highlights = "Dermatologically Tested|Free Delivery|7-Day Replacement|Cruelty Free"
                },
                new CatDef
                {
                    Name = "Sports & Outdoors", Slug = "sports", Code = "SPT",
                    Tagline = "Move more, breathe more",
                    Description = "Fitness gear and outdoor equipment for training days, trek days and every day in between.",
                    Brands = new[] { "IronPulse", "TrailBlaz", "Vortex", "AeroStep", "Summit" },
                    Types = new[] { "Yoga Mat", "Adjustable Dumbbells", "Cricket Bat", "Football", "Badminton Racket Set", "Camping Tent", "Trekking Backpack", "Resistance Bands", "Skipping Rope", "Cycling Helmet" },
                    Descs = new[] { "Pro", "Elite", "Trail", "Core", "Turbo", "Grip", "Flex", "Summit" },
                    Colors = new[] { "Black", "Blue", "Red", "Grey", "Orange" },
                    Sizes = new string[0],
                    Min = 299, Max = 24999,
                    Highlights = "Heavy-Duty Build|1-Year Warranty|Free Delivery|7-Day Replacement"
                },
                new CatDef
                {
                    Name = "Toys & Games", Slug = "toys", Code = "TOY",
                    Tagline = "Play time, upgraded",
                    Description = "STEM kits, board games and toys that keep kids (and grown-ups) happily busy for hours.",
                    Brands = new[] { "KiddoJoy", "BrickPlanet", "WonderWorks", "PuzzleNest" },
                    Types = new[] { "Building Blocks Set", "Remote Control Car", "Action Figure", "Board Game", "Jigsaw Puzzle 1000pc", "Plush Bear", "STEM Robotics Kit", "Doll House", "Carrom Board", "Classic Yoyo" },
                    Descs = new[] { "Super", "Mega", "Fun", "Junior", "Creator", "Adventure", "Galaxy", "Builder" },
                    Colors = new[] { "Multi", "Blue", "Red", "Yellow", "Green" },
                    Sizes = new string[0],
                    Min = 199, Max = 7999,
                    Highlights = "Non-Toxic Materials|Age 6+|Free Delivery|7-Day Replacement"
                },
                new CatDef
                {
                    Name = "Books & Stationery", Slug = "books", Code = "BOK",
                    Tagline = "Stories worth staying up for",
                    Description = "Best-loved fiction, non-fiction and stationery for readers, students and dreamers.",
                    Brands = new[] { "InkWell", "PageTurner", "StoryForge", "Codex", "PaperCraft" },
                    Types = new[] { "Fiction", "Mystery", "Science Fiction", "Romance", "Thriller", "Fantasy", "Self-Help", "Business", "Young Adult", "Children's Picture Book", "Cookbook", "Programming" },
                    Descs = new[] { "Classic" },
                    Colors = new string[0],
                    Sizes = new string[0],
                    Min = 149, Max = 1499,
                    Highlights = "Quality Paper|Free Delivery|7-Day Replacement|Gift Ready Packaging",
                    BookMode = true
                }
            };
        }
    }
}
