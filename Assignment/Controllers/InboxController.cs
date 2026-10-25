using System.Data;
using Assignment.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using X.PagedList.Extensions;

namespace Assignment.Controllers;

public class InboxController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helpers hp;

    public InboxController(DB db, IWebHostEnvironment en, Helpers hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
    }


    // Admin site CRUD
    // ---------------------
    public IActionResult List(string? title, string? filter_target, string? filter_status, string? sort, string? dir, int page_items = 7, int page = 1)
    {

        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // (1) Searching ------------------------
        ViewBag.Name = title = title?.Trim() ?? "";
        ViewBag.Target = filter_target;
        ViewBag.Status = filter_status;

        var searched = db.Inboxes.Where(s =>
                        s.Title.Contains(title) &&
                        (string.IsNullOrEmpty(filter_status) || s.Status.ToString() == filter_status) &&
                        (string.IsNullOrEmpty(filter_target) || s.Target.ToString() == filter_target) &&
                        s.Type == 1);

        // (2) Sorting --------------------------
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<Inbox, object> fn = sort switch
        {
            "Id" => s => s.Id,
            "Title" => s => s.Title,
            "Target" => s => s.Target,
            "Created_datetime" => s => s.Created_datetime,
            "Status" => s => s.Status,
            _ => s => s.Id,
        };

        var sorted = dir == "des" ?
                     searched.OrderByDescending(fn) :
                     searched.OrderBy(fn);

        // (3) Paging ---------------------------
        if (page < 1)
        {
            return RedirectToAction(null, new { title, filter_target, filter_status, sort, dir, page_items, page = 1 });
        }

        // filter items per page
        ViewBag.Page_items = page_items;

        var m = sorted.ToPagedList(page, page_items);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { title, filter_target, filter_status, sort, dir, page_items, page = m.PageCount });
        }

        if (Request.IsAjax())
        {
            return PartialView("Staff/_List", m);
        }

        return View("Staff/List", m);
    }

    // Get: Inbox/Create
    public IActionResult Create()
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        return View("Staff/Create");
    }

    // Post: Inbox/Create
    [HttpPost]
    public IActionResult Create(InboxVM vm)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        if (ModelState.IsValid)
        {
            db.Inboxes.Add(new()
            {
                Title = vm.Title.Trim(),
                Message = vm.Message.Trim(),
                Target = vm.Target,
                Type = 1,
                Status = 1
            });
            db.SaveChanges();

            TempData["Success_Title"] = "Create Success";
            TempData["Alert_Success"] = "The announcement has been created successfully.";
            return RedirectToAction("List");
        }
        else
        {
            TempData["Error_Title"] = "Create Failed";
            TempData["Alert_Error"] = "Failed to create announcement. Please try again later.";
        }

        return View("Staff/Create", vm);
    }

    // Get Inbox/Detail
    public IActionResult Detail(int id)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var s = db.Inboxes.FirstOrDefault(i => i.Id == id);

        if (s == null || s.Type != 1)
        {
            TempData["Error_Title"] = "Action Failed";
            TempData["Alert_Error"] = "Announcement Not Found";
            return RedirectToAction("List");
        }

        var vm = new InboxVM
        {
            Id = id,
            Title = s.Title.Trim(),
            Message = s.Message.Trim(),
            Target = (int)s.Target,
            Created_datetime = s.Created_datetime,
            Status = s.Status,
        };

        ViewBag.InboxList = new SelectList(db.Inboxes, "Id", "Name");
        return View("Staff/Detail", vm);
    }

    // Post Inbox/Detail
    [HttpPost]
    public IActionResult Detail(InboxVM vm)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var s = db.Inboxes.Find(vm.Id);

        if (s == null)
        {
            TempData["Error_Title"] = "Action Failed";
            TempData["Alert_Error"] = "Announcement Not Found";
            return RedirectToAction("List");
        }

        if (ModelState.IsValid)
        {
            s.Title = vm.Title;
            s.Message = vm.Message;
            s.Target = vm.Target;

            db.SaveChanges();

            TempData["Success_Title"] = "Update Success";
            TempData["Alert_Success"] = "The announcement has been updated successfully.";
            return RedirectToAction("List");
        }
        else
        {
            TempData["Error_Title"] = "Update Failed";
            TempData["Alert_Error"] = "Failed to update announcement. Please try again later.";
        }

        ViewBag.InboxList = new SelectList(db.Inboxes, "Id", "Name");
        return View("Staff/Detail", vm);
    }

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

        var s = db.Inboxes.Find(id);

        if (s != null)
        {
            s.Status = 0;
            db.SaveChanges();

            TempData["Success_Title"] = "Deactivate Success";
            TempData["Alert_Success"] = "The announcement has been deactivated.";
        }
        else
        {
            TempData["Error_Title"] = "Deactivate Failed";
            TempData["Alert_Error"] = "The announcement does not exist.";
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
        int updatedRows = db.Inboxes
            .Where(m => ids.Contains(m.Id) && m.Type == 1 && m.Status == 1)
            .ExecuteUpdate(m => m.SetProperty(m => m.Status, 0));

        // Prepare TempData based on the operation
        if (updatedRows > 0)
        {
            TempData["Success_Title"] = "Deactivate Success";
            TempData["Alert_Success"] = $"{updatedRows} announcement(s) have been deactivated.";
        }
        else
        {
            TempData["Error_Title"] = "Deactivate Failed";
            TempData["Alert_Error"] = "No announcement(s) were found to deactivate.";
        }

        // Redirect back to the previous page
        return Redirect(Request.Headers.Referer.ToString());
    }


    // Inbox for Personal
    // ---------------------------------

    // Inbox List
    public IActionResult PersonalInboxList(string? title, string? sort, string? dir, int page_items = 7, int page = 1)
    {
        // Store the page that user from
        hp.SetCookie("redirect", "/Inbox/PersonalInboxList");

        // Check user authorization
        if (!hp.CheckRole(new string[] { "staff", "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var userId = HttpContext.Session.GetInt32("_userId");

        // (1) Searching ------------------------
        ViewBag.Name = title = title?.Trim() ?? "";

        IQueryable<Inbox> searched;

        if (HttpContext.Session.GetString("_userRole") == "member")
        {
            searched = db.Inboxes.Where(s =>
                        (s.MemberId == null &&
                        (s.Target == 1 || s.Target == 3) || s.MemberId == userId) &&
                        s.Status == 1 &&
                        s.Title.Contains(title));
        }
        else
        {
            searched = db.Inboxes.Where(s =>
                        (s.StaffId == null &&
                        (s.Target == 2 || s.Target == 3) || s.StaffId == userId) &&
                        s.Status == 1 &&
                        s.Title.Contains(title));
        }

        // Order by the created date (newest first) and take the first 30 records
        var limited = searched.OrderByDescending(s => s.Created_datetime).Take(30);

        // (2) Sorting --------------------------
        ViewBag.Sort = sort;
        ViewBag.Dir = dir ?? "des";

        Func<Inbox, object> fn = sort switch
        {
            "ID" => s => s.Id,
            "Title" => s => s.Title,
            "Message" => s => s.Message,
            "Created_datetime" => s => s.Created_datetime,
            _ => s => s.Created_datetime,
        };

        var sorted = dir == "des" ? limited.OrderByDescending(fn) : limited.OrderBy(fn);

        // (3) Paging ---------------------------
        if (page < 1)
        {
            return RedirectToAction(null, new { title, sort, dir, page_items, page = 1 });
        }

        // filter items per page
        ViewBag.Page_items = page_items;

        var m = sorted.ToPagedList(page, page_items);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { title, sort, dir, page_items, page = m.PageCount });
        }

        if (Request.IsAjax())
        {
            return PartialView("User/_PersonalInboxList", m);
        }

        return View("User/PersonalInboxList", m);
    }


    // Redirect user to the announcement or notification detail
    public IActionResult PersonalInboxDetail(int id = 0)
    {
        // Authorizes the user
        var userPersonId = HttpContext.Session.GetInt32("_userPersonId");

        var inbox = db.Inboxes
                    .Include(s => s.Member.Person)
                    .Include(s => s.Staff.Person)
                    .FirstOrDefault(s => s.Id == id && s.Status == 1);
        if (userPersonId == null)
        {
            // Handle the case where the user ID is invalid
            TempData["Error_Title"] = "Access Denied";
            TempData["Alert_Error"] = "You have not logged in yet. Please log in to access this website.";
            return RedirectToAction("Home", "Home");
        }
        else if (inbox != null)
        {
            // Get user role
            var userRole = HttpContext.Session.GetString("_userRole") switch
            {
                "member" => 1,
                "staff" => 2,
                _ => 1
            };

            if (userRole == 1)
            {
                if(userPersonId == inbox.Member.PersonId && (inbox.Target == userRole || inbox.Target == 3))
                {
                    if (inbox.AppointmentId != null)
                    {
                        return RedirectToAction("Detail", "Appointment", new { id = inbox.AppointmentId });
                    }
                    else if (inbox.OrderId != null)
                    {
                        return RedirectToAction("MemberDetail", "Order", new { id = inbox.OrderId });   
                    }
                    else
                    {
                        return RedirectToAction("Wallet", "Member");
                    }
                }
            }
            else if(userRole == 2)
            {
                if (userPersonId == inbox.Staff.PersonId && (inbox.Target == userRole || inbox.Target == 3))
                {
                    if (inbox.AppointmentId != null)
                    {
                        return RedirectToAction("Detail", "Appointment", new { id = inbox.AppointmentId });
                    }
                    else if (inbox.OrderId != null)
                    {
                        return RedirectToAction("MemberDetail", "Order", new { id = inbox.OrderId });
                    }
                    else
                    {
                        return RedirectToAction("Detail", new { id = inbox.Id });
                    }
                }
            }
        }

        TempData["Error_Title"] = "Action Failed";
        TempData["Alert_Error"] = "You have no permission to access this inbox message. Try to login other valid account.";
        return RedirectToAction("PersonalInboxList");
    }

    [HttpPost]
    public IActionResult GetInboxDetail(int inboxId)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff", "member" }, TempData))
        {
            return Json(new { success = false, location = "/Home/Home" });
        }

        var userId = HttpContext.Session.GetInt32("_userId");

        var inbox = db.Inboxes
                    .FirstOrDefault(s => s.Id == inboxId);
        if (inbox != null)
        {
            // Get user role
            var role = 1;
            var userRole = HttpContext.Session.GetString("_userRole") switch
            {
                "member" => role = 1,
                "staff" => role = 2,
                _ => 1
            };

            //Check is it the inbox msg is belongs to the staff or member
            if ((userId == inbox.StaffId || userId == inbox.MemberId)
                || (inbox.StaffId == null && inbox.MemberId == null)
                && (inbox.Target == role || inbox.Target == 3)
                && (inbox.Status == 1))
            {
                return Json(new { success = true, inbox });
            }
        }

        return Json(new { success = false, title = "Access Denied", msg = "You have no permission to access this inbox message. Try to login other valid account." });
    }

}
