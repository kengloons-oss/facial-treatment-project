using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Org.BouncyCastle.Crypto.Engines.SM2Engine;
using AutoMapper;
using System.Web;
using System.Linq;
using X.PagedList.Extensions;
using Assignment.Models;
using Stripe;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;
using AutoMapper.Execution;

namespace Assignment.Controllers;

public class UserController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly IMapper _mapper;
    private readonly Helpers hp;

    public UserController(DB db, IWebHostEnvironment en, IMapper mapper, Helpers hp)
    {
        this.db = db;
        this.en = en;
        this._mapper = mapper;
        this.hp = hp;
    }

    // Get Staff, Manager & Member Profile
    public IActionResult Profile()
    {
        var userId = HttpContext.Session.GetInt32("_userId");
        var userRole = HttpContext.Session.GetString("_userRole");

        if (!hp.CheckRole(new string[] { "staff", "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }


        if (userRole == "staff")
        {
            var staff = db.Staffs
                    .Include(s => s.Person)
                    .FirstOrDefault(s => s.Id == userId && s.Person.Status == 1);

            if (staff == null)
            {
                // Handle the case where the user ID is invalid
                TempData["Error_Title"] = "Access Denied";
                TempData["Alert_Error"] = "You have not logged in or no access for this page. Try to log in with other account to access this website.";
                return RedirectToAction("Home", "Home");
            }

            // map Person to Staff model
            var m = _mapper.Map<StaffPersonVM>(staff);

            // Modify the Avatar become path
            var profilePath = "/images/user/staff/" + staff.Person.Avatar;
            var capture = TempData["capture_photo"];
            if (capture != null)
            {
                profilePath = "/images/user/temp/" + capture;
            }
            else
            {
                hp.ClearDirectory("wwwroot/images/user/temp");
            }

            // Reasign the avatar to string that contain whole path
            m.Person.Avatar = profilePath;

            // Pass the capture photo file name if exist
            ViewBag.CapturePhoto = capture;

            return View("Staff/Profile", m);
        }
        else
        {
            var member = db.Members
                                .Include(m => m.Person)
                                .FirstOrDefault(m => m.Id == userId && m.Person.Status == 1);

            if (member == null)
            {
                // Handle the case where the user ID is invalid
                TempData["Error_Title"] = "Access Denied";
                TempData["Alert_Error"] = "You have not logged in or no access for this page. Try to log in with other account to access this website.";
                return RedirectToAction("Home", "Home");
            }

            // map Person to Member model
            var m = _mapper.Map<MemberPersonVM>(member);

            // Modify the Avatar become path
            var profilePath = "/images/user/member/" + member.Person.Avatar;
            var capture = TempData["capture_photo"];
            if (capture != null)
            {
                profilePath = "/images/user/temp/" + capture;
            }
            else
            {
                hp.ClearDirectory("wwwroot/images/user/temp");
            }

            // Reasign the avatar to string that contain whole path
            m.Person.Avatar = profilePath;

            // Pass the capture photo file name if exist
            ViewBag.CapturePhoto = capture;

            return View("Member/Profile", m);
        }
    }

    [HttpPost]
    public IActionResult Profile(StaffPersonVM vm)
    {
        var pwd_verify = true;
        var userId = HttpContext.Session.GetInt32("_userId");
        var userRole = HttpContext.Session.GetString("_userRole");

        // Check user authorization
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var s = db.Staffs
                    .Include(s => s.Person)
                    .FirstOrDefault(s => s.Id == vm.Staff.Id);

        // check whether the Staff Id exists
        if (s == null)
        {
            return RedirectToAction("List");
        }

        // check if the Email is valid
        if (ModelState.IsValid("Person.Email"))
        {
            // check if the Email is duplicated
            if (db.Staffs.Any(s => s.Person.Email == vm.Person.Email && s.Id != vm.Staff.Id))
            {
                ModelState.AddModelError("Person.Email", "Duplicated Email.");
            }
            else if (!s.Person.Email.Equals(vm.Person.Email))
            {
                ModelState.AddModelError("Person.Email", "Please verify your email first.");
            }
        }

        // check if the Avatar is null or valid
        if (vm.Person.Upload_image != null)
        {
            var error = hp.ValidateImage(vm.Person.Upload_image);
            if (error != "") ModelState.AddModelError("Person.Upload_image", error);
        }
        else if (vm.Person.CapturePhoto != null)
        {
            // Get the filename
            string fileName = vm.Person.CapturePhoto;

            // Define the source and target paths
            string tempFolderPath = Path.Combine("wwwroot", "images", "user", "temp", fileName);
            string memberFolderPath = Path.Combine("wwwroot", "images", "user", "staff", fileName);

            try
            {
                // Ensure the temp file exists
                if (System.IO.File.Exists(tempFolderPath))
                {
                    // Create the target directory if it doesn't exist
                    string targetDirectory = Path.GetDirectoryName(memberFolderPath);
                    if (!Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    // Move the file to the new location
                    System.IO.File.Move(tempFolderPath, memberFolderPath);

                    // Optionally, update the database or model with the new file path
                    vm.Person.Avatar = fileName;
                }
                else
                {
                    // Handle the case where the file does not exist
                    TempData["Error"] = "The captured photo could not be found.";
                }
            }
            catch (Exception ex)
            {
                // Handle errors (e.g., permission issues, file locks, etc.)
                TempData["Error"] = $"An error occurred while moving the photo: {ex.Message}";
            }
        }

        // check current password
        if (!string.IsNullOrWhiteSpace(vm.Person.Current_password))
        {
            var password_err = hp.ValidatePassword(vm.Person.Current_password);
            if (password_err != null)
            {
                pwd_verify = true;
                ModelState.AddModelError("Person.Current_password", password_err);
            }
            else
            {
                if (!hp.VerifyPassword(s.Person.Password, vm.Person.Current_password))
                {
                    pwd_verify = true;
                    password_err = "Current password incorrect.";
                    ModelState.AddModelError("Person.Current_password", password_err);
                }
            }

            // check new password
            if (!string.IsNullOrWhiteSpace(vm.Person.New_password))
            {
                password_err = hp.ValidatePassword(vm.Person.New_password);
                if (password_err != null)
                {
                    pwd_verify = true;
                    ModelState.AddModelError("Person.New_password", password_err);
                }
            }
            else
            {
                pwd_verify = true;
                password_err = "New password cannot be empty.";
                ModelState.AddModelError("Person.New_password", password_err);
            }

            // check confirm password
            if (!string.IsNullOrWhiteSpace(vm.Person.Password))
            {
                password_err = hp.ValidatePassword(vm.Person.Password);
                if (password_err != null)
                {
                    pwd_verify = true;
                    ModelState.AddModelError("Person.Password", password_err);
                }
            }
            else
            {
                pwd_verify = true;
                password_err = "Confirm password cannot be empty.";
                ModelState.AddModelError("Person.Password", password_err);
            }

            // check new password and confrim password same or not
            if (pwd_verify == false)
            {
                if (!vm.Person.New_password.Equals(vm.Person.Password))
                {
                    pwd_verify = true;
                    password_err = "Confirm Password must same with New Password.";
                    ModelState.AddModelError("Person.Password", password_err);
                }
            }
        }

        // validate entire model
        if (ModelState.IsValid)
        {
            s.Person.Name = vm.Person.Name.Trim();
            s.Person.Gender = vm.Person.Gender;
            s.Person.Email = vm.Person.Email.Trim();
            s.Person.Tel_no = vm.Person.Tel_no.Trim();
            s.Address = vm.Staff.Address;

            // check whether new photo is uploaded
            if (vm.Person.Upload_image != null)
            {
                if (s.Person.Avatar != null) { hp.DeletePhoto(s.Person.Avatar, "images/user/staff"); }

                var stfFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/user/staff");

                // Check if the folder exists; if not, create it
                if (!Directory.Exists(stfFolderPath))
                {
                    Directory.CreateDirectory(stfFolderPath);
                }

                s.Person.Avatar = hp.SavePhoto(vm.Person.Upload_image, "images/user/staff").Result;
            }
            else if (vm.Person.CapturePhoto != null)
            {
                if (s.Person.Avatar != null) hp.DeletePhoto(s.Person.Avatar, "images/user/staff");
                s.Person.Avatar = vm.Person.Avatar;
            }

            // check whether new passowrd is added
            if (vm.Person.Password != null)
            {
                s.Person.Password = hp.HashPassword(vm.Person.Password.Trim());
            }

            db.SaveChanges();

            hp.UpdateUser(s.Person);

            TempData["Success_Title"] = "Update Success";
            TempData["Alert_Success"] = "Profile detail is updated.";
            return RedirectToAction("Profile");
        }

        TempData["Error_Title"] = "Update Failed";
        TempData["Alert_Error"] = "Profile detail failed to update. May have some input invalid. Try to update again.";
        if (userRole == "staff")
        {
            return View("Staff/Profile", vm);
        }
        return View("Member/Profile", vm);

    }

    [HttpPost]
    public IActionResult MemberProfile(MemberPersonVM vm)
    {
        var pwd_verify = true;
        var userId = HttpContext.Session.GetInt32("_userId");

        // Check user authorization
        if (!hp.CheckRole(new string[] { "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var m = db.Members
                    .Include(s => s.Person)
                    .FirstOrDefault(s => s.Id == vm.Member.Id);

        // check whether the Member Id exists
        if (m == null)
        {
            //return RedirectToAction("List");
            TempData["Error_Title"] = "Update Failed";
            TempData["Alert_Error"] = "The member does not exist.";

            return Redirect(Request.Headers.Referer.ToString());
        }

        // check if the Email is valid
        if (ModelState.IsValid("Person.Email"))
        {
            // check if the Email is duplicated
            if (db.Members.Any(m => m.Person.Email == vm.Person.Email && m.Id != vm.Member.Id))
            {
                ModelState.AddModelError("Person.Email", "Duplicated Email.");
            }
            else if (!m.Person.Email.Equals(vm.Person.Email))
            {
                ModelState.AddModelError("Person.Email", "Please verify your email first.");
            }
        }

        // check if the Avatar is null or valid
        if (vm.Person.Upload_image != null)
        {
            var error = hp.ValidateImage(vm.Person.Upload_image);
            if (error != "") ModelState.AddModelError("Person.Upload_image", error);
        }
        else if (vm.Person.CapturePhoto != null)
        {
            // Get the filename
            string fileName = vm.Person.CapturePhoto;

            // Define the source and target paths
            string tempFolderPath = Path.Combine("wwwroot", "images", "user", "temp", fileName);
            string memberFolderPath = Path.Combine("wwwroot", "images", "user", "member", fileName);

            try
            {
                // Ensure the temp file exists
                if (System.IO.File.Exists(tempFolderPath))
                {
                    // Create the target directory if it doesn't exist
                    string targetDirectory = Path.GetDirectoryName(memberFolderPath);
                    if (!Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    // Move the file to the new location
                    System.IO.File.Move(tempFolderPath, memberFolderPath);

                    // Optionally, update the database or model with the new file path
                    vm.Person.Avatar = fileName;
                }
                else
                {
                    // Handle the case where the file does not exist
                    TempData["Error"] = "The captured photo could not be found.";
                }
            }
            catch (Exception ex)
            {
                // Handle errors (e.g., permission issues, file locks, etc.)
                TempData["Error"] = $"An error occurred while moving the photo: {ex.Message}";
            }
        }

        // check current password
        if (!string.IsNullOrWhiteSpace(vm.Person.Current_password))
        {
            var password_err = hp.ValidatePassword(vm.Person.Current_password);
            if (password_err != null)
            {
                pwd_verify = true;
                ModelState.AddModelError("Person.Current_password", password_err);
            }
            else
            {
                if (!hp.VerifyPassword(m.Person.Password, vm.Person.Current_password))
                {
                    pwd_verify = true;
                    password_err = "Current password incorrect.";
                    ModelState.AddModelError("Person.Current_password", password_err);
                }
            }

            // check new password
            if (!string.IsNullOrWhiteSpace(vm.Person.New_password))
            {
                password_err = hp.ValidatePassword(vm.Person.New_password);
                if (password_err != null)
                {
                    pwd_verify = true;
                    ModelState.AddModelError("Person.New_password", password_err);
                }
            }
            else
            {
                pwd_verify = true;
                password_err = "New password cannot be empty.";
                ModelState.AddModelError("Person.New_password", password_err);
            }

            // check confirm password
            if (!string.IsNullOrWhiteSpace(vm.Person.Password))
            {
                password_err = hp.ValidatePassword(vm.Person.Password);
                if (password_err != null)
                {
                    pwd_verify = true;
                    ModelState.AddModelError("Person.Password", password_err);
                }
            }
            else
            {
                pwd_verify = true;
                password_err = "Confirm password cannot be empty.";
                ModelState.AddModelError("Person.Password", password_err);
            }

            // check new password and confrim password same or not
            if (pwd_verify == false)
            {
                if (!vm.Person.New_password.Equals(vm.Person.Password))
                {
                    pwd_verify = true;
                    password_err = "Confirm Password must same with New Password.";
                    ModelState.AddModelError("Person.Password", password_err);
                }
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
            if (vm.Person.Upload_image != null)
            {
                if (m.Person.Avatar != null) { hp.DeletePhoto(m.Person.Avatar, "images/user/member"); }

                var memFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/user/member");

                // Check if the folder exists; if not, create it
                if (!Directory.Exists(memFolderPath))
                {
                    Directory.CreateDirectory(memFolderPath);
                }

                m.Person.Avatar = hp.SavePhoto(vm.Person.Upload_image, "images/user/member").Result;
            }
            else if (vm.Person.CapturePhoto != null)
            {
                if (m.Person.Avatar != null) hp.DeletePhoto(m.Person.Avatar, "images/user/member");
                m.Person.Avatar = vm.Person.Avatar;
            }

            // check whether new passowrd is added
            if (vm.Person.Password != null)
            {
                m.Person.Password = hp.HashPassword(vm.Person.Password.Trim());
            }

            db.SaveChanges();

            hp.UpdateUser(m.Person);

            TempData["Success_Title"] = "Update Success";
            TempData["Alert_Success"] = "Profile detail is updated.";
            return RedirectToAction("Profile");
        }

        TempData["Error_Title"] = "Update Failed";
        TempData["Alert_Error"] = "Profile detail failed to update. May have some input invalid. Try to update again..";

        return View("Member/Profile", vm);
    }

    [Route("/User/Staff/Dashboard")]
    public IActionResult StaffDashboard()
    {
        // Check user authorization
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var userId = HttpContext.Session.GetInt32("_userId");
        ViewBag.Position = HttpContext.Session.GetString("_userPosition");

        if (userId == null)
        {
            return RedirectToAction("Home", "Home");
        }

        var staff = db.Staffs
                        .Include(s => s.Person)
                        .Where(s => s.Id == userId)
                        .FirstOrDefault();

        // ==================================
        // Staff : Total Appointment Pending
        // ==================================

        var appointment_pending = db.Appointments
                                        .Include(a => a.Staff)
                                        .Where(a => a.Staff.Id == userId
                                                && (a.Status == 1 || a.Status == 2)
                                                && a.Start_datetime > DateTime.Now
                                        )
                                        .Count();

        ViewBag.StaffPendingAppointment = appointment_pending;

        // ===========================
        // Staff : Monthly Commission
        // ===========================

        var currentYearCommission = CalculateMonthlyCommission(userId, DateTime.Now.Year);
        var currentMonthCommission = currentYearCommission
                                       .FirstOrDefault(c => c.Month == DateTime.Now.Month)?
                                       .MonthlyEarnedCommission ?? 0.00m;
        ViewBag.CurrentMonthCommission = currentMonthCommission;

        // =========================================
        // Staff : Estimated Income (Current Month)
        // =========================================

        var estimatedIncome = CalculateEstimatedIncome(userId, staff?.Salary, currentMonthCommission) ?? 0.00;
        ViewBag.EstimatedIncome = estimatedIncome;

        // =====================================
        // Admin : Total Profit (Current Month)
        // =====================================

        var currentDate = DateTime.Now;
        var startOfMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

        var packageSales = db.Orders
                                .Include(o => o.OrderPackages)
                                .Where(o => o.Payment_date >= startOfMonth && o.Payment_date <= endOfMonth)
                                .SelectMany(o => o.OrderPackages)
                                .Sum(op => op.Price);

        var serviceCost = db.Appointments
                            .Include(a => a.OrderPackageTreatment)
                                .ThenInclude(opt => opt.OrderPackageTreatmentServices)
                            .Where(a => a.Type == 2 && a.Status == 3 && a.Created_datetime >= startOfMonth && a.Created_datetime <= endOfMonth)
                            .SelectMany(a => a.OrderPackageTreatment.OrderPackageTreatmentServices)
                            .Sum(opts => opts.Price);

        var commissionCost = db.Appointments
                                .Include(a => a.OrderPackageTreatment)
                                    .ThenInclude(opt => opt.OrderPackageTreatmentServices)
                                .Where(a => a.Type == 2 && a.Status == 3 && a.Created_datetime >= startOfMonth && a.Created_datetime <= endOfMonth)
                                .SelectMany(a => a.OrderPackageTreatment.OrderPackageTreatmentServices)
                                .Sum(opts => opts.Commission);

        var totalProfit = (double)(packageSales - (serviceCost + commissionCost));
        ViewBag.TotalProfit = totalProfit;

        // ==============================================
        // Admin : Estimated Salary Cost (Current Month)
        // ==============================================

        var basicSalary = db.Staffs
                                .Sum(s => s.Salary);

        var allCommissionCost = db.Appointments
                                .Include(a => a.OrderPackageTreatment)
                                    .ThenInclude(opt => opt.OrderPackageTreatmentServices)
                                .Where(a => a.Type == 2 && a.Status != 0 && a.Created_datetime >= startOfMonth && a.Created_datetime <= endOfMonth)
                                .SelectMany(a => a.OrderPackageTreatment.OrderPackageTreatmentServices)
                                .Sum(opts => opts.Commission);

        var estimatedSalaryCost = (double)(basicSalary + allCommissionCost);
        ViewBag.EstimatedSalaryCost = estimatedSalaryCost;

        // ==============================
        // Admin : Package Sold
        // ==============================

        var packagesSoldPerMonth = db.Orders
                                        .Include(o => o.OrderPackages)
                                        .SelectMany(o => o.OrderPackages, (o, op) => new { o.Payment_date, op })
                                        .GroupBy(x => new { Year = x.Payment_date.Year, Month = x.Payment_date.Month })
                                        .Select(g => new
                                        {
                                            Year = g.Key.Year,
                                            Month = g.Key.Month,
                                            PackageCount = g.Count()
                                        })
                                        .OrderBy(x => x.Year)
                                        .ThenBy(x => x.Month)
                                        .ToList();

        ViewBag.PackagesSoldPerMonth = packagesSoldPerMonth;

        var currentMonthPackageCount = db.Orders
                                            .Include(o => o.OrderPackages)
                                            .SelectMany(o => o.OrderPackages, (o, op) => new { o.Payment_date, op })
                                            .Where(x => x.Payment_date.Year == DateTime.Now.Year && x.Payment_date.Month == DateTime.Now.Month)
                                            .Count();

        ViewBag.CurrentMonthPackageCount = currentMonthPackageCount;

        // ==============================================
        // Admin : Total Pending Appointment (All Staff)
        // ==============================================

        var totalPendingAppointments = db.Appointments
                                            .Where(a => a.Status == 1 || a.Status == 2)
                                            .GroupBy(a => a.Type)
                                            .Select(g => new
                                            {
                                                Type = g.Key,
                                                TotalPending = g.Count()
                                            })
                                            .OrderBy(x => x.Type)
                                            .ToList();

        ViewBag.TotalPendingAppointments = totalPendingAppointments;

        var currentPendingAppointments = db.Appointments
                                                .Include(a => a.Staff)
                                                    .ThenInclude(s => s.Person)
                                                .Include(a => a.Member)
                                                    .ThenInclude(m => m.Person)
                                                .Where(a => a.Status == 1 || a.Status == 2)
                                                .OrderBy(a => a.Created_datetime)
                                                .ToList();

        ViewBag.CurrentPendingAppointments = currentPendingAppointments;

        // =================================================
        // Chart : Staff & Admin : Commission Summary Chart
        // =================================================

        ViewBag.CommissionChartTitle = "Weekly Earned Commission";

        // create current commission summary chart data of year and month
        int commission_year = DateTime.Now.Year;
        int commission_month = DateTime.Now.Month;

        // get commission chart labels
        string[] commissionChartLabels = GetWeekLabels(commission_year, commission_month);
        ViewBag.CommissionChartLabels = commissionChartLabels;

        // get commission chart data
        var weekly_commission = CalculateWeeklyCommission(userId, commission_year, commission_month);

        int daysInMonth = DateTime.DaysInMonth(commission_year, commission_month);
        int totalWeeks = (int)Math.Ceiling(daysInMonth / 7.0);

        string[] commissionChartData = Enumerable.Repeat("0", totalWeeks).ToArray();

        foreach (var commission in weekly_commission)
        {
            int weekOfMonth = commission.WeekOfMonth - 1;
            commissionChartData[weekOfMonth] = commission.WeeklyEarnedCommission.ToString("F2");
        }
        ViewBag.CommissionChartData = commissionChartData;

        // ======================================
        // Chart : Admin : Profit Summary Report
        // ======================================

        var monthlyProfit = CalculateMonthlyProfit(userId, DateTime.Now.Year);
        ViewBag.MonthlyProfit = monthlyProfit;

        ViewBag.ProfitChartTitle = "Weekly Earned Profit";

        // get commission chart labels
        string[] profitChartLabels = GetWeekLabels(DateTime.Now.Year, DateTime.Now.Month);
        ViewBag.ProfitChartLabels = profitChartLabels;

        // get commission chart data
        var weekly_profit = CalculateWeeklyProfit(userId, DateTime.Now.Year, DateTime.Now.Month);

        string[] profitChartData = Enumerable.Repeat("0", totalWeeks).ToArray();

        foreach (var profit in weekly_profit)
        {
            int weekOfMonth = profit.WeekOfMonth - 1;
            profitChartData[weekOfMonth] = profit.TotalProfit.ToString("F2");
        }
        ViewBag.ProfitChartData = profitChartData;

        // ======================================
        // Chart : Admin : Top 5 Package Sold
        // ======================================

        var top5PackageSold = CalculateTop5PackageSold();
        ViewBag.Top5PackageSold = top5PackageSold;

        ViewBag.PackageChartTitle = "Weekly Top 5 Package Sold";

        // get commission chart data
        var weekly_package = CalculateWeeklyTop5PackageSold(DateTime.Now.Year, DateTime.Now.Month);

        //string[] chartData = Enumerable.Repeat("0", totalWeeks).ToArray();
        string[] packageChartData = Enumerable.Repeat("0", 5).ToArray();
        string[] packageChartLabels = Enumerable.Repeat("", 5).ToArray();

        for (int i = 0; i < weekly_package.Count(); i++)
        {
            packageChartLabels[i] = weekly_package[i].PackageName;
            packageChartData[i] = weekly_package[i].TotalSold.ToString("F2");
        }
        ViewBag.PackageChartLabels = packageChartLabels;
        ViewBag.PackageChartData = packageChartData;

        // ======================================
        // List : Appointment Pending List
        // ======================================

        var appointmentPendingList = db.Appointments
                        .Include(a => a.Member)
                            .ThenInclude(m => m.Person)
                        .Where(a => (a.Status == 1 || a.Status == 2)
                                    && a.MemberId == userId
                                    && a.Start_datetime > DateTime.Now)
                        .OrderBy(a => a.Start_datetime)
                        .ToList();
        ViewBag.AppointmentPendingList = appointmentPendingList;

        // ====================================
        // Tools
        // ====================================

        List<SelectListItem> monthSelectItems = new List<SelectListItem>();
        var monthNames = DateTimeFormatInfo.CurrentInfo.MonthNames;

        for (int month = 0; month < 12; month++)
        {
            monthSelectItems.Add(new SelectListItem
            {
                Text = monthNames[month],
                Value = (month + 1).ToString()
            });
        }
        ViewBag.MonthSelectItem = monthSelectItems;

        List<SelectListItem> yearSelectItem = new List<SelectListItem>();

        for (int year = DateTime.Now.Year - 20; year <= DateTime.Now.Year; year++)
        {
            yearSelectItem.Add(new SelectListItem
            {
                Text = year.ToString(),
                Value = year.ToString(),
                Selected = year == DateTime.Now.Year
            });
        }
        ViewBag.YearSelectItem = yearSelectItem;

        return View("Staff/Dashboard");
    }

    [Route("/User/Member/Dashboard")]
    public IActionResult MemberDashboard(int? year, int? month, int? week)
    {
        // Check user authorization
        if (!hp.CheckRole(new string[] { "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var userId = HttpContext.Session.GetInt32("_userId");

        // ==================================
        // Meow Wallet Balance & Num of Referral(s)
        // ==================================
        var member = db.Members.FirstOrDefault(m => m.Id == userId);
        ViewBag.Member = member;

        // ==================================
        // Available Pacakge
        // ==================================
        var availablePackages = db.OrderPackages
                            .Where(s => s.Order.MemberId == userId &&
                                        s.Expired_datetime > DateTime.Now &&
                                        s.Remaining_treatment > 0)
                            .Count();
        ViewBag.AvailablePackages = availablePackages;

        // ==================================
        // Appointment Pending List
        // ==================================
        var pendingAppointments = db.Appointments
                                    .Include(a => a.Staff.Person)
                                    .Where(a => a.MemberId == userId && a.Status == 1);

        // ==================================
        // Appointment Status Summary Chart (Vertical Bar Chart)
        // ==================================
        //if(year != null && month != null && week != null) ViewBag.ChartTitle = $"{year} {month} Week {week} Appointment Status Summary Chart";
        //else if (year != null && month != null) ViewBag.ChartTitle = $"{year} {month} Appointment Status Summary Chart";
        //else ViewBag.ChartTitle = $"{year} Appointment Status Summary Chart";

        ViewBag.ChartLabel = new[] { "Cancelled", "Pending", "Ongoing", "Completed" };

        string[] appointmentStatusCount = GetAppointmentStatusCount(userId.Value, year, month, week);

        ViewBag.ChartData = appointmentStatusCount;


        // Return ajax
        if (Request.IsAjax())
        {
            return PartialView("Member/_AppointmentStatusSummaryChart");
        }

        return View("Member/Dashboard", pendingAppointments);

    }

    

    public IActionResult GenAndSendOtp(string email)
    {


        if (!hp.CheckRole(new string[] { "staff", "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // Decode the email
        string decodedEmail = HttpUtility.UrlDecode(email);

        // check if the Email is duplicated
        if (db.Persons.Any(p => p.Email == decodedEmail) || !hp.IsValidEmail(decodedEmail))
        {
            ModelState.AddModelError("Person.Email", "Duplicated Email or Email Format Invalid.");
            return RedirectToAction("Profile");
        }

        hp.GenerateAndSendOTP(email);

        HttpContext.Session.SetString("email", decodedEmail);

        TempData["Success_Title"] = "Verification Email Sended";
        TempData["Alert_Success"] = "An OTP has been sent to your email. Please verify your email to complete registration.";
        return RedirectToAction("VerifyEmail", "Login");
    }

    public IActionResult CapturePhoto()
    {
        if (!hp.CheckRole(new string[] { "staff", "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CapturePhoto(string image)
    {
        var UserRole = HttpContext.Session.GetString("_userRole");

        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff", "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        if (string.IsNullOrEmpty(image))
        {
            return Json(new { success = false, message = "No image data provided." });
        }

        try
        {
            // Decode the base64 string
            var base64Data = image.Substring(image.IndexOf(",") + 1);
            var imageBytes = Convert.FromBase64String(base64Data);

            // Specify the folder where the file should be saved
            string folder = "images/user/temp";

            // Generate a unique file name
            var fileName = Guid.NewGuid().ToString("n") + ".jpg";

            // Get the full local file path
            var filePath = Path.Combine(en.WebRootPath, folder, fileName);

            // Save the image to the file system
            await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

            TempData["capture_photo"] = fileName;
            TempData["Warning_Title"] = "Capture Success";
            TempData["Alert_Warning"] = "Photo capture success. Remember to click \"Save\" to update your profile.";

            return Json(new { success = true, photo = fileName, filePath = $"{folder}/{fileName}" });
        }
        catch (Exception ex)
        {
            // Handle any errors
            return Json(new { success = false, message = $"Error: {ex.Message}" });
        }
    }

    public IActionResult CommissionHistory(DateTime? start_date, DateTime? end_date, string? sort, string? dir, int page_items = 7, int page = 1)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var UserId = HttpContext.Session.GetInt32("_userId");

        if (UserId == null)
        {
            return RedirectToAction("Login", "Account");
        }

        // Validate and adjust the date range
        if (start_date.HasValue && end_date.HasValue && end_date < start_date)
        {
            // If end_date is earlier than start_date, set end_date to null
            end_date = null;
        }

        // Filter: Adjust date range filtering
        ViewBag.StartDate = start_date;
        ViewBag.EndDate = end_date;

        // Create the base query for commission history
        var commissionHistoryQuery = db.CommissionHistories
            .Where(ch => ch.StaffId == UserId)
            .AsQueryable();

        // Apply date range filter based on the requirements
        // Adjust start_date to include the full day (00:00:00)
        if (start_date.HasValue && !end_date.HasValue)
        {
            var startOfDay = start_date.Value.Date; // Set the time to 00:00:00
            commissionHistoryQuery = commissionHistoryQuery.Where(ch => ch.Created_datetime >= startOfDay);
        }
        // Adjust end_date to include the full day (23:59:59)
        else if (!start_date.HasValue && end_date.HasValue)
        {
            var endOfDay = end_date.Value.Date.AddDays(1).AddTicks(-1); // Set the time to 23:59:59.9999999
            commissionHistoryQuery = commissionHistoryQuery.Where(ch => ch.Created_datetime <= endOfDay);
        }
        else if (start_date.HasValue && end_date.HasValue)
        {
            var startOfDay = start_date.Value.Date; // Set the time to 00:00:00
            var endOfDay = end_date.Value.Date.AddDays(1).AddTicks(-1); // Set the time to 23:59:59.9999999
            commissionHistoryQuery = commissionHistoryQuery.Where(ch => ch.Created_datetime >= startOfDay && ch.Created_datetime <= endOfDay);
        }


        // Select commission details
        var commissionHistoryWithDetails = commissionHistoryQuery
            .Select(ch => new CommissionHistoryDetail
            {
                AppointmentId = db.Appointments
                    .Where(a => a.Status == 3
                                && a.StaffId == UserId
                                && a.OrderPackageTreatmentId == ch.OrderPackageTreatmentService.OrderPackageTreatmentId)
                    .Select(a => a.Id)
                    .FirstOrDefault(),
                OrderServiceId = ch.OrderPackageTreatmentService.Id,
                OrderServiceName = ch.OrderPackageTreatmentService.Service.Name,
                CommissionAmount = ch.OrderPackageTreatmentService.Commission,
                CommissionBalance = ch.Total_commission,
                CommissionDate = ch.Created_datetime
            })
            .ToList();

        // Sorting logic
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<CommissionHistoryDetail, object> fn = sort switch
        {
            "Appointment ID" => chd => chd.AppointmentId,
            "Service" => chd => chd.OrderServiceName,
            "Commission (RM)" => chd => chd.CommissionAmount,
            "Commission Balance (RM)" => chd => chd.CommissionBalance,
            "Received Date" => chd => chd.CommissionDate,
            _ => chd => chd.CommissionDate,
        };

        var sorted = dir == "desc"
            ? commissionHistoryWithDetails.OrderByDescending(fn).ToList()
            : commissionHistoryWithDetails.OrderBy(fn).ToList();

        // Paging
        if (page < 1)
        {
            return RedirectToAction(null, new { start_date, end_date, sort, dir, page_items, page = 1 });
        }

        ViewBag.Page_items = page_items;
        var chd = sorted.ToPagedList(page, page_items);

        if (page > chd.PageCount && chd.PageCount > 0)
        {
            return RedirectToAction(null, new { start_date, end_date, sort, dir, page_items, page = chd.PageCount });
        }

        // Check whether it is an AJAX request
        if (Request.IsAjax())
        {
            return PartialView("Staff/_CommissionHistory", chd);
        }

        return View("Staff/CommissionHistory", chd);
    }


    // Validation Method
    public JsonResult checkCurrentPassword(string password)
    {
        var UserPersonId = HttpContext.Session.GetInt32("_userPersonId");

        var u = db.Persons.FirstOrDefault(u => u.Id == UserPersonId);

        if (!hp.VerifyPassword(u.Password, password))
        {

            return Json(new { success = false, error = "Current_password", message = "Current password not match" });
        }

        return Json(new { success = true });
    }


    // =====================
    //  Chart Generator Action
    // =====================
    // generate commission summary chart labels and data
    [Route("/User/Staff/CommissionSummaryChart")]
    public IActionResult CommissionSummaryChart(string commission_viewby, string commission_year_month, string commission_year)
    {
        // Check user authorization
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var userId = HttpContext.Session.GetInt32("_userId");

        if (userId == null)
        {
            return RedirectToAction("Home", "Home");
        }

        if (commission_viewby == "month")
        {
            int year = int.TryParse(commission_year_month.Split("-")[0], out int year_result) ? year_result : 0;
            int month = int.TryParse(commission_year_month.Split("-")[1], out int month_result) ? month_result : 0;

            if (year == 0 || month == 0)
            {
                // return error message
                TempData["Error_Title"] = "Commission Summary Chart Failed";
                TempData["Alert_Error"] = "Invalid commission summary data";
                return Redirect(Request.Headers.Referer.ToString());
            }

            ViewBag.CommissionChartTitle = "Weekly Earned Commission";

            // get commission chart labels
            string[] chartLabels = GetWeekLabels(year, month);
            ViewBag.CommissionChartLabels = chartLabels;

            // get commission chart data
            var weekly_commission = CalculateWeeklyCommission(userId, year, month);

            int daysInMonth = DateTime.DaysInMonth(year, month);
            int totalWeeks = (int)Math.Ceiling(daysInMonth / 7.0);

            string[] chartData = Enumerable.Repeat("0", totalWeeks).ToArray();

            foreach (var commission in weekly_commission)
            {
                int weekOfMonth = commission.WeekOfMonth - 1;
                chartData[weekOfMonth] = commission.WeeklyEarnedCommission.ToString("F2");
            }
            ViewBag.CommissionChartData = chartData;

            return PartialView("Staff/_CommissionSummaryChart");
        }
        else if (commission_viewby == "year")
        {
            int year = int.TryParse(commission_year, out int year_result) ? year_result : 0;

            if (year == 0)
            {
                // return error message
                TempData["Error_Title"] = "Commission Summary Chart Failed";
                TempData["Alert_Error"] = "Invalid commission summary data";
                return Redirect(Request.Headers.Referer.ToString());
            }

            ViewBag.CommissionChartTitle = "Monthly Earned Commission";

            // get commission chart labels
            string[] chartLabels = {
                                        "Jan", "Feb", "Mar", "Apr", "May", "Jun",
                                        "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
                                    };
            ViewBag.CommissionChartLabels = chartLabels;

            // get commission chart data
            var monthly_commission = CalculateMonthlyCommission(userId, year);

            string[] chartData = new string[12];

            foreach (var commission in monthly_commission)
            {
                int monthIndex = commission.Month - 1;
                chartData[monthIndex] = commission.MonthlyEarnedCommission.ToString("F2");
            }
            ViewBag.CommissionChartData = chartData;

            return PartialView("Staff/_CommissionSummaryChart");
        }
        else
        {
            // return error message
            TempData["Error_Title"] = "Commission Summary Chart Failed";
            TempData["Alert_Error"] = "Invalid commission summary data";
            return Redirect(Request.Headers.Referer.ToString());
        }
    }

    // generate profit summary chart labels and data
    [Route("/User/Staff/ProfitSummaryChart")]
    public IActionResult ProfitSummaryChart(string profit_viewby, string profit_year_month, string profit_year)
    {
        // Check user authorization
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var userId = HttpContext.Session.GetInt32("_userId");

        if (userId == null)
        {
            return RedirectToAction("Home", "Home");
        }

        if (profit_viewby == "month")
        {
            int year = int.TryParse(profit_year_month.Split("-")[0], out int year_result) ? year_result : 0;
            int month = int.TryParse(profit_year_month.Split("-")[1], out int month_result) ? month_result : 0;

            if (year == 0 || month == 0)
            {
                // return error message
                TempData["Error_Title"] = "Profit Summary Chart Failed";
                TempData["Alert_Error"] = "Invalid profit summary data";
                return Redirect(Request.Headers.Referer.ToString());
            }

            ViewBag.ProfitChartTitle = "Weekly Earned Profit";

            // get commission chart labels
            string[] chartLabels = GetWeekLabels(year, month);
            ViewBag.ProfitChartLabels = chartLabels;

            // get commission chart data
            var weekly_profit = CalculateWeeklyProfit(userId, year, month);

            int daysInMonth = DateTime.DaysInMonth(year, month);
            int totalWeeks = (int)Math.Ceiling(daysInMonth / 7.0);

            string[] chartData = Enumerable.Repeat("0", totalWeeks).ToArray();

            foreach (var profit in weekly_profit)
            {
                int weekOfMonth = profit.WeekOfMonth - 1;
                chartData[weekOfMonth] = profit.TotalProfit.ToString("F2");
            }
            ViewBag.ProfitChartData = chartData;

            return PartialView("Staff/_ProfitSummaryChart");
        }
        else if (profit_viewby == "year")
        {
            int year = int.TryParse(profit_year, out int year_result) ? year_result : 0;

            if (year == 0)
            {
                // return error message
                TempData["Error_Title"] = "Profit Summary Chart Failed";
                TempData["Alert_Error"] = "Invalid profit summary data";
                return Redirect(Request.Headers.Referer.ToString());
            }

            ViewBag.ProfitChartTitle = "Monthly Earned Profit";

            // get profit chart labels
            string[] chartLabels = {
                                        "Jan", "Feb", "Mar", "Apr", "May", "Jun",
                                        "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
                                    };
            ViewBag.ProfitChartLabels = chartLabels;

            // get profit chart data
            var monthly_profit = CalculateMonthlyProfit(userId, year);

            string[] chartData = new string[12];

            foreach (var profit in monthly_profit)
            {
                int monthIndex = profit.Month - 1;
                chartData[monthIndex] = profit.TotalProfit.ToString("F2");
            }
            ViewBag.ProfitChartData = chartData;

            return PartialView("Staff/_ProfitSummaryChart");
        }
        else
        {
            // return error message
            TempData["Error_Title"] = "Profit Summary Chart Failed";
            TempData["Alert_Error"] = "Invalid profit summary data";
            return Redirect(Request.Headers.Referer.ToString());
        }
    }

    [Route("/User/Staff/Top5PackageSoldChart")]
    public IActionResult Top5PackageSoldChart(string package_viewby, string package_year_month, string package_year)
    {
        // Check user authorization
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var userId = HttpContext.Session.GetInt32("_userId");

        if (userId == null)
        {
            return RedirectToAction("Home", "Home");
        }

        if (package_viewby == "month")
        {
            int year = int.TryParse(package_year_month.Split("-")[0], out int year_result) ? year_result : 0;
            int month = int.TryParse(package_year_month.Split("-")[1], out int month_result) ? month_result : 0;

            if (year == 0 || month == 0)
            {
                // return error message
                TempData["Error_Title"] = "Package Summary Chart Failed";
                TempData["Alert_Error"] = "Invalid package summary data";
                return Redirect(Request.Headers.Referer.ToString());
            }

            ViewBag.PackageChartTitle = "Weekly Top 5 Package Sold";

            // get commission chart data
            var weekly_package = CalculateWeeklyTop5PackageSold(year, month);

            string[] chartData = Enumerable.Repeat("0", 5).ToArray();
            string[] chartLabels = Enumerable.Repeat("", 5).ToArray();

            for (int i = 0; i < weekly_package.Count(); i++)
            {
                chartLabels[i] = weekly_package[i].PackageName;
                chartData[i] = weekly_package[i].TotalSold.ToString("F2");
            }

            ViewBag.PackageChartData = chartData;
            ViewBag.PackageChartLabels = chartLabels;

            return PartialView("Staff/_Top5PackageSoldSummaryChart");
        }
        else if (package_viewby == "year")
        {
            int year = int.TryParse(package_year, out int year_result) ? year_result : 0;

            if (year == 0)
            {
                // return error message
                TempData["Error_Title"] = "Package Summary Chart Failed";
                TempData["Alert_Error"] = "Invalid package summary data";
                return Redirect(Request.Headers.Referer.ToString());
            }

            ViewBag.PackageChartTitle = "Monthly Top 5 Package Sold";

            // get commission chart data
            var monthly_package = CalculateMonthlyTop5PackageSold(year);

            string[] chartData = Enumerable.Repeat("0", 5).ToArray();
            string[] chartLabels = Enumerable.Repeat("", 5).ToArray();

            for (int i = 0; i < monthly_package.Count(); i++)
            {
                chartLabels[i] = monthly_package[i].PackageName;
                chartData[i] = monthly_package[i].TotalSold.ToString("F2");
            }

            ViewBag.PackageChartData = chartData;
            ViewBag.PackageChartLabels = chartLabels;

            return PartialView("Staff/_Top5PackageSoldSummaryChart");
        }
        else
        {
            // return error message
            TempData["Error_Title"] = "Package Summary Chart Failed";
            TempData["Alert_Error"] = "Invalid package summary data";
            return Redirect(Request.Headers.Referer.ToString());
        }
    }

    public string[] GetWeekLabels(int year, int month)
    {
        // Get the total number of days in the specified month
        int daysInMonth = DateTime.DaysInMonth(year, month);

        // List to hold week labels
        List<string> weekLabels = new List<string>();

        // Calculate the week numbers
        int currentWeek = 1;
        for (int day = 1; day <= daysInMonth; day++)
        {
            // Add a new week label at the start of each week
            if (day == 1 || (day - 1) % 7 == 0)
            {
                weekLabels.Add($"Week {currentWeek}");
                currentWeek++;
            }
        }

        return weekLabels.ToArray();
    }

    // ===================
    //  Helper Tools
    // ===================

    private int GetWeekOfMonth(DateTime date)
    {
        var firstDayOfMonth = new DateTime(date.Year, date.Month, 1);
        var firstWeek = ISOWeek.GetWeekOfYear(firstDayOfMonth);
        var currentWeek = ISOWeek.GetWeekOfYear(date);

        return currentWeek - firstWeek + 1; // Calculate the week of the month
    }

    // calculate commission of each month in a year
    public List<dynamic> CalculateMonthlyCommission(int? userId, int? year)
    {
        // Check if the year parameter is provided
        if (year == null)
        {
            throw new ArgumentException("Year is required.");
        }

        // Get commission histories for the specified user and year
        var commissionHistories = db.CommissionHistories
                                     .Where(ch => ch.StaffId == userId && ch.Created_datetime.Year == year)
                                     .OrderBy(ch => ch.Created_datetime)
                                     .ToList();

        decimal previousMonthCommission = 0;
        var monthlyCommissions = new List<dynamic>();

        // Get the unique months for the specified year based on the commission history records
        var months = commissionHistories
                     .GroupBy(ch => new { Year = ch.Created_datetime.Year, Month = ch.Created_datetime.Month })
                     .OrderBy(g => g.Key.Month)  // Sort by month within the specified year
                     .ToList();

        foreach (var monthGroup in months)
        {
            // Get the last commission record for the current month with the largest Total_commission
            var lastCommissionOfCurrentMonth = monthGroup
                .Where(ch => ch.Created_datetime == monthGroup.Max(m => m.Created_datetime))
                .OrderByDescending(ch => ch.Total_commission)
                .FirstOrDefault();

            if (lastCommissionOfCurrentMonth == null) continue;

            if (previousMonthCommission == 0) // First month case
            {
                // First month: Use the largest total commission of the last record
                monthlyCommissions.Add(new
                {
                    Year = lastCommissionOfCurrentMonth.Created_datetime.Year,
                    Month = lastCommissionOfCurrentMonth.Created_datetime.Month,
                    LastRecordTime = lastCommissionOfCurrentMonth.Created_datetime,
                    MonthlyEarnedCommission = lastCommissionOfCurrentMonth.Total_commission
                });
            }
            else
            {
                // Subtract the previous month's largest commission from the current month's largest commission
                var monthlyEarnedCommission = lastCommissionOfCurrentMonth.Total_commission - previousMonthCommission;
                monthlyCommissions.Add(new
                {
                    Year = lastCommissionOfCurrentMonth.Created_datetime.Year,
                    Month = lastCommissionOfCurrentMonth.Created_datetime.Month,
                    LastRecordTime = lastCommissionOfCurrentMonth.Created_datetime,
                    MonthlyEarnedCommission = monthlyEarnedCommission
                });
            }

            // Update the previous month's commission for the next iteration
            previousMonthCommission = lastCommissionOfCurrentMonth.Total_commission;
        }

        return monthlyCommissions;
    }

    // calculate commission of each week in a month
    public List<dynamic> CalculateWeeklyCommission(int? userId, int? year = null, int? month = null)
    {
        var commissionHistories = db.CommissionHistories
            .Where(ch => ch.StaffId == userId)
            .OrderBy(ch => ch.Created_datetime)
            .ToList();

        decimal previousWeekCommission = 0;
        var weeklyCommissions = new List<dynamic>();

        // Group by year, month, and week of the month
        var weeks = commissionHistories
            .GroupBy(ch => new
            {
                Year = ch.Created_datetime.Year,
                Month = ch.Created_datetime.Month,
                WeekOfMonth = GetWeekOfMonth(ch.Created_datetime)
            })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month)
            .ThenBy(g => g.Key.WeekOfMonth)
            .ToList();

        foreach (var weekGroup in weeks)
        {
            // Get the last commission record for the current week with the largest Total_commission
            var lastCommissionOfCurrentWeek = weekGroup
                .Where(ch => ch.Created_datetime == weekGroup.Max(m => m.Created_datetime))
                .OrderByDescending(ch => ch.Total_commission)
                .FirstOrDefault();

            if (lastCommissionOfCurrentWeek == null) continue;

            if (previousWeekCommission == 0) // First week case
            {
                // First week: Use the largest total commission of the last record
                weeklyCommissions.Add(new
                {
                    Year = lastCommissionOfCurrentWeek.Created_datetime.Year,
                    Month = lastCommissionOfCurrentWeek.Created_datetime.Month,
                    WeekOfMonth = GetWeekOfMonth(lastCommissionOfCurrentWeek.Created_datetime),
                    LastRecordTime = lastCommissionOfCurrentWeek.Created_datetime,
                    WeeklyEarnedCommission = lastCommissionOfCurrentWeek.Total_commission
                });
            }
            else
            {
                // Subtract the previous week's largest commission from the current week's largest commission
                var weeklyEarnedCommission = lastCommissionOfCurrentWeek.Total_commission - previousWeekCommission;
                weeklyCommissions.Add(new
                {
                    Year = lastCommissionOfCurrentWeek.Created_datetime.Year,
                    Month = lastCommissionOfCurrentWeek.Created_datetime.Month,
                    WeekOfMonth = GetWeekOfMonth(lastCommissionOfCurrentWeek.Created_datetime),
                    LastRecordTime = lastCommissionOfCurrentWeek.Created_datetime,
                    WeeklyEarnedCommission = weeklyEarnedCommission
                });
            }

            // Update the previous week's commission for the next iteration
            previousWeekCommission = lastCommissionOfCurrentWeek.Total_commission;
        }

        // Filter the results by year and month if specified
        if (year.HasValue)
        {
            weeklyCommissions = weeklyCommissions
                .Where(wc => wc.Year == year.Value)
                .ToList();
        }

        if (month.HasValue)
        {
            weeklyCommissions = weeklyCommissions
                .Where(wc => wc.Month == month.Value)
                .ToList();
        }

        return weeklyCommissions;
    }

    public double? CalculateEstimatedIncome(int? userId, decimal? salary = 0.00m, decimal? currentDoneCommission = 0.00m)
    {
        var pendingAppointmentService = db.Appointments
                                            .Include(a => a.Staff)
                                            .Include(a => a.OrderPackageTreatment)
                                                .ThenInclude(opt => opt.OrderPackageTreatmentServices)
                                            .Where(a => a.Staff.Id == userId
                                                    && a.Type == 2
                                                    && (a.Status == 1 || a.Status == 2))
                                            .ToList();

        var currentPendingCommission = 0.00;

        foreach (var pendingApp in pendingAppointmentService)
        {
            foreach (var service in pendingApp.OrderPackageTreatment.OrderPackageTreatmentServices)
            {
                currentPendingCommission += (double)service.Commission;
            }
        }

        var estimatedIncome = (double?)salary + (double?)currentDoneCommission + currentPendingCommission;

        return estimatedIncome;
    }

    // calculate profit of each month in a year
    public List<dynamic> CalculateMonthlyProfit(int? userId, int year)
    {
        // Specify the start and end of the year based on the provided year
        var startOfYear = new DateTime(year, 1, 1); // First day of the provided year
        var endOfYear = new DateTime(year, 12, 31); // Last day of the provided year

        // =====================================
        // Admin : Total Profit (Grouped by Year and Month)
        // =====================================

        // Get total package sales for the specified year, grouped by month
        var packageSalesByMonth = db.Orders
            .Include(o => o.OrderPackages)
            .Where(o => o.Payment_date >= startOfYear && o.Payment_date <= endOfYear) // Filter for the specified year
            .GroupBy(o => new { o.Payment_date.Year, o.Payment_date.Month }) // Group by Year and Month
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                TotalPackageSales = g.SelectMany(o => o.OrderPackages)
                                     .Sum(op => op.Price)
            })
            .ToList();

        // Get total service cost for the specified year, grouped by month
        var serviceCostByMonth = db.Appointments
            .Include(a => a.OrderPackageTreatment)
                .ThenInclude(opt => opt.OrderPackageTreatmentServices)
            .Where(a => a.Type == 2 && a.Status == 3 && a.Created_datetime >= startOfYear && a.Created_datetime <= endOfYear) // Filter for the specified year
            .GroupBy(a => new { a.Created_datetime.Year, a.Created_datetime.Month }) // Group by Year and Month
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                TotalServiceCost = g.SelectMany(a => a.OrderPackageTreatment.OrderPackageTreatmentServices)
                                    .Sum(opts => opts.Price)
            })
            .ToList();

        // Get total commission cost for the specified year, grouped by month
        var commissionCostByMonth = db.Appointments
            .Include(a => a.OrderPackageTreatment)
                .ThenInclude(opt => opt.OrderPackageTreatmentServices)
            .Where(a => a.Type == 2 && a.Status == 3 && a.Created_datetime >= startOfYear && a.Created_datetime <= endOfYear) // Filter for the specified year
            .GroupBy(a => new { a.Created_datetime.Year, a.Created_datetime.Month }) // Group by Year and Month
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                TotalCommissionCost = g.SelectMany(a => a.OrderPackageTreatment.OrderPackageTreatmentServices)
                                       .Sum(opts => opts.Commission)
            })
            .ToList();

        // Combine the results by Year and Month to calculate total profit for each month
        var totalProfitByMonth = packageSalesByMonth
            .Join(serviceCostByMonth, ps => new { ps.Year, ps.Month }, sc => new { sc.Year, sc.Month }, (ps, sc) => new { ps.Year, ps.Month, ps.TotalPackageSales, sc.TotalServiceCost })
            .Join(commissionCostByMonth, combined => new { combined.Year, combined.Month }, cc => new { cc.Year, cc.Month }, (combined, cc) => new
            {
                Year = combined.Year,
                Month = combined.Month,
                TotalProfit = combined.TotalPackageSales - (combined.TotalServiceCost + cc.TotalCommissionCost)
            })
            .ToList();

        // Explicitly cast the result to List<dynamic> for compatibility
        return totalProfitByMonth.Select(item => new
        {
            item.Year,
            item.Month,
            item.TotalProfit
        }).Cast<dynamic>().ToList();
    }

    // calculate profit of each week in a month
    public List<dynamic> CalculateWeeklyProfit(int? userId, int year, int month)
    {
        var startOfMonth = new DateTime(year, month, 1); // First day of the specified month
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1); // Last day of the specified month

        // Get total package sales for the specified year and month
        var packageSalesByWeek = db.Orders
            .Include(o => o.OrderPackages)
            .Where(o => o.Payment_date >= startOfMonth && o.Payment_date <= endOfMonth) // Filter by the specific month
            .AsEnumerable() // Move to client-side
            .GroupBy(o => new
            {
                o.Payment_date.Year,
                o.Payment_date.Month,
                WeekOfMonth = GetWeekOfMonth(o.Payment_date) // Use the custom method after retrieving the data
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.WeekOfMonth,
                TotalPackageSales = g.SelectMany(o => o.OrderPackages)
                                     .Sum(op => op.Price)
            })
            .ToList();

        // Get total service cost for the specified year and month
        var serviceCostByWeek = db.Appointments
            .Include(a => a.OrderPackageTreatment)
                .ThenInclude(opt => opt.OrderPackageTreatmentServices)
            .Where(a => a.Type == 2 && a.Status == 3 && a.Created_datetime >= startOfMonth && a.Created_datetime <= endOfMonth) // Filter by the specific month
            .AsEnumerable() // Move to client-side
            .GroupBy(a => new
            {
                a.Created_datetime.Year,
                a.Created_datetime.Month,
                WeekOfMonth = GetWeekOfMonth(a.Created_datetime) // Use the custom method after retrieving the data
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.WeekOfMonth,
                TotalServiceCost = g.SelectMany(a => a.OrderPackageTreatment.OrderPackageTreatmentServices)
                                    .Sum(opts => opts.Price)
            })
            .ToList();

        // Get total commission cost for the specified year and month
        var commissionCostByWeek = db.Appointments
            .Include(a => a.OrderPackageTreatment)
                .ThenInclude(opt => opt.OrderPackageTreatmentServices)
            .Where(a => a.Type == 2 && a.Status == 3 && a.Created_datetime >= startOfMonth && a.Created_datetime <= endOfMonth) // Filter by the specific month
            .AsEnumerable() // Move to client-side
            .GroupBy(a => new
            {
                a.Created_datetime.Year,
                a.Created_datetime.Month,
                WeekOfMonth = GetWeekOfMonth(a.Created_datetime) // Use the custom method after retrieving the data
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.WeekOfMonth,
                TotalCommissionCost = g.SelectMany(a => a.OrderPackageTreatment.OrderPackageTreatmentServices)
                                       .Sum(opts => opts.Commission)
            })
            .ToList();

        // Combine the results by Year, Month, and Week to calculate total profit for each week
        var totalProfitByWeek = packageSalesByWeek
            .Join(serviceCostByWeek, ps => new { ps.Year, ps.Month, ps.WeekOfMonth }, sc => new { sc.Year, sc.Month, sc.WeekOfMonth }, (ps, sc) => new { ps.Year, ps.Month, ps.WeekOfMonth, ps.TotalPackageSales, sc.TotalServiceCost })
            .Join(commissionCostByWeek, combined => new { combined.Year, combined.Month, combined.WeekOfMonth }, cc => new { cc.Year, cc.Month, cc.WeekOfMonth }, (combined, cc) => new
            {
                combined.Year,
                combined.Month,
                combined.WeekOfMonth,
                TotalProfit = combined.TotalPackageSales - (combined.TotalServiceCost + cc.TotalCommissionCost)
            })
            .ToList();

        // Return the result for each week of the specified month
        return totalProfitByWeek.Select(item => new
        {
            item.Year,
            item.Month,
            item.WeekOfMonth,
            item.TotalProfit
        }).Cast<dynamic>().ToList();
    }


    public List<dynamic> CalculateTop5PackageSold()
    {
        var top5PackagesByMonth = db.Orders
                                    .Include(o => o.OrderPackages)
                                        .ThenInclude(op => op.Package) // Including Package information
                                            .ThenInclude(p => p.PackagePhotos)
                                    .SelectMany(o => o.OrderPackages) // Flatten the collection of OrderPackages
                                    .GroupBy(op => new
                                    {
                                        op.PackageId,
                                        op.Package.Name,
                                        Year = op.Order.Payment_date.Year,
                                        Month = op.Order.Payment_date.Month
                                    }) // Group by Year, Month, and Package Name
                                    .Select(g => new
                                    {
                                        Year = g.Key.Year,
                                        Month = g.Key.Month,
                                        PackageId = g.Key.PackageId,
                                        PackageName = g.Key.Name,
                                        PackagePhoto = g.SelectMany(op => op.Package.PackagePhotos)
                                                        .FirstOrDefault(pp => pp.Type == 1),
                                        TotalSold = g.Count() // Count how many times this package has been sold
                                    })
                                    .OrderByDescending(g => g.TotalSold) // Order by the count of total sold packages
                                    .Take(5) // Take top 5 packages sold
                                    .ToList<dynamic>();

        return top5PackagesByMonth;
    }

    public List<dynamic> CalculateMonthlyTop5PackageSold(int year)
    {
        // Step 1: Fetch raw data from the database
        var rawData = db.Orders
                        .Include(o => o.OrderPackages)
                            .ThenInclude(op => op.Package)
                        .Where(o => o.Payment_date.Year == year)
                        .SelectMany(o => o.OrderPackages.Select(op => new
                        {
                            PackageId = op.PackageId,
                            PackageName = op.Package.Name,
                            Month = o.Payment_date.Month,
                            TotalSold = 1 // Each record counts as 1 sale
                        }))
                        .ToList(); // Fetch data into memory

        // Step 2: Perform grouping and processing in memory
        var top5PackagesByYear = rawData
                                 .GroupBy(x => new { x.Month, x.PackageId, x.PackageName })
                                 .Select(g => new
                                 {
                                     Month = g.Key.Month,
                                     PackageId = g.Key.PackageId,
                                     PackageName = g.Key.PackageName,
                                     TotalSold = g.Sum(x => x.TotalSold)
                                 })
                                 .OrderByDescending(g => g.TotalSold) // Sort by sales
                                 .GroupBy(g => g.Month) // Group by month
                                 .SelectMany(monthGroup => monthGroup.Take(5)) // Take top 5 packages for each month
                                 .ToList<dynamic>(); // Convert to dynamic list for return

        return top5PackagesByYear;
    }


    public List<dynamic> CalculateWeeklyTop5PackageSold(int year, int month)
    {
        // Load relevant data into memory first
        var orders = db.Orders
                       .Include(o => o.OrderPackages)
                           .ThenInclude(op => op.Package)
                               .ThenInclude(p => p.PackagePhotos)
                       .Where(o => o.Payment_date.Year == year && o.Payment_date.Month == month)
                       .AsEnumerable(); // Switch to client-side evaluation

        // Perform the grouping and calculations on the client side
        var top5PackagesByWeek = orders
            .SelectMany(o => o.OrderPackages)
            .GroupBy(op => new
            {
                op.PackageId,
                op.Package.Name,
                WeekOfMonth = GetWeekOfMonth(op.Order.Payment_date), // Calculate the week of the month
                Year = op.Order.Payment_date.Year,
                Month = op.Order.Payment_date.Month
            })
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                WeekOfMonth = g.Key.WeekOfMonth,
                PackageId = g.Key.PackageId,
                PackageName = g.Key.Name,
                PackagePhoto = g.SelectMany(op => op.Package.PackagePhotos)
                                .FirstOrDefault(pp => pp.Type == 1),
                TotalSold = g.Count() // Count how many times this package has been sold
            })
            .OrderByDescending(g => g.TotalSold)
            .Take(5) // Take top 5 packages sold
            .ToList<dynamic>();

        return top5PackagesByWeek;
    }

    public string[] GetAppointmentStatusCount(int userId, int? year, int? month, int? week)
    {
        DateTime startOfPeriod;
        DateTime endOfPeriod;

        // Case 1: When year, month, and week are provided
        if (year.HasValue && month.HasValue && week.HasValue)
        {
            DateTime firstDayOfMonth = new DateTime(year.Value, month.Value, 1);
            startOfPeriod = firstDayOfMonth.AddDays((week.Value - 1) * 7);

            // Check if the week extends beyond the end of the month
            DateTime lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddTicks(-1);
            DateTime calculatedEndOfWeek = startOfPeriod.AddDays(6);

            // Ensure endOfPeriod does not exceed the last day of the month
            endOfPeriod = calculatedEndOfWeek > lastDayOfMonth ? lastDayOfMonth : calculatedEndOfWeek;
            endOfPeriod = endOfPeriod.Date.AddDays(1).AddTicks(-1); // End of the day

            ViewBag.ChartTitle = $"{year} {Helpers.GetMonth(month)} Week {week} Appointment Status Summary Chart";
        }
        // Case 2: When year and month are provided, but week is null
        else if (year.HasValue && month.HasValue)
        {
            DateTime firstDayOfMonth = new DateTime(year.Value, month.Value, 1);
            startOfPeriod = firstDayOfMonth;
            endOfPeriod = firstDayOfMonth.AddMonths(1).AddTicks(-1); // End of the month
            ViewBag.ChartTitle = $"{year} {Helpers.GetMonth(month)} Appointment Status Summary Chart";
        }
        // Case 3: When only year is provided, and month and week are null
        else if (year.HasValue)
        {
            startOfPeriod = new DateTime(year.Value, 1, 1); // Start of the year
            endOfPeriod = new DateTime(year.Value, 12, 31, 23, 59, 59, 999); // End of the year
            ViewBag.ChartTitle = $"{year} Appointment Status Summary Chart";
        }
        // Case 4: When all are null, use the current month
        else
        {
            DateTime currentDate = DateTime.Now;
            startOfPeriod = new DateTime(currentDate.Year, currentDate.Month, 1);
            endOfPeriod = startOfPeriod.AddMonths(1).AddTicks(-1); // End of the current month
            ViewBag.ChartTitle = $"Current Month Appointment Status Summary Chart";
        }

        // Query to filter and group appointments based on the calculated period
        var appointmentCounts = db.Appointments
            .Where(a =>
                a.MemberId == userId &&
                a.Start_datetime >= startOfPeriod &&
                a.Start_datetime <= endOfPeriod)
            .GroupBy(a => a.Status)
            .Select(g => new
            {
                Status = g.Key, // Status (0 = Cancelled, 1 = Pending, etc.)
                Count = g.Count() // Count of appointments in each status
            })
            .ToList();

        // Create a dictionary for default counts
        var statusCounts = new Dictionary<int, int>
        {
            { 0, 0 }, // Cancelled
            { 1, 0 }, // Pending
            { 2, 0 }, // Ongoing
            { 3, 0 }  // Completed
        };

        // Update the counts with actual data
        foreach (var item in appointmentCounts)
        {
            statusCounts[item.Status] = item.Count;
        }

        // Convert counts to a string array
        string[] result = statusCounts
            .OrderBy(kv => kv.Key) // Ensure the order is by status (0, 1, 2, 3)
            .Select(kv => kv.Value.ToString())
            .ToArray();

        return result;
    }

}
