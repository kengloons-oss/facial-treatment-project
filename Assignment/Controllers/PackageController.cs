using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using X.PagedList.Extensions;
using Stripe.Checkout; // Assuming Package model exists

using Newtonsoft.Json;
using QuestPDF.Elements; // Assuming Package model exists
using Assignment.Services.QRCodeGeneratorHelper;
using Microsoft.AspNetCore.DataProtection;

namespace Assignment.Controllers;

public class PackageController : Controller
{
    private readonly DB db;
    private readonly Helpers hp;
    private readonly IQRCodeGeneratorHelper qrCodeGeneratorHelper;
    private readonly IDataProtector protector;

    public PackageController(DB db, Helpers hp, IQRCodeGeneratorHelper qrCodeGeneratorHelper, IDataProtectionProvider dataProtectionProvider)
    {
        this.db = db;
        this.hp = hp;
        this.qrCodeGeneratorHelper = qrCodeGeneratorHelper;
        this.protector = dataProtectionProvider.CreateProtector("LoginDataProtector");
    }

    public IActionResult Index()
    {
        return View();
    }

    private int GetMemberIdFromSession()
    {
        var memberId = HttpContext.Session.GetInt32("_userId");
        if (!memberId.HasValue)
        {
            //if not found 
            throw new Exception("User not logged in. Redirect to login.");
        }
        return memberId.Value;
    }

    public IActionResult List(string? name, double? min_price, double? max_price, string? service_categories, string? sort, string? dir)
    {
        var packages = db.Packages
                            .AsNoTracking()
                            .AsSplitQuery()
                            .Include(p => p.PackagePhotos)
                            .Include(p => p.PackageTreatments)
                                .ThenInclude(pt => pt.PackageTreatmentServices)
                                    .ThenInclude(pts => pts.Service)
                                        .ThenInclude(s => s.Category)
                            .Include(p => p.OrderPackages)
                                .ThenInclude(op => op.OrderPackageTreatments)
                                    .ThenInclude(opt => opt.Appointment)
                                        .ThenInclude(a => a.AppointmentReviews)
                            .Where(p => p.Status == 1);

        // search package name
        ViewBag.Name = name = name?.Trim() ?? "";

        var searched = packages.Where(p => p.Name.Contains(name));

        // filter price range
        ViewBag.Min_price = min_price = min_price ?? 0.01;

        if (searched.Any())
        {
            ViewBag.Max_price = max_price = max_price ?? Convert.ToDouble(searched.Max(p => p.Price));
        }
        else
        {
            ViewBag.Max_price = max_price = max_price ?? 0.00;
        }

        var filter_price = searched.Where(p => p.Price >= Convert.ToDecimal(min_price)
                                            && p.Price <= Convert.ToDecimal(max_price));

        // filter category
        int[] selected_categories = [];

        try
        {
            if (!string.IsNullOrEmpty(service_categories))
            {
                selected_categories = System.Text.Json.JsonSerializer.Deserialize<int[]>(service_categories);
            }
        }
        catch (JsonException ex)
        {
            selected_categories = new int[] { };
        }

        ViewBag.Selected_categories = selected_categories;
        var filter_categories = filter_price;

        if (selected_categories.Any())
        {
            filter_categories = filter_categories
                                        .Where(o => selected_categories.All(catId =>
                                            o.OrderPackages.Any(op =>
                                                op.OrderPackageTreatments.Any(opt =>
                                                    opt.OrderPackageTreatmentServices.Any(opts =>
                                                        opts.Service.CategoryId == catId
                                                    )
                                                )
                                            )
                                        )
                                    );
        }

        // sorting
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<Package, object> fn = sort switch
        {
            "name" => p => p.Name,
            "validity" => p => p.Validity,
            "price-high-to-low" => p => p.Price,
            "price-low-to-high" => p => p.Price,
            _ => p => p.Id,
        };

        var sorted = dir == "desc" ? filter_categories.OrderByDescending(fn) : filter_categories.OrderBy(fn);

        // paging
        //ViewBag.Page_items = page_items;

        //var m = sorted.ToPagedList(page, page_items);
        var m = sorted.ToList();

        //if (page < 1)
        //{
        //    return RedirectToAction(null, new { name, min_price, max_price, service_categories, sort, dir, page_items, page = 1 });
        //}

        // check whether it is ajax
        if (Request.IsAjax())
        {
            return PartialView("Member/_PackageList", m);
        }

        // get service category list
        var serviceCategories = db.Categories.Where(c => c.Status == 1);
        ViewBag.ServiceCategories = serviceCategories;

        return View("Member/List", m);
    }

