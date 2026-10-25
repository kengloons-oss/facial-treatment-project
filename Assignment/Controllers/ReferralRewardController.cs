using Assignment.Services.QRCodeGeneratorHelper;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X.PagedList.Extensions;

namespace Assignment.Controllers;

public class ReferralRewardController : Controller
{
    private readonly DB db;
    private readonly Helpers hp;
    private readonly IQRCodeGeneratorHelper qrCodeGeneratorHelper;
    private readonly IDataProtector protector;
    
    public ReferralRewardController(DB db, Helpers hp, IQRCodeGeneratorHelper qrCodeGeneratorHelper, IDataProtectionProvider dataProtectionProvider)
    {
        this.db = db;
        this.hp = hp;
        this.qrCodeGeneratorHelper = qrCodeGeneratorHelper;
        this.protector = dataProtectionProvider.CreateProtector("LoginDataProtector");
    }

    [HttpGet]
    public IActionResult ReferralRewardList(
     string? filter_status,
     string? referralRange, // Selected value from Referral Range dropdown
     string? creditRange,   // Selected value from Credit Range dropdown
     int? minReferral,
     int? maxReferral,
     decimal? minCredit,
     decimal? maxCredit,
     string? sort,
     string? dir,
     int page = 1,
     int page_items = 7)
    {
        // Authorizes role of user (uncomment if role checking is needed)
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var rewards = db.ReferralRewards.Where(r => r.Status == 1).AsQueryable();

        // Parse the referral range if selected
        if (!string.IsNullOrEmpty(referralRange))
        {
            var rangeParts = referralRange.Split('-');
            if (rangeParts.Length == 2)
            {
                minReferral = int.Parse(rangeParts[0]);
                maxReferral = int.Parse(rangeParts[1]);
            }
        }

        // Parse the credit range if selected
        if (!string.IsNullOrEmpty(creditRange))
        {
            var rangeParts = creditRange.Split('-');
            if (rangeParts.Length == 2)
            {
                minCredit = decimal.Parse(rangeParts[0]);
                maxCredit = decimal.Parse(rangeParts[1]);
            }
        }

        // Apply filtering by referral range
        if (minReferral.HasValue || maxReferral.HasValue)
        {
            rewards = rewards.Where(r =>
                (!minReferral.HasValue || r.Num_of_referral >= minReferral) &&
                (!maxReferral.HasValue || r.Num_of_referral <= maxReferral)
            );
        }

        // Apply filtering by credit range
        if (minCredit.HasValue || maxCredit.HasValue)
        {
            rewards = rewards.Where(r =>
                (!minCredit.HasValue || r.Free_credit >= minCredit) &&
                (!maxCredit.HasValue || r.Free_credit <= maxCredit)
            );
        }

        // Apply filtering by status
        if (!string.IsNullOrEmpty(filter_status) && int.TryParse(filter_status, out int status))
        {
            rewards = rewards.Where(r => r.Status == status);
        }

        // Sorting logic
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<ReferralReward, object> fn = sort switch
        {
            "Id" => r => r.Id,
            "Num of Referral (Pax)" => r => r.Num_of_referral,
            "Free Credit" => r => r.Free_credit,
            _ => r => r.Id, // Default sorting by id
        };

        var sorted = dir == "desc"
            ? rewards.OrderByDescending(fn)
            : rewards.OrderBy(fn);

        // Validate the page number
        if (page < 1)
        {
            return RedirectToAction(null, new
            {
                filter_status,
                referralRange,
                creditRange,
                minReferral,
                maxReferral,
                minCredit,
                maxCredit,
                sort,
                dir,
                page = 1,
                page_items
            });
        }

        // filter items per page
        ViewBag.Page_items = page_items;
        var pagedRewards = sorted.ToPagedList(page, page_items);

        if (page > pagedRewards.PageCount && pagedRewards.PageCount > 0)
        {
            return RedirectToAction(null, new
            {
                filter_status,
                referralRange,
                creditRange,
                minReferral,
                maxReferral,
                minCredit,
                maxCredit,
                sort,
                dir,
                page = pagedRewards.PageCount,
                page_items
            });
        }

        // Prepare data for view
        ViewBag.ReferralRange = referralRange; // Pass the selected range back to the view
        ViewBag.CreditRange = creditRange;
        ViewBag.MinReferral = minReferral;
        ViewBag.MaxReferral = maxReferral;
        ViewBag.MinCredit = minCredit;
        ViewBag.MaxCredit = maxCredit;

        // Check for AJAX request and return partial view
        if (Request.IsAjax())
        {
            return PartialView("Staff/_RefferalRewardList", pagedRewards);
        }

        return View("Staff/ReferralRewardList", pagedRewards);
    }




    // Create Referral Reward
    [HttpGet]
    public IActionResult Create()
    {
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("ReferralRewardList");
        }

