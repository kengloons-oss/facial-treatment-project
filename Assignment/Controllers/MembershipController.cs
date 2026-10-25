using System.Linq;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using X.PagedList.Extensions;

namespace Assignment.Controllers;

public class MembershipController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helpers hp;

    public MembershipController(DB db, IWebHostEnvironment en, Helpers hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
    }

    // Admin Site CRUD
    public IActionResult List(int? level, decimal? min_expenseStart, decimal? min_expenseEnd, decimal? discount_rateStart, decimal? discount_rateEnd, string? sort, string? dir, int page_items = 7, int page = 1)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        ViewBag.UserPosition = HttpContext.Session.GetString("_userPosition");

        // Fetch the list from the database
        var levelList = db.Memberships
                          .Where(m => m.Status == 1)
                          .Select(m => new SelectListItem
                          {
                              Value = m.Id.ToString(), // The key
                              Text = $"{m.Level}" // The display value
                          })
                          .ToList();

        // Insert "All" option at the first position
        levelList.Insert(0, new SelectListItem
        {
            Value = null, // Use an appropriate value to represent "All"
            Text = "All"
        });

        // Pass the levelList to the ViewBag to use in the view
        ViewBag.LevelList = levelList;

        // (1) Searching ------------------------
        ViewBag.Level = level;
        ViewBag.Expense_Start = min_expenseStart;
        ViewBag.Expense_End = min_expenseEnd;
        ViewBag.Discount_Start = discount_rateStart;
        ViewBag.Discount_End = discount_rateEnd;

        var searched = db.Memberships.Where(s =>
                        (level == null || s.Level == level) &&
                        (min_expenseStart == null && min_expenseEnd != null ? s.Min_expense <= min_expenseEnd : true) && // Case 1
                        (min_expenseEnd == null && min_expenseStart != null ? s.Min_expense >= min_expenseStart : true) && // Case 2
                        (min_expenseStart != null && min_expenseEnd != null ?
                            (s.Min_expense >= min_expenseStart && s.Min_expense <= min_expenseEnd) : true) && // Case 3
                        (discount_rateStart == null && discount_rateEnd != null ? s.Discount_rate <= discount_rateEnd : true) && // Case 4
                        (discount_rateEnd == null && discount_rateStart != null ? s.Discount_rate >= discount_rateStart : true) && // Case 5
                        (discount_rateStart != null && discount_rateEnd != null ?
                            (s.Discount_rate >= discount_rateStart && s.Discount_rate <= discount_rateEnd) : true) && // Case 6
                        s.Status == 1);

        // (2) Sorting --------------------------
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<Membership, object> fn = sort switch
        {
            "Id" => s => s.Id,
            "Level" => s => s.Level,
            "Icon" => s => s.Icon,
            "Min Expense (RM)" => s => s.Min_expense,
            "Discount Rate (%)" => s => s.Discount_rate,
            "Status" => s => s.Status,
            _ => s => s.Id,
        };

        var sorted = dir == "des" ?
                     searched.OrderByDescending(fn) :
                     searched.OrderBy(fn);

        // (3) Paging ---------------------------
        if (page < 1)
        {
            return RedirectToAction(null, new { level, min_expenseStart, min_expenseEnd, discount_rateStart, discount_rateEnd, sort, dir, page_items, page = 1 });
        }

        // filter items per page
        ViewBag.Page_items = page_items;

        var m = sorted.ToPagedList(page, page_items);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { level, min_expenseStart, min_expenseEnd, discount_rateStart, discount_rateEnd, sort, dir, page_items, page = m.PageCount });
        }

        if (Request.IsAjax())
        {
            return PartialView("Staff/_List", m);
        }

        
        return View("Staff/List", m);
    }

    // Get: Membership/Create
    public IActionResult Create()
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

        ViewBag.MembershipList = new SelectList(db.Memberships, "Id", "Name");
        return View("Staff/Create");
    }

    // Post: Membership/Post
    [HttpPost]
    public IActionResult Create(MembershipVM vm)
    {

        // check if the Email is valid
        if (ModelState.IsValid("Level"))
        {
            // check if the Email is duplicated
            if (db.Memberships.Any(m => m.Level == vm.Level && m.Status == 1))
            {
                ModelState.AddModelError("Level", "Duplicated Level.");
            }
        }

        // check if the Icon is null or valid
        if (vm.Upload_image != null)
        {
            var error = hp.ValidateImage(vm.Upload_image);
            if (error != "") ModelState.AddModelError("Upload_image", error);
        }

        // check whether photo is uploaded
            if (vm.Upload_image != null)
        {
            vm.Icon = hp.SavePhoto(vm.Upload_image, "images/membership").Result;
        }

        // validate entire model
        if (ModelState.IsValid)
        {
            var membership = new Membership
            {
                Level = vm.Level,
                Min_expense = vm.Min_expense,
                Discount_rate = vm.Discount_rate,
                Icon = vm.Icon,
                Status = 1,
            };

            db.Memberships.Add(membership);
            db.SaveChanges();

            TempData["Success_Title"] = "Membership Create Success";
            TempData["Alert_Success"] = "Membership is created successfully.";
            return RedirectToAction("List");
        }

        return View("Staff/Create", vm);
    }

    // Get: Membership/Detail
    public IActionResult Detail(int id)
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

        var m = db.Memberships.FirstOrDefault(m => m.Id == id && m.Status == 1);

        if (m == null)
        {
            TempData["Error_Title"] = "Action Failed";
            TempData["Alert_Error"] = "Membership not found.";
            return RedirectToAction("List");
        }

        var vm = new MembershipVM
        {
            Id = id,
            Level = m.Level,
            Min_expense= m.Min_expense,
            Discount_rate = m.Discount_rate,
            Icon = m.Icon,
        };

        return View("Staff/Detail", vm);
    }

    // Post: Membership/Detail
    [HttpPost]
    public IActionResult Detail(MembershipVM vm)
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

        var m = db.Memberships.FirstOrDefault(m => m.Id == vm.Id && m.Status == 1);
        if (m == null)
        {
            TempData["Error_Title"] = "Action Failed";
            TempData["Alert_Error"] = "Membership not found.";
            return RedirectToAction("List");
        }

        // check if the Level is valid
        if (ModelState.IsValid("Level"))
        {
            // check if the Email is duplicated
            if (m.Level != vm.Level && db.Memberships.Any(m => m.Level == vm.Level && m.Status == 1))
            {
                ModelState.AddModelError("Level", "Duplicated Level.");
            }
        }

        // check if the Icon is null or valid
        if (vm.Upload_image != null)
        {
            var error = hp.ValidateImage(vm.Upload_image);
            if (error != "") ModelState.AddModelError("Upload_image", error);
        };

        // validate entire model
        if (ModelState.IsValid)
        {
            m.Level = vm.Level;
            m.Min_expense = vm.Min_expense;
            m.Discount_rate = vm.Discount_rate;

            if (vm.Upload_image != null)
            {
                if (m.Icon != null) hp.DeletePhoto(m.Icon, "images/membership");
                m.Icon = hp.SavePhoto(vm.Upload_image, "images/membership").Result;
            }

            db.SaveChanges();

            TempData["Success_Title"] = "Membership Update Success";
            TempData["Alert_Success"] = "Membership is update successfully.";
            return RedirectToAction("List");
        }

        TempData["Error_Title"] = "Membership Update Failed";
        TempData["Alert_Error"] = "Membership is update failed. Try to check your input update again later.";
        return View("Staff/Detail", vm);
    }

    // Deactivate Membership Level
    public IActionResult Deactivate(int? id)
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

        var s = db.Memberships.Find(id);

        if (s != null)
        {
            s.Status = 0;
            db.SaveChanges();

            TempData["Success_Title"] = "Deactivate Success";
            TempData["Alert_Success"] = "The membership level has been deactivated.";
        }
        else
        {
            TempData["Error_Title"] = "Deactivate Failed";
            TempData["Alert_Error"] = "The membership level does not exist.";
        }

        return Redirect(Request.Headers.Referer.ToString());
    }

    // Deactivate Membership Level
    public IActionResult DeactivateMany(int[] ids)
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

        // Update the Status for all matching Memberships
        int updatedRows = db.Memberships
            .Where(m => ids.Contains(m.Id))
            .ExecuteUpdate(m => m.SetProperty(m => m.Status, 0));

        // Prepare TempData based on the operation
        if (updatedRows > 0)
        {
            TempData["Success_Title"] = "Deactivate Success";
            TempData["Alert_Success"] = $"{updatedRows} membership level(s) have been deactivated.";
        }
        else
        {
            TempData["Error_Title"] = "Deactivate Failed";
            TempData["Alert_Error"] = "No membership levels were found to deactivate.";
        }

        // Redirect back to the previous page
        return Redirect(Request.Headers.Referer.ToString());
    }

    // Validation method
    public JsonResult CheckLevelExist(int level)
    {
        if (db.Memberships.Any(m => m.Level == level && m.Status == 1))
        {
            return Json(new { success = false, message = "Membership level already exist" });
        }

        return Json(new { success = true });
    }


}
