using Assignment.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using X.PagedList.Extensions;

namespace Assignment.Controllers;

public class ReviewController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helpers hp;

    public ReviewController(DB db, IWebHostEnvironment en, Helpers hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
    }

    public IActionResult List(string? title, DateTime? start_datetime, DateTime? end_datetime, int? min_review, int? max_review, string? sort, string? dir, int page_items = 7, int page = 1)
    {
        // Check user authorization
        if (!hp.CheckRole(new string[] { "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var UserId = HttpContext.Session.GetInt32("_userId");

        var appointments = db.Appointments
                        .Include(a => a.AppointmentReviews)
                        .Where(a => a.MemberId == UserId
                                    && a.Status == 3
                                    && a.AppointmentReviews.Any());

        // search appointment title
        ViewBag.Title = title = title?.Trim() ?? "";

        var searched = appointments.Where(a => a.Title.Contains(title));

        // filter appointment date range
        ViewBag.Start_datetime = start_datetime = start_datetime ?? appointments
                                                            .OrderBy(a => a.Start_datetime)
                                                            .Select(a => a.Start_datetime)
                                                            .FirstOrDefault();
        ViewBag.End_datetime = end_datetime = end_datetime ?? appointments.OrderByDescending(a => a.Start_datetime)
                                                            .Select(a => a.Start_datetime)
                                                            .FirstOrDefault();

        var filter_daterange = searched.Where(a => a.Start_datetime >= start_datetime
                                                && a.Start_datetime <= end_datetime);

        // filter total review
        ViewBag.Min_review = min_review = min_review ?? appointments
                                                            .AsEnumerable()
                                                            .Select(a => a.AppointmentReviews.Count)
                                                            .DefaultIfEmpty(0)
                                                            .Min();
        ViewBag.Max_review = max_review = max_review ?? appointments
                                                            .AsEnumerable()
                                                            .Select(a => a.AppointmentReviews.Count)
                                                            .DefaultIfEmpty(0)
                                                            .Max();

        var filter_reviewrange = filter_daterange.Where(a => a.AppointmentReviews.Count >= min_review
                                                        && a.AppointmentReviews.Count <= max_review);

        // sorting
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<Appointment, object> fn = sort switch
        {
            "ID" => a => a.Id,
            "Appointment Title" => a => a.Title,
            "Time" => a => $"{Helpers.FormatTime(a.Start_datetime)} - {Helpers.FormatTime(a.End_datetime)}",
            "Created Review" => a => a.AppointmentReviews.Count(),
            "Last Review Date" => a => a.AppointmentReviews.Max(ar => ar.Comment_date),
            _ => a => a.Id
        };

        var sorted = dir == "desc" ? filter_reviewrange.OrderByDescending(fn) : filter_reviewrange.OrderBy(fn);

        // paging
        if (page < 1)
        {
            return RedirectToAction(null, new { title, start_datetime, end_datetime, min_review, max_review, sort, dir, page_items, page = 1 });
        }

        // filter items per page
        ViewBag.Page_items = page_items;

        var m = sorted.ToPagedList(page, page_items);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { title, start_datetime, end_datetime, min_review, max_review, sort, dir, page_items, page = m.PageCount });
        }

        // check whether it is ajax
        if (Request.IsAjax())
        {
            return PartialView("Member/_ReviewList", m);
        }

        return View("Member/List", m);
    }

    public IActionResult Create(int id)
    {
        // Check user authorization
        if (!hp.CheckRole(new string[] { "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // get appointment review data
        var appointment = db.Appointments
                                .Include(a => a.OrderPackageTreatment)
                                    .ThenInclude(opt => opt.OrderPackageTreatmentServices)
                                        .ThenInclude(opts => opts.Service)
                                            .ThenInclude(s => s.ServicePhotos)
                                .Include(a => a.AppointmentReviews)
                                    .ThenInclude(ar => ar.ReviewPhotos)
                                .Include(a => a.Staff)
                                    .ThenInclude(s => s.Person)
                                .Include(a => a.Room)
                                .Where(a => a.Id == id && a.Status == 3)
                                .FirstOrDefault();

        if (appointment == null)
        {
            TempData["Error_Title"] = "Appointment Not Found";
            TempData["Alert_Error"] = "Invalid Appointment ID";

            return RedirectToAction("Calendar", "Appointment");
        }

        ViewBag.Appointment = appointment;

        // get session data
        var vm = new AppointmentReviewVM
        {
            AppointmentId = appointment.Id,
            //Comment = HttpContext.Session.GetString("temp_review_comment") ?? "",
            //Star = HttpContext.Session.GetInt32("temp_review_star") ?? 0,
            //Anonymous = HttpContext.Session.GetInt32("temp_review_anonymmous") == 1 ? true : false,
            //Upload_photos = JsonConvert.DeserializeObject<IFormFile[]>(HttpContext.Session.GetString("temp_review_upload_photos") ?? "[]"),
        };

        return View("Member/Review", vm);
    }

    [HttpPost]
    public IActionResult Create(AppointmentReviewVM vm)
    {
        // Check user authorization
        if (!hp.CheckRole(new string[] { "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var UserId = HttpContext.Session.GetInt32("_userId");

        if (ModelState.IsValid)
        {
            // check appointment id
            var appointment = db.Appointments
                                    .Where(a => a.Status == 3
                                            && a.MemberId == UserId
                                            && a.Id == vm.AppointmentId)
                                    .FirstOrDefault();

            if (appointment == null)
            {
                TempData["Error_Title"] = "Appointment Not Found";
                TempData["Alert_Error"] = "Invalid Appointment ID";

                return RedirectToAction("Calendar", "Appointment");
            }

            // insert appointment review
            var newAppointmentReview = new AppointmentReview
            {
                AppointmentId = appointment.Id,
                Star = vm.Star,
                Comment = vm.Comment,
                Anonymous = vm.Anonymous,
            };
            db.AppointmentReviews.Add(newAppointmentReview);
            db.SaveChanges();

            var review_id = newAppointmentReview.Id;

            // check and store uploaded images
            if (vm.Upload_photos != null && vm.Upload_photos.Length > 0)
            {
                foreach (var photo in vm.Upload_photos) {
                    var error = hp.ValidateImage(photo);

                    if (error != "")
                    {
                        return Json(new
                        {
                            result = false,
                            error_title = "Appointment Review Create Failed",
                            error_msg = "Invalid uploaded photos.",
                        });
                    }
                    
                    // save photos
                    var store_photo = hp.SavePhoto(photo, "images/review").Result;

                    if (store_photo != null)
                    {
                        var review_photo = new ReviewPhoto
                        {
                            Path = store_photo,
                            AppointmentReviewId = review_id
                        };
                        db.ReviewPhotos.Add(review_photo);
                        db.SaveChanges();
                    }
                }
            }
        }

        return Json(new
        {
            result = true,
            success_title = "Appointment Review Create Success",
            success_msg = "Review has been created successfully.",
        });
    }

    //[HttpPost]
    //public IActionResult CapturePhoto(AppointmentReviewVM vm)
    //{
    //    // Check user authorization
    //    if (!hp.CheckRole(new string[] { "member" }, TempData))
    //    {
    //        return RedirectToAction("Home", "Home");
    //    }

    //    HttpContext.Session.SetInt32("temp_review_appointmentId", vm.AppointmentId ?? 0);
    //    HttpContext.Session.SetString("temp_review_comment", vm.Comment ?? "");
    //    HttpContext.Session.SetInt32("temp_review_star", vm.Star ?? 0);
    //    HttpContext.Session.SetInt32("temp_review_anonymmous", vm.Anonymous ? 1 : 0);
    //    HttpContext.Session.SetString("temp_review_upload_photos", JsonConvert.SerializeObject(vm.Upload_photos) ?? "");
    //    HttpContext.Session.SetString("temp_review_photo_str", JsonConvert.SerializeObject(vm.PhotoStr) ?? "");

    //    return View(vm);
    //}

    //[HttpPost]
    //public async Task<IActionResult> SaveCapturedPhoto(string image)
    //{
    //    var UserRole = HttpContext.Session.GetString("_userRole");

    //    // Authorizes role of user
    //    if (!hp.CheckRole(new string[] { "member" }, TempData))
    //    {
    //        return RedirectToAction("Home", "Home");
    //    }

    //    if (string.IsNullOrEmpty(image))
    //    {
    //        return Json(new { success = false, message = "No image data provided." });
    //    }

    //    try
    //    {
    //        // Decode the base64 string
    //        var base64Data = image.Substring(image.IndexOf(",") + 1);
    //        var imageBytes = Convert.FromBase64String(base64Data);

    //        // Specify the folder where the file should be saved
    //        string folder = "images/review/temp";

    //        var tempFolderPath = Path.Combine(Directory.GetCurrentDirectory(), $"wwwroot/{folder}");

    //        // Check if the folder exists; if not, create it
    //        if (!Directory.Exists(tempFolderPath))
    //        {
    //            Directory.CreateDirectory(tempFolderPath);
    //        }

    //        // Generate a unique file name
    //        var fileName = Guid.NewGuid().ToString("n") + ".jpg";

    //        // Get the full local file path
    //        var filePath = Path.Combine(en.WebRootPath, folder, fileName);

    //        // Save the image to the file system
    //        await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

    //        TempData["capture_photo"] = fileName;
    //        TempData["Warning_Title"] = "Capture Success";
    //        TempData["Alert_Warning"] = "Photo capture success. Navigating back to review page...";

    //        return Json(new { success = true, photo = fileName, filePath = $"{folder}/{fileName}" });
    //    }
    //    catch (Exception ex)
    //    {
    //        // Handle any errors
    //        return Json(new { success = false, message = $"Error: {ex.Message}" });
    //    }
    //}
}
