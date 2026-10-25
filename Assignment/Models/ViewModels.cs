using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using X.PagedList;

namespace Assignment.Models;

#nullable disable warnings

public class RegisterVM
{
    [StringLength(50, ErrorMessage = "{0} must not exceed 50 characters.")]
    public string Name { get; set; }

    [StringLength(1, ErrorMessage = "{0} must be only 1 character.")]
    [RegularExpression("^[FM]$", ErrorMessage = "{0} must be either 'F' or 'M'.")]
    public string Gender { get; set; }

    [StringLength(150, ErrorMessage = "{0} must not exceed 150 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Invalid email address format.")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{6,}$",
    ErrorMessage = "Password must be at least 6 characters long and contain at least 1 uppercase letter, 1 lowercase letter, and 1 digit.")]
    public string New_password { get; set; }

    [Required(ErrorMessage = "Confirm Password is required.")]
    [StringLength(100, ErrorMessage = "{0} must not exceed 100 characters.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{6,}$",
    ErrorMessage = "Password must be at least 6 characters long and contain at least 1 uppercase letter, 1 lowercase letter, and 1 digit.")]
    public string Password { get; set; }

    [StringLength(11, ErrorMessage = "{0} must not exceed 11 digits.")]
    [RegularExpression(@"^01[0-9]-?[0-9]{7,8}$", ErrorMessage = "Invalid telephone number.")]
    public string? Tel_no { get; set; }

    public string? Referred_person_id { get; set; }
}

// Inbox
public class InboxVM
{
    public int? Id { get; set; }

    public string? Code { get; set; }

    [MinLength(1, ErrorMessage = "{0} must be at least 1 characters long.")]
    [StringLength(50, ErrorMessage = "{0} cannot exceed 50 characters.")]
    public string Title { get; set; }

    [MinLength(1, ErrorMessage = "{0} must be at least 1 characters long.")]
    [StringLength(300, ErrorMessage = "{0} cannot exceed 300 characters.")]
    public string Message { get; set; }

    [RegularExpression(@"^[1-3]$", ErrorMessage = "{0} is required.")]
    public int Target { get; set; }

    [DisplayName("Post Date")]
    public DateTime? Created_datetime { get; set; } = DateTime.UtcNow;

    public int? Status { get; set; }
}

public class RoomVM
{
    public int? Id { get; set; }

    public string? Code { get; set; }

    [MinLength(1, ErrorMessage = "{0} must be at least 1 characters long.")]
    [StringLength(300, ErrorMessage = "{0} cannot exceed 50 characters.")]
    public string Name { get; set; }

    public int? Status { get; set; }
}

public class TopUpRewardVM
{

    [Range(5, 999999.99, ErrorMessage = "{0} must between 5 and 999999.99 only.")]
    [Display(Name = "Minimum Top Up Amount")]
    public int Min_topup { get; set; }

    [Range(0.01, 999999.99, ErrorMessage = "{0} must between 0.01 and 999999.99 only.")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "Invalid {0} format.")]
    [Display(Name = "Free Credit")]
    public decimal Free_credit { get; set; }
}

public class ReferralRewardVM
{

    [Range(1, 9999, ErrorMessage = "{0} must between 1 and 9999 only.")]
    [Display(Name = "Number of Referral (pax)")]
    public int Num_of_referral { get; set; }

    [Range(0.01, 999999.99, ErrorMessage = "{0} must between 0.01 and 999999.99 only.")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "Invalid {0} format.")]
    [Display(Name = "Free Credit")]
    public decimal Free_credit { get; set; }
}

public class PersonVM
{
    [Range(1, int.MaxValue, ErrorMessage = "{0} must be a valid positive number.")]
    [Display(Name = "Person ID")]
    public int? Id { get; set; }

    [StringLength(50, ErrorMessage = "{0} must not exceed 50 characters.")]
    public string Name { get; set; }

    [StringLength(1, ErrorMessage = "{0} must be only 1 character.")]
    [RegularExpression("^[FM]$", ErrorMessage = "{0} must be either 'F' or 'M'.")]
    public string Gender { get; set; }

    [StringLength(300, ErrorMessage = "{0} must not exceed 300 characters.")]
    public string? Avatar { get; set; }

    [StringLength(300, ErrorMessage = "{0} must not exceed 300 characters.")]
    public string? CapturePhoto { get; set; }

    [StringLength(150, ErrorMessage = "{0} must not exceed 150 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Invalid email address format.")]
    public string Email { get; set; }

    [StringLength(100, ErrorMessage = "{0} must not exceed 100 characters.")]
    public string? Current_password { get; set; }

