using Assignment.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using X.PagedList.Extensions;

namespace Assignment.Controllers;

public class RoomController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helpers hp;

    public RoomController(DB db, IWebHostEnvironment en, Helpers hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
    }

    // Admin Site CRUD
    // ------------------------------------------
    public IActionResult List(string? name, string? filter_status, string? sort, string? dir, int page_items = 7, int page = 1)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // (1) Searching ------------------------
        ViewBag.Name = name = name?.Trim() ?? "";
        ViewBag.Status = filter_status;

        var searched = db.Rooms.Where(s => 
                        (s.Name.Contains(name) || name == null) &&
                        (string.IsNullOrEmpty(filter_status) || s.Status.ToString() == filter_status));

        // (2) Sorting --------------------------
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<Room, object> fn = sort switch
        {
            "Id" => s => s.Id,
            "Name" => s => s.Name,
            "Status" => s => s.Status,
            _ => s => s.Id,
        };

        var sorted = dir == "des" ?
                     searched.OrderByDescending(fn) :
                     searched.OrderBy(fn);

        // (3) Paging ---------------------------
        if (page < 1)
        {
            return RedirectToAction(null, new { name, filter_status, sort, dir, page_items, page = 1 });
        }

        // filter items per page
        ViewBag.Page_items = page_items;

        var m = sorted.ToPagedList(page, page_items);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { name, filter_status, sort, dir, page_items, page = m.PageCount });
        }

        if (Request.IsAjax())
        {
            return PartialView("_List", m);
        }

        return View(m);
    }

    // Get: Room/Create
    public IActionResult Create()
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        } else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("List");
        }

        ViewBag.RoomList = new SelectList(db.Rooms, "Id", "Name");
        return View();
    }

    // Post: Room/Create
    [HttpPost]
    public IActionResult Create(RoomVM vm)
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

        if (ModelState.IsValid)
        {
            db.Rooms.Add(new()
            {
                Name = vm.Name.Trim(),
                Status = 1
            });
            db.SaveChanges();


            TempData["Success_Title"] = "Create Success";
            TempData["Alert_Success"] = "The room has been created successfully.";
            return RedirectToAction("List");
        }
        else
        {
            TempData["Error_Title"] = "Create Failed";
            TempData["Alert_Error"] = "Failed to create a room. Please try again later.";
        }

        ViewBag.RoomList = new SelectList(db.Rooms, "Id", "Name");

        return RedirectToAction("Create");
    }

    // Get Room/Detail
    public IActionResult Detail(int id)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var s = db.Rooms.Find(id);

        if (s == null)
        {
            TempData["Error_Title"] = "Action Failed";
            TempData["Alert_Error"] = "Room Not Found";
            return RedirectToAction("List");
        }

        var vm = new RoomVM
        {
            Id = id,
            Name = s.Name.Trim(),
            Status = s.Status,
        };

        ViewBag.RoomList = new SelectList(db.Rooms, "Id", "Name");
        return View(vm);
    }

    // Post Room/Detail
    [HttpPost]
    public IActionResult Detail(RoomVM vm)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var s = db.Rooms.Find(vm.Id);

        if (s == null)
        {
            TempData["Error_Title"] = "Action Failed";
            TempData["Alert_Error"] = "Room Not Found";
            return RedirectToAction("List");
        }

        if (ModelState.IsValid)
        {
            s.Name = vm.Name;

            db.SaveChanges();

            TempData["Success_Title"] = "Update Success";
            TempData["Alert_Success"] = "The room has been updated successfully.";
            return RedirectToAction("List");
        }
        else
        {
            TempData["Error_Title"] = "Update Failed";
            TempData["Alert_Error"] = "Failed to update room. Please try again later.";
        }

        ViewBag.RoomList = new SelectList(db.Rooms, "Id", "Name");
        return View(vm);
    }

    // Deactivate Room
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

        var s = db.Rooms.Find(id);

        if (s != null)
        {
            s.Status = 0;
            db.SaveChanges();

            TempData["Success_Title"] = "Deactivate Success";
            TempData["Alert_Success"] = "The room has been deactivated.";
        }
        else
        {
            TempData["Error_Title"] = "Deactivate Failed";
            TempData["Alert_Error"] = "The room does not exist.";
        }

        return Redirect(Request.Headers.Referer.ToString());
    }

    // Activate Room
    public IActionResult Activate(int? id)
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

        var s = db.Rooms.Find(id);

        if (s != null)
        {
            s.Status = 1;
            db.SaveChanges();

            TempData["Success_Title"] = "Activate Success";
            TempData["Alert_Success"] = "The room has been deactivated.";
        }
        else
        {
            TempData["Error_Title"] = "Activate Failed";
            TempData["Alert_Error"] = "The room does not exist.";
        }

        return Redirect(Request.Headers.Referer.ToString());
    }
}
