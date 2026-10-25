using Microsoft.AspNetCore.Mvc;

namespace Assignment.Controllers;

public class HomeController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helpers hp;

    public HomeController(DB db, IWebHostEnvironment en, Helpers hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
    }

    public IActionResult Home()
    {
        // Get the current month's top 7 packages by sales
        var currentMonth = DateTime.Now.Month;
        var currentYear = DateTime.Now.Year;

        // Step 1: Get the current month's orders
        var orders = db.Orders
                       .Where(o => o.Payment_date.Month == currentMonth && o.Payment_date.Year == currentYear)
                       .Select(o => o.Id)
                       .ToList();

        // Step 2: Count occurrences of each package (1 record = 1 quantity)
        var topPackages = db.OrderPackages
                            .Where(op => orders.Contains(op.OrderId))
                            .GroupBy(op => op.PackageId)
                            .Select(group => new
                            {
                                PackageId = group.Key,
                                TotalQuantity = group.Count() // Count records, not sum quantity
                            })
                            .OrderByDescending(p => p.TotalQuantity)
                            .Take(7)
                            .ToList();


        // Step 3: Check if we have enough records
        if (topPackages.Count < 7)
        {
            // Step 4: Fetch the latest packages by descending ID to make up for the shortfall
            var topPackageIds = topPackages.Select(p => p.PackageId).ToList();
            var additionalPackages = db.Packages
                                       .Where(p => !topPackageIds.Contains(p.Id))
                                       .OrderByDescending(p => p.Id) // Sort by descending ID
                                       .Take(7 - topPackages.Count)
                                       .Select(p => new
                                       {
                                           PackageId = p.Id,
                                           TotalQuantity = 0 // These have no sales for the current month
                                       })
                                       .ToList();

            // Combine the top packages and additional packages
            topPackages.AddRange(additionalPackages);
        }


        // Step 5: Get photos for each package (cover photo, type = 1)
        var packageIds = topPackages.Select(p => p.PackageId).ToList();
        var packagePhotos = db.PackagePhotos
                              .Where(pp => packageIds.Contains(pp.PackageId) && pp.Type == 1)
                              .ToDictionary(pp => pp.PackageId, pp => pp.Path); // Map PackageId to PhotoUrl

        // Step 6: Merge package data with photos
        var result = topPackages
                     .Select(p => new
                     {
                         PackageId = p.PackageId,
                         TotalQuantity = p.TotalQuantity,
                         PhotoUrl = packagePhotos.ContainsKey(p.PackageId)
                                    ? packagePhotos[p.PackageId]
                                    : "../defaultImage.png" // Default to null if no photo exists
                     })
                     .ToList();

        while (result.Count < 7)
        {
            result.Add(new
            {
                PackageId = 0, // Default or placeholder value
                TotalQuantity = 0, // Default quantity
                PhotoUrl = "/images/package/defaultImage.png" // Default photo
            });
        }

        // Convert to an array if needed
        var finalResult = result.ToArray();

        return View(finalResult);
    }

    public IActionResult ScanQR()
    {
        return View();
    }

    [HttpGet]
    public ActionResult SearchBar(string keyword)
    {
        // Find the packages/services matching the keyword
        var results = db.Packages
                        .Where(p => p.Name.Contains(keyword))
                        .Take(10)  // Limit the results (optional)
                        .ToList();

        // If you want to return a partial view instead of JSON:
        return PartialView("_SearchResults", results);  // You need to create this partial view
    }

    //=======================
    // Helper Function
    //=======================
    [HttpGet]
    public JsonResult GetCartItemsCount()
    {
        // Assuming Helpers.CartItemsCount() gets the current cart item count
        int cartItemsCount = hp.CartItemsCount();

        return Json(cartItemsCount);
    }

}
