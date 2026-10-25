using System.Collections.Generic;
using Assignment.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using X.PagedList.Extensions;


namespace Assignment.Controllers;

public class ServiceController : Controller
{
    private readonly DB db;
    private readonly Helpers hp;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public ServiceController(DB db, Helpers hp, IWebHostEnvironment webHostEnvironment)
    {
        this.db = db;
        this.hp = hp;
        _webHostEnvironment = webHostEnvironment;
    }


    //////////////////////
    //Staff Services Part/
    /////////////////////
    [HttpGet]
    public IActionResult ServicesList(
     string? name,
     string? filter_status,
     decimal? priceStart,
     decimal? priceEnd,
     string? categoryId,
     string? sort,
     string? dir,
     int page = 1,
     int page_items = 7)
    {
        //Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // Trim and apply searchString as per Rooms search logic
        ViewBag.Name = name = name?.Trim() ?? "";
        ViewBag.Status = filter_status;
        ViewBag.PriceStart = priceStart;
        ViewBag.PriceEnd = priceEnd;
        ViewBag.CategoryId = categoryId;

        var services = db.Services
                         .Include(s => s.Category)
                         .Where(s => s.Name.Contains(name) &&
                                 (priceStart == null && priceEnd != null ? s.Price <= priceEnd : true) && // Case 1
                                 (priceEnd == null && priceStart != null ? s.Price >= priceStart : true) && // Case 2
                                 (priceStart != null && priceEnd != null ? (s.Price >= priceStart && s.Price <= priceEnd) : true) && // Case 3
                                 (string.IsNullOrEmpty(filter_status) || s.Status.ToString() == filter_status) &&
                                 (string.IsNullOrEmpty(categoryId) || categoryId == "0" || s.CategoryId.ToString() == categoryId) // Handling "All" (0) case
                             );


        // (2) Sorting --------------------------
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<Service, object> sortFn = sort switch
        {
            "Id" => s => s.Id,
            "Name" => s => s.Name,
            "Description" => s => s.Description,
            "Price" => s => s.Price,
            "Category" => s => s.Category.Name,
            _ => s => s.Id
        };

        var sortedServices = dir == "desc"
            ? services.OrderByDescending(sortFn)
            : services.OrderBy(sortFn);

        // (3) Paging ---------------------------
        if (page < 1)
        {
            return RedirectToAction(null, new
            {
                name,
                priceStart,
                priceEnd,
                sort,
                dir,
                page_items,
                page = 1
            });
        }

        // filter items per page
        ViewBag.Page_items = page_items;
        var pagedServices = sortedServices.ToPagedList(page, page_items);

        if (page > pagedServices.PageCount && pagedServices.PageCount > 0)
        {
            return RedirectToAction(null, new
            {
                name,
                priceStart,
                priceEnd,
                sort,
                dir,
                page_items,
                page = pagedServices.PageCount
            });
        }

        // Get service categories
        var categories = db.Categories.ToList();

        // Check whether service categories are empty
        if (!categories.Any()) // If there are no categories
        {
            ViewBag.Service_cat_selectlist = new List<SelectListItem>();
        }
        else
        {
            // Create a list of SelectListItems and add "All" as the first item
            var categoriesList = new List<SelectListItem>
            {
                new SelectListItem
                {
                    Value = "0", // You can use any value that indicates "All", e.g., 0 or an empty string
                    Text = "All"
                }
            };

            categoriesList.AddRange(categories
                            .Select(category => new SelectListItem
                            {
                                Value = category.Id.ToString(),
                                Text = category.Name
                            })
                            .ToList());

            ViewBag.CategoryList = categoriesList;
        }


        // Check for AJAX request and return partial view
        if (Request.IsAjax())
        {
            return PartialView("Staff/_ServiceList", pagedServices);
        }

        return View("Staff/ServiceList", pagedServices);
    }



    // GET: ServiceCreate
    [Route("Service/Staff/CreateService")]
    [HttpGet]
    public IActionResult ServiceCreate()
    {
        // use if-else statement to check user role
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("ServicesList");
        }
        // Load categories for dropdown
        ViewBag.Categories = db.Categories