    [StringLength(100, ErrorMessage = "{0} must not exceed 100 characters.")]
    public string? New_password { get; set; }

    [StringLength(100, ErrorMessage = "{0} must not exceed 100 characters.")]
    public string? Password { get; set; }

    [StringLength(11, ErrorMessage = "{0} must not exceed 11 digits.")]
    [RegularExpression(@"^01[0-9]-?[0-9]{7,8}$", ErrorMessage = "Invalid telephone number.")]
    [Display(Name = "Tel No.")]
    public string Tel_no { get; set; }

    [Display(Name = "Registration Date")]
    public DateTime Registration_date { get; set; } = DateTime.UtcNow;

    [Range(0, 1, ErrorMessage = "{0} must be 1 (active) or 0 (inactive).")]
    public int Status { set; get; }

    // other properties
    public IFormFile? Upload_image { get; set; }
}

public class StaffVM
{
    [Range(1, int.MaxValue, ErrorMessage = "{0} must be a valid positive number.")]
    [Display(Name = "Staff ID")]
    public int? Id { get; set; }

    public string? Code { get; set; }

    [Range(1, 2, ErrorMessage = "{0} must be 1 (manager) or 2 (staff).")]
    public int? Role { get; set; }

    [Range(0.01, 999999.99, ErrorMessage = "{0} must between 0,01 and 999999.99 only.")]
    [Display(Name = "Current Salary (RM)")]
    public decimal Salary { get; set; }

    [StringLength(300, ErrorMessage = "{0} must not exceed 300 characters.")]
    public string Address { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "{0} must be a valid positive number.")]
    public int? PersonId { get; set; }
}

public class StaffPersonVM
{
    public StaffVM Staff { get; set; }
    public PersonVM Person { get; set; }
}

public class MemberVM
{
    [Range(1, int.MaxValue, ErrorMessage = "{0} must be a valid positive number.")]
    [Display(Name = "Member ID")]
    public int? Id { get; set; }

    public string? Code { get; set; }

    [Range(0, 99, ErrorMessage = "{0} must between 0 and 99 only.")]
    [Display(Name = "Membership Level")]
    public int? Level { get; set; }

    [Range(0, 999999.99, ErrorMessage = "{0} must between 0 and 999999.99 only.")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "Invalid {0} format.")]
    [Display(Name = "Credit (RM)")]
    public decimal? Credit { get; set; }

    [Range(0, 999999.99, ErrorMessage = "{0} must between 0 and 999999.99 only.")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "Invalid {0} format.")]
    [Display(Name = "Total Expenses(RM)")]
    public decimal? Total_expenses { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "{0} must be a valid positive number.")]
    [Display(Name = "Person ID")]
    public int? PersonId { get; set; }

    [StringLength(8)]
    [Display(Name = "Referral ID")]
    public string? Referral_id { get; set; }

    [StringLength(8)]
    [Display(Name = "Referral Person ID")]
    public string? Referral_person_id { get; set; }
}

public class MemberPersonVM
{
    public MemberVM Member { get; set; }
    public PersonVM Person { get; set; }
}

public class MembershipVM
{
    [Range(1, int.MaxValue, ErrorMessage = "{0} must be a valid positive number.")]
    [Display(Name = "Membership ID")]
    public int? Id { get; set; }

    [Range(1, 99, ErrorMessage = "{0} must between 1 and 99 only.")]
    public int Level { get; set; }

    [StringLength(300, ErrorMessage = "{0} must not exceed 300 characters.")]
    public string? Icon { get; set; }

    [Range(0, 999999.99, ErrorMessage = "{0} must between 0 and 999999.99 only.")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "Invalid {0} format.")]
    public decimal Min_expense { get; set; }

    [Range(0, 1, ErrorMessage = "{0} must between 0 and 1 only.")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "Invalid {0} format.")]
    public decimal Discount_rate { get; set; }

    [Range(0, 1)]
    public int? Status { get; set; }

    // other properties
    public IFormFile? Upload_image { get; set; }
}

public class PackageVM
{
    //[Range(1, int.MaxValue, ErrorMessage = "{0} must be a valid positive number.")]
    //public int Id { get; set; }

    [StringLength(50, ErrorMessage = "{0} must not exceed 50 characters.")]
    public string Name { get; set; }

    [StringLength(300, ErrorMessage = "{0} must not exceed 300 characters.")]
    public string Description { get; set; }

    [Range(1, 30, ErrorMessage = "{0} must between 1 and 30 only.")]
    public int Treatment { get; set; }

    [Range(0, 999999.99, ErrorMessage = "{0} must between 0 and 999999.99 only.")]
    public decimal Price { get; set; }

