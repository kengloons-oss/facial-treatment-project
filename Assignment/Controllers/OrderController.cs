using System.Net.Mail;
using System.Text;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using X.PagedList.Extensions;

namespace Assignment.Controllers;

public class OrderController : Controller
{
    private readonly DB db;
    private readonly IMapper mapper;
    private readonly Helpers hp;
    private readonly InvoiceRenderingService invoiceRenderingService;

    public OrderController(DB db, IMapper mapper, Helpers hp, InvoiceRenderingService invoiceRenderingService)
    {
        this.db = db;
        this.mapper = mapper;
        this.hp = hp;
        this.invoiceRenderingService = invoiceRenderingService;
    }

    // =======================
    // Member Site
    // =======================

    public IActionResult List(int? payment_method, string? service_categories, double? max_amt, double? min_amt, string? sort, string? dir, int page_items = 7, int page = 1)
    {
        // Check user authorization
        if (!hp.CheckRole(new string[] { "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var userId = HttpContext.Session.GetInt32("_userId");

        // payment method filter
        ViewBag.Payment_method = payment_method = payment_method ?? null;
        var filter_payment = db.Orders
                                    .Include(o => o.Member)
                                        .ThenInclude(m => m.Person)
                                    .Include(o => o.OrderPackages)
                                        .ThenInclude(op => op.OrderPackageTreatments)
                                            .ThenInclude(opt => opt.OrderPackageTreatmentServices)
                                                .ThenInclude(opts => opts.Service)
                                    .Where(o => o.Member.Id == userId)
                                    .Where(o => payment_method == null || o.Payment_method == payment_method);

        // service category filter
        int[] selected_categories = [];

        if (!string.IsNullOrEmpty(service_categories))
        {
            selected_categories = System.Text.Json.JsonSerializer.Deserialize<int[]>(service_categories);
        }

        ViewBag.Selected_categories = selected_categories;
        var filter_service_category = filter_payment;

        if (selected_categories.Any())
        {
            filter_service_category = filter_service_category
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

        // amount filter
        var filter_amount = filter_service_category;
        ViewBag.Min_amt = min_amt = min_amt ?? 0.01;
        ViewBag.Max_amt = max_amt = max_amt ?? 999999.99;

        if (min_amt > 0.00 && max_amt <= 999999.99 && min_amt < max_amt)
        {
            filter_amount = filter_service_category
                                .Where(o => o.Amount >= (decimal) min_amt && o.Amount <= (decimal) max_amt);
        }

        // sorting
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<Order, object> fn = sort switch
        {
            "ID" => o => o.Id,
            "Package Purchased" => o => o.OrderPackages.Count(),
            "Amount (RM)" => o => o.Amount,
            "Payment Method" => o => o.Payment_method,
            "Payment Data" => o => o.Payment_date,
            _ => o => o.Id
        };

        var sorted = dir == "desc" ? filter_amount.OrderByDescending(fn) : filter_amount.OrderBy(fn);

        // paging
        ViewBag.Page_items = page_items;

        var m = sorted.ToPagedList(page, page_items);

        if (page < 1)
        {
            return RedirectToAction(null, new { payment_method, service_categories, max_amt, min_amt, sort, dir, page_items, page = 1 });
        }

        // check whether it is ajax
        if (Request.IsAjax())
        {
            return PartialView("Member/_OrderList", m);
        }

        // get service category list
        var serviceCategories = db.Categories
            .Select(category => new SelectListItem
            {
                Value = category.Id.ToString(),
                Text = category.Name
            })
            .ToList();

        var service_cat_selectlist = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "All", Disabled = false, Selected = true }
            };

        service_cat_selectlist.AddRange(serviceCategories);

        //ViewBag.Service_cat_selectlist = service_cat_selectlist;
        ViewBag.Service_cat_selectlist = serviceCategories;

        return View("Member/List", m);
    }

    public IActionResult MemberDetail(int id)
    {
        // Check user authorization
        if (!hp.CheckRole(new string[] { "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var userId = HttpContext.Session.GetInt32("_userId");

        // check if the order is belongs to the user
        var m = db.Orders
                    .Include(o => o.Member)
                    .Include(o => o.OrderPackages)
                        .ThenInclude(op => op.Package)
                            .ThenInclude(p => p.PackagePhotos)
                    .Include(o => o.OrderPackages)
                        .ThenInclude(op => op.OrderPackageTreatments)
                            .ThenInclude(opt => opt.OrderPackageTreatmentServices)
                                .ThenInclude(opts => opts.Service)
                    .Where(o => o.Id == id && o.Member.Id == userId)
                    .FirstOrDefault();


        if (m == null)
        {
            TempData["Error_Title"] = "Invalid Order";
            TempData["Alert_Error"] = "Invalid order or do not have access to the order.";

            return Redirect(Request.Headers.Referer.ToString());
        }

        return View("Member/Detail", m);
    }

    // =======================
    // Admin Site
    // =======================

    [Route("Order/Staff/List")]
    public IActionResult StaffList(string? member_email, int? package_num, string? sort, string? dir, int page_items = 7, int page = 1)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // search email
        ViewBag.Member_email = member_email = member_email?.Trim() ?? "";

        var searched = db.Orders
                .Include(o => o.Member)
                .ThenInclude(m => m.Person)
                .Where(o => o.Member.Person.Email.Contains(member_email))
                .Include(o => o.OrderPackages)
                .ToList();

        // filter number of package purchased
        var max_package_num = 0;

        if (searched.Count() > 0)
        {
            max_package_num = searched
                                .Select(o => o.OrderPackages.Count())
                                .Max();
        }

        package_num = package_num ?? max_package_num;
        ViewBag.Package_num = package_num;

        var filtered_package_num = searched
                                        .Where(o => o.OrderPackages.Count() <= package_num)
                                        .ToList();

        // sorting
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<Order, object> fn = sort switch
        {
            "ID" => o => o.Id,
            "Member Email" => o => o.Member.Person.Email,
            "Package Purchased (Num)" => o => o.OrderPackages.Count(),
            "Incompleted Packages" => o => o.OrderPackages.Where(op => op.Remaining_treatment > 0).Count(),
            "Amount (RM)" => o => o.Amount,
            "Payment Date" => o => o.Payment_date,
            _ => o => o.Id
        };

        var sorted = dir == "desc" ? filtered_package_num.OrderByDescending(fn) : filtered_package_num.OrderBy(fn);

        // paging
        if (page < 1)
        {
            return RedirectToAction(null, new { member_email, package_num, sort, dir, page_items, page = 1 });
        }

        // filter items per page
        ViewBag.Page_items = page_items;

        var m = sorted.ToPagedList(page, page_items);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { member_email, package_num, sort, dir, page_items, page = m.PageCount });
        }

        // check whether it is ajax
        if (Request.IsAjax())
        {
            return PartialView("Staff/_OrderList", m);
        }

        return View("Staff/List", m);
    }

    [Route("Order/Staff/Detail/{id}")]
    public IActionResult Detail(int id)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var m = db.Orders
                    .Where(o => o.Id == id)
                    .Include(o => o.Member.Person)
                    .Include(o => o.OrderPackages)
                        .ThenInclude(op => op.Package)
                            .ThenInclude(p => p.PackagePhotos)
                    .Include(o => o.OrderPackages)
                        .ThenInclude(op => op.OrderPackageTreatments)
                            .ThenInclude(opt => opt.OrderPackageTreatmentServices)
                                .ThenInclude(opts => opts.Service)
                    .FirstOrDefault();

        if (m == null)
        {
            return RedirectToAction("StaffList");
        }

        return View("Staff/Detail", m);
    }

    [Route("Order/Staff/Create")]
    public IActionResult Create(string? member_id, string? package_name, int page_items = 7, int page = 1)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // find existing members
        var exist_members = db.Members
                        .Include(m => m.Person)
                        .Where(m => m.Person.Status == 1)
                        .ToList();

        ViewBag.Members = exist_members;

        // find selected member's discount
        ViewBag.Member_id = member_id = member_id?.Trim() ?? "";

        if (int.TryParse(member_id, out int result))
        {
            var member_level = db.Members
                                .Where(m => m.Id == int.Parse(member_id))
                                .Select(m => m.Level)
                                .FirstOrDefault();

            var member_discount = db.Memberships
                                        .Where(ms => ms.Level == member_level)
                                        .Select(ms => ms.Discount_rate)
                                        .FirstOrDefault();

            ViewBag.Member_discount = member_discount;
        }
        else
        {
            ViewBag.Member_discount = 0.00;
        }

        // find available packages
        ViewBag.Package_name = package_name = package_name?.Trim() ?? "";

        var available_packages = db.Packages
                                        .Include(p => p.PackagePhotos)
                                        .Where(p => p.Status == 1 && p.Name.Contains(package_name))
                                        .ToList();

        // paging
        if (page < 1)
        {
            return RedirectToAction(null, new { member_id, package_name, page_items, page = 1 });
        }

        // filter items per page
        ViewBag.Page_items = page_items;

        var m = available_packages.ToPagedList(page, page_items);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { member_id, package_name, page_items, page = m.PageCount });
        }

        // check whether it is ajax
        if (Request.IsAjax())
        {
            return PartialView("Staff/_PackageList", m);
        }

        return View("Staff/Create", m);
    }

    [HttpPost]
    [Route("Order/Staff/Checkout")]
    public IActionResult Checkout(int member_id, string packages)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // check whether the member exists
        var member = db.Members
                            .Where(m => m.Id == member_id)
                            .Include(m => m.Person)
                            .FirstOrDefault();

        if (member == null)
        {
            TempData["Error_Title"] = "Checkout Failed";
            TempData["Alert_Error"] = "Invalid member.";

            return Redirect(Request.Headers.Referer.ToString());
        }

        ViewBag.Member = member;

        // get member discount_rate
        var member_discount_rate = db.Memberships
                                        .Where(ms => ms.Level == member.Level)
                                        .Select(ms => ms.Discount_rate)
                                        .FirstOrDefault();

        if (member_discount_rate == null)
        {
            TempData["Error_Title"] = "Checkout Failed";
            TempData["Alert_Error"] = "Invalid member level.";

            return Redirect(Request.Headers.Referer.ToString());
        }

        ViewBag.Member_discount = member_discount_rate;

        // check whether the package list is empty
        var packagesList = System.Text.Json.JsonSerializer.Deserialize<int[]>(packages);

        if (packagesList == null || packagesList.Length == 0)
        {
            TempData["Error_Title"] = "Checkout Failed";
            TempData["Alert_Error"] = "Something went wrong.";

            return Redirect(Request.Headers.Referer.ToString());
        }

        // create packages model & calculate subtotal
        List<Package> packages_model = new List<Package>();
        var subtotal = 0.00;

        foreach (var package_id in packagesList)
        {
            var pkg = db.Packages
                            .Where(p => p.Id == package_id)
                            .Include(p => p.PackagePhotos)
                            .FirstOrDefault();

            if (pkg == null || packages_model.Contains(pkg))
            {
                TempData["Error_Title"] = "Checkout Failed";
                TempData["Alert_Error"] = "Something went wrong.";

                return Redirect(Request.Headers.Referer.ToString());
            }

            subtotal += (double)pkg.Price;
            packages_model.Add(pkg);
        };

        // calculate summary
        var tax = subtotal * 0.08;
        var discount = (subtotal + tax) * (double)member_discount_rate;
        var total_amount = (subtotal + tax) - discount;

        ViewBag.Subtotal = (Math.Round(subtotal, 2)).ToString("F2");
        ViewBag.Tax = Math.Round(tax, 2).ToString("F2");
        ViewBag.Discount = Math.Round(discount, 2).ToString("F2");
        ViewBag.TotalAmount = Math.Round(total_amount, 2).ToString("F2");

        return View("Staff/Checkout", packages_model);
    }

    [HttpPost]
    [Route("Order/Staff/Payment")]
    public string Payment(int member_id, string packages, int payment_method)
    {
        // check whether the member exists
        var member = db.Members
                            .Where(m => m.Id == member_id)
                            .Include(m => m.Person)
                            .FirstOrDefault();

        if (member == null)
        {
            return System.Text.Json.JsonSerializer.Serialize(
                new string[] {
                    "error",
                    "Member does not exist."
                }
            );
        }

        ViewBag.Member = member;

        // get member discount_rate
        var member_discount_rate = db.Memberships
                                        .Where(ms => ms.Level == member.Level)
                                        .Select(ms => ms.Discount_rate)
                                        .FirstOrDefault();

        if (member_discount_rate == null)
        {
            return System.Text.Json.JsonSerializer.Serialize(
                new string[] {
                    "error",
                    "Invalid member level."
                }
            );
        }

        // check whether the package list is empty
        var packagesList = System.Text.Json.JsonSerializer.Deserialize<int[]>(packages);

        if (packagesList == null || packagesList.Length == 0)
        {
            TempData["Error_Title"] = "Checkout Failed";
            TempData["Alert_Error"] = "Something went wrong.";

            return System.Text.Json.JsonSerializer.Serialize(
                new string[] {
                    "error",
                    "No package is selected."
                }
            );
        }

        // create packages model & calculate subtotal
        List<Package> packages_model = new List<Package>();
        var subtotal = (decimal)0.00;

        foreach (var package_id in packagesList)
        {
            var pkg = db.Packages
                            .Where(p => p.Id == package_id)
                            .Include(p => p.PackagePhotos)
                            .FirstOrDefault();

            if (pkg == null || packages_model.Contains(pkg))
            {
                return System.Text.Json.JsonSerializer.Serialize(
                    new string[] {
                        "error",
                        "Package duplicated."
                    }
                );
            }

            subtotal += pkg.Price;
            packages_model.Add(pkg);
        };

        // calculate summary
        var tax = (decimal)((double)subtotal * 0.08);
        var discount = (subtotal + tax) * member_discount_rate;
        var total_amount = (subtotal + tax) - discount;

        // check payment method
        if (payment_method != 1 && payment_method != 3)
        {
            return System.Text.Json.JsonSerializer.Serialize(
                new string[] {
                    "error",
                    "Invalid payment method."
                }
            );
        }

        var credit_before = (decimal)0.00;
        var credit_after = (decimal)0.00;

        if (payment_method == 3)
        {
            if (member.Credit < total_amount)
            {
                return System.Text.Json.JsonSerializer.Serialize(
                    new string[] {
                        "error",
                        "Insufficient credit balance."
                    }
                );
            }

            credit_before = member.Credit;
            credit_after = member.Credit - total_amount;
        }

        // ========================
        // Update member credit
        // ========================

        // Update member credit if using meow-wallet to pay
        if (payment_method == 3)
        {
            member.Credit = credit_after;
        }

        member.Total_expenses += total_amount;
        db.SaveChanges();

        // =========================================
        // Update referred person referral_purchase
        // =========================================
        if (!string.IsNullOrEmpty(member.Referred_person_id))
        {
            var order_count = db.Orders
                                    .Where(o => o.MemberId == member.Id)
                                    .Count();

            var refer_member = db.Members
                                    .Include(m => m.Person)
                                    .Where(m => m.Referral_id == member.Referred_person_id && m.Person.Status == 1)
                                    .FirstOrDefault();

            if (order_count == 0 && refer_member != null)
            {
                refer_member.Referral_purchase++;

                var referral_reward = db.ReferralRewards
                                            .Where(rr => rr.Status == 1 && rr.Num_of_referral == refer_member.Referral_purchase)
                                            .FirstOrDefault();

                if (referral_reward != null)
                {
                    refer_member.Credit += referral_reward.Free_credit;

                    // insert referral inbox
                    var referral_inbox = new Inbox
                    {
                        Title = "Referral Achievement Reward",
                        Message = $"Dear {refer_member.Person.Name},\n" +
                                    $"Congratulations, your referral person has achieved {referral_reward.Num_of_referral}!" +
                                    $" We have gifted a free RM{referral_reward.Free_credit} to your Meow Wallet. Let's check it now!",
                        Target = 1,
                        Type = 2,
                        Status = 1,
                        MemberId = refer_member.Id,
                    };

                    db.Inboxes.Add(referral_inbox);
                    db.SaveChanges();

                    // send referral email
                    var referral_email_body = $@"
                        <!DOCTYPE html>
                        <html lang='en'>
                        <head>
                            <meta charset='UTF-8'>
                            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                            <title>Referral Achievement Reward</title>
                        </head>
                        <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f4f4f4;'>
                            <div style='background-color: #fff; padding: 20px; border-radius: 8px; box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);'>
                                <h2>Congratulations, {refer_member.Person.Name}!</h2>
                                <p>You've earned a free <strong>RM {referral_reward.Free_credit}</strong> for achieving <strong>{referral_reward.Num_of_referral}</strong> successful referrals.</p>
                                <p>Check your Meow Wallet now and enjoy your reward!</p>
                                <p style='font-size: 12px; color: #777; margin-top: 20px;'>Thank you for being part of our community!</p>
                            </div>
                        </body>
                        </html>
                    ";
                    hp.sendEmail(refer_member.Person.Email, referral_inbox.Title, referral_email_body);
                }
            }
            db.SaveChanges();
        }

        // ========================
        // Create order
        // ========================

        // Create new Order
        var order = new Order
        {
            Tax = tax,
            Subtotal = subtotal,
            Discount = discount,
            Amount = total_amount,
            Payment_date = DateTime.Now,
            Payment_method = payment_method,
            Credit_before = credit_before,
            Credit_after = credit_after,
            MemberId = member.Id,
        };

        db.Orders.Add(order);
        db.SaveChanges();

        int order_id = order.Id;

        // Create new OrderPackage
        foreach (var package in packages_model)
        {
            var order_package = new OrderPackage
            {
                Price = package.Price,
                Remaining_treatment = package.Treatment,
                Expired_datetime = DateTime.Now.AddMonths(package.Validity),
                OrderId = order_id,
                PackageId = package.Id
            };

            db.OrderPackages.Add(order_package);
            db.SaveChanges();

            // Create new OrderPackageTreatments
            var order_package_id = order_package.Id;
            var package_treatments = db.PackageTreatments
                                            .Where(pt => pt.PackageId == package.Id)
                                            .ToList();

            foreach (var package_treatment in package_treatments)
            {
                var order_package_treatment = new OrderPackageTreatment
                {
                    Treatment = package_treatment.Treatment,
                    Duration = package_treatment.Duration,
                    //Status = 1,
                    OrderPackageId = order_package_id
                };

                db.OrderPackageTreatments.Add(order_package_treatment);
                db.SaveChanges();

                // Create new OrderPackageTreatmentServices
                var order_package_treatment_id = order_package_treatment.Id;
                var package_treatment_services = db.PackageTreatmentServices
                                                            .Where(pts => pts.PackageTreatmentId == package_treatment.Id)
                                                            .Include(pts => pts.Service)
                                                            .ToList();

                foreach (var package_treatment_service in package_treatment_services)
                {
                    var order_package_treatment_service = new OrderPackageTreatmentService
                    {
                        Cost = package_treatment_service.Service.Cost,
                        Price = package_treatment_service.Service.Price,
                        Commission = package_treatment_service.Service.Commission,
                        Duration = package_treatment_service.Service.Duration,
                        ServiceId = package_treatment_service.ServiceId,
                        OrderPackageTreatmentId = order_package_treatment_id
                    };

                    db.OrderPackageTreatmentServices.Add(order_package_treatment_service);
                    db.SaveChanges();
                }
            }
        }

        // ========================
        // Create inbox
        // ========================
        var inbox = new Inbox
        {
            Title = "Order " + order.Code + " has been created.",
            Message = "Thank you for purchasing our services!"
                        + " Don't forget to make appointment for enjoying our services!",
            Target = 1,
            Type = 2,
            Status = 1,
            MemberId = member_id,
            OrderId = order_id
        };

        db.Inboxes.Add(inbox);
        db.SaveChanges();

        // ========================
        // Send email
        // ========================
        var mail = new MailMessage();
        mail.To.Add(new MailAddress(member.Person.Email, member.Person.Name));
        mail.Subject = inbox.Title;
        mail.IsBodyHtml = true;

        var email_body = new StringBuilder();

        email_body.Append("<p style='font-family:Arial, sans-serif; font-size:14px; color:#333;'>Thank you for purchasing our services! Don't forget to make an appointment to enjoy our services!</p>" +
            "<br /><br />" +
            "<table style='width:100%; border-collapse:collapse; font-family:Arial, sans-serif; font-size:14px; color:#333;'>" +
            "<tr>" +
            "<th colspan='4' style='text-align:left; padding:10px; background-color:#f2f2f2; border:1px solid #ddd;'>Order ID: " + order.Code + "</th>" +
            "</tr>" +
            "<tr>" +
            "<th style='text-align:left; padding:10px; background-color:#4CAF50; color:white; border:1px solid #ddd;'>No.</th>" +
            "<th style='text-align:left; padding:10px; background-color:#4CAF50; color:white; border:1px solid #ddd;'>Image</th>" +
            "<th style='text-align:left; padding:10px; background-color:#4CAF50; color:white; border:1px solid #ddd;'>Package Name</th>" +
            "<th style='text-align:left; padding:10px; background-color:#4CAF50; color:white; border:1px solid #ddd;'>Price</th>" +
            "</tr>");

        var email_order_packages = db.OrderPackages
                                        .Where(op => op.OrderId == order_id)
                                        .Include(op => op.Package)
                                        .ThenInclude(p => p.PackagePhotos)
                                        .ToList();


        //foreach (var order_package in email_order_packages)
        for (int i = 0; i < email_order_packages.Count; i++)
        {
            var package_image_path = "";

            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

            if (email_order_packages[i].Package.PackagePhotos.Count() == 0)
            {
                package_image_path = Path.Combine(basePath, "images", "package", "defaultImage.png");
            }
            else
            {
                if (!string.IsNullOrEmpty(email_order_packages[i].Package.PackagePhotos[0].Path))
                {
                    package_image_path = Path.Combine(basePath, "images", "package", "cover",email_order_packages[i].Package.PackagePhotos[0].Path);
                }
                else
                {
                    package_image_path = Path.Combine(basePath, "images", "package", "defaultImage.png");
                }
            }

            var att = new Attachment(package_image_path);
            mail.Attachments.Add(att);
            att.ContentId = $"photo{i}";


            // append purchased packages item
            email_body.Append("<tr>" +
                "<td style='text-align:left; padding:10px; border:1px solid #ddd;'>" + (i + 1) + ".</td>" +
                "<td style='text-align:center; padding:10px; border:1px solid #ddd;'>" +
                "<img src='cid:" + $"photo{i}" + "' alt='" + email_order_packages[i].Package.Name + "' style='max-width:100px; height:auto;' />" +
                "</td>" +
                "<td style='text-align:left; padding:10px; border:1px solid #ddd;'>" + email_order_packages[i].Package.Name + "</td>" +
                "<td style='text-align:left; padding:10px; border:1px solid #ddd;'>RM " + email_order_packages[i].Price + "</td>" +
                "</tr>");
        }

        // append summary
        email_body.Append("<tr>" +
            "<td colspan='3' style='text-align:right; padding:10px; font-weight:bold; border:1px solid #ddd;'>Subtotal</td>" +
            "<td style='text-align:left; padding:10px; border:1px solid #ddd;'>RM " + subtotal.ToString("F2") + "</td>" +
            "</tr>");

        email_body.Append("<tr>" +
            "<td colspan='3' style='text-align:right; padding:10px; font-weight:bold; border:1px solid #ddd;'>Tax (8%)</td>" +
            "<td style='text-align:left; padding:10px; border:1px solid #ddd;'>RM " + tax.ToString("F2") + "</td>" +
            "</tr>");

        email_body.Append("<tr>" +
            "<td colspan='3' style='text-align:right; padding:10px; font-weight:bold; border:1px solid #ddd;'>Discount</td>" +
            "<td style='text-align:left; padding:10px; border:1px solid #ddd;'>RM " + discount.ToString("F2") + "</td>" +
            "</tr>");

        email_body.Append("<tr>" +
            "<td colspan='3' style='text-align:right; padding:10px; font-weight:bold; background-color:#f2f2f2; border:1px solid #ddd;'>Total</td>" +
            "<td style='text-align:left; padding:10px; background-color:#f2f2f2; border:1px solid #ddd;'>RM " + total_amount.ToString("F2") + "</td>" +
            "</tr>");

        email_body.Append("</table>");
        string final_email_body = email_body.ToString();
        mail.Body = final_email_body;

        hp.sendEmail2(mail);

        return System.Text.Json.JsonSerializer.Serialize(
            new string[] {
                "success",
                "Order is created successfully.",
                order_id.ToString(),
            }
        );
    }

    [Route("Order/Invoice/{id}")]
    public IActionResult Invoice(int id)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff", "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // get order data
        var order = db.Orders
                        .Where(o => o.Id == id)
                        .Include(o => o.OrderPackages)
                            .ThenInclude(op => op.Package)
                            .ThenInclude(p => p.PackagePhotos)
                        .Include(o => o.Member)
                            .ThenInclude(m => m.Person)
                        .FirstOrDefault();

        if (order == null)
        {
            TempData["Error_Title"] = "Order Not Found";
            TempData["Alert_Error"] = "The requested order does not exist.";
            return Redirect(Request.Headers.Referer.ToString());
        }

        var userId = HttpContext.Session.GetInt32("_userId");
        var userRole = HttpContext.Session.GetString("_userRole");

        if (userRole == "member" && order.Member?.Id != userId)
        {
            TempData["Error_Title"] = "Access Denied";
            TempData["Alert_Error"] = "You do not have access to this order.";
            return Redirect(Request.Headers.Referer.ToString());
        }

        var document = invoiceRenderingService.GenerateInvoicePdf(order);
        return File(document, "application/pdf", $"Invoice_{order.Code}.pdf");
    }
}
