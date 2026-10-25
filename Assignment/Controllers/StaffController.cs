using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X.PagedList.Extensions;
using AutoMapper;
using Assignment.Models;
using System.Globalization;
using System.Reflection.Metadata.Ecma335;
using ExcelDataReader;

namespace Assignment.Controllers;

public class StaffController : Controller
{
    private readonly DB db;
    private readonly IMapper _mapper;
    private readonly Helpers hp;

    public StaffController(DB db, IMapper mapper, Helpers hp)
    {
        this.db = db;
        this._mapper = mapper;
        this.hp = hp;
    }

    public IActionResult List(string? name, string? sort, string? dir, string? role, int page_items = 7, int page = 1)
    {
        // Authorizes role of user
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // filter role
        ViewBag.Role = role ?? "";

        var filtered_role = role switch
        {
            "" => db.Staffs,
            "1" => db.Staffs
                            .Where(s => s.Role.Equals(1)),
            "2" => db.Staffs
                            .Where(s => s.Role.Equals(2)),
            _ => db.Staffs
        };

        // searching staff name
        ViewBag.Name = name = name?.Trim() ?? "";

        var searched = filtered_role
                    .Where(s => s.Person.Name.Contains(name))
                    .Include(s => s.Person).ToList();

        // pending appointment amount
        Dictionary<int, int> pending_apt_dict = new Dictionary<int, int>();

        foreach (var stf in searched)
        {
            var pending_appointment = db.Appointments
                                            .Where(ap => ap.StaffId == stf.Id && ap.Status == 1)
                                            .Count();

            pending_apt_dict.Add(stf.Id, pending_appointment);
        }

        ViewBag.pending_apt_dict = pending_apt_dict;

        // sorting
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<Staff, object> fn = sort switch
        {
            "ID" => s => s.Id,
            "Name" => s => s.Person.Name,
            "Gender" => s => s.Person.Gender,
            "Role" => s => s.Role,
            "Pending Appointment(s)" => s => pending_apt_dict.ContainsKey(s.Id) ? pending_apt_dict[s.Id] : 0,
            "Email" => s => s.Person.Registration_date,
            _ => s => s.Id,
        };

        var sorted = dir == "desc" ? searched.OrderByDescending(fn) : searched.OrderBy(fn);

        // paging
        if (page < 1)
        {
            return RedirectToAction(null, new { name, sort, dir, role, page_items, page = 1 });
        }

        // filter items per page
        ViewBag.Page_items = page_items;

        var m = sorted.ToPagedList(page, page_items);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { name, sort, dir, role, page_items, page = m.PageCount });
        }

        // check whether it is ajax
        if (Request.IsAjax())
        {
            return PartialView("_StaffList", m);
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
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("List");
        }

        // Get the Preview Id for the new staff
        var latest_id = db.Staffs
                        .Max(s => s.Id);
        ViewBag.New_id = latest_id + 1;

        return View();
    }

    [HttpPost]
    public IActionResult Create(StaffPersonVM vm)
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

        // check Staff Id
        if (ModelState.IsValid("Staff.Id") && db.Staffs.Any(s => s.Id == vm.Staff.Id))
        {
            TempData["Error_Title"] = "Staff Create Failed";
            TempData["Alert_Error"] = "Invalid Id.";

            // return to previous access page
            return Redirect(Request.Headers.Referer.ToString());
        }

        // check if the Email is valid
        if (ModelState.IsValid("Person.Email"))
        {
            // check if the Email is duplicated
            if (db.Persons.Any(p => p.Email == vm.Person.Email))
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

        // check password
        if (!string.IsNullOrWhiteSpace(vm.Person.Password))
        {
            var password_err = hp.ValidatePassword(vm.Person.Password);
            if (password_err != null)
            {
                ModelState.AddModelError("Person.Password", password_err);
            }
        } else
        {
            ModelState.AddModelError("Person.Password", "Password cannot be empty.");
        }

        // check whether photo is uploaded
        if (vm.Person.Upload_image != null)
        {
            var staffFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/user/staff");

            // Check if the folder exists; if not, create it
            if (!Directory.Exists(staffFolderPath))
            {
                Directory.CreateDirectory(staffFolderPath);
            }

            vm.Person.Avatar = hp.SavePhoto(vm.Person.Upload_image, "images/user/staff").Result;
        }

        // validate entire model
        if (ModelState.IsValid)
        {
            var person = new Person
            {
                Name = vm.Person.Name.Trim(),
                Gender = vm.Person.Gender,
                Avatar = vm.Person.Avatar,
                Email = vm.Person.Email.Trim(),
                Password = hp.HashPassword(vm.Person.Password.Trim()),
                Tel_no = vm.Person.Tel_no.Trim(),
                Registration_date = DateTime.Now,
                Status = vm.Person.Status,
            };

            db.Persons.Add(person);
            db.SaveChanges();

            int personId = person.Id;

            db.Staffs.Add(new()
            {
                Role = 2,
                Salary = vm.Staff.Salary,
                Address = vm.Staff.Address.Trim(),
                PersonId = personId
            });

            db.SaveChanges();

            TempData["Success_Title"] = "Staff Create Success";
            TempData["Alert_Success"] = "Staff is created successfully.";
            return RedirectToAction("List");
        }

        // redeclare New_id (if error occurs)
        var latest_id = db.Staffs
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
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("List");
        }

        // Get the staff detail
        var staff = db.Staffs
                    .Include(s => s.Person)
                    .FirstOrDefault(s => s.Id == id);

        if (staff == null)
        {
            return RedirectToAction("List");
        }

        // map Person to Staff model
        var vm = _mapper.Map<StaffPersonVM>(staff);

        return View(vm);
    }

    [HttpPost]
    public IActionResult Detail(StaffPersonVM vm)
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

        // Find the staff detail
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
            if (db.Persons.Any(p => p.Email == vm.Person.Email && p.Id != s.Person.Id))
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
            s.Person.Name = vm.Person.Name.Trim();
            s.Person.Gender = vm.Person.Gender;
            s.Person.Email = vm.Person.Email.Trim();
            s.Person.Tel_no = vm.Person.Tel_no.Trim();
            s.Salary = vm.Staff.Salary;
            s.Address = vm.Staff.Address;

            // check whether new photo is uploaded
            if (vm.Person.Upload_image != null)
            {
                var staffFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/user/staff");

                // Check if the folder exists; if not, create it
                if (!Directory.Exists(staffFolderPath))
                {
                    Directory.CreateDirectory(staffFolderPath);
                }

                if (s.Person.Avatar != null) hp.DeletePhoto(s.Person.Avatar, "images/user/staff");
                s.Person.Avatar = hp.SavePhoto(vm.Person.Upload_image, "images/user/staff").Result;
            }

            // check whether new passowrd is added
            if (vm.Person.Password != null)
            {
                s.Person.Password = hp.HashPassword(vm.Person.Password.Trim());
            }

            db.SaveChanges();

            TempData["Success_Title"] = "Update Success";
            TempData["Alert_Success"] = "Staff detail is updated.";
            return RedirectToAction("List");
        }
        //else
        //{
        //    // debugging purpose only
        //    foreach (var state in ModelState)
        //    {
        //        var key = state.Key; // Name of the field
        //        var errors = state.Value.Errors; // List of errors for this field

        //        foreach (var error in errors)
        //        {
        //            Console.WriteLine($"Field: {key}, Error: {error.ErrorMessage}");
        //        }
        //    }
        //}

        return View(vm);
    }

    public IActionResult SwitchRole(int id)
    {
        int user_id = int.Parse(Helpers.GetUser().UserId);

        var user_role = db.Staffs
                            .Where(s => s.Id == user_id)
                            .Select(s => s.Role)
                            .FirstOrDefault();

        // get current user and selected staff record
        var u = db.Staffs.Find(user_id);
        var s = db.Staffs.Find(id);

        // check current user role and existence of u and s
        if (user_role == 1 && u != null && s != null)
        {
            // switch role
            s.Role = 1;
            u.Role = 2;
            db.SaveChanges();

            // define success message
            TempData["Success_Title"] = "Role Switch Success";
            TempData["Alert_Success"] = "The role has been switched successfully.";
        }
        else
        {
            TempData["Error_Title"] = "Role Switch Failed";
            TempData["Alert_Error"] = "The role has failed to switch.";

            // return to previous access page
            return Redirect(Request.Headers.Referer.ToString());
        }

        return RedirectToAction("List");
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

        var s = db.Staffs
                    .Include(s => s.Person)
                    .FirstOrDefault(s => s.Id == id);

        if (s != null)
        {
            s.Person.Status = 1;
            s.Person.Login_failed_time = 0;
            db.SaveChanges();

            TempData["Success_Title"] = "Activate Success";
            TempData["Alert_Success"] = "The staff has been activated.";
        }
        else
        {
            TempData["Error_Title"] = "Activate Failed";
            TempData["Alert_Error"] = "The staff does not exist.";
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
        
        var s = db.Staffs
                    .Include(s => s.Person)
                    .FirstOrDefault(s => s.Id == id);

        if (s != null)
        {
            s.Person.Status = 0;
            db.SaveChanges();

            TempData["Success_Title"] = "Deactivate Success";
            TempData["Alert_Success"] = "The staff has been deactivated.";
        }
        else
        {
            TempData["Error_Title"] = "Deactivate Failed";
            TempData["Alert_Error"] = "The staff does not exist.";
        }

        return Redirect(Request.Headers.Referer.ToString());
    }

    // Staff & Manager Profile
    public IActionResult Profile()
    {
        var userId = HttpContext.Session.GetInt32("_userId");
        var userRole = HttpContext.Session.GetString("_userRole");

        if (userId == null || !userRole.Equals("staff"))
        {
            // Handle the case where the user ID is invalid
            TempData["Error_Title"] = "Access Denied";
            TempData["Alert_Error"] = "You have not logged in or no access for this page. Try to log in with other account to access this website.";
            return RedirectToAction("Home", "Home");
        }
        else
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

            return View(m);
        }
    }

    [HttpPost]
    public IActionResult Profile(StaffPersonVM vm)
    {
        var pwd_verify = true;

        var userId = HttpContext.Session.GetInt32("_userId");
        var userRole = HttpContext.Session.GetString("_userRole");

        if (userId == null || !userRole.Equals("staff"))
        {
            // Handle the case where the user ID is invalid
            TempData["Error_Title"] = "Access Denied";
            TempData["Alert_Error"] = "You have not logged in or no access for this page. Try to log in with other account to access this website.";
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
        }

        // check if the Avatar is null or valid
        if (vm.Person.Upload_image != null)
        {
            var error = hp.ValidateImage(vm.Person.Upload_image);
            if (error != "") ModelState.AddModelError("Person.Upload_image", error);
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
                if (!vm.Person.Current_password.Equals(s.Person.Password))
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
                var staffFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/user/staff");

                // Check if the folder exists; if not, create it
                if (!Directory.Exists(staffFolderPath))
                {
                    Directory.CreateDirectory(staffFolderPath);
                }

                if (s.Person.Avatar != null) hp.DeletePhoto(s.Person.Avatar, "images/user/staff");
                s.Person.Avatar = hp.SavePhoto(vm.Person.Upload_image, "images/user/staff").Result;
            }

            // check whether new passowrd is added
            if (vm.Person.Password != null)
            {
                s.Person.Password = hp.HashPassword(vm.Person.Password.Trim());
            }

            db.SaveChanges();

            TempData["Success_Title"] = "Update Success";
            TempData["Alert_Success"] = "Profile detail is updated.";
            return RedirectToAction("Profile");
        }

        TempData["Error_Title"] = "Update Failed";
        TempData["Alert_Error"] = "Profile detail failed to update. May have some input invalid. Try to update again..";
        return View(vm);
    }

    public IActionResult ImportStaff()
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

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ImportStaff(IFormFile file, [FromServices] Microsoft.AspNetCore.Hosting.IHostingEnvironment hostingEnvironment)
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
            List<Staff> staffs = GetStaffList(filePath);
            int totalCount = staffs?.Count ?? 0;
            int successCount = 0;
            int failureCount = 0;

            if (staffs != null && staffs.Count > 0)
            {
                // Collect emails to detect duplicates within the file
                var emailSet = new HashSet<string>();

                foreach (var staff in staffs)
                {
                    // Validate required fields: Name, Email, and Gender
                    if (string.IsNullOrEmpty(staff.Person.Name) ||
                        string.IsNullOrEmpty(staff.Person.Email) ||
                        string.IsNullOrEmpty(staff.Person.Gender) ||
                        !(staff.Person.Gender.ToUpper() == "F" || staff.Person.Gender.ToLower() == "m"))
                    {
                        failureCount++;
                        continue;
                    }

                    // Check for duplicate email within the file
                    if (!emailSet.Add(staff.Person.Email))
                    {
                        failureCount++;
                        continue;
                    }

                    // Check if the email already exists in the database
                    var existingPerson = await db.Persons
                                            .FirstOrDefaultAsync(p => p.Email == staff.Person.Email);

                    if (existingPerson != null)
                    {
                        // If the email already exists, count as failed and continue
                        failureCount++;
                        continue;
                    }

                    // If all validations pass, add the staff and person to the database
                    try
                    {
                        // Add person to the database
                        db.Persons.Add(staff.Person);
                        await db.SaveChangesAsync();

                        int personId = staff.Person.Id;

                        db.Staffs.Add(staff);

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

        return RedirectToAction("ImportStaff");
    }


    private List<Staff> GetStaffList(string filePath)
    {
        List<Staff> staffs = new List<Staff>();
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

                    staffs.Add(new Staff
                    {
                        Person = new Person
                        {
                            Name = values.ElementAtOrDefault(0),
                            Gender = values.ElementAtOrDefault(1),
                            Email = values.ElementAtOrDefault(2),
                            Password = values.ElementAtOrDefault(3),
                            Tel_no = values.ElementAtOrDefault(4)
                        },
                        Role = 2,
                        Salary = decimal.TryParse(values.ElementAtOrDefault(5), out decimal salary) ? salary : 0m, // Default to 0 if parsing fails
                        Address = values.ElementAtOrDefault(6)
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

                        staffs.Add(new Staff
                        {
                            Person = new Person
                            {
                                Name = reader.GetValue(0)?.ToString(),
                                Gender = reader.GetValue(1)?.ToString(),
                                Email = reader.GetValue(2)?.ToString(),
                                Password = reader.GetValue(3)?.ToString(),
                                Tel_no = reader.GetValue(4)?.ToString()
                            },
                            Role = 2, // Assign the default role (you can change it as needed)
                            Salary = decimal.TryParse(reader.GetValue(5)?.ToString(), out decimal salary) ? salary : 0m, // Convert to decimal for salary
                            Address = reader.GetValue(6)?.ToString() // Assuming address is at column index 6
                        });
                    }
                }
            }
        }

        return staffs;
    }
}