                            .Where(c => c.Status == 1)
                              .Select(c => new { c.Id, c.Name })
                            .ToList();
        return View("Staff/CreateService");
    }

    [Route("Service/Staff/CreateService")]
    [HttpPost]
    public IActionResult ServiceCreate(Service service, IFormFile cover_image, List<IFormFile> normal_image)
    {
        // use if-else statement to check user role
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("CategoryList");
        }
        service.Status = 1;
        // Save the service data to the database
        db.Services.Add(service);
        db.SaveChanges();

        // Process the cover image
        if (cover_image != null && cover_image.Length > 0)
        {
            var fileName = Path.GetFileName(cover_image.FileName);
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/servicePhoto", fileName);

            // Ensure the directory exists
            var directoryPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Save the photo to the specified path
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                cover_image.CopyTo(fileStream);
            }

            // Create and save the photo entry in the database
            var coverPhoto = new ServicePhoto
            {
                Path = filePath,
                ServiceId = service.Id,
                Type = 1 // Type 1 for cover image
            };
            db.ServicePhotos.Add(coverPhoto);
            db.SaveChanges();
        }

        // Process the normal images
        if (normal_image != null && normal_image.Count > 0)
        {
            foreach (var image in normal_image)
            {
                if (image.Length > 0)
                {
                    var fileName = Path.GetFileName(image.FileName);
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/servicePhoto", fileName);

                    // Ensure the directory exists
                    var directoryPath = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    // Save the photo to the specified path
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        image.CopyTo(fileStream);
                    }

                    // Create and save the photo entry in the database
                    var normalPhoto = new ServicePhoto
                    {
                        Path = filePath,
                        ServiceId = service.Id,
                        Type = 2 // Type 2 for normal images
                    };
                    db.ServicePhotos.Add(normalPhoto);
                }
            }
            db.SaveChanges();
        }

        return RedirectToAction("ServicesList");
    }

    // GET: ServiceEdit
    [HttpGet]
    public IActionResult ServiceEdit(int id)
    {
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("ServicesList");
        }

        var service = db.Services.Find(id);
        if (service == null)
        {
            return NotFound();
        }

        // Load categories for dropdown
        ViewBag.Categories = db.Categories
                              .Where(c => c.Status == 1)
                              .Select(c => new { c.Id, c.Name })
                              .ToList();

        // Load current images for the service (optional for display in the form)
        var currentCoverImage = db.ServicePhotos.FirstOrDefault(sp => sp.ServiceId == id && sp.Type == 1);
        var currentNormalImages = db.ServicePhotos.Where(sp => sp.ServiceId == id && sp.Type == 2).ToList();

        // Pass data to the view
        ViewBag.CurrentCoverImage = currentCoverImage?.Path;
        ViewBag.CurrentNormalImages = currentNormalImages.Select(i => i.Path).ToList();

        return View("Staff/EditService", service);
    }

    // POST: ServiceEdit
    [HttpPost]
    public IActionResult ServiceEdit(Service service, IFormFile cover_image, List<IFormFile> normal_image)
    {
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("ServicesList");
        }

        // Update the service data
        db.Services.Update(service);
        db.SaveChanges();

        // Process and save the cover image (if provided)
        if (cover_image != null && cover_image.Length > 0)
        {
            var fileName = Path.GetFileName(cover_image.FileName);
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/servicePhoto", fileName);

            // Ensure the directory exists
            var directoryPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Save the cover image
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                cover_image.CopyTo(fileStream);
            }

            // Delete old cover image (if exists) and add new one
            var oldCoverImage = db.ServicePhotos.FirstOrDefault(sp => sp.ServiceId == service.Id && sp.Type == 1);
            if (oldCoverImage != null)
            {
                db.ServicePhotos.Remove(oldCoverImage);
                db.SaveChanges();
            }

            // Create and save the new cover image entry
            var coverPhoto = new ServicePhoto
            {
                Path = filePath,
                ServiceId = service.Id,
                Type = 1 // Type 1 for cover image
            };
            db.ServicePhotos.Add(coverPhoto);
            db.SaveChanges();
        }

        // Process and save normal images (if provided)
        if (normal_image != null && normal_image.Count > 0)
        {
            foreach (var image in normal_image)
            {
                if (image.Length > 0)
                {
                    var fileName = Path.GetFileName(image.FileName);
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/servicePhoto", fileName);

                    // Ensure the directory exists
                    var directoryPath = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    // Save the normal image
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        image.CopyTo(fileStream);
                    }

                    // Create and save the normal image entry
                    var normalPhoto = new ServicePhoto
                    {
                        Path = filePath,
                        ServiceId = service.Id,
                        Type = 2 // Type 2 for normal images
                    };
                    db.ServicePhotos.Add(normalPhoto);
                }
            }
            db.SaveChanges();
        }

        return RedirectToAction("ServicesList");
    }



    [HttpPost]
    public IActionResult Deactivate(int serviceId)
    {
        // use if-else statement to check user role
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("ServicesList");
        }

        var service = db.Services.Include(s => s.Category).FirstOrDefault(s => s.Id == serviceId && s.Status == 1);

        if (service == null)
        {
            TempData["Error_Title"] = "Deactivate Failed";
            TempData["Alert_Error"] = "The service does not exist.";
            return Redirect(Request.Headers.Referer.ToString());
        }
        else
        {
            int updatedPackageRows = 0;

            // Deactivate Service
            service.Status = 0;

            var relatedPackages = db.Packages
                .Where(p => p.PackageTreatments
                    .Any(pt => pt.PackageTreatmentServices
                        .Any(pts => pts.ServiceId == serviceId)))
                .ToList();

            // Update package statuses
            updatedPackageRows = relatedPackages.Count;
            foreach (var package in relatedPackages)
            {
                package.Status = 1; // Activate the package
            }

            db.SaveChanges(); // Save changes to the database

            TempData["Success_Title"] = "Deactivate Success";
            TempData["Alert_Success"] = $"The service has been deactivated. <br><br><small>*<strong>{updatedPackageRows}</strong> package(s) have been deactivated together.</small>";
        }

        return RedirectToAction("ServicesList");
    }

    [HttpPost]
    public IActionResult Activate(int id, int?[] packageIds)
    {
        // use if-else statement to check user role
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("ServicesList");
        }

        var service = db.Services.Include(s => s.Category).FirstOrDefault(s => s.Id == id);
        int totalPackagesSelected = packageIds?.Length ?? 0;
        int successfullyActivatedPackages = 0;
        int failedToActivatePackages = 0;

        if (service != null && service.Category.Status != 0)
        {
            // Activate the service
            service.Status = 1; // 1 = Active, 0 = Inactive

            // Find packages from provided packageIds
            var relatedPackages = db.Packages
                .Where(p => packageIds.Contains(p.Id) && p.Status != 1) // Exclude already active packages
                .ToList();

            // Filter packages with no inactive services
            var packagesToActivate = relatedPackages
                .Where(p => !p.PackageTreatments
                    .Any(pt => pt.PackageTreatmentServices
                        .Any(pts => pts.Service.Status == 0))) // Exclude packages linked to inactive services
                .ToList();

            successfullyActivatedPackages = packagesToActivate.Count;
            failedToActivatePackages = totalPackagesSelected - successfullyActivatedPackages;

            // Update statuses for the packages to activate
            foreach (var package in packagesToActivate)
            {
                package.Status = 1; // Activate the package
            }

            db.SaveChanges(); // Save changes to the database

            // Success message with details
            TempData["Success_Title"] = "Activate Successfully";
            TempData["Alert_Success"] = $"The service has been successfully activated. <br><br>" +
                                         $"<strong>Total Packages Selected:</strong> {totalPackagesSelected}<br>" +
                                         $"<strong>Successfully Activated:</strong> {successfullyActivatedPackages}<br>" +
                                         $"<strong>Failed to Activate:</strong> {failedToActivatePackages}<br><br>" +
                                         $"<small>*Package(s) failed to activate may due to contain other inactive service(s).</small>";
        }
        else
        {
            // Failure message
            TempData["Error_Title"] = "Activate Failed";
            TempData["Alert_Error"] = "No services were found or the service is under a category that is deactivated. <br><br>*<small>Try to activate the category first.</small>";
        }

        return RedirectToAction("ServicesList");
    }

    [HttpPost]
    public IActionResult GetRelatedPackages(int serviceId)
    {
        var packages = db.Packages
            .Where(p => p.PackageTreatments
                .Any(pt => pt.PackageTreatmentServices
                    .Any(pts => pts.ServiceId == serviceId)) && p.Status == 0)
            .ToList();

        return Json(packages);
    }

}

