using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using Assignment.Models;
using AutoMapper.Execution;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using Microsoft.VisualBasic;
using static Org.BouncyCastle.Crypto.Engines.SM2Engine;


namespace Assignment.Controllers;

public class AppointmentController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helpers hp;
    private string err = "";

    public AppointmentController(DB db, IWebHostEnvironment en, Helpers hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
    }

    public IActionResult Calendar(string? type, string? status)
    {
        // Store the page that user from
        hp.SetCookie("redirect", "/Appointment/Calendar");

        // Check user authorization
        if (!hp.CheckRole(new string[] { "staff", "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        ViewBag.Type = type;
        ViewBag.Status = status;

        // Update the appointment status
        CheckAppointment();

        // Get user role
        var UserRole = HttpContext.Session.GetString("_userRole");
        var UserId = HttpContext.Session.GetInt32("_userId");

        IQueryable<Appointment> appointmentsQuery;

        if (UserRole == "member")
        {
            appointmentsQuery = db.Appointments
                .Where(s => s.MemberId == UserId || s.Type == 3); // Filter appointments for the member

            // Filter by type if the parameter is provided
            if (!string.IsNullOrEmpty(type))
            {
                int parsedType;
                if (int.TryParse(type, out parsedType))
                {
                    appointmentsQuery = appointmentsQuery.Where(a => a.Type == parsedType);
                }
            }

            // Filter by status if the parameter is provided
            if (!string.IsNullOrEmpty(status))
            {
                int parsedStatus;
                if (int.TryParse(status, out parsedStatus))
                {
                    appointmentsQuery = appointmentsQuery.Where(a => a.Status == parsedStatus);
                }
            }

            var appointments = appointmentsQuery
                .Where(a => !(a.Type == 3 && a.Status == 0)) // Exclude the cancelled holiday
                .Select(a => new
                {
                    // type 1 = training, 2 = working, 3 = holiday
                    Id = a.Id,
                    CalendarId = a.Type == 2 ? "working" : (a.Type == 3 ? "holiday" : "others"),
                    Title = a.Title,
                    IsAllDay = a.All_day,
                    Start = a.Start_datetime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    End = a.End_datetime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    Category = a.Type == 3 ? "allday" : "time",
                    State = (string?)null,
                    Location = a.RoomId == null ? null : a.Room.Name,
                    Attendees = a.MemberId == null ? null : new[] { a.Member.Person.Name },
                    Body = "xxx",
                    Raw = new
                    {
                        Staff = a.Staff.Person.Name,
                        Treatment = Helpers.FormatId("PK", a.OrderPackageTreatment.OrderPackage.Id) + " - " + a.OrderPackageTreatment.Treatment,
                        Email = a.Staff.Person.Email,
                        Status = Helpers.GetAppointmentStatus(a.Status),
                        AppointmentId = a.Id
                    }
                })
                .ToList();

            return View("Member/Calendar", appointments);
        }
        else if (UserRole == "staff")
        {
            appointmentsQuery = db.Appointments
                .Where(s => s.StaffId == UserId || s.Type == 3); // Filter appointments for the staff

            // Filter by type if the parameter is provided
            if (!string.IsNullOrEmpty(type))
            {
                int parsedType;
                if (int.TryParse(type, out parsedType))
                {
                    appointmentsQuery = appointmentsQuery.Where(a => a.Type == parsedType);
                }
            }

            // Filter by status if the parameter is provided
            if (!string.IsNullOrEmpty(status))
            {
                int parsedStatus;
                if (int.TryParse(status, out parsedStatus))
                {
                    appointmentsQuery = appointmentsQuery.Where(a => a.Status == parsedStatus);
                }
            }

            var appointments = appointmentsQuery
                .Where(a => !(a.Type == 3 && a.Status == 0)) // Exclude the cancelled holiday
                .Select(a => new
                {
                    // type 1 = training, 2 = working, 3 = holiday
                    Id = a.Id,
                    CalendarId = a.Type == 2 ? "working" : (a.Type == 3 ? "holiday" : (a.Type == 1 ? "training" : "others")),
                    Title = a.Title,
                    IsAllDay = a.All_day,
                    Start = a.Start_datetime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    End = a.End_datetime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    Category = (a.Type == 3 || (a.Type == 1 && (EF.Functions.DateDiffDay(a.Start_datetime, a.End_datetime) >= 1))) ? "allday" : "time",
                    State = (string?)null,
                    Location = a.RoomId == null ? null : a.Room.Name,
                    Attendees = a.MemberId == null ? null : new[] { a.Member.Person.Name },
                    Body = "xxx",
                    Raw = new
                    {
                        Staff = a.Staff.Person.Name,
                        Treatment = a.OrderPackageTreatmentId != null
                            ? Helpers.FormatId("PK", a.OrderPackageTreatment.OrderPackage.Id) + " - " + a.OrderPackageTreatment.Treatment
                            : "N/A",
                        Email = a.Staff.Person.Email,
                        Status = Helpers.GetAppointmentStatus(a.Status),
                        AppointmentId = a.Id,
                    }
                })
                .ToList();

            // check whether it is ajax
            if (Request.IsAjax())
            {
                return PartialView("Staff/_Calendar", appointments);
            }

            return View("Staff/Calendar", appointments);
        }

        return RedirectToAction("Home", "Home");
    }

    public IActionResult FullCalendar()
    {
        // Check user authorization
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // Update the appointment status
        CheckAppointment();

        var appointments = db.Appointments
            .Where(a => !(a.Type == 3 && a.Status == 0)) // Exclude the cancelled holiday
            .Select(a => new
            {
                // type 1 = training, 2 = working, 3 = holiday
                Id = a.Id,
                CalendarId = a.Type == 2 ? "working" : (a.Type == 3 ? "holiday" : (a.Type == 1 ? "training" : "others")),
                Title = a.Title,
                IsAllDay = a.All_day, // Default back to the original value if not type 2 or condition not met
                Start = a.Start_datetime.ToString("yyyy-MM-ddTHH:mm:ss"),
                End = a.End_datetime.ToString("yyyy-MM-ddTHH:mm:ss"),
                Category = (a.Type == 3 || (a.Type == 1 && (EF.Functions.DateDiffDay(a.Start_datetime, a.End_datetime) >= 1))) ? "allday" : "time",
                State = (string?)null,
                Location = a.RoomId == null ? null : a.Room.Name,
                Attendees = a.MemberId == null ? null : new[] { a.Member.Person.Name },
                Body = "xxx",
                Raw = new
                {
                    Staff = a.Staff.Person.Name,
                    Treatment = a.OrderPackageTreatmentId != null
                            ? Helpers.FormatId("PK", a.OrderPackageTreatment.OrderPackage.Id) + " - " + a.OrderPackageTreatment.Treatment
                            : "N/A",
                    Email = a.Staff.Person.Email,
                    Status = Helpers.GetAppointmentStatus(a.Status),
                    AppointmentId = a.Id
                }
            })
            .ToList();

        return View("Staff/FullCalendar", appointments);
    }

    public IActionResult Create()
    {
        // Store the page that user from
        hp.SetCookie("redirect", "/Appointment/Create");

        // Check user authorization
        if (!hp.CheckRole(new string[] { "staff", "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // Update the appointment status
        CheckAppointment();

        // Get user role
        var UserRole = HttpContext.Session.GetString("_userRole");
        var UserId = HttpContext.Session.GetInt32("_userId");

        if (UserRole == "member")
        {
            var packages = GetOrderPackages(UserId);
            var staffList = GetStaffs();

            var m = db.Members.Find(UserId);
            ViewBag.MemberLevel = m.Level;
            ViewBag.PackageList = packages;
            ViewBag.StaffList = staffList;

            return View("Member/Create");


        }
        else if (UserRole == "staff")
        {
            var memberList = GetMembers();
            ViewBag.MemberList = memberList;

            return View("Staff/Create");
        }

        return RedirectToAction("Home", "Home");
    }

    [HttpPost]
    public IActionResult Create(AppointmentVM vm)
    {
        // Check user authorization
        if (!hp.CheckRole(new string[] { "staff", "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // Update the appointment status
        CheckAppointment();
        var isMember = HttpContext.Session.GetString("_userRole") == "member" ? true : false;

        // Get member id
        if (isMember)
        {
            vm.MemberId = HttpContext.Session.GetInt32("_userId");
        }

        //========================================
        // Check for is it member and staff exist
        //========================================
        var member = db.Members.Where(s => s.Id == vm.MemberId).Include(s => s.Person).FirstOrDefault();
        if (member == null)
        {
            ModelState.AddModelError("MemberId", "The selected member does not exist.");
        }

        var staff = db.Staffs.Where(s => s.Id == vm.StaffId).Include(s => s.Person).FirstOrDefault();
        if (staff == null && vm.StaffId != 0)
        {
            ModelState.AddModelError("StaffId", "The selected staff does not exist.");
        }

        //========================================
        // Validate appointment date and time
        //========================================
        if (vm.AppointmentDate == null)
        {
            ModelState.AddModelError("AppointmentDate", "Appointment date is required.");
        }

        // Combine AppointmentDate with Start_datetime and End_datetime
        DateTime startDateTime = vm.AppointmentDate.Date.Add(vm.Start_datetime.TimeOfDay);
        DateTime endDateTime = vm.AppointmentDate.Date.Add(vm.End_datetime.TimeOfDay);
        DateTime currentDateTime = DateTime.Now;

        if (startDateTime < currentDateTime.Date ||
            (startDateTime.Date == currentDateTime.Date && startDateTime < currentDateTime.AddMinutes(15)))
        {
            ModelState.AddModelError("AppointmentDate", "The appointment date must be today or a future date.");
            ModelState.AddModelError("Start_datetime", "Start time must be at least 15 minutes from now.");
        }
        else if (!hp.ValidateDateTime(startDateTime, endDateTime))
        {
            ModelState.AddModelError("End_dateTime", "End time must be after the start time and not in the past.");
        }

        //========================================
        // Check for overlapping appointments for the member
        //========================================
        var overlappingMemberAppointments = db.Appointments
                            .Where(a => a.MemberId == vm.MemberId &&
                                        (
                                            ((startDateTime >= a.Start_datetime && startDateTime < a.End_datetime) ||
                                             (endDateTime > a.Start_datetime && endDateTime <= a.End_datetime))
                                        ) &&
                                        (a.Status == 1 || a.Status == 2))
                            .ToList();

        if (overlappingMemberAppointments.Any())
        {
            err = "The selected time conflicts with another appointment for the member.";
            if (isMember)
            {
                err = "The selected time conflicts with another appointment you have, please choose a different time.";
            }
            ModelState.AddModelError("AppointmentDate", err);
        }

        //========================================
        // Check for overlapping holidays
        //========================================
        var conflictingHolidays = db.Appointments
            .Where(a => a.Type == 3 && // Holiday type
                        ((startDateTime >= a.Start_datetime && startDateTime < a.End_datetime) ||
                         (endDateTime > a.Start_datetime && endDateTime <= a.End_datetime)) &&
                         (a.Status == 1 || a.Status == 2))
            .ToList();

        if (conflictingHolidays.Any())
        {
            ModelState.AddModelError("AppointmentDate", "The selected time conflicts with a holiday.");
        }

        //========================================
        // Check for overlapping appointments with the same room
        //========================================
        // Fetch available rooms for the selected date and time
        var availableRooms = db.Rooms
            .Where(r => r.Status == 1) // Only active rooms
            .Where(r => !db.Appointments.Any(a =>
                        a.RoomId == r.Id &&
                        (a.Status == 1 || a.Status == 2) && // Ongoing or booked appointments
                        ((startDateTime < a.End_datetime && startDateTime >= a.Start_datetime) || // Overlapping start
                         (endDateTime > a.Start_datetime && endDateTime <= a.End_datetime) ||    // Overlapping end
                         (startDateTime <= a.Start_datetime && endDateTime >= a.End_datetime)))) // Fully contained overlap
            .ToList();

        if (!availableRooms.Any())
        {
            // Add a meaningful error message to the model state
            ModelState.AddModelError("AppointmentDate", "The selected date and time has no available rooms. Please choose a different time.");
        }
        else
        {
            // Assign the first available room (or implement your preferred selection logic)
            vm.RoomId = availableRooms.First().Id;
        }

        //========================================
        // Check for overlapping appointments with the selected staff
        //========================================
        if (vm.StaffId != 0) // Specific staff selected
        {
            if (CheckStaffOverlappingAppointment(vm.StaffId, startDateTime, endDateTime))
            {
                ModelState.AddModelError("StaffId", "The selected time is unavailable for the chosen staff.");
            }
        }
        else // Auto-assign staff
        {
            // Find all available staff for the given time slot
            var availableStaff = db.Staffs
                .Where(s => s.Person.Status == 1) // Active staff
                .Where(s => !db.Appointments.Any(a =>
                            a.StaffId == s.Id &&
                            (a.Status == 1 || a.Status == 2) && // Ongoing or booked appointments
                            ((startDateTime < a.End_datetime && startDateTime >= a.Start_datetime) || // Overlapping start
                             (endDateTime > a.Start_datetime && endDateTime <= a.End_datetime) ||    // Overlapping end
                             (startDateTime <= a.Start_datetime && endDateTime >= a.End_datetime)))) // Fully contained overlap
                .ToList();

            if (!availableStaff.Any()) // No staff available
            {
                ModelState.AddModelError("StaffId", "No staff available for the selected date and time.");
            }
            else
            {
                // Randomly select a staff member
                var random = new Random();
                var selectedStaff = availableStaff[random.Next(availableStaff.Count)];
                vm.StaffId = selectedStaff.Id;
                staff = db.Staffs.Include(s => s.Person).Where(s => s.Id == vm.StaffId).FirstOrDefault();
            }
        }

        //========================================
        // Check if the selected package belongs to the member
        //========================================
        var orderPackage = db.OrderPackages
                         .Include(o => o.Package)
                         .FirstOrDefault(p => p.Id == vm.PackageId && p.Order.MemberId == vm.MemberId && p.Expired_datetime > DateTime.Now);

        if (orderPackage == null)
        {
            err = "The selected package does not belong to the member.";
            if (isMember)
            {
                err = "The selected package does not belong to you.";
            }
            ModelState.AddModelError("SelectedPackage", err);
        }
        else if (orderPackage.Remaining_treatment <= 0) // Validate remaining time for the package
        {
            ModelState.AddModelError("SelectedPackage", "The selected package has no remaining time.");
        }
        else if (orderPackage != null)
        {
            //========================================
            // After validating, Get the OrderPackageTreatmentId
            //========================================

            var treatment = orderPackage.Package.Treatment - orderPackage.Remaining_treatment + 1;
            var orderPackageTreatment = db.OrderPackageTreatments
                                          .Where(t => t.OrderPackageId == vm.PackageId &&
                                                      t.OrderPackage.Order.MemberId == vm.MemberId &&
                                                      t.Treatment == treatment)
                                          .FirstOrDefault();

            if (orderPackageTreatment == null)
            {
                ModelState.AddModelError("SelectedPackage", "No treatment associated with the selected package.");
            }
            else if (orderPackageTreatment.Treatment != treatment) // Verify that the treatment matches the calculated value
            {
                ModelState.AddModelError("SelectedPackage", "The treatment in the selected order package does not match the expected treatment value.");
            }

            //========================================
            // All field valid
            //========================================

            if (ModelState.IsValid)
            {
                return CreateVMValid(member, staff, startDateTime, endDateTime, vm, orderPackage, orderPackageTreatment);
            }
            else
            {
                TempData["Error_Title"] = "Appointment Create Failed";
                TempData["Alert_Error"] = "Appointment creation failed. Please ensure all fields are correctly filled and try again.";
            }
        }


        // Input invalid or have error redirect page base role
        if (isMember) // if role is member
        {
            var packages = GetOrderPackages(vm.MemberId);
            var staffList = GetStaffs();

            var m = db.Members.Find(vm.MemberId);
            ViewBag.MemberLevel = m.Level;
            ViewBag.PackageList = packages;
            ViewBag.StaffList = staffList;
            vm.AppointmentDate = DateTime.MinValue;

            return View("Member/Create", vm);
        }

        // If role is staff
        var memberList = GetMembers();
        ViewBag.MemberList = memberList;
        return View("Staff/Create", vm);
    }

    // Handle insert data to db when create appointmetn field all are valid
    public IActionResult CreateVMValid(Models.Member member, Staff staff, DateTime startDateTime, DateTime endDateTime, AppointmentVM vm, OrderPackage orderPackage, OrderPackageTreatment orderPackageTreatment)
    {
        // Generate code for barcode
        string uniqueCode = hp.GenerateRandomCode();

        // Save the OrderPackageTreatmentId in the appointment
        var newAppointment = new Appointment
        {
            OrderPackageTreatmentId = orderPackageTreatment.Id,
            MemberId = member.Id,
            StaffId = vm.StaffId,
            RoomId = vm.RoomId,
            Title = orderPackage.Package.Name,
            Type = 2, // Working Aoppointment
            Start_datetime = startDateTime,
            End_datetime = endDateTime,
            Created_datetime = DateTime.Now,
            BarCode = uniqueCode,
            Status = 1
        };

        // Deduct the time from the order package's remaining time
        orderPackage.Remaining_treatment--;

        db.Appointments.Add(newAppointment);
        db.SaveChanges();

        // Create the Inbox Notification after the appointment is created
        var title = "Appointment Booked Successfully";
        var msg = $"You have a new appointment (#{newAppointment.Code}) has been booked from {newAppointment.Start_datetime} to {newAppointment.End_datetime}.";

        AddInboxMessage(title, msg, 3, 2, newAppointment.StaffId, member.Id, newAppointment.Id);

        if (member != null && staff != null) // just for double checking
        {
            // Send notification to email
            SendNotification(title, msg, member.Person.Email);
            SendNotification(title, msg, staff.Person.Email);
        }

        db.SaveChanges();

        TempData["Success_Title"] = "New Appointment Booked";
        TempData["Alert_Success"] = $"A new appointment has been successfully booked from {hp.ControllerFormatDateTime(newAppointment.Start_datetime)} to {hp.ControllerFormatDateTime(newAppointment.End_datetime)}.";

        if (HttpContext.Session.GetString("_userRole") == "member")
        {
            return RedirectToAction("Calendar");
        }
        else
        {
            return RedirectToAction("FullCalendar");
        }
    }

    // Get: Create not working appointment
    public IActionResult CreateOther()
    {
        // Check user authorization
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // Update the appointment status
        CheckAppointment();

        var staffList = GetStaffs(false);
        ViewBag.StaffList = staffList;

        return View("Staff/CreateOther");
    }

    // Post: Create not working appointment
    [HttpPost]
    public IActionResult CreateOther(AppointmentVM vm)
    {
        // Check user authorization
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // Update the appointment status
        CheckAppointment();

        // Check is it select staff if is training appointment
        if (vm.Type == null || (vm.Type != 1 && vm.Type != 3))
        {
            ModelState.AddModelError("Type", "Type is required.");
        }
        else if (vm.Type == 1 && (vm.StaffList == null || vm.StaffList.Count == 0))
        {
            ModelState.AddModelError("StaffList", "Staff is required (One or many).");
        }
        else if (vm.Type == 1 && vm.StaffList != null && vm.StaffList.Count > 5)
        {
            ModelState.AddModelError("StaffList", "Maximum select 5 staff for one training appointment creation.");
        }

        // Check Title
        if (vm.Title == null)
        {
            ModelState.AddModelError("Title", "Title is required.");
        }

        // Check Start Date
        if (vm.Start_datetime < DateTime.Now.AddMinutes(15))
        {
            ModelState.AddModelError("Start_datetime", "Start DateTime must be at least 15 minutes after current time.");
        }

        // Check End Date and validate the End Date cannot earlier than Start Date
        if (vm.Start_datetime > vm.End_datetime)
        {
            ModelState.AddModelError("End_datetime", "EndEnd DateTime cannot be same or earlier than Start DateTime.");
        }

        if (ModelState.IsValid)
        {
            if (vm.Type == 1) // Training appointment
            {
                int fail = 0;
                List<string> staffEmails = new List<string>(); // To collect staff emails for sending notifications

                foreach (var staffId in vm.StaffList)
                {
                    // Check if the staff exists and their status is 1
                    var staffExists = db.Staffs.Any(s => s.Id == staffId && s.Person.Status == 1);

                    var hasConflict = true;
                    if (staffExists)
                    {
                        // Check if the staff has conflicting appointments
                        hasConflict = CheckStaffOverlappingAppointment(staffId, vm.Start_datetime, vm.End_datetime);

                        if (!hasConflict)
                        {
                            // Add the appointment if no conflict is found
                            Appointment workingAppointment = new Appointment
                            {
                                Type = vm.Type ?? 0,
                                Title = vm.Title,
                                Start_datetime = vm.Start_datetime,
                                End_datetime = vm.End_datetime,
                                Status = 1, // Active status
                                Created_datetime = DateTime.Now,
                                StaffId = staffId, // Assign each staff ID to a separate appointment
                            };

                            db.Appointments.Add(workingAppointment);
                            db.SaveChanges();


                            // Collect email for notification
                            var staff = db.Staffs.Include(s => s.Person).FirstOrDefault(s => s.Id == staffId);
                            if (staff != null)
                            {
                                staffEmails.Add(staff.Person.Email);

                                // Add inbox notification for the staff
                                string inboxTitle = "New Training Appointment Scheduled";
                                string inboxMessage = $"Dear {staff.Person.Name},\n\nYou have been scheduled for a training appointment: {vm.Title}.\n\nStart Date: {hp.ControllerFormatDateTime(vm.Start_datetime)}\nEnd Date: {hp.ControllerFormatDateTime(vm.End_datetime)}\n\nPlease ensure you are prepared.\n\nThank you!";

                                AddInboxMessage(inboxTitle, inboxMessage, 2, 2, staffId, null, workingAppointment.Id);

                            }
                        }
                    }

                    if (!staffExists || hasConflict) { fail++; }
                }

                if (fail < vm.StaffList.Count)
                {

                    // Send email notifications to all enrolled staff
                    foreach (var staffEmail in staffEmails)
                    {
                        string title = "New Training Appointment Scheduled";
                        string msg = $"Dear Staff,\n\nYou have been scheduled for a training appointment: {vm.Title}.\n\nStart Date: {vm.Start_datetime}\nEnd Date: {vm.End_datetime}\n\nPlease be prepared.\n\nThank you!";

                        SendNotification(title, msg, staffEmail);
                    }

                    // When at least one staff is available
                    TempData["Success_Title"] = "Appointment Creation Success";
                    TempData["Alert_Success"] = $"Training appointment successfully created.\nTotal selected staff: {vm.StaffList.Count}\nStaff booked successfully: {vm.StaffList.Count - fail}\nStaff booking failed: {fail}.";

                    // Save changes to the database (inboxes and appointments)
                    db.SaveChanges();

                    return RedirectToAction("FullCalendar");
                }
                else
                {
                    // When all staff in the list are unavailable
                    TempData["Error_Title"] = "Appointment Creation Failed";
                    TempData["Alert_Error"] = "None of the selected staff are available or exist.";
                }
            }
            else if (vm.Type == 3) // Holiday appointment
            {
                if (vm.All_day == true)
                {
                    // Keep the same date for End_datetime but set the time to 11:59 PM
                    vm.End_datetime = new DateTime(vm.End_datetime.Year, vm.End_datetime.Month, vm.End_datetime.Day, 23, 59, 0);
                }

                Appointment holidayAppointment = new Appointment
                {
                    Type = vm.Type ?? 0,
                    Title = vm.Title,
                    Start_datetime = vm.Start_datetime,
                    End_datetime = vm.End_datetime,
                    Status = 1, // Active holiday
                    All_day = vm.All_day ?? false,
                    Created_datetime = DateTime.Now,
                };

                db.Appointments.Add(holidayAppointment);
                db.SaveChanges(); // Save the holiday appointment first

                // Find conflicting non-holiday appointments
                var conflictingAppointments = db.Appointments
                    .Include(a => a.Member.Person)
                    .Include(a => a.Staff.Person)
                    .Where(a =>
                        a.Type != 3 && // Exclude other holiday appointments
                        (a.Status == 1 || a.Status == 2) && // Only active or pending appointments
                        ((vm.Start_datetime >= a.Start_datetime && vm.Start_datetime < a.End_datetime) || // Start time overlaps
                         (vm.End_datetime > a.Start_datetime && vm.End_datetime <= a.End_datetime) ||    // End time overlaps
                         (vm.Start_datetime <= a.Start_datetime && vm.End_datetime >= a.End_datetime)) &&
                         a.Status == 1)  // Encapsulates an existing appointment
                    .ToList();

                int cancelledCount = 0;

                foreach (var appointment in conflictingAppointments)
                {
                    appointment.Status = 0; // Cancelled status
                    cancelledCount++;

                    // Create the notification title and message
                    string title = "Appointment Cancelled";
                    string msg = $"Your appointment titled '{appointment.Title}', scheduled from {appointment.Start_datetime} to {appointment.End_datetime}, has been cancelled due to a holiday.";

                    // Add inbox notification
                    AddInboxMessage(title, msg, 3, 2, appointment.StaffId, appointment.MemberId, appointment.Id);

                    // Send email notification to both staff and member (if it's a working appointment, Type == 2)
                    if (appointment.Type == 2) // Working appointment
                    {
                        SendNotification(title, msg, appointment.Member.Person.Email);
                        SendNotification(title, msg, appointment.Staff.Person.Email);
                    }
                    else if (appointment.Type == 1) // Training appointment
                    {
                        SendNotification(title, msg, appointment.Staff.Person.Email); // Only to the staff
                    }
                }

                db.SaveChanges(); // Save all updates to conflicting appointments

                TempData["Success_Title"] = "Holiday Appointment Created";
                TempData["Alert_Success"] = $"Holiday created successfully. {cancelledCount} conflicting appointments have been cancelled.";
                return RedirectToAction("FullCalendar");
            }
        }

        return View("Staff/CreateOther", vm);
    }

    [HttpPost]
    public IActionResult Cancel(int id)
    {
        // Check user authorization
        if (!hp.CheckRole(new string[] { "staff", "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // Update the appointment status
        CheckAppointment();

        // Get user role
        var UserRole = HttpContext.Session.GetString("_userRole");

        // Check whether sucess update status or not
        var update = false;

        var appointment = db.Appointments
                            .Where(a => a.Id == id && a.Status == 1 && a.Start_datetime.AddMinutes(-15) >= DateTime.Now)
                            .Include(a => a.Member.Person)
                            .Include(a => a.Staff.Person)
                            .FirstOrDefault();

        int? memberId = appointment.MemberId;
        if (UserRole == "member")
        {
            memberId = HttpContext.Session.GetInt32("_userId");

            appointment = db.Appointments
                            .Where(a => a.Id == id && a.MemberId == memberId && a.Status == 1 && a.Start_datetime >= DateTime.Now)
                            .Include(a => a.Member.Person)
                            .Include(a => a.Staff.Person)
                            .FirstOrDefault();
        }

        if (appointment != null)
        {
            var orderPackageTreatment = db.OrderPackageTreatments.Find(appointment.OrderPackageTreatmentId);

            if (orderPackageTreatment != null)
            {
                var orderPackage = db.OrderPackages
                                .Include(o => o.Package)
                                .FirstOrDefault(o => o.Id == orderPackageTreatment.OrderPackageId &&
                                            o.Order.MemberId == memberId && o.Expired_datetime > DateTime.Now);

                if (orderPackage != null)
                {
                    appointment.Status = 0;
                    orderPackage.Remaining_treatment++;

                    // Update notification to inbox
                    string title = "Appointment Canceled";
                    string msg = $"Dear User,\n\nThe appointment {appointment.Code}, scheduled from {appointment.Start_datetime} to {appointment.End_datetime}, has been canceled.\n\nWe apologize for any inconvenience caused. If you need further assistance or wish to reschedule, please don't hesitate to reach out.\n\nThank you for your understanding.";


                    // Add notification to inboxes
                    AddInboxMessage(title, msg, 3, 2, appointment.StaffId, appointment.MemberId, appointment.Id);

                    // Send notification to email
                    SendNotification(title, msg, appointment.Member.Person.Email);
                    SendNotification(title, msg, appointment.Staff.Person.Email);

                    db.SaveChanges();

                    update = true;

                    TempData["Success_Title"] = "Appointment Cancellation Success";
                    TempData["Alert_Success"] = "The appointment has been successfully canceled. We apologize for any inconvenience caused.";
                }

            }

        }

        if (update == false)
        {
            TempData["Error_Title"] = "Appointment Cancellation Failed";
            TempData["Alert_Error"] = "The appointment does not exist or is not in a cancellable status. Please check the appointment details and try again.";
        }
        return Redirect(Request.Headers.Referer.ToString());
    }

    // View only for the apppointment detail
    public IActionResult Detail(int id)
    {
        // Store the page that user from
        hp.SetCookie("redirect", $"/Appointment/Detail/{id}");

        // Check user authorization
        if (!hp.CheckRole(new string[] { "staff", "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // Update the appointment status
        CheckAppointment();

        if (HttpContext.Session.GetString("_userRole") == "member")
        {
            var memberId = HttpContext.Session.GetInt32("_userId");

            var appointment = db.Appointments
                                .Include(a => a.Room) // Include Room
                                .Include(a => a.Staff.Person) // Include Staff and Person
                                .Include(a => a.Member.Person) // Include Member and Person
                                .Include(a => a.OrderPackageTreatment.OrderPackage) // Include nested OrderPackageTreatment and OrderPackage
                                .Include(a => a.OrderPackageTreatment.OrderPackage.Package) // Include nested OrderPackageTreatment and OrderPackage
                                .Where(a => a.Id == id && a.MemberId == memberId)
                                .FirstOrDefault();

            if (appointment != null && appointment.Type == 2)
            {
                // Generate barcode image
                string barcodeImage = hp.GenerateBarCodeImage(appointment.BarCode);

                // Fetch related services
                var services = db.OrderPackageTreatmentServices
                                .Include(s => s.Service)
                                .Where(s => s.OrderPackageTreatmentId == appointment.OrderPackageTreatmentId)
                                .ToList(); // Ensures it's not null


                // Create the view model
                var viewModel = new AppointmentDetailVM
                {
                    Appointment = appointment,
                    Services = services,
                    BarCodeImg = barcodeImage
                };

                // Pass view model to the view
                return View("Member/Detail", viewModel);
            }
            else
            {
                string msg = appointment.Type == 1 ? "Training" : "Holiday";
                // Set record not found msg
                TempData["Error_Title"] = "Action Failed";
                TempData["Alert_Error"] = msg + " no detail ~ Enjoy your " + (appointment.Type == 1 ? "training" : "holiday");
            }

        }
        else // When userRole is staff
        {
            var appointment = db.Appointments
                                .Include(a => a.Room) // Include Room
                                .Include(a => a.Staff.Person) // Include Staff and Person
                                .Include(a => a.Member.Person) // Include Staff and Person
                                .Include(a => a.OrderPackageTreatment.OrderPackage) // Include nested OrderPackageTreatment and OrderPackage
                                .Include(a => a.OrderPackageTreatment.OrderPackage.Package) // Include nested OrderPackageTreatment and OrderPackage
                                .Where(a => a.Id == id && (a.Type == 1 || a.Type == 2))
                                .FirstOrDefault();

            if (appointment != null)
            {
                dynamic services = null;

                if (appointment.Type == 2)
                {
                    // Fetch related services
                    services = db.OrderPackageTreatmentServices
                                    .Include(s => s.Service)
                                    .Where(s => s.OrderPackageTreatmentId == appointment.OrderPackageTreatmentId)
                                    .ToList(); // Ensures it's not null
                }



                // Create the view model
                var viewModel = new AppointmentDetailVM
                {
                    Appointment = appointment,
                    Services = services,
                    BarCodeImg = null
                };

                // Pass view model to the view
                return View("Staff/Detail", viewModel);
            }
        }

        // Set record not found msg
        TempData["Error_Title"] = "Action Failed";
        TempData["Alert_Error"] = "Appointment does not found.";

        return RedirectToAction("Calendar");
    }

    // ========================================
    // Method for generate and scan barcode
    // ========================================
    public IActionResult ScanBarCode(int id)
    {
        // Check user authorization
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // Update the appointment status
        CheckAppointment();

        var appointment = db.Appointments.Find(id);

        if (appointment != null)
        {
            return View("Staff/ScanBarCode", id);
        }
        else
        {
            TempData["Error_Title"] = "Action Failed";
            TempData["Alert_Error"] = "The requested appointment does not exist. Please verify the details and try again.";
            return RedirectToAction("Calendar");
        }

    }

    // Post: Get the barcode value and update appointment status
    [HttpPost]
    public IActionResult ScanBarCode(string BarcodeValue, int id)
    {
        // Check user authorization
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // Update the appointment status
        CheckAppointment();

        if (string.IsNullOrEmpty(BarcodeValue))
        {
            return Json(new { success = false, message = "The barcode is invalid or has expired. Please verify the barcode and try again." });
        }

        // Get the current time
        DateTime now = DateTime.Now;

        // Find the appointment and check the time range
        var appointment = db.Appointments
            .Include(a => a.Member.Person)
            .Include(a => a.Staff.Person)
            .Include(a => a.OrderPackageTreatment)
            .Where(a => a.Id == id && a.BarCode == BarcodeValue && a.Status == 1)
            .FirstOrDefault();

        if (appointment != null)
        {
            // Check if the start_datetime is within the 5-minute range
            if (now >= appointment.Start_datetime.AddMinutes(-5) && now <= appointment.Start_datetime.AddMinutes(15))
            {
                // Materialize the OrderPackageTreatmentServices query
                var orderServices = db.OrderPackageTreatmentServices
                    .Where(s => s.OrderPackageTreatmentId == appointment.OrderPackageTreatmentId)
                    .ToList(); // Fetch all related records upfront

                // Get all services in advance
                var serviceIds = orderServices.Select(o => o.ServiceId).Distinct().ToList();
                var services = db.Services.Where(s => serviceIds.Contains(s.Id)).ToDictionary(s => s.Id); // Map ServiceId to Service

                foreach (var orderService in orderServices)
                {
                    if (services.TryGetValue(orderService.ServiceId, out var service))
                    {
                        // Update properties
                        orderService.Cost = service.Cost;
                        orderService.Price = service.Price;
                        orderService.Commission = service.Commission;
                    }
                }

                // Save all changes at once
                db.SaveChanges();

                // Update appointment status
                appointment.Status = 2;

                var title = "Appointment Started";
                var msg = $"Your appointment {appointment.Code} has now started. The estimated duration is {appointment.OrderPackageTreatment.Duration} hours.";

                // Add notification to inboxes
                AddInboxMessage(title, msg, 3, 2, appointment.StaffId, appointment.MemberId, appointment.Id);

                // Send notification to email
                SendNotification(title, msg, appointment.Member.Person.Email);
                SendNotification(title, msg, appointment.Staff.Person.Email);

                db.SaveChanges();

                return Json(new { success = true, message = "The barcode was successfully processed, and the appointment has now started." });
            }

            return Json(new
            {
                success = false,
                message = "The appointment is outside the allowed time range. You can only scan the barcode 5 minutes before the scheduled time and up to 15 minutes after."
            });
        }

        return Json(new { success = false, message = "The barcode is either invalid or has expired. Please verify and try again." });
    }



    // ========================================
    // Method for return ajax
    // ========================================
    [HttpGet]
    public JsonResult GetAvailableTimes(int? memberId, int staffId, string appointmentDate, int selectedPackageId, bool isAutoAssign = false)
    {
        if (!hp.CheckRole(new string[] { "staff", "member" }, TempData))
        {
            return Json(new { success = false, error = "Access Denied", message = "You do not have the necessary permissions. Please log in again." });
        }

        // Convert the appointment date from string to DateTime
        DateTime selectedDate;
        if (!DateTime.TryParse(appointmentDate, out selectedDate))
        {
            return Json(new { success = false, error = "AppointmentDate", message = "The date format is invalid. Please enter a valid date." });
        }


        if (HttpContext.Session.GetString("_userRole") == "member")
        {
            memberId = HttpContext.Session.GetInt32("_userId");
        }

        // ====================================================
        // Get the selected package and its treatment duration
        // ====================================================

        var orderPackage = db.OrderPackages
                        .Include(p => p.Package)
                        .FirstOrDefault(p => p.Id == selectedPackageId && p.Order.MemberId == memberId && p.Expired_datetime > DateTime.Now);

        if (orderPackage == null)
        {
            return Json(new { success = false, error = "PackageId", message = "The selected package could not be found. Please check the package ID and try again." });
        }

        // Get the remaining treatments for the selected package
        var treatment = orderPackage.Package.Treatment - orderPackage.Remaining_treatment + 1;

        var packageTreatment = db.OrderPackageTreatments
                                 .FirstOrDefault(t => t.OrderPackage.Id == selectedPackageId && t.Treatment == treatment);

        if (packageTreatment == null)
        {
            return Json(new { success = false, error = "PackageId", message = "The treatment for the selected package could not be found. Please verify the package details and try again." });
        }

        int treatmentDurationInMinutes = (int)(packageTreatment.Duration * 60);

        // ====================================================
        // Check for staff
        // ====================================================

        if (isAutoAssign)
        {
            // Member level >= 3 with auto-assign: Check any available staff
            var availableStaff = db.Staffs
                .Where(s => !db.Appointments
                    .Where(a => a.Start_datetime.Date == selectedDate.Date && a.Start_datetime.ToString("HH:mm") == appointmentDate)
                    .Select(a => a.StaffId)
                    .Contains(s.Id))
                .ToList();

            if (availableStaff.Count == 0)
            {
                return Json(new { success = true, message = "Staff is available for the selected date and time." });
            }

            return Json(new { success = true, message = "Staff is available for the selected time." });
        }
        else
        {
            // Member level >= 3 and selecting specific staff
            var staff = db.Staffs.Find(staffId);
            if (staff == null)
            {
                return Json(new { success = false, error = "StaffId", message = "Staff not found." });
            }

            var bookedTimesForStaff = db.Appointments
                                    .Where(a => a.StaffId == staffId && a.Start_datetime.Date == selectedDate.Date && (a.Status == 1 || a.Status == 2))
                                    .Select(a => new { a.Start_datetime, a.End_datetime }) // Select both start and end times
                                    .ToList() // Fetch the data into memory
                                    .Select(a => (StartTime: a.Start_datetime, EndTime: a.End_datetime)) // Convert to tuple
                                    .ToList(); // Return as a list of tuples


            var availableTimesForStaff = GetAvailableTimeSlots(selectedDate, bookedTimesForStaff, treatmentDurationInMinutes);


            if (availableTimesForStaff.Count == 0)
            {
                return Json(new { success = false, error = "StaffId", message = "No available time slots for the selected staff at the chosen date and time." });
            }

            return Json(new { success = true, availableTimes = availableTimesForStaff });
        }
    }

    private List<string> GetAvailableTimeSlots(DateTime selectedDate, List<(DateTime StartTime, DateTime EndTime)> bookedTimes, int treatmentDurationInMinutes)
    {
        // generate time slots from 10 AM to 10 PM with 15-minute intervals
        var allSlots = new List<string>();
        var startTime = selectedDate.Date.AddHours(10); // Start at 10 AM
        var endTime = selectedDate.Date.AddHours(22); // End at 10 PM

        // Generate 15-minute intervals
        for (var time = startTime; time < endTime; time = time.AddMinutes(15))
        {
            // Calculate the end time of the slot by adding the treatment duration
            var slotEndTime = time.AddMinutes(treatmentDurationInMinutes);

            // If the slot end time exceeds 10 PM, exclude it
            if (slotEndTime > endTime)
            {
                continue; // Skip this slot as it goes beyond 10 PM
            }

            allSlots.Add(time.ToString("HH:mm"));
        }

        // Filter out booked times and those that overlap with existing appointments
        var availableSlots = allSlots.Where(slot => !IsSlotOccupied(slot, treatmentDurationInMinutes, bookedTimes, selectedDate)).ToList();

        return availableSlots;
    }

    private bool IsSlotOccupied(string slot, int treatmentDurationInMinutes, List<(DateTime StartTime, DateTime EndTime)> bookedTimes, DateTime selectedDate)
    {
        // Parse the slot time by combining it with the selectedDate (ensuring the correct date)
        var slotTime = DateTime.Parse($"{selectedDate.ToString("yyyy-MM-dd")} {slot}");

        // Calculate the end time of the slot
        var slotEndTime = slotTime.AddMinutes(treatmentDurationInMinutes);

        // Loop through booked times and check for overlap with slot's duration
        foreach (var bookedTime in bookedTimes)
        {
            // Check if the slot overlaps with the booked time range
            if (slotTime < bookedTime.EndTime.AddMinutes(15) && slotEndTime > bookedTime.StartTime)
            {
                return true; // There is an overlap
            }
        }
        return false; // No overlap, the slot is available
    }

    public JsonResult GetAvailableStaff(string startTime, string endTime)
    {
        // Parse the start and end time from the request
        DateTime start = DateTime.Parse(startTime);
        DateTime end = DateTime.Parse(endTime);

        // Fetch the available staff who do not have appointments overlapping with the given time range
        var availableStaff = db.Staffs
            .Where(s => !s.Appointments.Any(a =>
                (a.Start_datetime < end && a.End_datetime > start) &&
                (a.Status == 1 || a.Status == 2)) &&
                s.Person.Status == 1)
            .Select(s => new { s.Id, s.Person.Name })
            .ToList();

        return Json(availableStaff);
    }

    [HttpGet]
    public JsonResult GetTreatmentDuration(int selectedPackageId)
    {
        var orderPackage = db.OrderPackages
                             .Include(o => o.Package)
                             .FirstOrDefault(o => o.Id == selectedPackageId);

        var treatment = orderPackage.Package.Treatment - orderPackage.Remaining_treatment + 1;

        // Get the selected package
        var packageTreatment = db.OrderPackageTreatments.Where(p => p.OrderPackageId == selectedPackageId && p.Treatment == treatment).FirstOrDefault();
        if (packageTreatment == null)
        {
            return Json(new { success = false, error = "PackageId", message = "Package not found." });
        }

        // Assuming that the package has a treatment duration (in minutes)
        var treatmentDurationInMinutes = packageTreatment.Duration * 60;

        return Json(new { success = true, treatmentDuration = treatmentDurationInMinutes });
    }

    public JsonResult GetPackagesAndStaffs(int memberId)
    {
        if (memberId <= 0)
        {
            return new JsonResult(new { success = false, message = "Invalid member ID." })
            {
                StatusCode = 400 // Bad Request
            };
        }

        try
        {
            var packages = GetOrderPackages(memberId); // Get packages for the member
            var staffList = GetStaffs(); // Get staff list

            return Json(new
            {
                success = true,
                packages = packages,
                staffList = staffList
            });
        }
        catch (SqlException ex)
        {
            // Log the exception as needed
            return new JsonResult(new
            {
                success = false,
                message = "An error occurred while fetching packages.",
                details = ex.Message
            })
            {
                StatusCode = 500 // Internal Server Error
            };
        }
    }


    // ========================================
    // Auto Check & update appointment status
    // ========================================
    private void CheckAppointment()
    {
        try
        {
            // Current time
            DateTime currentTime = DateTime.Now;

            // Fetch appointments with pending status (Status = 1) and start time past 15 minutes
            var pendingAppointments = db.Appointments
                .Where(a => a.Status == 1 && a.Start_datetime.AddMinutes(15) <= currentTime)
                .ToList();

            foreach (var appointment in pendingAppointments)
            {
                // Update appointment status to 0
                if (appointment.Type == 1 || appointment.Type == 3)
                {
                    appointment.Status = 3;
                }
                else
                {
                    appointment.Status = 0;
                }

                // Find the associated OrderPackageTreatment
                var orderPackageTreatment = db.OrderPackageTreatments
                    .FirstOrDefault(opt => opt.Id == appointment.OrderPackageTreatmentId);

                if (orderPackageTreatment != null)
                {
                    // Find the associated OrderPackage
                    var orderPackage = db.OrderPackages
                        .FirstOrDefault(op => op.Id == orderPackageTreatment.OrderPackageId);

                    if (orderPackage != null)
                    {
                        // Increment the remaining treatment count
                        orderPackage.Remaining_treatment++;
                    }
                }
            }

            // Fetch appointments where the current time is >= end_datetime and status is 2
            var ongoingAppointments = db.Appointments
                .Include(a => a.Member.Person)
                .Include(a => a.Staff.Person)
                //.Where(a => a.Status == 2 && a.End_datetime.AddDays(-10) <= currentTime)
                .Where(a => a.Status == 2 && a.End_datetime <= currentTime)
                .ToList();

            foreach (var appointment in ongoingAppointments)
            {
                // Materialize the OrderPackageTreatmentServices query
                var orderServices = db.OrderPackageTreatmentServices
                    .Where(s => s.OrderPackageTreatmentId == appointment.OrderPackageTreatmentId)
                    .ToList(); // Fetch all related records upfront

                decimal totalCommissionPerAppointment = 0;

                foreach (var orderService in orderServices)
                {

                    // Calculate the latest commission of the staff
                    var totalCommission = db.CommissionHistories
                            .Where(c => c.StaffId == appointment.StaffId)
                            .OrderByDescending(c => c.Created_datetime) // Ensure sorting by date to get the most recent record
                            .FirstOrDefault(); // Use FirstOrDefault to get the latest commission

                    decimal latestCommission = (totalCommission?.Total_commission ?? 0) + orderService.Commission;

                    db.CommissionHistories.Add(new CommissionHistory
                    {
                        StaffId = appointment.StaffId ?? 0, // Default to 0 if StaffId is null
                        OrderPackageTreatmentServiceId = orderService.Id,
                        Total_commission = latestCommission, // Default to 0 if Commission is null
                        Created_datetime = DateTime.Now,
                    });

                    db.SaveChanges();


                    totalCommissionPerAppointment += orderService.Commission;
                }

                // Update appointment status to 3 (completed)
                appointment.Status = 3;

                // Send notification to both member and staff
                var title = "Appointment Completed";
                var memberMsg = $"Your appointment {appointment.Code} have been completed. Thank you for selecting our services.";
                var staffMsg = $"Appointment {appointment.Code} have been completed. You have get RM {totalCommissionPerAppointment} from this appointment. Keep fighthing !";

                AddInboxMessage(title, memberMsg, 1, 2, null, appointment.MemberId, appointment.Id);
                AddInboxMessage(title, staffMsg, 2, 2, appointment.StaffId, null, appointment.Id);

                SendNotification(title, memberMsg, appointment.Member.Person.Email);
                SendNotification(title, staffMsg, appointment.Staff.Person.Email);
            }

            // Save all changes to the database
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            // Log or handle exceptions
            Console.WriteLine("An error occurred while updating appointments: " + ex.Message);
        }
    }

    private bool CheckStaffOverlappingAppointment(int? staffId, DateTime start, DateTime end)
    {
        return db.Appointments.Any(a =>
                            a.StaffId == staffId &&
                            (a.Status == 1 || a.Status == 2) &&
                            ((start >= a.Start_datetime && start < a.End_datetime) || // Start time overlaps
                             (end > a.Start_datetime && end <= a.End_datetime) ||   // End time overlaps
                             (start <= a.Start_datetime && end >= a.End_datetime))); // Encapsulates an existing appointment

    }

    // ========================================
    // Get Needed List
    // ========================================
    private List<SelectListItem>? GetOrderPackages(int? memberId = 0)
    {
        var packages = db.OrderPackages
                            .Where(s => s.Order.MemberId == memberId &&
                                        s.Expired_datetime > DateTime.Now &&
                                        s.Remaining_treatment > 0)
                            .Select(p => new SelectListItem
                            {
                                Value = p.Id.ToString(),
                                Text = $"{p.Code} - {p.Package.Name} ({p.Remaining_treatment} treatments left)"
                            })
                            .ToList();

        return packages;
    }

    private List<SelectListItem> GetStaffs(bool includeAutoAssign = true)
    {
        var staffList = db.Staffs
               .Where(s => s.Person.Status == 1) // Assuming IsActive determines active staff
               .Select(s => new SelectListItem
               {
                   Value = s.Id.ToString(),
                   Text = s.Person.Name
               })
           .ToList();

        if (includeAutoAssign)
        {
            // Add a default placeholder item with value 0
            staffList.Insert(0, new SelectListItem
            {
                Value = "0",
                Text = "Auto-Assign",
                Selected = true // Mark this as the default selection
            });
        }

        return staffList;
    }

    private List<SelectListItem> GetMembers()
    {
        var memberList = db.Members
               .Where(s => s.Person.Status == 1) // Assuming IsActive determines active staff
               .Select(s => new SelectListItem
               {
                   Value = s.Id.ToString(),
                   Text = s.Person.Name + "( " + s.Person.Email + " )"
               })
           .ToList();

        return memberList;
    }

    // ========================================
    // Send Email function
    // ========================================
    public void AddInboxMessage(string title, string message, int target, int type, int? staffId, int? memberId, int? appointmentId)
    {
        var inbox = new Inbox
        {
            Title = title,
            Message = message,
            Target = 3, // Targeting staff (or change to 1 for member, or 3 for both)
            Type = 2, // Type 2 for notification
            Status = 1, // Status 1 for active
            MemberId = memberId, // Member ID from the view model
            StaffId = staffId, // Staff ID from the selected staff
            AppointmentId = appointmentId, // Appointment ID
            Created_datetime = DateTime.UtcNow
        };

        // Add inbox to the database and save
        db.Inboxes.Add(inbox);
    }

    private void SendNotification(string title, string msg, string email)
    {
        var email_title = title;
        var email_body = new StringBuilder();

        email_body.Append(
            "<p style='font-family:Arial, sans-serif; font-size:14px; color:#333;'>" + msg + "</p>" +
            "<br />");

        string final_email_body = email_body.ToString();

        // send email
        hp.sendEmail(email, email_title, final_email_body);
        hp.sendEmail(email, email_title, final_email_body);
    }

}
