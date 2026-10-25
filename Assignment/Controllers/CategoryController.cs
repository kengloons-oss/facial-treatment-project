using System.Linq;
using Assignment.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Crypto;
using X.PagedList.Extensions;
namespace Assignment.Controllers;

public class CategoryController : Controller
{
    private readonly DB db;
    private readonly Helpers hp;

    public CategoryController(DB db, Helpers hp)
    {
        this.db = db;
        this.hp = hp;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult CategoryList(string? name, string? filter_status, string? sort, string? dir, int page = 1, int page_items = 7)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // Trim and apply searchString as per Rooms search logic
        ViewBag.name = name = name?.Trim() ?? "";
        ViewBag.Status = filter_status;

        // Filter categories by search string or ID
        var categories = db.Categories
                        .Where(c => c.Name.Contains(name) &&
                                    (string.IsNullOrEmpty(filter_status) || c.Status.ToString() == filter_status))
                        .Select(c => new CategoryListing
                        {
                            Category = c,
                            TotalServices = db.Services.Count(s => s.CategoryId == c.Id)
                        });

        // Sorting logic
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<CategoryListing, object> fn = sort switch
        {
            "ID" => m => m.Category.Id,
            "Name" => m => m.Category.Name,
            "Total Services" => m => m.TotalServices,
            _ => m => m.Category.Id,
        };

        //var pagedCategories = sorted.ToPagedList(page, page_items);
        var sorted = dir == "desc" ? categories.OrderByDescending(fn) : categories.OrderBy(fn);

        // Ensure page number is valid
        if (page < 1)
        {
            return RedirectToAction(null, new
            {
                name,
                filter_status,
                sort,
                dir,
                page_items,
                page = 1
            });
        }

        // filter items per page
        ViewBag.Page_items = page_items;
        var m = sorted.ToPagedList(page, page_items);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new
            {
                name,
                filter_status,
                sort,
                dir,
                page_items,
                page = m.PageCount
            });
        }

        // Check for AJAX request and return partial view
        if (Request.IsAjax())
        {
            return PartialView("Staff/_CategoryList", m);
        }

        return View("Staff/CategoryList", m);
    }


    [HttpGet]
    public IActionResult CategoryCreate()
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("CategoryList");
        }
        return View("Staff/CategoryCreate");
    }

    [HttpPost]
    public IActionResult CategoryCreate(CategoryVM vm)
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
            db.Categories.Add(new()
            {
                Name = vm.Name.Trim(),
                Description = vm.Description.Trim(),
                Status = 1
            });

            db.SaveChanges();

            TempData["Success_Title"] = "Create Success";
            TempData["Alert_Success"] = "The category has been created successfully.";
            return RedirectToAction("CategoryList");
        }
        else
        {
            TempData["Error_Title"] = "Create Failed";
            TempData["Alert_Error"] = "Failed to create a category. Try to check on your input.";
        }

        return View("Staff/CategoryCreate", vm);
    }

    //Staff Category Edit 
    [HttpGet]
    public IActionResult CategoryEdit(int id)
    {
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var category = db.Categories.Find(id);
        if (category == null)
        {
            TempData["Error_Title"] = "Action Failed";
            TempData["Alert_Error"] = "Category does not exist. Try to view other category.";
            return RedirectToAction("CategoryList");
        }

        var vm = new CategoryVM
        {
            Id = id,
            Name = category.Name,
            Description = category.Description
        };

        return View("Staff/CategoryEdit", vm);
    }

    [HttpPost]
    public IActionResult CategoryEdit(CategoryVM vm)
    {
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("List");
        }


        if (vm.Id == null)
        {

            ModelState.AddModelError("Id", "Category Id invalid.");
        }

        var category = db.Categories.Find(vm.Id);

        if (ModelState.IsValid && category != null)
        {
            category.Name = vm.Name;
            category.Description = vm.Description;

            db.SaveChanges();


            TempData["Success_Title"] = "Update Success";
            TempData["Alert_Success"] = "The category has been updated successfully.";
            return RedirectToAction("CategoryList");
        }

        TempData["Error_Title"] = "Action Failed";
        TempData["Alert_Error"] = "Category does not exist. Try to view other category.";
        return View("CategoryList", category);
    }


    // Toggle Status
    [HttpPost]
    public IActionResult ToggleStatus(int id)
    {
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("List");
        }

        var category = db.Categories.Find(id);
        if (category != null)
        {
            category.Status = 0;


            //Deactivate the related services
            var relatedServices = db.Services.Where(s => s.CategoryId == category.Id).ToList();
            foreach (var service in relatedServices)
            {
                service.Status = 0;

                //Deactivate the package linked to these services via PackageTreatmentServices
                var packageTreatments = db.PackageTreatmentServices.Where(pts => pts.ServiceId ==
                service.Id).Select(pts => pts.PackageTreatmentId).Distinct().ToList();

                var relatedPackages = db.PackageTreatments.Where(pt =>
                                      packageTreatments.Contains(pt.Id))
                                      .Select(pt => pt.PackageId).Distinct().ToList();

                var packages = db.Packages.Where(p => relatedPackages.Contains(p.Id)).ToList();
                foreach (var package in packages)
                {
                    package.Status = 0;
                }

                // Deactivate package treatment services
                var treatmentServices = db.PackageTreatmentServices
                    .Where(pts => pts.ServiceId == service.Id)
                    .ToList();
                foreach (var treatmentService in treatmentServices)
                {
                    treatmentService.Status = 0; // Assuming PackageTreatmentServices table has a Status field
                }

            }

            TempData["Success_Title"] = "Deactivate Successfull";
            TempData["Alert_Success"] = "The category already be deactivatated. <br><br><small>The related services and packages also successfully be deactivated together.</small>";
            db.SaveChanges();
        }

        TempData["Error_Title"] = "Action Failed";
        TempData["Alert_Error"] = "Category does not exist. Try to view other category.";
        return RedirectToAction("CategoryList");
    }

    [HttpPost]
    public IActionResult Activate(int id, int?[] serviceIds)
    {
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("List");
        }

        var category = db.Categories.Find(id);
        int updatedServiceRows = 0;

        if (category != null)
        {
            // Activate the category
            category.Status = 1; // 1 = Active, 0 = Inactive

            // Activate the selected services
            updatedServiceRows = db.Services
                .Where(s => serviceIds.Contains(s.Id))
                .ExecuteUpdate(s => s.SetProperty(s => s.Status, 1));

            db.SaveChanges(); // Save changes to the database

            TempData["Success_Title"] = "Activate Successfully";
            TempData["Alert_Success"] = $"The category has been successfully activated. <br><br><small>*<strong>{updatedServiceRows}</strong> service(s) successfully activated together.</small>";
        }
        else
        {
            TempData["Error_Title"] = "Activate Failed";
            TempData["Alert_Error"] = "No category was found to deactivate.";
        }

        return RedirectToAction("CategoryList");
    }

    [HttpPost]
    public IActionResult GetRelatedServices(int categoryId)
    {
        var services = db.Services
            .Where(s => s.CategoryId == categoryId && s.Status == 0)
            .Select(s => new { s.Id, s.Name }) // Ensure only the necessary fields are included
            .ToList();

        return Json(services);
    }


}
