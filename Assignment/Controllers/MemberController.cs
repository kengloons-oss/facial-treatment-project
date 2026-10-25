using Assignment.Models;
using AutoMapper;
using ExcelDataReader;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Stripe.Checkout;
using X.PagedList.Extensions;

namespace Assignment.Controllers;

public class MemberController : Controller
{
    private readonly DB db;
    private readonly IMapper mapper;
    private readonly Helpers hp;

    public MemberController(DB db, IMapper mapper, Helpers hp)
    {
        this.db = db;
        this.mapper = mapper;
        this.hp = hp;
    }

    public IActionResult List(string? name, string? sort, string? dir, int? level, int page_items = 7, int page = 1)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // filter level
        ViewBag.Level = level ?? 0;

        var membership_levels = db.Memberships
                                    .Select(m => m.Level)
                                    .ToList();

        var filtered_level = !membership_levels.IsNullOrEmpty() && level != 0 && membership_levels.Contains(level ?? 0)
                                ? db.Members
                                    .Where(m => m.Level == level)
                                : db.Members;

        // searching member name
        ViewBag.Name = name = name?.Trim() ?? "";

        var searched = filtered_level
                            .Where(m => m.Person.Name.Contains(name))
                            .Include(m => m.Person)
                            .ToList();

        // pending appointment amount
        Dictionary<int, int> pending_apt_dict = new Dictionary<int, int>();

        foreach (var member in searched)
        {
            var pending_appointment = db.Appointments
                                            .Where(ap => ap.MemberId == member.Id && ap.Status == 1)
                                            .Count();

            pending_apt_dict.Add(member.Id, pending_appointment);
        }

        ViewBag.pending_apt_dict = pending_apt_dict;

        // sorting
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<Member, object> fn = sort switch
        {
            "ID" => m => m.Id,
            "Name" => m => m.Person.Name,
            "Gender" => m => m.Person.Gender,
            "Member Level" => m => m.Level,
            "Pending Appointment(s)" => m => pending_apt_dict.ContainsKey(m.Id) ? pending_apt_dict[m.Id] : 0,
            "Registration Date" => m => m.Person.Registration_date,
            _ => m => m.Id,
        };

        var sorted = dir == "desc" ? searched.OrderByDescending(fn) : searched.OrderBy(fn);

        // paging
        if (page < 1)
        {
            return RedirectToAction(null, new { name, sort, dir, level, page_items, page = 1 });
        }

        // filter items per page
        ViewBag.Page_items = page_items;

