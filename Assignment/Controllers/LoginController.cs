using System;
using Assignment.Models;
using AutoMapper.Execution;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Http;
using Stripe;
using Stripe.Climate;
using System.Text;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json;
using static System.Net.WebRequestMethods;
using FluentEmail.Core;
using Microsoft.Extensions.Options;

namespace Assignment.Controllers
{
    public class LoginController : Controller
    {
        private readonly DB db;
        private readonly IWebHostEnvironment en;
        private readonly IDataProtector _protector;
        private readonly Helpers hp;
        private readonly IConfiguration configuration;

        public LoginController(DB db, IWebHostEnvironment en, IDataProtectionProvider dataProtectionProvider, Helpers hp, IConfiguration configuration)
        {
            this.db = db;
            this.en = en;
            this._protector = dataProtectionProvider.CreateProtector("LoginDataProtector");
            this.hp = hp;
            this.configuration = configuration;
        }

        public IActionResult Login(string? referral = null)
        {
            // clear session
            HttpContext.Session.Remove("pendingPerson");
            HttpContext.Session.Remove("PendingPersonReferredId");

            ViewBag._UserRole = HttpContext.Session.GetString("_userRole");

            //Get the remember me from cookie of have
            string protectedEmail = Request.Cookies["LoginEmail"];
            string protectedPassword = Request.Cookies["LoginPassword"];
            string loginEmail = string.Empty;
            string loginPassword = string.Empty;

            if (!string.IsNullOrEmpty(protectedEmail) && !string.IsNullOrEmpty(protectedPassword))
            {
                // Decrypt the email and passsword
                loginEmail = _protector.Unprotect(protectedEmail);
                loginPassword = _protector.Unprotect(protectedPassword);
            }

            ViewBag.LoginEmail = loginEmail ?? ""; // Assign empty string if nulls
            ViewBag.LoginPassword = loginPassword ?? "";
            ViewBag.RememberMe = !string.IsNullOrEmpty(loginEmail) && !string.IsNullOrEmpty(loginPassword);

            // check whether referral id exists
            if (!string.IsNullOrEmpty(referral))
            {
                try
                {
                    var unprotectReferral = _protector.Unprotect(referral);

                    // validate referral id
                    var result = db.Members
                                        .Include(m => m.Person)
                                        .Where(m => m.Referral_id == unprotectReferral && m.Person.Status == 1)
                                        .Count();

                    if (result > 0)
                    {
                        HttpContext.Session.SetString("PendingPersonReferredId", unprotectReferral);
                    }
                    else
                    {
                        return RedirectToAction("Login");
                    }
                }
                catch (Exception ex)
                {
                    return RedirectToAction("Login");
                }
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string loginEmail, string loginPassword, bool rememberMe = false)
        {
            // Only allow the configured bypass in Development. Other environments
            // always require server-side verification, even if their config says otherwise.
            bool recaptchaEnabled = !en.IsDevelopment()
                || configuration.GetValue<bool>("ReCaptchaSettings:Enabled", true);
            bool isValid = true;

            if (recaptchaEnabled)
            {
                string googleRecaptchaToken = Request.Form["g-recaptcha-response"].ToString();
                string secretKey = configuration["ReCaptchaSettings:SecretKey"];
                string verificationUrl = configuration["ReCaptchaSettings:VerificationUrl"];
                isValid = await RecaptchaService.verifyReCaptchaV3(googleRecaptchaToken, secretKey, verificationUrl);
            }

            if (!isValid)
            {
                TempData["Error_Title"] = "Error Verification";
                TempData["Alert_Error"] = "Please verify you are not a robot.";
            }
            else if (loginEmail != null && loginPassword != null)
            {

                var person = db.Persons
                            .Where(p => p.Email == loginEmail && p.Status == 1)
                            .FirstOrDefault();

                if (person != null)
                {
                    if (!hp.VerifyPassword(person.Password, loginPassword))
                    {
                        person.Login_failed_time++;

                        double lockMinute = person.Login_failed_time switch
                        {
                            3 => 0.5,  // 30 seconds (expressed as 0.5 minutes)
                            4 => 1,    // 1 minute
                            5 => 30,   // 30 minutes
                            6 => 60,   // 1 hour
                            7 => 180,  // 3 hours
                            8 => -1,   // -1 for deactivation (special handling)
                            _ => 0     // Default: no lock
                        };

                        if (lockMinute == -1)
                        {
                            person.Status = 0;
                            TempData["Error_Title"] = "Login Failed";
                            TempData["Alert_Error"] = "Login failed too many times. Your account have been deactivated. Contact our staff for activate the account.";

                        }
                        else if (lockMinute != 0)
                        {
                            person.Lock_expired_time = DateTime.Now.AddMinutes(lockMinute);
                            TempData["Error_Title"] = "Login Failed";
                            TempData["Alert_Error"] = $"Account have been locked. Unlocked at {person.Lock_expired_time}";
                        }
                        else
                        {
                            TempData["Error_Title"] = "Login Failed";
                            TempData["Alert_Error"] = "Email or password incorrect. Try to login again.";
                        }

                        db.SaveChanges();
                        return Redirect(Request.Headers.Referer.ToString());
                    }
                    else if (person.Lock_expired_time > DateTime.Now)
                    {
                        TempData["Error_Title"] = "Login Failed";
                        TempData["Alert_Error"] = $"Account have been locked. Unlocked at {Helpers.FormatDateTime(person.Lock_expired_time)}";
                        return Redirect(Request.Headers.Referer.ToString());
                    }

                    // Check if this person is a Member
                    var member = db.Members
                        .Where(m => m.PersonId == person.Id)
                        .FirstOrDefault();

                    // Add a persistent cookie for "Remember Me"
                    var options = new CookieOptions
                    {
                        Domain = "localhost", // Set the domain for the cookie
                        Path = "/", // Cookie is available within the entire application
                        Expires = DateTime.Now.AddDays(7), // Cookie expiration
                        HttpOnly = true,
                        Secure = true
                    };

                    if (rememberMe)
                    {
                        // Encrypt the email and password before storing in cookies
                        string protectedEmail = _protector.Protect(loginEmail);
                        string protectedPassword = _protector.Protect(loginPassword);
    
                        Response.Cookies.Delete("LoginEmail", options);
                        Response.Cookies.Delete("LoginPassword", options);
                        
                        Response.Cookies.Append("LoginEmail", protectedEmail, options);
                        Response.Cookies.Append("LoginPassword", protectedPassword, options);
                    }
                    else
                    {
                        Response.Cookies.Delete("LoginEmail", options);
                        Response.Cookies.Delete("LoginPassword", options);
                    }

                    if (member != null)
                    {

                        // Login as Member
                        HttpContext.Session.SetString("_userName", person.Name);
                        HttpContext.Session.SetString("_userAvatar", person.Avatar ?? "defaultProfile.png");
                        HttpContext.Session.SetInt32("_userId", member.Id);
                        HttpContext.Session.SetString("_userEmail", person.Email);
                        HttpContext.Session.SetInt32("_userPersonId", person.Id);
                        HttpContext.Session.SetString("_userRole", "member");

                        TempData["Success_Title"] = "Login Success";
                        TempData["Alert_Success"] = "Welcome to OwO Meow Beauty.";
                    }
                    else
                    {
                        // Check if this person is a Staff
                        var staff = db.Staffs
                            .Where(s => s.PersonId == person.Id)
                            .FirstOrDefault();

                        if (staff != null)
                        {
                            // Login as Staff
                            HttpContext.Session.SetString("_userName", person.Name);
                            HttpContext.Session.SetString("_userAvatar", person.Avatar ?? "defaultProfile.png");
                            HttpContext.Session.SetInt32("_userId", staff.Id);
                            HttpContext.Session.SetString("_userEmail", person.Email);
                            HttpContext.Session.SetInt32("_userPersonId", person.Id);
                            HttpContext.Session.SetString("_userRole", "staff");
                            HttpContext.Session.SetString("_userPosition", staff.Role == 1 ? "manager" : "staff");

                            TempData["Success_Title"] = "Login Success";
                            TempData["Alert_Success"] = "Welcome to OwO Meow Beauty.";

                        }
                        else
                        {
                            // No associated Member or Staff record found
                            TempData["Error_Title"] = "Login Failed";
                            TempData["Alert_Error"] = $"Email or password incorrect. Try to login again.";
                        }
                    }

                    person.Login_failed_time = 0;
                    db.SaveChanges();
                }
                else
                {
                    TempData["Error_Title"] = "Login Failed";
                    TempData["Alert_Error"] = $"Email or password incorrect. Try to login again.";
                }

            }
            else
            {
                TempData["Error_Title"] = "Login Failed";
                TempData["Alert_Error"] = $"Email or password incorrect. Try to login again.";
            }

            return Redirect(Request.Headers.Referer.ToString());
        }

        public IActionResult Logout()
        {
            // Login as Member
            HttpContext.Session.Remove("_userName");
            HttpContext.Session.Remove("_userAvatar");
            HttpContext.Session.Remove("_userId");
            HttpContext.Session.Remove("_userEmail");
            HttpContext.Session.Remove("_userPersonId");
            HttpContext.Session.Remove("_userRole");
            HttpContext.Session.Remove("_userPosition");

            return View();
        }

        [HttpPost]
        public IActionResult Register(RegisterVM vm)
        {

            // generate member id
            var member_id = db.Members
                        .Max(s => s.Id) + 1;

            // check if the Email is valid
            if (ModelState.IsValid("Email"))
            {
                // check if the Email is duplicated
                if (db.Persons.Any(p => p.Email == vm.Email))
                {
                    ModelState.AddModelError("Email", "Duplicated Email.");
                }
            }

            var pwd_verify = false;
            // check password
            if (!string.IsNullOrWhiteSpace(vm.New_password))
            {
                var password_err = hp.ValidatePassword(vm.New_password);
                if (password_err != null)
                {
                    pwd_verify = true;
                    ModelState.AddModelError("New_password", password_err);
                }
            }
            else
            {
                pwd_verify = true;
                ModelState.AddModelError("Password", "Password cannot be empty.");
            }

            // check confirm password
            if (!string.IsNullOrWhiteSpace(vm.Password))
            {
                var password_err = hp.ValidatePassword(vm.Password);
                if (password_err != null)
                {
                    pwd_verify = true;
                    ModelState.AddModelError("Password", password_err);
                }
            }
            else
            {
                pwd_verify = true;
                ModelState.AddModelError("Password", "Confirm password cannot be empty.");
            }

            if (!pwd_verify)
            {
                if (!vm.New_password.Equals(vm.Password))
                {
                    pwd_verify = true;
                    ModelState.AddModelError("Password", "Confirm password should same with pasword.");
                }
            }

            // validate entire model
            if (ModelState.IsValid)
            {

                // Generate a 6-digit random OTP
                hp.GenerateAndSendOTP(vm.Email);

                var person = new Assignment.Models.Person
                {
                    Name = vm.Name.Trim(),
                    Gender = vm.Gender,
                    Avatar = null,
                    Email = vm.Email.Trim(),
                    Password = hp.HashPassword(vm.Password.Trim()),
                    Tel_no = vm.Tel_no.Trim(),
                    Status = 1,
                };

                // Store the person object in session as JSON
                HttpContext.Session.SetString("PendingPerson", JsonConvert.SerializeObject(person));

                TempData["Success_Title"] = "Verification Required";
                TempData["Alert_Success"] = "An OTP has been sent to your email. Please verify your email to complete registration.";

                // Redirect to the email verification page
                return RedirectToAction("VerifyEmail");     
            }

            TempData["Error_Title"] = "Register Failed";
            TempData["Alert_Error"] =  "Opps! Register failed. Try to check the information that you fill in and register again.";
            return Redirect(Request.Headers.Referer.ToString());
        }

        // Get: verify email
        public IActionResult VerifyEmail()
        {
            ClearExpiredToken();
            var UserId = HttpContext.Session.GetInt32("_userId");
           
            if (UserId != null)
            {
                return View();
            }

            var person = GetRegisterSession();
            if (person != null) 
            {
                return View();
            }
            
            return RedirectToAction("Login");
        }

        // Post: verify email
        [HttpPost]
        public IActionResult VerifyEmail(OTPVM vm)
        {
            // ======================================================
            // Clear the expired token
            // ======================================================
            ClearExpiredToken();

            // ======================================================
            // Check is the user is null else check for the person
            // ======================================================
            var UserPersonId = HttpContext.Session.GetInt32("_userPersonId");
            Models.Person person = null;
            var email = "";

            if(UserPersonId == null)
            {
                person = GetRegisterSession();
                if (person == null) return RedirectToAction("Login");
                else email = person.Email;
            }
            else
            {
                email = HttpContext.Session.GetString("email");
            }

            if (string.IsNullOrEmpty(vm.Otp) || vm.Otp.Length != 6)
            {
                TempData["Error_Title"] = "Invalid OTP";
                TempData["Alert_Error"] = "The OTP must be a 6-digit number.";
                return View(vm);
            }

            // ======================================================
            // Check whether the otp match or not
            // ======================================================
            var storedOtp = db.OTPs.FirstOrDefault(o => o.OtpId.ToString() == vm.Otp && o.Expire > DateTime.Now && o.Email == email);

            if (storedOtp == null)
            {
                TempData["Error_Title"] = "Invalid OTP";
                TempData["Alert_Error"] = "The OTP is invalid or expired. Please try again.";
                return View(vm);
            }

            // ======================================================
            // Update db
            // ======================================================
            // if is come from register and person not null
            if (person != null)
            {
                db.Persons.Add(person);
                db.SaveChanges();

                int personId = person.Id;
                var referred_person_id = HttpContext.Session.GetString("PendingPersonReferredId");

                db.Members.Add(new()
                {
                    Level = 1,
                    Credit = 0,
                    Total_expenses = 0,
                    PersonId = personId,
                    Referral_id = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                    Referred_person_id = !string.IsNullOrEmpty(referred_person_id) ? referred_person_id : null,
                });
            }
            else if(UserPersonId != null) // if come from change email
            {
                var userPerson = db.Persons.Where(p => p.Id == UserPersonId).FirstOrDefault();

                userPerson.Email = email;
                db.OTPs.Remove(storedOtp);
                db.SaveChanges();

                TempData["Success_Title"] = "Email Verify Success";
                TempData["Alert_Success"] = "Your email have been success verify and save.";
                return RedirectToAction("Profile", "User");
            }

            db.OTPs.Remove(storedOtp);
            db.SaveChanges();

            // ======================================================
            // Success msg
            // ======================================================
            TempData["Success_Title"] = "Register Success";
            TempData["Alert_Success"] = "You have register successfully. Login and start you beauty journey ~ ~";
            return RedirectToAction("Login");
            
        }

        // Get: send email for reset password
        public IActionResult EmailResetPwd()
        {
            return View();
        }

        // Post: send email for reset password
        [HttpPost]
        public IActionResult EmailResetPwd(EmailResetPwdVM vm)
        {
            var person = db.Persons.Where(p => p.Email == vm.Email).FirstOrDefault();

            if (person == null)
            {
                ModelState.AddModelError("Email", "Email does not exist. Please try again.");
            }

            if (ModelState.IsValid)
            {
                // Generate a unique TokenId (using Guid for simplicity)
                string uniqueTokenId = Guid.NewGuid().ToString();

                // Set the expiration time to 5 minutes from now
                DateTime expireTime = DateTime.Now.AddMinutes(5);

                // Create a new Token instance
                Assignment.Models.Token newToken = new Assignment.Models.Token
                {
                    TokenId = uniqueTokenId,
                    Expire = expireTime,
                    PersonId = person.Id
                };

                // Add the Token to the database
                db.Tokens.Add(newToken);

                // Save changes to the database
                db.SaveChanges();


                // ========================
                // Send email
                // ========================
                // Declare the base URL
                string baseURL = $"{Request.Scheme}://{Request.Host.Value}";

                // Generate the full URL with the base
                string dynamicUrl = $"{baseURL}{Url.Action("Reset", "Login", new { token = uniqueTokenId })}";


                var email_title = "Reset Password";
                var email_body = new StringBuilder();

                email_body.Append(
                    "<p style='font-family:Arial, sans-serif; font-size:14px; color:#333;'>Click <a href=\"" + dynamicUrl + "\" >here</a> to Reset Password</p>" +
                    "<br />" +
                    "<p>ps: The link will be expired after 5 minute.</p>");

                string final_email_body = email_body.ToString();
                
                // send email
                hp.sendEmail(vm.Email, email_title, final_email_body);

                // Return a success response or redirect to another page
                TempData["Success_Title"] = "Email Sended";
                TempData["Alert_Success"] = "The reset password URL have been sended to your email. Please check on your email or junk mail.";
                
                return RedirectToAction("EmailResetPwd");  // You can change the redirection to any page you want
            }

            TempData["Error_Title"] = "Email Invalid";
            TempData["Alert_Error"] = "Email Does not exist or invalid format";
            return View(vm);
        }

        // Get: Reset password page
        public IActionResult Reset(string token)
        {
            ClearExpiredToken();
            var tokenExist = db.Tokens.Find(token);
            if (tokenExist == null) 
            {
                TempData["Error_Title"] = "Denied Access";
                TempData["Alert_Error"] = "Token expired or does not exist. Try to send email again for reset password.";

                return RedirectToAction("EmailResetPwd");
            }

            HttpContext.Session.SetString("token", token);
            return View();
        }

        // Get: Reset password page
        [HttpPost]
        public IActionResult Reset(ResetPasswordVM vm)
        {
            // Clear the expired token
            ClearExpiredToken();
            var tokenId = HttpContext.Session.GetString("token");
            var tokenExist = db.Tokens.Find(tokenId);
            if (tokenExist == null)
            {
                TempData["Error_Title"] = "Denied Access";
                TempData["Alert_Error"] = "Token expired or does not exist. Try to send email again for reset password.";

                return RedirectToAction("EmailResetPwd");
            }

            var p = db.Persons.Where( p => p.Id == tokenExist.PersonId && p.Status == 1).FirstOrDefault();
            if (p == null)
            {
                TempData["Error_Title"] = "Denied Access";
                TempData["Alert_Error"] = "Token expired or does not exist. Try to send email again for reset password.";

                return RedirectToAction("EmailResetPwd");
            }

            // Check password
            if (ModelState.IsValid("New_Password") && ModelState.IsValid("Password"))
            {
                var new_password_err = hp.ValidatePassword(vm.New_password);
                var password_err = hp.ValidatePassword(vm.Password);
                if (new_password_err != null)
                {
                    ModelState.AddModelError("New_password", new_password_err);
                }

                if (password_err != null)
                {
                    ModelState.AddModelError("Password", password_err);
                }

                if (password_err == null && new_password_err == null)
                {
                    password_err = "Confirm password must be same with password.";
                    if (vm.New_password != vm.Password)
                    {
                        ModelState.AddModelError("Password", password_err);
                    }
                }
            }

            
            if (ModelState.IsValid) 
            {

                p.Password = hp.HashPassword(vm.Password.Trim());
                db.Tokens.Remove(tokenExist);
                HttpContext.Session.Remove("token");
                db.SaveChanges();

                TempData["Success_Title"] = "Password Reset Success";
                TempData["Alert_Success"] = "Your password have been reset. Lets go login!";
                return RedirectToAction("Login");
            }
            return View(vm);
        }

        private void ClearExpiredToken()
        {
            // Get the current date and time
            DateTime currentTime = DateTime.Now;

            // Clear expired tokens
            var expiredTokens = db.Tokens.Where(t => t.Expire <= currentTime);
            db.Tokens.RemoveRange(expiredTokens);

            // Clear expired OTPs
            var expiredOTPs = db.OTPs.Where(o => o.Expire <= currentTime);
            db.OTPs.RemoveRange(expiredOTPs);

            // Save changes to the database
            db.SaveChanges();
        }

        private Assignment.Models.Person GetRegisterSession()
        {
            var personJson = HttpContext.Session.GetString("PendingPerson");
            if (string.IsNullOrEmpty(personJson))
            {
                TempData["Error_Title"] = "Session Expired";
                TempData["Alert_Error"] = "Your session has expired. Please try registering again.";
                return null; // Return null if session is missing
            }

            var person = JsonConvert.DeserializeObject<Assignment.Models.Person>(personJson);
           
            return person;
           
        }

        // AJAX Validation Checking
        public JsonResult CheckEmailExist(string email)
        {
            if (db.Persons.Any(p => p.Email == email))
            {
                return Json(new { success = false, error = "Email", message = "Email already exist." });
            }

            return Json(new { success = true });

        }
    }
}