    //Package details page
    public IActionResult Detail(int id, bool? showAll, bool? withImages, int? star, string? sort, string? dir, int page_items = 7, int page = 1)
    {
        // Store the page member from before login
        hp.SetCookie("redirect", $"/Package/Detail/{id}");

        // obtain package
        var package = db.Packages
                            .AsNoTracking()
                            .AsSplitQuery()
                            .Include(p => p.PackagePhotos)
                            .Include(p => p.PackageTreatments)
                                .ThenInclude(pt => pt.PackageTreatmentServices)
                                    .ThenInclude(pts => pts.Service)
                                        .ThenInclude(s => s.ServicePhotos)
                            .Include(p => p.OrderPackages)
                                .ThenInclude(op => op.OrderPackageTreatments)
                                    .ThenInclude(opt => opt.Appointment)
                                        .ThenInclude(a => a.AppointmentReviews)
                                            .ThenInclude(ar => ar.ReviewPhotos)
                            .Where(p => p.Id == id)
                            .FirstOrDefault();

        // handle when package not found
        if (package == null)
        {
            TempData["Error_Title"] = "Invalid Package";
            TempData["Alert_Error"] = "Package does not exist.";

            return RedirectToAction("List");
        }
        ViewBag.Id = id;

        // generate qr code
        var baseURL = $"{Request.Scheme}://{Request.Host}/";
        var url = baseURL + $"Package/Detail/{id}";

        byte[] QRCodeAsBytes = qrCodeGeneratorHelper.GenerateQRCode(url);
        string QRCodeAsImageBase64 = $"data:image/png;base64,{Convert.ToBase64String(QRCodeAsBytes)}";

        ViewBag.QRCodeUrl = url;
        ViewBag.QRCodeImageUrl = QRCodeAsImageBase64;

        // obtain reviews
        var reviews = db.AppointmentReviews
                        .Include(ar => ar.Appointment)
                            .ThenInclude(a => a.OrderPackageTreatment)
                                .ThenInclude(a => a.OrderPackage)
                                    .ThenInclude(a => a.Package)
                        .Include(ar => ar.Appointment)
                            .ThenInclude(a => a.Member)
                                .ThenInclude(m => m.Person)
                        .Include(ar => ar.ReviewPhotos)
                        .Where(ar => ar.Appointment.OrderPackageTreatment.OrderPackage.Package.Id == id);

        if (reviews == null)
        {
            TempData["Error_Title"] = "Invalid Review";
            TempData["Alert_Error"] = "Review does not exist.";

            return RedirectToAction("List");
        }

        // filter "show all"
        var filter = reviews;

        ViewBag.ShowAll = showAll = showAll ?? true;

        if (showAll == true)
        {
            filter = reviews;
        }

        // filter "with images"
        ViewBag.WithImages = withImages = withImages ?? false;

        if (withImages == true)
        {
            filter = filter.Where(ar => ar.ReviewPhotos.Any());
        }

        // filter star
        ViewBag.Star = star = star ?? 0;
        var filter_star = filter;

        if (star != 0)
        {
            filter_star = filter.Where(ar => ar.Star == star);
        }

        // sorting
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<AppointmentReview, object> fn = sort switch
        {
            "Star" => ar => ar.Star,
            "Created Date" => ar => ar.Comment_date,
            "Images Number" => ar => ar.ReviewPhotos?.Count() ?? 0,
            _ => ar => ar.Id,
        };

        var sorted = dir == "desc" ? filter_star.OrderByDescending(fn) : filter_star.OrderBy(fn);

        // paging
        if (page < 1)
        {
            return RedirectToAction(null, new { id, showAll, withImages, star, sort, dir, page_items, page = 1 });
        }

        // filter items per page
        ViewBag.Page_items = page_items;

        var m = sorted.ToPagedList(page, page_items);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { id, showAll, withImages, star, sort, dir, page_items, page = m.PageCount });
        }

        var vm = new PackageReviewVM
        {
            Package = package,
            Reviews = m
        };

        // check whether it is ajax
        if (Request.IsAjax())
        {
            return PartialView("Member/_ReviewList", vm);
        }

        return View("Member/Detail", vm);
    }

    // package detail review list update
    public IActionResult UpdateReviewList(int package_id, bool? showAll, bool? withImages, int? star, string? sort, string? dir, int page_items = 7, int page = 1)
    {
        // obtain reviews
        var reviews = db.AppointmentReviews
                        .Include(ar => ar.Appointment)
                            .ThenInclude(a => a.OrderPackageTreatment)
                                .ThenInclude(a => a.OrderPackage)
                                    .ThenInclude(a => a.Package)
                        .Include(ar => ar.ReviewPhotos)
                        .Where(ar => ar.Appointment.OrderPackageTreatment.OrderPackage.Package.Id == package_id);

        if (reviews == null)
        {
            TempData["Error_Title"] = "Invalid Review";
            TempData["Alert_Error"] = "Review does not exist.";

            return RedirectToAction("List");
        }

        // filter "show all"
        var filter = reviews;

        ViewBag.ShowAll = showAll = showAll ?? true;

        if (showAll == true)
        {
            filter = reviews;
        }

        // filter "with images"
        ViewBag.WithImages = withImages = withImages ?? false;

        if (withImages == true)
        {
            filter = filter.Where(ar => ar.Appointment.OrderPackageTreatment.OrderPackage.Package.PackagePhotos.Any());
        }

        // filter star
        ViewBag.Star = star = star ?? 0;
        var filter_star = filter;

        if (star != 0)
        {
            filter_star = filter.Where(ar => ar.Star == star);
        }

        // sorting
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<AppointmentReview, object> fn = sort switch
        {
            "Star" => ar => ar.Star,
            "Created Date" => ar => ar.Comment_date,
            "Images Number" => ar => ar.ReviewPhotos?.Count() ?? 0,
            _ => ar => ar.Id,
        };

        var sorted = dir == "desc" ? filter_star.OrderByDescending(fn) : filter_star.OrderBy(fn);

        // paging
        if (page < 1)
        {
            return RedirectToAction(null, new { package_id, showAll, withImages, star, sort, dir, page_items, page = 1 });
        }

        // filter items per page
        ViewBag.Page_items = page_items;

        var m = sorted.ToPagedList(page, page_items);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { package_id, showAll, withImages, star, sort, dir, page_items, page = m.PageCount });
        }

        // check whether it is ajax
        if (Request.IsAjax())
        {
            return PartialView("Member/_ReviewList", m);
        }

        return RedirectToAction("List");
    }

    [HttpPost]
    public IActionResult AddToCart(int packageId, int quantity = 1)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "member" }, TempData))
        {
            //return RedirectToAction("Home", "Home");
            return Json(new { success = false, message = "Please login first." });
        }

        try
        {
            var memberId = HttpContext.Session.GetInt32("_userId");

            // Validate the package ID
            var package = db.Packages.Find(packageId);
            if (package == null)
            {
                return Json(new { success = false, message = "Package not found." });
            }

            // Check if the item already exists in the cart
            var cartItem = db.Carts.FirstOrDefault(c => c.MemberId == memberId && c.PackageId == packageId);

            if (cartItem != null)
            {
                return Json(new { success = false, message = "Item already exists in the cart." });
            }

            // Add a new entry to the cart
            db.Carts.Add(new Cart
            {
                MemberId = (int)memberId,
                PackageId = packageId,
            });
            db.SaveChanges();

            // retrieve cart items count
            var item_count = db.Carts
                                .Where(c => c.MemberId == memberId)
                                .Count();

            return Json(new { success = true, message = "Item added to cart successfully.", item_count = item_count });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    public IActionResult Cart()
    {
        try
        {
            // Store the page member from before login
            hp.SetCookie("redirect", $"/Package/Cart");

            if (!hp.CheckRole(new string[] { "member" }, TempData))
            {
                return RedirectToAction("Home", "Home");
            }

            var memberId = HttpContext.Session.GetInt32("_userId");

            // Fetch member details
            var member = db.Members.Include(m => m.Person)
                .FirstOrDefault(m => m.Id == memberId);

            // Fetch the cart items for this member
            var cartItems = db.Carts
                .Where(c => c.MemberId == memberId)
                .Include(c => c.Package)
                .ThenInclude(p => p.PackagePhotos)
                .Select(c => new
                {
                    c.Package.Code,
                    c.Package.Id,
                    c.Package.Name,
                    c.Package.Price,
                    Subtotal = c.Package.Price,
                    PhotoPath = c.Package.PackagePhotos.Where(pp => pp.Type == 1).FirstOrDefault().Path // Fetch the first photo path
                })
                .ToList();

            // Fetch all the treatments for each package in the cart
            var packageIds = cartItems.Select(ci => ci.Id).ToList();

            var packageTreatments = db.PackageTreatments
                .Where(pt => packageIds.Contains(pt.PackageId))
                .Include(pt => pt.PackageTreatmentServices)
                .ThenInclude(pts => pts.Service)
                .ToList();

            // Calculate the total number of treatments
            var totalTreatments = packageTreatments.Count();

            // Fetch services included (service name and quantity)
            var serviceDetails = packageTreatments
                .SelectMany(pt => pt.PackageTreatmentServices)
                .GroupBy(pt => pt.Service.Name)
                .Select(g => new { ServiceName = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .ToList();

            // Build a string for services included (e.g., "ServiceA*3, ServiceB*1")
            var serviceString = string.Join(", ", serviceDetails.Select(s => $"{s.ServiceName}*{s.Quantity}"));

            // Calculate Discount
            var membership = db.Memberships.FirstOrDefault(m => m.Level == member.Level);
            double discountRate = (double)(membership?.Discount_rate ?? 0);
            double taxRate = 0.08; // 8% tax

            // Apply Discount and Tax to cart items
            var cartViewModel = cartItems.Select(ci => new
            {
                ci.Id,
                ci.Code,
                ci.Name,
                ci.Price,
                ci.Subtotal,
                ci.PhotoPath, // Include photo path in the view model
                DiscountedTotal = ci.Subtotal * (1 - (decimal)discountRate / 100),
                TaxAmount = ci.Subtotal * (decimal)taxRate,
                FinalTotal = ci.Subtotal * (1 - (decimal)discountRate / 100) * (1 + (decimal)taxRate)
            }).ToList();

            // Calculate totals
            var totalTax = cartViewModel.Sum(ci => ci.TaxAmount);
            var grandTotal = cartViewModel.Sum(ci => ci.FinalTotal);

            // Pass data to the view
            ViewBag.DiscountRate = (discountRate * 100);
            ViewBag.TaxRate = taxRate * 100; // For percentage display
            ViewBag.TotalTax = totalTax;
            ViewBag.GrandTotal = grandTotal;

            // Pass total treatments and service details to the view
            ViewBag.TotalTreatments = totalTreatments;
            ViewBag.ServiceIncluded = serviceString;


            var hasItems = cartItems.Any();
            ViewBag.HasItems = hasItems;

            return View("Member/Cart", cartViewModel);
        }
        catch (Exception ex)
        {
            return RedirectToAction("Login");
        }
    }

    [HttpPost]
    public IActionResult DeleteCart(int packageId)
    {
        var memberId = HttpContext.Session.GetInt32("_userId");

        var cartItem = db.Carts.FirstOrDefault(c => c.MemberId == memberId && c.PackageId == packageId);

        if (cartItem == null)
        {
            return Json(new { success = false, message = "Cart item not found." });
        }

        db.Carts.Remove(cartItem);
        db.SaveChanges();

        return Json(new { success = true, message = "Item removed from cart." });
    }

    [HttpPost]
    public IActionResult DeleteSelectedCartItems([FromBody] List<int> packageIds)
    {
        var memberId = HttpContext.Session.GetInt32("_userId");

        if (packageIds == null || !packageIds.Any())
        {
            return Json(new { success = false, message = "No items selected for deletion." });
        }

        var cartItems = db.Carts
            .Where(c => c.MemberId == memberId && packageIds.Contains(c.PackageId))
            .ToList();

        if (!cartItems.Any())
        {
            return Json(new { success = false, message = "No matching cart items found." });
        }

        db.Carts.RemoveRange(cartItems);
        db.SaveChanges();

        return Json(new { success = true, message = "Selected items removed from cart." });
    }

    [HttpPost]
    public IActionResult Checkout(List<int> selectedItems)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        if (selectedItems == null || !selectedItems.Any())
        {
            // Debugging to check the selected items
            return RedirectToAction("Cart");
        }

        HttpContext.Session.SetString("SelectedItems", JsonConvert.SerializeObject(selectedItems));

        var memberId = HttpContext.Session.GetInt32("_userId");

        //Fetch Member information, including  membership level
        var member = db.Members.Where(m => m.Id == memberId).Select(m => new
        {
            m.Level,
            Membership = db.Memberships.FirstOrDefault(ms => ms.Level == m.Level)
        }).FirstOrDefault();


        if (member == null)
        {
            return RedirectToAction("Login"); // Redirect if no member is found (not logged in)
        }

        // If no items are selected, redirect to an empty cart page
        if (selectedItems == null || !selectedItems.Any())
        {
            return RedirectToAction("Cart");
        }

        var cartItems = db.Carts
                            .Include(c => c.Package)
                                .ThenInclude(c => c.PackagePhotos)
                            .Where(c => c.MemberId == memberId && selectedItems.Contains(c.PackageId))
                            .ToList();

        if (!cartItems.Any())
        {
            return RedirectToAction("Cart"); // Redirect to an empty cart view if no items
        }

        // Calculate total and apply discount if member has a valid membership
        var totalAmount = cartItems.Sum(ci => ci.Package.Price);
        decimal discountRate = member.Membership?.Discount_rate ?? 0;
        var discountedAmount = totalAmount * (1 - discountRate);

        // Calculate tax
        decimal taxRate = 0.08m; // 8% tax
        decimal taxAmount = discountedAmount * taxRate;
      
        // Pass the data to the view
        ViewBag.TotalAmount = totalAmount;
        ViewBag.DiscountRate = discountRate;
        ViewBag.DiscountedAmount = discountedAmount;
        ViewBag.TaxAmount = taxAmount;
        ViewBag.FinalTotal = discountedAmount + taxAmount;
        return View("Member/Checkout", cartItems);
    }

    [HttpPost]
    public IActionResult PlaceOrder(int payment_method)
    {
        try
        {
            var memberId = HttpContext.Session.GetInt32("_userId");
            var member = db.Members.Where(m => m.Id == memberId)
                                .Include(m => m.Person)
                                .FirstOrDefault();

            var selectedItems = JsonConvert.DeserializeObject<List<int>>(HttpContext.Session.GetString("SelectedItems"));

            // Load cart items
            var cartItems = db.Carts
                .Where(c => c.MemberId == memberId && selectedItems.Contains(c.PackageId))
                .Include(c => c.Package)
                .ThenInclude(p => p.PackageTreatments)
                .ThenInclude(pt => pt.PackageTreatmentServices)
                .ThenInclude(s => s.Service)
                .ToList();

            if (!cartItems.Any())
            {
                TempData["Error_Title"] = "Cart is empty";
                TempData["Alert_Error"] = "Please add items to the cart before proceeding.";
                return RedirectToAction("Cart");
            }

            // Calculate subtotal
            var subtotal = cartItems.Sum(c => c.Package.Price);
            var membership = db.Memberships.FirstOrDefault(m => m.Id == memberId);
            var discount = subtotal * (membership?.Discount_rate ?? 0);
            var totalAmount = subtotal - discount;

            // Tax calculation
            const decimal taxRate = 0.08m; // Example: 6% tax
            var taxAmount = totalAmount * taxRate;
            var finalAmount = totalAmount + taxAmount;
            // Create order
            var newOrder = new Order
            {
                Tax = subtotal * 0.06m,
                Subtotal = subtotal,
                Discount = discount,
                Amount = finalAmount,
                Payment_date = DateTime.Now,
                Payment_method = payment_method,
                MemberId = (int)memberId
            };

            db.Orders.Add(newOrder);
            db.SaveChanges();

            // Add OrderPackages and related entries
            foreach (var cartItem in cartItems)
            {
                var orderPackage = new OrderPackage
                {
                    OrderId = newOrder.Id, // Ensure the OrderId is set
                    PackageId = cartItem.Package.Id,
                    Expired_datetime = DateTime.Now.AddMonths(cartItem.Package.Validity),
                    Price = cartItem.Package.Price,
                    Remaining_treatment = cartItem.Package.Treatment
                };
                db.OrderPackages.Add(orderPackage);
                db.SaveChanges();



                foreach (var treatment in cartItem.Package.PackageTreatments)
                {
                    var orderTreatment = new OrderPackageTreatment
                    {
                        OrderPackageId = orderPackage.Id,
                        // Appointment booking looks up a purchased treatment by its sequence number.
                        // Copy it from the package treatment instead of leaving the int default (0).
                        Treatment = treatment.Treatment,
                        Duration = treatment.Duration
                    };
                    db.OrderPackageTreatments.Add(orderTreatment);
                    db.SaveChanges();


                    foreach (var service in treatment.PackageTreatmentServices)
                    {
                        db.OrderPackageTreatmentServices.Add(new OrderPackageTreatmentService
                        {

                            OrderPackageTreatmentId = orderTreatment.Id,
                            Cost = service.Service.Cost,
                            Price = service.Service.Price,
                            Commission = service.Service.Commission,
                            Duration = service.Service.Duration,
                            ServiceId = service.ServiceId,
                        });
                        db.SaveChanges();
                    }
                }

            }



            // Handle payment methods (Stripe, Cash, Meow Wallet)
            switch (payment_method)
            {
                case 2: // Stripe
                    var domain = "https://localhost:7124/";
                    var options = new SessionCreateOptions
                    {
                        PaymentMethodTypes = new List<string> { "card" },
                        LineItems = cartItems.Select(ci => new SessionLineItemOptions
                        {
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                UnitAmount = (long)(finalAmount * 100), // Convert to cents
                                Currency = "myr",
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = ci.Package.Name
                                }
                            },
                            Quantity = 1
                        }).ToList(),
                        Mode = "payment",
                        SuccessUrl = domain + "Package/OrderSuccess?orderId=" + newOrder.Id,
                        CancelUrl = domain + "Package/Member/Checkout"
                    };
                    var service = new SessionService();
                    var session = service.Create(options);

                    db.Inboxes.Add(new Inbox
                    {
                        Title = "Order Confirmation",
                        Message = $"Your order #{newOrder.Id} has been successfully placed. Total Amount: {finalAmount:C}.",
                        Target = 1,
                        Type = 2, // Define the type for order-related messages
                        Created_datetime = DateTime.Now,
                        Status = 1, 
                        MemberId = memberId,
                        OrderId = newOrder.Id
                    });
                    var InboxOrderConfirmation = new Inbox
                    {
                        Title = "Order Confirmation",
                        Message = $"Your order #{newOrder.Id} has been successfully placed. Total Amount: {finalAmount:C}.",
                        Target = 1,
                        Type = 2, // Define the type for order-related messages
                        Status = 1, // Assuming 0 means unread
                        MemberId = memberId,
                        OrderId = newOrder.Id
                    };

                    // Send email notification using the Helper method
                    string emailBody = $"Thank you for your order #{newOrder.Id}. Your total is {finalAmount:C}.";
                    hp.sendEmail(member.Person.Email, InboxOrderConfirmation.Title, emailBody);

                    db.SaveChanges();
                    // Remove selected items from cart
                    db.Carts.RemoveRange(cartItems);
                    db.SaveChanges();

                    TempData["Success_Title"] = "Processing Payment";
                    TempData["Alert_Success"] = "Your payment is being processed through Stripe.";
                    return Redirect(session.Url);

                case 1: // Cash
                    bool isStaffOrder = true; // Replace with actual check
                    if (!isStaffOrder)
                    {
                        TempData["Error_Title"] = "Cash Payment Error";
                        TempData["Alert_Error"] = "Cash payment is only available for staff members.";
                        return RedirectToAction("Cart");
                    }
                    break;

                case 3: // Meow Wallet


                    if (member == null)
                    {
                        ModelState.AddModelError("", "Member not found.");
                        return View("Member/OrderFail", ModelState);
                    }

                    if (member.Credit >= totalAmount)
                    {
                        member.Credit -= totalAmount;
                        newOrder.Credit_before = member.Credit + totalAmount;
                        newOrder.Credit_after = member.Credit;
                        if (member.Total_expenses == 0 && member.Referred_person_id != null)
                        {
                            var referrer = db.Members.FirstOrDefault(m => m.Id.ToString() == member.Referred_person_id);

                            if (referrer != null)
                            {
                                referrer.Referral_purchase++;

                                var rewardTiers = db.ReferralRewards
                                                    .Where(r => r.Status == 1)
                                                    .OrderBy(r => r.Num_of_referral)
                                                    .ToList();

                                var applicableReward = rewardTiers
                                                .Where(r => referrer.Referral_purchase == r.Num_of_referral)
                                                .OrderByDescending(r => r.Num_of_referral)
                                                .FirstOrDefault();
                                if (applicableReward != null)
                                {
                                    referrer.Credit += applicableReward.Free_credit;

                                    // Notify the referrer
                                    db.Inboxes.Add(new Inbox
                                    {
                                        Title = "Referral Reward Earned",
                                        Message = $"Congratulations! You have earned {applicableReward.Free_credit:C} for reaching {applicableReward.Num_of_referral} referrals.",
                                        Target = 1,
                                        Type = 2, // Define the type for referral-related messages
                                        Created_datetime = DateTime.Now,
                                        Status = 1 
                                    });

                                    // Update the database
                                    db.SaveChanges();
                                }
                            }
                        }

                        db.Inboxes.Add(new Inbox
                        {
                            Title = "Order Confirmation",
                            Message = $"Your order #{newOrder.Id} has been successfully placed. Total Amount: {finalAmount:C}.",
                            Target = 1,
                            Type = 2, // Define the type for order-related messages
                            Created_datetime = DateTime.Now,
                            Status = 1,
                            MemberId = memberId,
                            OrderId = newOrder.Id
                        });
                        db.SaveChanges();

                        var InboxOrderConfirmation1 = new Inbox
                        {
                            Title = "Order Confirmation",
                            Message = $"Your order #{newOrder.Id} has been successfully placed. Total Amount: {finalAmount:C}.",
                            Target = 1,
                            Type = 2, // Define the type for order-related messages
                            Status = 1, // Assuming 0 means unread
                            MemberId = memberId,
                            OrderId = newOrder.Id
                        };

                        // Send email notification using the Helper method
                        string emailBody1 = $"Thank you for your order #{newOrder.Id}. Your total is {finalAmount:C}.";
                        hp.sendEmail(member.Person.Email, InboxOrderConfirmation1.Title, emailBody1);

                        TempData["Success_Title"] = "Order Successful";
                        TempData["Alert_Success"] = "Your order has been successfully placed using Meow Wallet.";

                        // Remove selected items from cart
                        db.Carts.RemoveRange(cartItems);
                        db.SaveChanges();
                        // Return success view
                        return RedirectToAction("OrderSuccess", new { orderId = newOrder.Id });
                    }
                    else
                    {
                        TempData["Error_Title"] = "Insufficient Balance";
                        TempData["Alert_Error"] = "You do not have enough balance in your Meow Wallet.";
                        return RedirectToAction("Cart");
                    }

                default:
                    TempData["Error_Title"] = "Invalid Payment Method";
                    TempData["Alert_Error"] = "Please select a valid payment method.";
                    return RedirectToAction("Cart");
            }
            // Ensure all paths return a value
            return View("Member/Checkout");
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("Login/Login"); // Redirect unauthorized users to login
        }
        catch (Exception ex)
        {
            TempData["Error_Title"] = "Order Failed";
            TempData["Alert_Error"] = $"An error occurred while placing the order: {ex.Message}";
            return RedirectToAction("Cart");
        }
    }

    [HttpGet]
    public IActionResult OrderSuccess(int orderId)
    {
        var order = db.Orders
            .Where(o => o.Id == orderId)
            .Select(o => new
            {
                Order = o,
                Packages = db.OrderPackages
                    .Where(op => op.OrderId == o.Id)
                    .Select(op => new
                    {
                        Package = op,
                        Treatments = db.OrderPackageTreatments
                            .Where(opt => opt.OrderPackageId == op.Id)
                            .Select(opt => new
                            {
                                Treatment = opt,
                                Services = db.OrderPackageTreatmentServices
                                    .Where(opts => opts.OrderPackageTreatmentId == opt.Id)
                                    .Select(opts => new
                                    {
                                        Service = opts,
                                        ServiceDetails = db.Services.FirstOrDefault(s => s.Id == opts.ServiceId)
                                    })
                                    .ToList()
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .FirstOrDefault();

        if (order == null)
        {
            return RedirectToAction("Cart");
        }

        return View("Member/OrderSuccess", order);
    }

    [HttpPost]
    public IActionResult GetPackageDetail(int packageId)
    {
        var packageDetail = db.Packages
                                .AsNoTracking()
                              .Include(p => p.PackageTreatments)
                                  .ThenInclude(pt => pt.PackageTreatmentServices)
                                      .ThenInclude(pts => pts.Service)
                                          .ThenInclude(s => s.ServicePhotos)
                              .FirstOrDefault(p => p.Id == packageId);

        if (packageDetail == null)
        {
            return Json(null); // No package found
        }

        return Json(packageDetail);
    }



    // ===============
    // Staff Site
    // ===============

    // staff package list
    [Route("Package/Staff/List")]
    public IActionResult StaffList(string? package_name, string? service_categories, int? treatment_num, int? package_price, string? sort, string? dir, int page_items = 7, int page = 1)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // search package name
        ViewBag.Package_name = package_name = package_name?.Trim() ?? "";

        var searched = db.Packages
                            .Where(p => p.Name.Contains(package_name))
                            .Include(p => p.PackageTreatments)
                                .ThenInclude(pt => pt.PackageTreatmentServices)
                                .ThenInclude(pts => pts.Service);
        //.ToList();

        var baseQuery = db.Packages.AsQueryable();
        baseQuery = baseQuery.Where(p => p.Name.Contains(package_name));

        // filter service category
        int[] selected_categories = [];

        if (!string.IsNullOrEmpty(service_categories))
        {
            selected_categories = System.Text.Json.JsonSerializer.Deserialize<int[]>(service_categories);
        }

        ViewBag.Selected_categories = selected_categories;
        var filtered_services = searched;

        if (selected_categories.Any())
        {
            baseQuery = baseQuery.Where(p =>
                selected_categories.All(catId =>
                    p.PackageTreatments.Any(pt =>
                        pt.PackageTreatmentServices.Any(pts =>
                            pts.Service.CategoryId == catId
                        )
                    )
                )
            );
        }

        // filter num of treatment in each package
        var max_package_treatment = db.Packages.Any()
                                ? db.Packages
                                    .Select(p => p.PackageTreatments.Count())
                                    .Max()
                                : 0; // If no packages, return 0

        treatment_num = treatment_num ?? max_package_treatment;
        ViewBag.Treatment_num = treatment_num;

        baseQuery = baseQuery.Where(p => p.PackageTreatments.Count() <= treatment_num);

        // filter package price range
        var max_package_price = db.Packages.Any()
                            ? db.Packages.Select(p => p.Price).Max()
                            : 0; // If no packages, return 0


        package_price = package_price ?? Convert.ToInt32(max_package_price);

        ViewBag.Package_price = package_price;

        baseQuery = baseQuery.Where(p => p.Price <= package_price);

        // get filtered packages
        var filtered_packages = baseQuery
                                    .Include(p => p.PackageTreatments)
                                        .ThenInclude(pt => pt.PackageTreatmentServices)
                                        .ThenInclude(pts => pts.Service)
                                    .ToList();

        // sorting
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<Package, object> fn = sort switch
        {
            "ID" => o => o.Id,
            "Package Name" => o => o.Name,
            "Number of Treatment" => o => o.PackageTreatments.Count(),
            "Price (RM)" => o => o.Price,
            "Validity (months)" => o => o.Validity,
            "Status" => o => o.Status,
            _ => o => o.Id
        };

        var sorted = dir == "desc" ? filtered_packages.OrderByDescending(fn) : filtered_packages.OrderBy(fn);

        // paging
        if (page < 1)
        {
            return RedirectToAction(null, new { package_name, service_categories, treatment_num, package_price, sort, dir, page_items, page = 1 });
        }

        // filter items per page
        ViewBag.Page_items = page_items;

        var m = sorted.ToPagedList(page, page_items);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { package_name, service_categories, treatment_num, package_price, sort, dir, page_items, page = m.PageCount });
        }

        // check whether it is ajax
        if (Request.IsAjax())
        {
            return PartialView("Staff/_PackageList", m);
        }

        // get service categories
        var categories = db.Categories
                            .ToList();

        // check whether service category is null
        if (categories == null)
        {
            ViewBag.Service_categories = new List<SelectListItem>();
        }
        else
        {
            var serviceCategories = categories
                .Select(category => new SelectListItem
                {
                    Value = category.Id.ToString(),
                    Text = category.Name
                })
                .ToList();

            //var service_cat_selectlist = new List<SelectListItem>
            //{
            //    new SelectListItem { Value = "", Text = "-- Please select a category --", Disabled = true, Selected = true }
            //};

            //service_cat_selectlist.AddRange(serviceCategories);

            //ViewBag.Service_cat_selectlist = service_cat_selectlist;
            ViewBag.Service_cat_selectlist = serviceCategories;
        }

        // clear temp folder
        string clearPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "package", "temp");

        if (Directory.Exists(clearPath))
        {
            string[] files = Directory.GetFiles(clearPath);

            foreach (string file in files)
            {
                var filename = Path.GetFileName(file);
                hp.DeletePhoto(filename, "images/package/temp");
            }
        }

        return View("Staff/List", m);
    }

    // staff package create
    [Route("Package/Staff/Create")]
    public IActionResult Create()
    {
        // use if-else statement to check user role
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("StaffList");
        }

        // Get the Preview Id for the new package
        int latest_id = db.Packages.Any() ? db.Packages.Max(p => p.Id) : 0;
        ViewBag.New_id = latest_id + 1;

        // get the select list of services
        var services = db.Services
                            .Where(s => s.Status == 1)
                            .ToList();
        ViewBag.Services = services;

        var services_list = services
            .Select(service => new SelectListItem
            {
                Value = service.Id.ToString(),
                Text = $"{service.Name} - {service.Duration} hour(s)"
            })
            .ToList();

        //var service_select_list = new List<SelectListItem>
        //{
        //    new SelectListItem { Value = "", Text = "-- Please select a service --", Disabled = true, Selected = true }
        //};

        //service_select_list.AddRange(services_list);
        ViewBag.Service_select_list = services_list;

        // check whether form input is prepared
        IFormFile cover_image_file = null;
        List<IFormFile> normal_image_file = new List<IFormFile>();

        try
        {
            if (HttpContext.Session.GetString("cover_image") != null)
            {
                cover_image_file = new FileStreamWrapper("wwwroot/images/package/temp/" + HttpContext.Session.GetString("cover_image"));
            }

            if (HttpContext.Session.GetString("normal_images") != null)
            {
                var imageNameArr = System.Text.Json.JsonSerializer.Deserialize<List<string>>(HttpContext.Session.GetString("normal_images"));

                foreach (var image_name in imageNameArr)
                {
                    normal_image_file.Add(new FileStreamWrapper("wwwroot/images/package/temp/" + image_name));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            // dispose cover image files
            if (cover_image_file is FileStreamWrapper coverWrapper)
            {
                coverWrapper.Dispose();
            }

            // dispose normal image files
            foreach (var normalFile in normal_image_file)
            {
                if (normalFile is FileStreamWrapper normalWrapper)
                {
                    normalWrapper.Dispose();
                }
            }
        }

        var vm = new PackageVM
        {
            Name = HttpContext.Session.GetString("package_name") ?? "",
            Description = HttpContext.Session.GetString("package_desc") ?? "",
            Treatment = HttpContext.Session.GetInt32("package_treatment") ?? 0,
            Price = decimal.TryParse(HttpContext.Session.GetString("package_price"), out decimal result) ? result : 0m,
            Validity = HttpContext.Session.GetInt32("package_validity") ?? 0,
            ServiceIds = HttpContext.Session.GetString("package_services") ?? "",
            Cover_image = cover_image_file ?? null,
            Normal_image = normal_image_file.ToArray() ?? null
        };

        return View("Staff/Create", vm);
    }

    [Route("Package/Staff/CreatePackage1")]
    [HttpPost]
    public IActionResult CreatePackage1(PackageVM vm)
    {
        // Authorizes role of user
        // use if-else statement to check user role
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("StaffList");
        }

        // validate input data
        if (ModelState.IsValid)
        {
            // store form input into session
            HttpContext.Session.SetString("package_name", vm.Name);
            HttpContext.Session.SetString("package_desc", vm.Description);
            HttpContext.Session.SetString("package_price", vm.Price.ToString());
            HttpContext.Session.SetInt32("package_validity", vm.Validity);
            HttpContext.Session.SetString("package_services", vm.ServiceIds);

            var cover_image = "";
            List<string> normal_images = new List<string>();

            // check whether cover image is uploaded
            if (vm.Cover_image != null)
            {
                var tempFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/package/temp");

                // Check if the folder exists; if not, create it
                if (!Directory.Exists(tempFolderPath))
                {
                    Directory.CreateDirectory(tempFolderPath);
                }

                cover_image = hp.SavePhoto(vm.Cover_image, "images/package/temp").Result;

                // set session for cover image name
                HttpContext.Session.SetString("cover_image", cover_image);
            }

            // check whether normal images are uploaded
            if (vm.Normal_image != null)
            {
                var tempFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/package/temp");

                // Check if the folder exists; if not, create it
                if (!Directory.Exists(tempFolderPath))
                {
                    Directory.CreateDirectory(tempFolderPath);
                }

                foreach (var image in vm.Normal_image)
                {
                    var image_name = hp.SavePhoto(image, "images/package/temp").Result;
                    normal_images.Add(image_name);
                }

                // set session for normal images' name
                HttpContext.Session.SetString("normal_images", System.Text.Json.JsonSerializer.Serialize(normal_images));
            }

            // store treatment number
            ViewBag.Treatment = vm.Treatment;
            HttpContext.Session.SetInt32("package_treatment", vm.Treatment);

            // validate service sequence
            var selected_services_id = vm.ServiceIds
                                        .Split(',')
                                        .Select(int.Parse)
                                        .ToArray();

            List<Service> services_sequence = new List<Service>();

            if (selected_services_id.Length == 0)
            {
                ModelState.AddModelError("ServiceIds", "Invalid services selected.");
            }

            var isValidService = false;

            foreach (var service_id in selected_services_id)
            {
                var service = db.Services
                                    .Where(s => s.Id == service_id && s.Status == 1)
                                    .FirstOrDefault();

                if (service == null)
                {
                    isValidService = false;
                    ModelState.AddModelError("ServiceIds", "Invlid services selected.");
                    break;
                }
                else
                {
                    isValidService = true;
                    services_sequence.Add(service);
                }
            }

            if (!isValidService)
            {
                TempData["Error_Title"] = "Package Create Failed";
                TempData["Alert_Error"] = "Package treatment services are invalid";

                return Redirect(Request.Headers.Referer.ToString());
            }

            // store selected services' default sequence
            ViewBag.Selected_services = services_sequence;

            // prepare service selectl list option
            var services_list = db.Services
                                    .Where(s => s.Status == 1)
                                    .Select(service => new SelectListItem
                                    {
                                        Value = service.Id.ToString(),
                                        Text = $"{service.Name} - {service.Duration} hour(s)",
                                    })
                                    .ToList();

            ViewBag.Service_select_list = services_list;

            return View("Staff/ArrangeServices", services_sequence);
        }

        return Redirect(Request.Headers.Referer.ToString());
    }

    [Route("Package/Staff/CreatePackage2")]
    [HttpPost]
    public IActionResult CreatePackage2(string treatmentSequence)
    {

        // use if-else statement to check user role
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("StaffList");
        }

        var name = HttpContext.Session.GetString("package_name")?.Trim() ?? null;
        var desc = HttpContext.Session.GetString("package_desc")?.Trim() ?? null;
        var treatment = HttpContext.Session.GetInt32("package_treatment") ?? 0;
        var price = decimal.TryParse(HttpContext.Session.GetString("package_price"), out decimal result) ? result : 0m;
        var validity = HttpContext.Session.GetInt32("package_validity") ?? 0;
        var servicesJSON = HttpContext.Session.GetString("package_services") ?? null;
        var services = new int[0];
        var cover_image = HttpContext.Session.GetString("cover_image")?.Trim() ?? null;
        var normal_images_JSON = HttpContext.Session.GetString("normal_images")?.Trim() ?? null;
        var normal_images = new string[0];

        // validate name
        if (string.IsNullOrEmpty(name))
        {
            return Json(new
            {
                result = false,
                error_title = "Package Create Failed",
                error_msg = "Name should not be empty."
            });
        }
        else if (name.Length > 30)
        {
            return Json(new
            {
                result = false,
                error_title = "Package Create Failed",
                error_msg = "Name should not more than 50 characters."
            });
        }

        // validate desc
        if (string.IsNullOrEmpty(desc))
        {
            return Json(new
            {
                result = false,
                error_title = "Package Create Failed",
                error_msg = "Description should not be empty."
            });
        }
        else if (desc.Length > 300)
        {
            return Json(new
            {
                result = false,
                error_title = "Package Create Failed",
                error_msg = "Description should not more than 300 characters."
            });
        }

        // validate treatment
        if (treatment < 1 || treatment > 30)
        {
            return Json(new
            {
                result = false,
                error_title = "Package Create Failed",
                error_msg = "Treatment must between 1 and 30 only."
            });
        }

        // validate price
        if (price < 0.01m || price > 999999.99m)
        {
            return Json(new
            {
                result = false,
                error_title = "Package Create Failed",
                error_msg = "Price must between 0.01 and 999999.99 only."
            });
        }

        // validate validity
        if (validity < 1 || validity > 36)
        {
            return Json(new
            {
                result = false,
                error_title = "Package Create Failed",
                error_msg = "Validity must between 1 and 36 only."
            });
        }

        // validate services
        if (servicesJSON != null)
        {
            // convert json to array
            try
            {
                services = servicesJSON
                        .Split(',')
                        .Select(int.Parse)
                        .ToArray();
            }
            catch (Exception e)
            {
                return Json(new
                {
                    result = false,
                    error_title = "Package Create Failed",
                    error_msg = "Invalid services."
                });
            }

            // check whether all services are valid
            foreach (var service_id in services)
            {
                var service = db.Services
                                    .Where(s => s.Id == service_id && s.Status == 1)
                                    .FirstOrDefault();

                if (service == null)
                {
                    return Json(new
                    {
                        result = false,
                        error_title = "Package Create Failed",
                        error_msg = "Invalid service."
                    });
                }
            }
        }

        // validate treatment services sequence
        var treatment_services = System.Text.Json.JsonSerializer.Deserialize<int[][]>(treatmentSequence);

        if (treatment_services == null || treatment_services.Length == 0)
        {
            return Json(new
            {
                result = false,
                error_title = "Package Create Failed",
                error_msg = "Invalid treatment services."
            });
        }

        foreach (var ts in treatment_services)
        {
            var treatmentDuration = 0.00;

            // check whether each treatment has at least one service
            if (ts.Length == 0)
            {
                return Json(new
                {
                    result = false,
                    error_title = "Package Create Failed",
                    error_msg = "Invalid treatment services."
                });
            }

            foreach (var service_id in ts)
            {
                // validate service
                var service = db.Services
                                    .Where(s => s.Id == service_id && s.Status == 1)
                                    .FirstOrDefault();

                if (service == null)
                {
                    return Json(new
                    {
                        result = false,
                        error_title = "Package Create Failed",
                        error_msg = "Invalid service."
                    });
                }

                // add duration of service
                treatmentDuration += service.Duration;
            }

            // check if there are redundant services in each treatment
            var duplicates = ts
                                .GroupBy(id => id)
                                .Where(group => group.Count() > 1)
                                .Select(group => group.Key)
                                .ToList();

            if (duplicates.Any())
            {
                return Json(new
                {
                    result = false,
                    error_title = "Package Create Failed",
                    error_msg = "Duplicated services."
                });
            }

            // validate each treatment duration
            if (treatmentDuration > 6)
            {
                return Json(new
                {
                    result = false,
                    error_title = "Package Create Failed",
                    error_msg = "Each treatment duration must not exceed 6 hours."
                });
            }
        }

        // validate cover image
        if (!string.IsNullOrEmpty(cover_image))
        {
            string tempFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "package", "temp");
            string coverFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "package", "cover");

            string sourceFilePath = Path.Combine(tempFolderPath, cover_image);
            string targetFilePath = Path.Combine(coverFolderPath, cover_image);

            // check whether the file is exist
            if (System.IO.File.Exists(sourceFilePath))
            {
                // check whether folder is exist
                if (!Directory.Exists(coverFolderPath))
                {
                    Directory.CreateDirectory(coverFolderPath);
                }

                // move file to package cover image folder
                System.IO.File.Move(sourceFilePath, targetFilePath);
            }
            else
            {
                return Json(new
                {
                    result = false,
                    error_title = "Package Create Failed",
                    error_msg = $"Image '{cover_image}' does not exist."
                });
            }
        }

        // validate normal images
        if (!string.IsNullOrEmpty(normal_images_JSON))
        {
            normal_images = System.Text.Json.JsonSerializer.Deserialize<string[]>(normal_images_JSON);

            if (normal_images == null || normal_images.Length == 0)
            {
                return Json(new
                {
                    result = false,
                    error_title = "Package Create Failed",
                    error_msg = "Invalid normal images."
                });
            }

            // Define source and target folders
            string tempFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "package", "temp");
            string normalFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "package", "normal");

            // check whether folder is exist
            if (!Directory.Exists(normalFolderPath))
            {
                Directory.CreateDirectory(normalFolderPath);
            }

            foreach (var imageName in normal_images)
            {
                string sourceFilePath = Path.Combine(tempFolderPath, imageName);
                string targetFilePath = Path.Combine(normalFolderPath, imageName);

                // check whether the file is exist
                if (System.IO.File.Exists(sourceFilePath))
                {
                    // move file to package normal image folder
                    System.IO.File.Move(sourceFilePath, targetFilePath);
                }
                else
                {
                    return Json(new
                    {
                        result = false,
                        error_title = "Package Create Failed",
                        error_msg = $"Image '{imageName}' does not exist."
                    });
                }
            }
        }

        // insert Package
        var newPackage = new Package
        {
            Name = name,
            Description = desc,
            Treatment = treatment,
            Price = price,
            Status = 1,
            Validity = validity
        };

        db.Packages.Add(newPackage);
        db.SaveChanges();

        var packageId = newPackage.Id;

        // insert Package Cover Photo
        if (!string.IsNullOrEmpty(cover_image))
        {
            var newCoverPhoto = new PackagePhoto
            {
                Path = cover_image,
                Type = 1,
                PackageId = packageId,
            };

            db.PackagePhotos.Add(newCoverPhoto);
        }

        // insert Package Normal Photo
        if (normal_images.Length > 0)
        {
            foreach (var img_name in normal_images)
            {
                var newNormalPhoto = new PackagePhoto
                {
                    Path = img_name,
                    Type = 2,
                    PackageId = packageId,
                };

                db.PackagePhotos.Add(newNormalPhoto);
            }
        }

        // insert PackageTreatment
        for (int i = 0; i < treatment_services.Length; i++)
        {
            var ts = treatment_services[i];
            var treatmentDuration = 0.00;

            foreach (var service_id in ts)
            {
                treatmentDuration += db.Services
                                            .Where(s => s.Id == service_id)
                                            .Select(s => s.Duration)
                                            .FirstOrDefault();
            }

            var newPackageTreatment = new PackageTreatment
            {
                Treatment = (i + 1),
                Duration = treatmentDuration,
                PackageId = packageId,
            };

            db.PackageTreatments.Add(newPackageTreatment);
            db.SaveChanges();

            var packageTreatmentId = newPackageTreatment.Id;

            // insert PackageTreatmentService
            for (int j = 0; j < ts.Length; j++)
            {
                var newPackageTreatmentService = new PackageTreatmentService
                {
                    PackageTreatmentId = packageTreatmentId,
                    ServiceId = ts[j],
                    Quantity = 1,
                    Priority = (j + 1),
                    Status = 1,
                };

                db.PackageTreatmentServices.Add(newPackageTreatmentService);
            }
        }
        db.SaveChanges();

        // clear session
        HttpContext.Session.Remove("package_name");
        HttpContext.Session.Remove("package_desc");
        HttpContext.Session.Remove("package_treatment");
        HttpContext.Session.Remove("package_price");
        HttpContext.Session.Remove("package_validity");
        HttpContext.Session.Remove("package_services");
        HttpContext.Session.Remove("cover_image");
        HttpContext.Session.Remove("normal_images");

        // clear temp folder
        string clearPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "package", "temp");

        if (Directory.Exists(clearPath))
        {
            string[] files = Directory.GetFiles(clearPath);

            foreach (string file in files)
            {
                System.IO.File.Delete(file);
            }
        }

        return Json(new
        {
            result = true,
            success_title = "Package Create Success",
            success_msg = "Package has been created successfully.",
            package_id = packageId,
        });
    }

    [Route("Package/Staff/Detail/{id}")]
    public IActionResult StaffDetail(int id)
    {
        // use if-else statement to check user role
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var model = db.Packages
                        .Where(p => p.Id == id)
                        .Include(p => p.PackagePhotos)
                        .Include(p => p.PackageTreatments)
                            .ThenInclude(pt => pt.PackageTreatmentServices)
                                .ThenInclude(pts => pts.Service)
                        .FirstOrDefault();

        if (model == null)
        {
            TempData["Error_Title"] = "Package Not Found";
            TempData["Alert_Error"] = "Invalid Package ID";

            return RedirectToAction("StaffList");
        }

        return View("Staff/Detail", model);
    }

    [Route("Package/Staff/ExitFromCreation")]
    [HttpGet]
    public IActionResult ExitFromCreation()
    {
        // clear session
        HttpContext.Session.Remove("package_name");
        HttpContext.Session.Remove("package_desc");
        HttpContext.Session.Remove("package_treatment");
        HttpContext.Session.Remove("package_price");
        HttpContext.Session.Remove("package_validity");
        HttpContext.Session.Remove("package_services");
        HttpContext.Session.Remove("cover_image");
        HttpContext.Session.Remove("normal_images");

        // clear temp folder
        string clearPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "package", "temp");

        if (Directory.Exists(clearPath))
        {
            string[] files = Directory.GetFiles(clearPath);

            foreach (string file in files)
            {
                var filename = Path.GetFileName(file);
                hp.DeletePhoto(filename, "images/package/temp");
            }
        }

        // back to list
        return RedirectToAction("StaffList");
    }

    // staff activate package
    [Route("Package/Staff/Activate/{id}")]
    public IActionResult Activate(int id)
    {
        // Authorizes role of user
        // use if-else statement to check user role
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("StaffList");
        }

        string[] msg = [];

        var p = db.Packages
                .Where(p => p.Id == id)
                .Include(p => p.PackageTreatments)
                    .ThenInclude(pt => pt.PackageTreatmentServices)
                    .ThenInclude(pts => pts.Service)
                .FirstOrDefault();

        if (p != null)
        {
            List<string> inactive_services = new List<string>();

            foreach (var pt in p.PackageTreatments)
            {
                foreach (var pts in pt.PackageTreatmentServices)
                {
                    if (pts.Service.Status == 0)
                    {
                        inactive_services.Add(pts.Service.Name);
                    }
                }
            }

            if (inactive_services.Count() > 0)
            {
                TempData["Error_Title"] = "Activate Failed";
                TempData["Alert_Error"] = "There are inactive services within this package. " +
                    "Please activate the services before activating the package: ";

                for (int i = 0; i < inactive_services.Count; i++)
                {
                    if (i == inactive_services.Count - 1)
                    {
                        TempData["Alert_Error"] += $"{inactive_services[i]}.";
                    }
                    else
                    {
                        TempData["Alert_Error"] += $"{inactive_services[i]}, ";
                    }
                }
                TempData["Alert_Error"] = TempData["Alert_Error"].ToString().Replace("'", "");

                msg = ["error_navigate", TempData["Error_Title"].ToString(), TempData["Alert_Error"].ToString()];
            }
            else
            {
                p.Status = 1;
                db.SaveChanges();

                TempData["Success_Title"] = "Activate Success";
                TempData["Alert_Success"] = "The package has been activated.";
                msg = ["success", TempData["Success_Title"].ToString(), TempData["Alert_Success"].ToString()];
            }

        }
        else
        {
            TempData["Error_Title"] = "Activate Failed";
            TempData["Alert_Error"] = "The package does not exist.";
            msg = ["error", TempData["Error_Title"].ToString(), TempData["Alert_Error"].ToString()];
        }

        //return Redirect(Request.Headers.Referer.ToString());
        return Json(msg);
    }

    // Staff deactivate package
    [Route("Package/Staff/Deactivate/{id}")]
    public IActionResult Deactivate(int id)
    {
        // Authorizes role of user
        // use if-else statement to check user role
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("StaffList");
        }

        var p = db.Packages
                .FirstOrDefault(p => p.Id == id);

        if (p != null)
        {
            p.Status = 0;
            db.SaveChanges();

            TempData["Success_Title"] = "Deactivate Success";
            TempData["Alert_Success"] = "The package has been deactivated.";
        }
        else
        {
            TempData["Error_Title"] = "Deactivate Failed";
            TempData["Alert_Error"] = "The package does not exist.";
        }

        return Redirect(Request.Headers.Referer.ToString());
    }
}
