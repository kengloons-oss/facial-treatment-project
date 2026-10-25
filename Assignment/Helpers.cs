using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using SixLabors.ImageSharp.Formats.Jpeg;
using MimeKit;
using System.Text;
using System.Diagnostics;
using Assignment.Models;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using SixLabors.ImageSharp;
using Image = SixLabors.ImageSharp.Image;
using Stripe;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Elements;
using System.Net.Mail;
using System.Net;
using Azure;
using System.Net.NetworkInformation;
//using SixLabors.ImageSharp.Formats.Png;


namespace Assignment;

public class Helpers
{
    private readonly IWebHostEnvironment en;
    private readonly DB db;
    private static IHttpContextAccessor? _httpContextAccessor;
    private IWebHostEnvironment _webHostEnvironment;
    private readonly IConfiguration cf;

    public Helpers(DB db, IWebHostEnvironment en, IWebHostEnvironment _webHostEnvironment, IConfiguration cf)
    {
        this.db = db;
        this.en = en;
        this._webHostEnvironment = _webHostEnvironment;
        this.cf = cf;
    }


    // Method to initialize IHttpContextAccessor
    public static void Configure(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // ===========================
    // Format Data Display
    // ===========================

    // format id (general)
    public static string FormatId(string format, int? id = 0)
    {
        return $"{format}{id:D4}";
    }

    // format gender
    public static string FormatGender(string gender)
    {
        return (gender == "M") ? "Male" : "Female";
    }

    // format role
    public static string FormatRole(int role)
    {
        return role switch
        {
            1 => "Manager",
            2 => "Staff",
        };
    }

    // format status
    public static string FormatStatus(int? status)
    {
        return status switch
        {
            1 => "Active",
            0 => "Inactive"
        };
    }

    // format datetime -> date
    public static string FormatDate(DateTime? datetime)
    {
        // Return a default value or empty string if datetime is null
        return datetime?.ToString("dd-MM-yyyy") ?? "N/A";
    }

    // format datetime -> time
    public static string FormatTime(DateTime datetime)
    {
        return datetime.ToString("HH:mm:ss");
    }

    public static string FormatTime2(DateTime datetime)
    {
        return datetime.ToString("hh:mm tt");
    }

    // format datetime -> date HH:mm
    public static string FormatDateTime(DateTime datetime)
    {
        return datetime.ToString("dd-MM-yyyy hh:mm tt");
    }

    public string ControllerFormatDateTime(DateTime datetime)
    {
        return datetime.ToString("dd-MM-yyyy hh:mm tt");
    }

    // format money (2 decimal places)
    public static string FormatMoneyStr(double? money)
    {
        return money?.ToString("F2") ?? "0.00";
    }

    // format payment method
    public static string FormatPaymentMethod(int method)
    {
        return method switch
        {
            1 => "Cash",
            2 => "Stripe",
            3 => "Meow Wallet"
        };
    }

    // ===========================
    // Get localhost data
    // ===========================
    public static string GetBaseURL()
    {
        return null;    // currently havent find out the way to get base URL
    }

    // ===========================
    // Authorization
    // ===========================
    // Get string session value
    public static string? GetStringSessionValue(string key)
    {
        return _httpContextAccessor?.HttpContext?.Session.GetString(key);
    }

    // Get the int32 session
    public static int? GetIntSessionValue(string key)
    {
        return _httpContextAccessor?.HttpContext?.Session.GetInt32(key);
    }

    public static void SetStringSessionValue(string key, string data)
    {
         _httpContextAccessor?.HttpContext?.Session.SetString(key, data);
    }

    public static void SetIntSessionValue(string key, int data)
    {
        _httpContextAccessor?.HttpContext?.Session.SetInt32(key, data);
    }

    public static void RemoveSession(string key)
    {
        _httpContextAccessor?.HttpContext?.Session.Remove(key);
    }

    public static dynamic GetUser()
    {
        return new
        {
            UserName = GetStringSessionValue("_userName"),
            UserAvatar = GetStringSessionValue("_userAvatar"),
            UserId = GetIntSessionValue("_userId"),
            UserEmail = GetStringSessionValue("_userEmail"),
            UserPersonId = GetIntSessionValue("_userPersonId"),
            UserRole = GetStringSessionValue("_userRole"),
            UserPosition = GetStringSessionValue("_userPosition"),
        };
    }

    public void UpdateUser(Models.Person person)
    {
        SetStringSessionValue("_userName", person.Name);
        SetStringSessionValue("_userAvatar", person.Avatar);
        SetStringSessionValue("_userEmail", person.Email);
    }

    public void ClearUser()
    {
        RemoveSession("_userId");   
        RemoveSession("_userName");   
        RemoveSession("_userAvatar");   
        RemoveSession("_userEmail");   
        RemoveSession("_userPersonId");   
        RemoveSession("_userRole");   
        RemoveSession("_userPosition");   
    }


    public bool CheckRole(string[]? roles, ITempDataDictionary _tempData)
    {
        var userRole = GetStringSessionValue("_userRole");

        // If roles are not null and contain the userRole
        if (roles != null && roles.Contains(userRole))
        {
            // Check for the user status still active or not
            var userPersonId = GetIntSessionValue("_userPersonId");
            var searched = db.Persons.FirstOrDefault(p => p.Id == userPersonId && p.Status == 1);

            if (searched != null)
            {
                return true;  // Role is valid, no error
            }
        }
        else if (roles == null)
        {
            return true;
        }

        ClearUser();

        // Set the error msg to the TempData
        _tempData["Error_Title"] = "Access Denied";
        _tempData["Alert_Error"] = "User role invalid. Please log in again to access this website.";

        return false;
    }

    public bool CheckPosition(string? position, ITempDataDictionary _tempData)
    {
        var userPosition = Helpers.GetStringSessionValue("_userPosition");

        // roleWanted is which role can be access
        if (position != null && position.Equals(userPosition))
        {
            return true;
        }
        else if (position == null)
        {
            return true;
        }

        // Set the error msg to the TempData
        _tempData["Error_Title"] = "Permission Denied";
        _tempData["Alert_Error"] = "You have no permission to access this action. Try to login with another account with enough permission.";

        // Return error messages
        return false;
    }

    public static bool isManager()
    {
        return GetStringSessionValue("_userPosition") == "manager";
    }


    public int CartItemsCount()
    {
        int userId = GetUser().UserId ?? 0; // Assuming GetUser() returns the current user
        int cartItemsCount = db.Carts.Where(c => c.MemberId == userId).Count();

        return cartItemsCount;
    }


    // ===========================
    // Cookie
    // ===========================
    // Method to set cookies
    public void SetCookie(string key, string value, int? expireTimeInMinutes = null)
    {
        if (_httpContextAccessor == null)
        {
            throw new InvalidOperationException("IHttpContextAccessor has not been configured. Please call Configure() first.");
        }

        var context = _httpContextAccessor.HttpContext;
        if (context != null)
        {
            var options = new CookieOptions
            {
                Domain = "localhost", // Set the domain for the cookie
                Path = "/", // Cookie is available within the entire application
                Expires = expireTimeInMinutes.HasValue
                    ? DateTime.Now.AddMinutes(expireTimeInMinutes.Value)
                    : DateTime.Now.AddMinutes(30),
                HttpOnly = true,
                Secure = true
            };

            context.Response.Cookies.Append(key, value, options);
        }
        else
        {
            throw new InvalidOperationException("HttpContext is null.");
        }
    }

    // ===========================
    // Password Processing
    // ===========================

    // generate hashed password
    public string HashPassword(string password)
    {
        var passwordHasher = new PasswordHasher<object>();
        var hashed = passwordHasher.HashPassword(null, password);
        return hashed;
    }

    // verify password
    public bool VerifyPassword(string? stored_password, string? input_password)
    {
        if (string.IsNullOrEmpty(stored_password) || string.IsNullOrEmpty(input_password))
        {
            return false;
        }

        var passwordHasher = new PasswordHasher<object>();
        var result = passwordHasher.VerifyHashedPassword(null, stored_password, input_password);

        return result == PasswordVerificationResult.Success;
    }

    // validate password
    public string ValidatePassword(string password)
    {
        // check password length
        if (password.Length < 6) return "Password must have at least 6 characters.";

        // check password uppercase
        if (!password.Any(c => char.IsUpper(c))) return "Password must contain at least 1 uppercase alphabet.";

        // check password lowercase
        if (!password.Any(c => char.IsLower(c))) return "Password must contain at least 1 lowercase alphabet.";

        // check password digit
        if (!password.Any(c => char.IsDigit(c))) return "Password must contain at least 1 digit.";

        return null;
    }

    // ===========================
    // Photo Processing
    // ===========================

    public string ValidateImage(IFormFile f)
    {
        var reType = new Regex(@"^image\/(jpeg|jpg|png)$", RegexOptions.IgnoreCase);

        var reName = new Regex(@"^.+\.(jpeg|jpg|png)$", RegexOptions.IgnoreCase);

        if (!reType.IsMatch(f.ContentType) || !reName.IsMatch(f.FileName))
        {
            return "Only JPG, PNG and JPEG photo is allowed.";
        }
        else if (f.Length > 3 * 1024 * 1024)
        {
            return "Photo size cannot more than 3MB.";
        }

        return "";
    }

    public async Task<string> SavePhoto(IFormFile f, string folder)
    {
        var maxSize = 1 * 1024 * 1024;

        // generate unique file name
        var file = Guid.NewGuid().ToString("n") + ".jpg";

        // get the full local file path
        var path = Path.Combine(en.WebRootPath, folder, file);

        // load the image from the uploaded file
        using (var stream = f.OpenReadStream())
        using (var img = Image.Load(stream))  // Ensure Image is disposed properly
        {
            // adjust quality
            if (f.Length > maxSize)
            {
                int quality = 95;
                byte[] imageBytes;
                var encoder = new JpegEncoder { Quality = quality };

                do
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        encoder = new JpegEncoder { Quality = quality };

                        img.Save(memoryStream, encoder);
                        imageBytes = memoryStream.ToArray();
                    }
                    quality -= 5;

                } while (imageBytes.Length > maxSize && quality > 10);

                await System.IO.File.WriteAllBytesAsync(path, imageBytes);
            }
            else
            {
                img.Save(path);
            }
        }