        return View("Staff/CreateReferralReward");
    }

    [HttpPost]
    public IActionResult Create(ReferralRewardVM vm)
    {
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if(!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("ReferralRewardList");
        }

        var exits = CheckRewardExist(vm.Num_of_referral) as JsonResult;

        // Deserialize the JSON to access properties
        dynamic jsonData = exits?.Value;

        if (jsonData != null && !jsonData.success)
        {
            ModelState.AddModelError("Num_of_referral", jsonData.message);
        }

        if (ModelState.IsValid)
        {
            db.ReferralRewards.Add(new()
            {
                Num_of_referral= vm.Num_of_referral,
                Free_credit = vm.Free_credit,
                Status = 1
            });
            db.SaveChanges();

            TempData["Success_Title"] = "Create Success";
            TempData["Alert_Success"] = "The referral reward has been created successfully.";
            return RedirectToAction("ReferralRewardList");
        }
        else
        {
            TempData["Error_Title"] = "Create Failed";
            TempData["Alert_Error"] = "Failed to create a referral reward. Try to check you input, the number of referral (pax) can not be duplicate.";
        }

        return View("Staff/CreateReferralReward");
    }

    // Edit Referral Reward
    [HttpGet]
    public IActionResult Edit(int id)
    {
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("ReferralRewardList");
        }

        var reward = db.ReferralRewards.Find(id);
        if (reward == null)
        {
            TempData["Error"] = "Referral Reward not found!";
            return RedirectToAction("ReferralRewardList");
        }
        return View("Staff/EditReferralReward", reward);
    }

    [HttpPost]
    public IActionResult Edit(ReferralReward model)
    {
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            TempData["Error"] = "Unauthorized access.";
            return Json(new { success = false, message = TempData["Error"] });
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            TempData["Error"] = "Access denied.";
            return Json(new { success = false, message = TempData["Error"] });
        }
        model.Status = 1;

        if (ModelState.IsValid)
        {
            if (db.ReferralRewards.Any(r => r.Num_of_referral == model.Num_of_referral && r.Status == 1 && r.Id != model.Id))
            {
                TempData["Error"] = "Referral number already exists for an active reward.";
                return Json(new { success = false, message = TempData["Error"] });
            }

            db.ReferralRewards.Update(model);
            db.SaveChanges();

            TempData["Success"] = "Referral Reward updated successfully!";
            return Json(new { success = true, message = TempData["Success"] });
        }

        TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
        return Json(new { success = false, message = TempData["Error"] });
    }


    // Activate Referral Reward
    [HttpPost]
    public IActionResult Activate(int id)
    {
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("ReferralRewardList");
        }

        var reward = db.ReferralRewards.Find(id);
        if (reward != null)
        {
            if (reward.Status == 1)
            {
                TempData["Error"] = "Referral Reward is already active!";
                return RedirectToAction("ReferralRewardList");
            }

            reward.Status = 1; // Set to active
            db.SaveChanges();

            TempData["Success"] = "Referral Reward activated successfully!";
        }
        else
        {
            TempData["Error"] = "Referral Reward not found!";
        }
        return RedirectToAction("ReferralRewardList");
    }


    // Deactivate Referral Reward
    [HttpPost]
    public IActionResult Deactivate(int id)
    {
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("ReferralRewardList");
        }

        var reward = db.ReferralRewards.Find(id);
        if (reward != null)
        {
            if (reward.Status == 0)
            {
                TempData["Error"] = "Reward is already deactivated!";
                return RedirectToAction("ReferralRewardList");
            }

            reward.Status = 0; // Set to inactive
            db.SaveChanges();

            TempData["Success"] = "Referral Reward deactivated successfully!";
        }
        else
        {
            TempData["Error"] = "Referral Reward not found!";
        }
        return RedirectToAction("ReferralRewardList");
    }

    public IActionResult MemberReferral()
    {
        var userId = HttpContext.Session.GetInt32("_userId");

        // Check user authorization
        if (!hp.CheckRole(new string[] { "member" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        // get member data
        var m = db.Members.Find(userId);

        // generate qr code
        var referral_id = m.Referral_id;
        var encrpytReferralId = protector.Protect(referral_id);
        var baseURL = $"{Request.Scheme}://{Request.Host}/";

        var url = baseURL + "Login/Login?referral=" + encrpytReferralId;

        byte[] QRCodeAsBytes = qrCodeGeneratorHelper.GenerateQRCode(url);
        string QRCodeAsImageBase64 = $"data:image/png;base64,{Convert.ToBase64String(QRCodeAsBytes)}";

        ViewBag.QRCodeUrl = url;
        ViewBag.QRCodeImageUrl = QRCodeAsImageBase64;

        // get referral reward list
        var reward_list = db.ReferralRewards.Where(rr => rr.Status == 1).ToList();
        ViewBag.RewardList = reward_list;

        // get number of person who has referred the member but havent create any order
        var member_without_orders = db.Members
                                        .Include(m => m.Orders) // Include Orders to be able to check them
                                        .Where(m => m.Referred_person_id == referral_id && !m.Orders.Any()) // Check if Orders collection is empty
                                        .Count();

        ViewBag.ReferralNum = member_without_orders;

        // get next achieveable reward
        var next_reward = db.ReferralRewards
                                .Where(rr => rr.Num_of_referral > m.Referral_purchase && rr.Status == 1)
                                .OrderBy(rr => rr.Num_of_referral)
                                .FirstOrDefault();
        ViewBag.NextReward = next_reward;

        return View("Member/Referral", m);
    }

    public JsonResult CheckRewardExist(int numOfReferral)
    {
        var exist = db.ReferralRewards.FirstOrDefault(e => e.Num_of_referral == numOfReferral && e.Status == 1);

        if (exist == null)
        {
            return Json(new { success = true });
        }

        return Json(new { success = false, message = "Referral reward for this number of pax already exist." });
    }

}