    [Range(0, 1)]
    public int? Status { get; set; }

    [Range(1, 36, ErrorMessage = "{0} must between 1 and 36 only.")]
    public int Validity { get; set; }

    public string ServiceIds { get; set; }

    // other properties
    public IFormFile? Cover_image { get; set; }
    public IFormFile[]? Normal_image { get; set; }
}

public class AppointmentVM
{
    [Range(1, int.MaxValue, ErrorMessage = "{0} must be a valid positive number.")]
    public int? Id { get; set; }

    [Range(1, 4, ErrorMessage = "{0} must be a valid type.")]
    public int? Type { get; set; }

    [StringLength(50, ErrorMessage = "{0} must not exceed 300 characters.")]
    public string? Title { get; set; }

    public DateTime AppointmentDate { get; set; }

    public DateTime Start_datetime { get; set; }

    public DateTime End_datetime { get; set; }

    public bool? All_day { get; set; }

    public int? Status { get; set; }

    public DateTime Created_datetime { get; set; }

    // This is OrderPackageId
    [Range(1, int.MaxValue, ErrorMessage = "{0} must be a valid positive number.")]
    public int? PackageId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "{0} must be a valid positive number.")]
    public int? MemberId { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "{0} must be a valid positive number.")]
    public int? StaffId { get; set; }

    public List<int>? StaffList { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "{0} must be a valid positive number.")]
    public int? OrderPackageTreatmentId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "{0} must be a valid positive number.")]
    public int? RoomId { get; set; }
}

public class AppointmentDetailVM
{
    public Appointment Appointment { get; set; }
    public IEnumerable<OrderPackageTreatmentService>? Services { get; set; }
    public string? BarCodeImg { get; set; }
}

public class EmailResetPwdVM
{
    [StringLength(150, ErrorMessage = "{0} must not exceed 150 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Invalid email address format.")]
    public string Email { get; set; }
}

public class ResetPasswordVM
{
    [Required(ErrorMessage = "Password is required.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{6,}$",
    ErrorMessage = "Password must be at least 6 characters long and contain at least 1 uppercase letter, 1 lowercase letter, and 1 digit.")]
    [StringLength(100, ErrorMessage = "{0} must not exceed 100 characters.")]
    public string New_password { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{6,}$",
    ErrorMessage = "Password must be at least 6 characters long and contain at least 1 uppercase letter, 1 lowercase letter, and 1 digit.")]
    [StringLength(100, ErrorMessage = "{0} must not exceed 100 characters.")]
    public string Password { get; set; }
}

public class OTPVM
{
    [Required(ErrorMessage = "OTP is required.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be a 6-digit number.")]
    public string Otp { get; set; }
}

public class BarcodeRequestVM
{
    public int Id { get; set; }
    public string BarcodeValue { get; set; }
}

public class PackageViewModel
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public List<PackageTreatment> Treatments { get; set; }
    public List<PackagePhoto> Photos { get; set; }
}

public class CategoryVM 
{
    [Range(1, int.MaxValue, ErrorMessage = "{0} must be a valid positive number.")]
    public int? Id { get; set; }

    [StringLength(50, ErrorMessage = "{0} must not exceed 50 characters.")]
    public string Name { get; set; }

    [StringLength(300, ErrorMessage = "{0} must not exceed 300 characters.")]
    public string Description { get; set; }
}

public class AppointmentReviewVM
{
    [Range(1, 5, ErrorMessage = "{0} must be between 1 to 5 only.")]
    public int Star { get; set; }

    [StringLength(300, ErrorMessage = "{0} must not exceed 300 characters.")]
    public string? Comment { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "{0} must be a valid positive number.")]
    public int AppointmentId { get; set; }

    [DefaultValue(false)]
    public bool Anonymous { get; set; }

    // other properties
    public IFormFile[]? Upload_photos { get; set; }
}

// Handle detail
public class CommissionHistoryDetail
{
    public int AppointmentId { get; set; }
    public int OrderServiceId { get; set; }
    public string OrderServiceName { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal CommissionBalance { get; set; }
    public DateTime CommissionDate { get; set; }
}

public class WalletTransactionViewModel
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public decimal FreeCredit { get; set; }
    public string PaymentMethod { get; set; }
    public decimal CreditBefore { get; set; }
    public decimal CreditAfter { get; set; }
    public string RecordType { get; set; } // "Order" or "Top-Up"
    public decimal AdjustedAmount { get; set; }
}

public class CategoryListing
{
    public Category Category { get; set; }
    public int TotalServices { get; set; }
}

public class PackageReviewVM
{
    public Package Package { get; set; }
    public IPagedList<AppointmentReview> Reviews { get; set; }
}