        return file;
    }

    public void DeletePhoto(string file, string folder)
    {
        file = Path.GetFileName(file);  // get file name
        var path = Path.Combine(en.WebRootPath, folder, file);  // get full local file path

        // Wait a moment before attempting to delete the file
        System.Threading.Thread.Sleep(1000);  // 1-second delay

        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);  // delete the file
        }

    }

    public bool ClearDirectory(string directoryPath)
    {
        try
        {
            if (string.IsNullOrEmpty(directoryPath))
                throw new ArgumentNullException(nameof(directoryPath), "Directory path cannot be null or empty.");

            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException($"The directory '{directoryPath}' does not exist.");

            // Get all files in the directory
            string[] files = Directory.GetFiles(directoryPath);

            // Delete each file
            foreach (string file in files)
            {
                System.IO.File.Delete(file);
            }

            return true; // Success
        }
        catch (Exception ex)
        {
            // Log the error (optional)
            Console.WriteLine($"Error clearing directory: {ex.Message}");
            return false; // Failure
        }
    }

    // ===========================
    // email
    // ===========================

    //validate email
    public bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        string emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email, emailRegex, RegexOptions.IgnoreCase);
    }

    public void sendEmail(string recipient, string subject, string body)
    {
        var email = new MimeMessage();
        email.From.Add(new MailboxAddress("OwO Meow Beauty", "owomeowartgallery0101@gmail.com"));
        email.To.Add(new MailboxAddress("", recipient));
        email.Subject = subject;

        email.Body = new TextPart("html")
        {
            Text = body
        };

        using (var smtpClient = new MailKit.Net.Smtp.SmtpClient())
        {
            smtpClient.Connect("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
            smtpClient.Authenticate("owomeowartgallery0101@gmail.com", "tknr itks djgr oaod");
            smtpClient.Send(email);
            smtpClient.Disconnect(true);
        }
    }

    public void sendEmail2(MailMessage mail)
    {
        string user = cf["Smtp:User"] ?? "";
        string pass = cf["Smtp:Pass"] ?? "";
        string name = cf["Smtp:Name"] ?? "";
        string host= cf["Smtp:Host"] ?? "";
        int port = cf.GetValue<int>("Smtp:Port");

        mail.From = new MailAddress(user, name);

        using var smtp = new SmtpClient
        {
            Host = host,
            Port = port,
            EnableSsl = true,
            Credentials = new NetworkCredential(user, pass)
        };

        smtp.Send(mail);
    }

    public string ConvertImageToBase64(string imagePath)
    {
        try
        {
            byte[] imageBytes = System.IO.File.ReadAllBytes(imagePath);
            return Convert.ToBase64String(imageBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error converting image to Base64: " + ex.Message);
            return string.Empty;
        }
    }

    public void GenerateAndSendOTP(string email)
    {
        // Find the OTP record with the given email
        var existingOtp = db.OTPs.FirstOrDefault(o => o.Email == email);

        // If a record exists, remove it
        if (existingOtp != null)
        {
            db.OTPs.Remove(existingOtp);
            db.SaveChanges(); // Commit the deletion
        }

        // Generate a 6-digit random OTP
        Random random = new Random();
        int otp = random.Next(100000, 999999); // Generates a number between 100000 and 999999

        // Log or save the OTP to the database (assuming you have an OTP model and table)
        var Otp = new OTP
        {
            OtpId = otp,
            Expire = DateTime.Now.AddMinutes(5), // Set expiration time to 5 minutes from now
            Email = email
        };


        db.OTPs.Add(Otp);
        db.SaveChanges();

        // ========================
        // Send email
        // ========================
        var email_title = "Verify Email";
        var email_body = new StringBuilder();

        email_body.Append(
            "<p style='font-family:Arial, sans-serif; font-size:14px; color:#333;'>Your OTP Code is <b>" + otp + "</b></p>" +
            "<br />" +
            "<p>ps: The code will be expired after 5 minute.</p>");

        string final_email_body = email_body.ToString();

        // send email
        sendEmail(email, email_title, final_email_body);
    }


    // ===========================
    // Barcode
    // ===========================
    public string GenerateBarCodeImage(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        // Generate Code 128 as SVG with ZXing.Net. This avoids the expired
        // IronBarcode trial key and keeps the existing Quagga Code 128 scanner compatible.
        var modules = new ZXing.OneD.Code128Writer().encode(code);
        const int quietZone = 12;
        const int barHeight = 150;
        var svgWidth = modules.Length + quietZone * 2;
        var bars = new StringBuilder();
        var x = 0;

        while (x < modules.Length)
        {
            if (!modules[x])
            {
                x++;
                continue;
            }

            var runStart = x;
            while (x < modules.Length && modules[x])
            {
                x++;
            }

            bars.Append($"<rect x=\"{runStart + quietZone}\" y=\"0\" width=\"{x - runStart}\" height=\"{barHeight}\" fill=\"#000\"/>");
        }

        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{svgWidth}\" height=\"{barHeight}\" viewBox=\"0 0 {svgWidth} {barHeight}\"><rect width=\"100%\" height=\"100%\" fill=\"white\"/>{bars}</svg>";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
    }

    public string GenerateRandomCode(int length = 8)
    {
        var random = new Random();
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var stringBuilder = new StringBuilder();

        for (int i = 0; i < length; i++)
        {
            stringBuilder.Append(chars[random.Next(chars.Length)]);
        }

        return stringBuilder.ToString();
    }


    // ===========================
    // Validation
    // ===========================

    public bool ValidateDateTime(DateTime start, DateTime end)
    {
        DateTime currentDate = DateTime.Now;

        // Ensure both dates are in the future and end time is after the start time
        return start >= currentDate && end > currentDate && end > start;
    }

    // ===========================
    // Reusable Components
    // ===========================

    public static List<SelectListItem> page_items = new List<SelectListItem>{
        new SelectListItem { Value = "7", Text = "7" },
        new SelectListItem { Value = "10", Text = "10" },
        new SelectListItem { Value = "15", Text = "15" }
    };

    public static List<SelectListItem> inbox_target = new List<SelectListItem>{
        new SelectListItem { Value = "", Text = "All" },
        new SelectListItem { Value = "1", Text = "Member" },
        new SelectListItem { Value = "2", Text = "Staff" },
        new SelectListItem { Value = "3", Text = "Both" }
    };

    public static List<SelectListItem> status_list = new List<SelectListItem>{
        new SelectListItem { Value = "", Text = "All" },
        new SelectListItem { Value = "1", Text = "Active" },
        new SelectListItem { Value = "0", Text = "Inactive" }
    };

    public static List<SelectListItem> roles = new List<SelectListItem>{
        new SelectListItem { Value = "", Text = "All" },
        new SelectListItem { Value = "1", Text = "Manager" },
        new SelectListItem { Value = "2", Text = "Staff" }
    };

    public static List<SelectListItem> appointment_types = new List<SelectListItem>{
        new SelectListItem { Value = "", Text = "All" },
        new SelectListItem { Value = "1", Text = "Training" },
        new SelectListItem { Value = "2", Text = "Working" },
        new SelectListItem { Value = "3", Text = "Holiday" }
    };

    public static List<SelectListItem> appointment_status = new List<SelectListItem>{
        new SelectListItem { Value = "", Text = "All" },
        new SelectListItem { Value = "0", Text = "Cancelled" },
        new SelectListItem { Value = "1", Text = "Pending" },
        new SelectListItem { Value = "2", Text = "Ongoing" },
        new SelectListItem { Value = "3", Text = "Completed" }
    };

    public static List<SelectListItem> payment_methods = new List<SelectListItem>{
        new SelectListItem { Value = "", Text = "All" },
        new SelectListItem { Value = "1", Text = "Cash" },
        new SelectListItem { Value = "2", Text = "Stripe" },
        new SelectListItem { Value = "3", Text = "Meow Wallet" }
    };

    public static List<SelectListItem> _monthList = new List<SelectListItem>{
        new SelectListItem { Value = "", Text = "All" },
        new SelectListItem { Value = "1", Text = "Jan" },
        new SelectListItem { Value = "2", Text = "Feb" },
        new SelectListItem { Value = "3", Text = "Mar" },
        new SelectListItem { Value = "4", Text = "Apr" },
        new SelectListItem { Value = "5", Text = "May" },
        new SelectListItem { Value = "6", Text = "Jun" },
        new SelectListItem { Value = "7", Text = "Jul" },
        new SelectListItem { Value = "8", Text = "Aug" },
        new SelectListItem { Value = "9", Text = "Sep" },
        new SelectListItem { Value = "10", Text = "Oct" },
        new SelectListItem { Value = "11", Text = "Nov" },
        new SelectListItem { Value = "12", Text = "Dec" }
    };

    public static List<SelectListItem> amount_range_list = new List<SelectListItem>{
        new SelectListItem { Value = "", Text = "All" },
        new SelectListItem { Value = "0-250", Text = "0 - 250" },
        new SelectListItem { Value = "251-500", Text = "251 - 500" },
        new SelectListItem { Value = "501-750", Text = "501 - 750" },
        new SelectListItem { Value = "751-1000", Text = "751 - 1000" },
        new SelectListItem { Value = "1001-999999", Text = "1001 - 999999" },
    };

    public static List<SelectListItem> duration_range_list = new List<SelectListItem>{
        new SelectListItem { Value = "", Text = "All" },
        new SelectListItem { Value = "0-30", Text = "0 - 30 minutes" },
        new SelectListItem { Value = "31-60", Text = "31 - 60 minutes" },
        new SelectListItem { Value = "61-120", Text = "61 - 120 minutes" }
    };

    public static List<SelectListItem> price_range_list = new List<SelectListItem>{
        new SelectListItem { Value = "", Text = "All" },
        new SelectListItem { Value = "0-50", Text = "0 - 50" },
        new SelectListItem { Value = "51-100", Text = "51 - 100" },
        new SelectListItem { Value = "101-500", Text = "101 - 500" }
    };

    public static List<SelectListItem> commission_range_list = new List<SelectListItem>{
        new SelectListItem { Value = "", Text = "All" },
        new SelectListItem { Value = "0-10", Text = "0% - 10%" },
        new SelectListItem { Value = "11-20", Text = "11% - 20%" },
        new SelectListItem { Value = "21-50", Text = "21% - 50%" }
    };

    public static List<SelectListItem> cost_range_list = new List<SelectListItem>{
        new SelectListItem { Value = "", Text = "All" },
        new SelectListItem { Value = "0-50", Text = "0 - 50" },
        new SelectListItem { Value = "51-100", Text = "51 - 100" },
        new SelectListItem { Value = "101-500", Text = "101 - 500" }
    };

    public static List<SelectListItem> referral_range_list = new List<SelectListItem>
{
    new SelectListItem { Value = "", Text = "All" },
    new SelectListItem { Value = "1-10", Text = "1-10 referrals" },
    new SelectListItem { Value = "11-20", Text = "11-20 referrals" },
    new SelectListItem { Value = "21-50", Text = "21-50 referrals" },
    new SelectListItem { Value = "51+", Text = "51+ referrals" }
};

    public static List<SelectListItem> credit_range_list = new List<SelectListItem>
{
    new SelectListItem { Value = "", Text = "All" },
    new SelectListItem { Value = "10-50", Text = "RM10-RM50 credit" },
    new SelectListItem { Value = "51-100", Text = "RM51-RM100 credit" },
    new SelectListItem { Value = "101-200", Text = "RM101-RM200 credit" },
    new SelectListItem { Value = "201+", Text = "RM201+ credit" }
};

    public static Dictionary<int?, string> _Month = new Dictionary<int?, string>
    {
        {  1,  "Jan" },
        {  2,  "Feb" },
        {  3,  "Mar" },
        {  4,  "Apr" },
        {  5,  "May" },
        {  6,  "Jun" },
        {  7,  "Jul" },
        {  8,  "Aug" },
        {  9,  "Sep" },
        {  10, "Oct" },
        {  11, "Nov" },
        {  12, "Dec" }
    };

    public static Dictionary<string, string> _InboxTarget = new Dictionary<string, string>
    {
        { "1", "Member" },
        { "2", "Staff" },
        { "3", "Both" }
    };

    public static Dictionary<int, string> _Status = new Dictionary<int, string>
    {
        { 0, "Deactivated" },
        { 1, "Activated" }
    };

    public static Dictionary<int, string> _AppointmentStatus = new Dictionary<int, string>
    {
        // 0 = cancelled, 1 = pending, 2 = ongoing, 3 = completed
        { 0, "Cancelled" },
        { 1, "Pending" },
        { 2, "Ongoing" },
        { 3, "Completed" }
    };

    public static Dictionary<int, string> AppointmentType = new Dictionary<int, string>
    {
        // 1 = training, 2  = working, 3 = Holiday, 4 = others
        { 1, "training" },
        { 2, "working" },
        { 3, "holiday" },
        { 4, "others" }
    };

    public static List<SelectListItem> transactionTypeFilter = new List<SelectListItem>
    {
        new SelectListItem { Value = "Cash In", Text = "Cash In" },
        new SelectListItem { Value = "Cash Out", Text = "Cash Out" }
    };


    public static string GetAppointmentStatus(int statusId)
    {
        return _AppointmentStatus[statusId];
    }

    public static string GetMonth(int? month)
    {
        return _Month[month];
    }

    public static string GetAppointmentType(int type)
    {
        return AppointmentType[type];
    }

    public static string GetInboxTarget(int? target = 0)
    {
        return _InboxTarget[target.ToString()];
    }
}