        var m = sorted.ToPagedList(page, page_items);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { name, sort, dir, level, page_items, page = m.PageCount });
        }

        // membership level select lists
        List<SelectListItem> vb_membership_levels = new List<SelectListItem>
        {
            new SelectListItem { Value = "0", Text = "All" }
        };

        foreach (var memberLvl in membership_levels)
        {
            vb_membership_levels.Add(new SelectListItem { Value = memberLvl.ToString(), Text = memberLvl.ToString() });
        }


        var membershipLevels = db.Memberships
                         .Select(m => new { Level = (int?)m.Level, Display = m.Level.ToString() })
                         .ToList();

        membershipLevels.Insert(0, new { Level = (int?)0, Display = "All" });

        ViewBag.Membership_levels = new SelectList(membershipLevels, "Level", "Display");

        //ViewBag.Membership_levels = new SelectList(db.Memberships, "Level", "Level");

        // check whether it is ajax
        if (Request.IsAjax())
        {
            return PartialView("_MemberList", m);
        }

        return View(m);
    }

    public IActionResult Create()
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var latest_id = db.Members
                        .Max(s => s.Id);
        ViewBag.New_id = latest_id + 1;

        return View();
    }

    [HttpPost]
    public IActionResult Create(PersonVM vm)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // generate member id
        var member_id = db.Members
                    .Max(s => s.Id) + 1;

        // check if the Email is valid
        if (ModelState.IsValid("Person.Email"))
        {
            // check if the Email is duplicated
            if (db.Persons.Any(p => p.Email == vm.Email))
            {
                ModelState.AddModelError("Person.Email", "Duplicated Email.");
            }
        }

        // check password
        if (!string.IsNullOrWhiteSpace(vm.Password))
        {
            var password_err = hp.ValidatePassword(vm.Password);
            if (password_err != null)
            {
                ModelState.AddModelError("Person.Password", password_err);
            }
        }
        else
        {
            ModelState.AddModelError("Person.Password", "Password cannot be empty.");
        }

        // check if the Avatar is null or valid
        if (vm.Upload_image != null)
        {
            var error = hp.ValidateImage(vm.Upload_image);
            if (error != "") ModelState.AddModelError("Person.Upload_image", error);
        }

        // check whether photo is uploaded
        if (vm.Upload_image != null)
        {
            var memberFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/user/member");

            // Check if the folder exists; if not, create it
            if (!Directory.Exists(memberFolderPath))
            {
                Directory.CreateDirectory(memberFolderPath);
            }

            vm.Avatar = hp.SavePhoto(vm.Upload_image, "images/user/member").Result;
        }

        // validate entire model
        if (ModelState.IsValid)
        {
            var person = new Assignment.Models.Person
            {
                Name = vm.Name.Trim(),
                Gender = vm.Gender,
                Avatar = vm.Avatar,
                Email = vm.Email.Trim(),
                Password = hp.HashPassword(vm.Password.Trim()),
                Tel_no = vm.Tel_no.Trim(),
                Registration_date = DateTime.Now,
                Status = 1,
            };

            db.Persons.Add(person);
            db.SaveChanges();

            int personId = person.Id;

            db.Members.Add(new()
            {
                Level = 1,
                Credit = 0,
                Total_expenses = 0,
                PersonId = personId,
                Referral_id = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()
            });

            db.SaveChanges();

            TempData["Success_Title"] = "Member Create Success";
            TempData["Alert_Success"] = "Member is created successfully.";
            return RedirectToAction("List");
        }

        // redeclare New_id (if error occurs)
        var latest_id = db.Members
                        .Max(s => s.Id);
        ViewBag.New_id = latest_id + 1;

        return View(vm);
    }

    public IActionResult Detail(int id)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var member = db.Members
                        .Include(m => m.Person)
                        .FirstOrDefault(m => m.Id == id);

        if (member == null)
        {
            return RedirectToAction("List");
        }

        // map Person to Member model
        var vm = mapper.Map<MemberPersonVM>(member);

        return View(vm);
    }

    [HttpPost]
    public IActionResult Detail(MemberPersonVM vm)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var m = db.Members
                    .Include(m => m.Person)
                    .FirstOrDefault(m => m.Id == vm.Member.Id);

        // check whether the Member Id exists
        if (m == null)
        {
            return RedirectToAction("List");
        }

        // check if the Email is valid
        if (ModelState.IsValid("Person.Email"))
        {
            // check if the Email is duplicated
            if (db.Persons.Any(p => p.Email == vm.Person.Email && p.Id != m.Person.Id))
            {
                ModelState.AddModelError("Person.Email", "Duplicated Email.");
            }
        }

        // check if the Avatar is null or valid
        if (vm.Person.Upload_image != null)
        {
            var error = hp.ValidateImage(vm.Person.Upload_image);
            if (error != "") ModelState.AddModelError("Person.Upload_image", error);
        }

        // check updated password
        if (!string.IsNullOrWhiteSpace(vm.Person.Password))
        {
            var password_err = hp.ValidatePassword(vm.Person.Password);
            if (password_err != null)
            {
                ModelState.AddModelError("Person.Password", password_err);
            }
        }

        // validate entire model
        if (ModelState.IsValid)
        {
            m.Person.Name = vm.Person.Name.Trim();
            m.Person.Gender = vm.Person.Gender;
            m.Person.Email = vm.Person.Email.Trim();
            m.Person.Tel_no = vm.Person.Tel_no.Trim();

            // check whether new photo is uploaded
            // check whether new photo is uploaded
            if (vm.Person.Upload_image != null)
            {
                var memberFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/user/member");

                // Check if the folder exists; if not, create it
                if (!Directory.Exists(memberFolderPath))
                {
                    Directory.CreateDirectory(memberFolderPath);
                }

                if (m.Person.Avatar != null) hp.DeletePhoto(m.Person.Avatar, "images/user/member");
                m.Person.Avatar = hp.SavePhoto(vm.Person.Upload_image, "images/user/member").Result;
            }

            // check whether new passowrd is added
            if (vm.Person.Password != null)
            {
                m.Person.Password = hp.HashPassword(vm.Person.Password.Trim());
            }

            db.SaveChanges();

            TempData["Success_Title"] = "Update Success";
            TempData["Alert_Success"] = "Member detail is updated.";
            return RedirectToAction("List");
        }

        return View(vm);
    }

    public IActionResult Activate(int id)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("List");
        }

        var s = db.Members
                    .Include(s => s.Person)
                    .FirstOrDefault(s => s.Id == id);

        if (s != null)
        {
            s.Person.Status = 1;
            s.Person.Login_failed_time = 0;
            db.SaveChanges();

            TempData["Success_Title"] = "Activate Success";
            TempData["Alert_Success"] = "The member has been activated.";
        }
        else
        {
            TempData["Error_Title"] = "Activate Failed";
            TempData["Alert_Error"] = "The member does not exist.";
        }

        return Redirect(Request.Headers.Referer.ToString());
    }

    public IActionResult Deactivate(int id)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("List");
        }

        var s = db.Members
                    .Include(s => s.Person)
                    .FirstOrDefault(s => s.Id == id);

        if (s != null)
        {
            s.Person.Status = 0;
            db.SaveChanges();

            TempData["Success_Title"] = "Deactivate Success";
            TempData["Alert_Success"] = "The member has been deactivated.";
        }
        else
        {
            TempData["Error_Title"] = "Deactivate Failed";
            TempData["Alert_Error"] = "The member does not exist.";
        }

        return Redirect(Request.Headers.Referer.ToString());
    }

    [HttpGet]
    public IActionResult Wallet(string? filterType = null, string? filterMonth = null, string? sort = "dateDesc", string? dir = "desc", int page = 1, int page_items = 7)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var memberId = HttpContext.Session.GetInt32("_userId");
        if (memberId == null)
        {
            return RedirectToAction("Login");
        }

        var member = db.Members.FirstOrDefault(m => m.Id == memberId);
        if (member == null)
        {
            return RedirectToAction("Login", new { message = "Member not found." });
        }

        // Retrieve top-up history and map to WalletTransactionViewModel
        var topUpOrders = db.TopUpOrders
            .Where(t => t.MemberId == memberId)
            .Select(t => new WalletTransactionViewModel
            {
                Date = t.Payment_date,
                Amount = t.Amount,
                FreeCredit = t.Free_credit,
                CreditBefore = t.Credit_before,
                CreditAfter = t.Credit_after,
                RecordType = "Cash In",
                AdjustedAmount = t.Amount // Cash In is positive
            });

        // Retrieve wallet usage orders and map to WalletTransactionViewModel
        var orders = db.Orders
            .Where(o => o.MemberId == memberId && o.Payment_method == 3)
            .Select(o => new WalletTransactionViewModel
            {
                Date = o.Payment_date,
                Amount = o.Amount,
                FreeCredit = 0m,
                CreditBefore = o.Credit_before ?? 0m,
                CreditAfter = o.Credit_after ?? 0m,
                RecordType = "Cash Out",
                AdjustedAmount = -o.Amount // Cash Out is negative
            });

        // Merge both top-up orders and wallet usage orders
        var mergedTransactions = topUpOrders.Concat(orders).ToList();

        // Apply filtering by selected month (if any)
        if (!string.IsNullOrEmpty(filterMonth))
        {
            int filterMonthInt = int.Parse(filterMonth);
            mergedTransactions = mergedTransactions
                .Where(t => t.Date.Month == filterMonthInt)
                .ToList();
        }


        // Apply filtering by record type (if any)
        if (!string.IsNullOrEmpty(filterType))
        {
            mergedTransactions = mergedTransactions
                .Where(t => t.RecordType == filterType)
                .ToList();
        }


        // Sorting logic
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<WalletTransactionViewModel, object> fn = sort switch
        {
            "Type" => t => t.RecordType,
            "Amount" => t => t.Amount,
            "AdjustedAmount" => t => t.AdjustedAmount,
            "CreditBefore" => t => t.CreditBefore,
            "CreditAfter" => t => t.CreditAfter,
            _ => t => t.Date,
        };

        mergedTransactions = dir == "desc"
            ? mergedTransactions.OrderByDescending(fn).ToList()
            : mergedTransactions.OrderBy(fn).ToList();

        if (page < 1)
        {
            return RedirectToAction("Wallet", new { filterMonth, sort, dir, filterType, page_items, page = 1 });
        }

        // Paging with X.PagedList
        var pagedTransactions = mergedTransactions.ToPagedList(page, page_items);

        if (page > pagedTransactions.PageCount && pagedTransactions.PageCount > 0)
        {
            return RedirectToAction("Wallet", new { filterMonth, sort, dir, filterType, page_items, page = pagedTransactions.PageCount });
        }

        // Prepare data for view
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = pagedTransactions.PageCount;
        ViewBag.SortOrder = sort;
        ViewBag.Dir = dir;
        ViewBag.FilterMonth = filterMonth;
        ViewBag.FilterType = filterType; // Pass the selected type back to the view

        // Check for AJAX request and return partial view
        if (Request.IsAjax())
        {
            return PartialView("_Wallet", pagedTransactions);
        }

        return View("Wallet", pagedTransactions);
    }

    [HttpGet]
    public IActionResult TopUp()
    {
        if (!hp.CheckRole(new string[] { "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        var memberId = HttpContext.Session.GetInt32("_userId");

        if (memberId == null)
        {
            return RedirectToAction("Login");
        }
        var member = db.Members.FirstOrDefault(m => m.Id == memberId);
        if (member == null)
        {
            return RedirectToAction("Login", new { message = "Member not found." });
        }

        return View("TopUp", member);
    }

    [HttpPost]
    public IActionResult TopUp(decimal topUpAmount, int paymentMethod)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        try
        {
            if (topUpAmount < 5)
            {
                TempData["Alert_Error"] = "Minimum top-up amount is RM5.";
                TempData["Error_Title"] = "Top Up Failed";
                return RedirectToAction("Wallet");
            }

            var memberId = HttpContext.Session.GetInt32("_userId");
            if (memberId == null)
            {
                return RedirectToAction("Login");
            }

            var member = db.Members.FirstOrDefault(m => m.Id == memberId);
            if (member == null)
            {
                return RedirectToAction("Error", new { message = "Member not found." });
            }

            // Get Applicable free credit
            var reward = db.TopUpRewards.Where(r => r.Status == 1 && topUpAmount >= r.Min_topup)
                        .OrderByDescending(r => r.Min_topup).FirstOrDefault();

            var freeCredit = reward?.Free_credit ?? 0;

            // Update Credit Balance
            var creditBefore = member.Credit;
            member.Credit += topUpAmount + freeCredit;

            // Create top-up order record
            var topUpOrder = new TopUpOrder
            {
                Amount = topUpAmount,
                Free_credit = freeCredit,
                Payment_method = paymentMethod,
                Payment_date = DateTime.Now,
                Credit_before = creditBefore,
                Credit_after = member.Credit,
                MemberId = member.Id,
            };
            db.TopUpOrders.Add(topUpOrder);
            db.SaveChanges();

            // Create inbox notification
            var inbox = new Inbox
            {
                Title = "Top-Up Successful",
                Message = $"You have successfully topped up RM{topUpAmount:F2}. Free credit added: RM{freeCredit:F2}. Current balance: RM{member.Credit:F2}.",
                Target = 1, // Specify a target if needed
                Type = 2, // Example: 1 for general notifications
                Created_datetime = DateTime.Now,
                Status = 1, 
                MemberId = member.Id
            };
            db.Inboxes.Add(inbox);
            db.SaveChanges();

            // Create Stripe session for top-up
            // Handle the payment method logic (Stripe)
            if (paymentMethod == 2) // Stripe
            {

                //Your Stripe logic here(previously provided)
                var domain = "https://localhost:7124/"; // Replace with your actual domain
                var options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string> { "card" },
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                UnitAmount = (long)(topUpAmount * 100), // Convert to cents
                                Currency = "myr",
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = $"Top-up of RM{topUpAmount:F2}" // Description of the top-up
                                }
                            },
                            Quantity = 1 // One item for the top-up
                        }
                    },
                    Mode = "payment",
                    //SuccessUrl = domain + "TopUp/PaymentSuccess?orderId=" + topUpOrder.Id,
                    SuccessUrl = domain + "Member/Wallet",
                    CancelUrl = domain + "Member/Wallet"
                };

                var service = new SessionService();
                var session = service.Create(options);


                if (session == null || string.IsNullOrEmpty(session.Url))
                {
                    TempData["Alert_Error"] = "Stripe session creation failed. Please try again.";
                    return RedirectToAction("Wallet");
                }
                TempData["Success"] = $"Top-up successful! Free credit added: {freeCredit:C}. Current balance: {member.Credit:C}.";

                // Redirect user to Stripe Checkout
                return Redirect(session.Url);

            }

            TempData["Success"] = $"Top-up successful! Free credit added: {freeCredit:C}. Current balance: {member.Credit:C}.";
            return RedirectToAction("Wallet");
        }
        catch (Exception ex)
        {
            TempData["Alert_Error"] = $"An error occurred: {ex.Message}";
            TempData["Error_Title"] = "Top Up Failed";
            return RedirectToAction("Wallet");
        }
    }


    public IActionResult ImportMember()
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ImportMember(IFormFile file, [FromServices] Microsoft.AspNetCore.Hosting.IHostingEnvironment hostingEnvironment)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // Generate a unique file name
        string uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
        string filePath = $"{hostingEnvironment.WebRootPath}\\files\\{uniqueFileName}";

        try
        {
            // Save the file temporarily with a unique name
            using (FileStream fileStream = System.IO.File.Create(filePath))
            {
                await file.CopyToAsync(fileStream);
                fileStream.Flush();
            }

            // Process the file
            List<Person> persons = GetPersonList(filePath);
            int totalCount = persons?.Count ?? 0;
            int successCount = 0;
            int failureCount = 0;

            if (persons != null && persons.Count > 0)
            {
                // Collect emails to detect duplicates within the file
                var emailSet = new HashSet<string>();

                foreach (var person in persons)
                {
                    // Validate required fields: Name, Email, and Gender
                    if (string.IsNullOrEmpty(person.Name) ||
                        string.IsNullOrEmpty(person.Email) ||
                        string.IsNullOrEmpty(person.Gender) ||
                        !(person.Gender.ToUpper() == "F" || person.Gender.ToUpper() == "M"))
                    {
                        failureCount++;
                        continue;
                    }

                    // Check for duplicate email within the file
                    if (!emailSet.Add(person.Email))
                    {
                        failureCount++;
                        continue;
                    }

                    // Check if the email already exists in the database
                    var existingPerson = await db.Persons
                                            .FirstOrDefaultAsync(p => p.Email == person.Email);

                    if (existingPerson != null)
                    {
                        // If the email already exists, count as failed and continue
                        failureCount++;
                        continue;
                    }

                    // If all validations pass, add the person to the database
                    try
                    {
                        db.Persons.Add(person);
                        await db.SaveChangesAsync();

                        int personId = person.Id;

                        db.Members.Add(new()
                        {
                            Level = 1,
                            Credit = 0,
                            Total_expenses = 0,
                            PersonId = personId,
                            Referral_id = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()
                        });

                        await db.SaveChangesAsync();
                        successCount++;
                    }
                    catch (Exception)
                    {
                        failureCount++;
                    }
                }

                TempData["Success_Title"] = "Import Done";
                TempData["Alert_Success"] = $"Total Records: {totalCount}, Success: {successCount}, Failed: {failureCount}. (Check for duplicate email if there are any record imports failed.)";
            }
            else
            {
                TempData["Error_Title"] = "Import Failed";
                TempData["Alert_Error"] = "No record found in the file or format incorrect. Try to use the template provided and import again.";
            }
        }
        finally
        {
            // Delete the file after processing
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }

        return RedirectToAction("ImportMember");
    }


    private List<Person> GetPersonList(string filePath)
    {
        List<Person> persons = new List<Person>();
        bool isFirstRow = true; // To skip the first row (header)

        var fileExtension = Path.GetExtension(filePath).ToLower();
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        if (fileExtension == ".csv")
        {
            // Handle CSV
            using (var reader = new StreamReader(filePath))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (isFirstRow)
                    {
                        isFirstRow = false; // Skip the header row
                        continue;
                    }

                    var values = line.Split(',');

                    // Check if the row is empty
                    if (values.All(string.IsNullOrWhiteSpace))
                    {
                        break; // Stop processing if an empty row is encountered
                    }

                    persons.Add(new Person
                    {
                        Name = values.ElementAtOrDefault(0),
                        Gender = values.ElementAtOrDefault(1),
                        Email = values.ElementAtOrDefault(2),
                        Password = values.ElementAtOrDefault(3),
                        Tel_no = values.ElementAtOrDefault(4)
                    });
                }
            }
        }
        else if (fileExtension == ".xlsx" || fileExtension == ".xls")
        {
            // Handle Excel
            using (var stream = System.IO.File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    while (reader.Read())
                    {
                        if (isFirstRow)
                        {
                            isFirstRow = false; // Skip the header row
                            continue;
                        }

                        // Check if the row is empty
                        if (Enumerable.Range(0, reader.FieldCount).All(i => reader.GetValue(i) == null))
                        {
                            break; // Stop processing if an empty row is encountered
                        }

                        persons.Add(new Person
                        {
                            Name = reader.GetValue(0)?.ToString(),
                            Gender = reader.GetValue(1)?.ToString(),
                            Email = reader.GetValue(2)?.ToString(),
                            Password = reader.GetValue(3)?.ToString(),
                            Tel_no = reader.GetValue(4)?.ToString()
                        });
                    }
                }
            }
        }

        return persons;
    }



}
