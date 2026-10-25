using Assignment.Models;
using Microsoft.AspNetCore.Mvc;
using X.PagedList.Extensions;

namespace Assignment.Controllers;

public class TopUpRewardController : Controller
{
    private readonly DB db;
    private readonly Helpers hp;

    public TopUpRewardController(DB db , Helpers hp)
    {
        this.db = db;
        this.hp = hp;
    }

    // Create Reward (GET)
    [HttpGet]
    public IActionResult RewardList(
    string? amountRange,
    decimal? minAmount,
    decimal? maxAmount,
    string? sort,
    string? dir,
    int page = 1,
    int page_items = 7)
    {
        //Authorizes role of user(uncomment if role checking is needed)
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }

        var rewards = db.TopUpRewards.Where(r => r.Status == 1).AsQueryable();

        // Parse the amount range if selected
        if (!string.IsNullOrEmpty(amountRange))
        {
            var rangeParts = amountRange.Split('-');
            if (rangeParts.Length == 2)
            {
                minAmount = decimal.Parse(rangeParts[0]);
                maxAmount = decimal.Parse(rangeParts[1]);
            }
        }

        // Apply filtering by amount range
        if (minAmount.HasValue || maxAmount.HasValue)
        {
            rewards = rewards.Where(r =>
                (!minAmount.HasValue || r.Min_topup >= minAmount) &&
                (!maxAmount.HasValue || r.Min_topup <= maxAmount)
            );
        }

        // Sorting logic
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<TopUpReward, object> fn = sort switch
        {
            "Id" => r => r.Id,
            "Minimum Top Up (RM)" => r => r.Min_topup,
            "Free Credit" => r => r.Free_credit,
            _ => r => r.Id, // Default sorting by id
        };

        var sorted = dir == "desc"
            ? rewards.OrderByDescending(fn)
            : rewards.OrderBy(fn);

        // Ensure page number is valid
        if (page < 1)
        {
            return RedirectToAction(null, new { amountRange, minAmount, maxAmount, sort, dir, page_items, page = 1 });
        }

        // filter items per page
        ViewBag.Page_items = page_items;

        var pagedRewards = sorted.ToPagedList(page, page_items);

        if (page > pagedRewards.PageCount && pagedRewards.PageCount > 0)
        {
            return RedirectToAction(null, new { amountRange, minAmount, maxAmount, sort, dir, page_items, page = pagedRewards.PageCount });
        }

        // Prepare data for view
        ViewBag.AmountRange = amountRange; // Pass the selected range back to the view
        

        // Check for AJAX request and return partial view
        if (Request.IsAjax())
        {
            return PartialView("Staff/_RewardList", pagedRewards);
        }

        return View("Staff/RewardList", pagedRewards);
    }

    [HttpGet]
    public IActionResult Create()
    {
        // use if-else statement to check user role
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("RewardList");
        }
        return View("Staff/CreateReward");
    }

    // Create Reward (POST)
    [HttpPost]
    public IActionResult Create(TopUpRewardVM vm)
    {
        // use if-else statement to check user role
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("RewardList");
        }


        // Check is the reward exist or not
        var exits = CheckRewardExist(vm.Min_topup) as JsonResult;

        // Deserialize the JSON to access properties
        dynamic jsonData = exits?.Value;

        if (jsonData != null && !jsonData.success)
        {
            ModelState.AddModelError("Min_topup", jsonData.message);
        }

        if (ModelState.IsValid)
        {
            db.TopUpRewards.Add(new()
            {
                Min_topup = vm.Min_topup,
                Free_credit = vm.Free_credit,
                Status = 1
            });
            db.SaveChanges();

            TempData["Success_Title"] = "Create Success";
            TempData["Alert_Success"] = "The top up reward has been created successfully.";
            return RedirectToAction("RewardList");
        }

        TempData["Error_Title"] = "Create Failed";
        TempData["Alert_Error"] = "Failed to create a referral reward. Try to check you input, the number of referral (pax) can not be duplicate.";
        return View("Staff/CreateReward", vm);
    }

    // Edit Reward (GET)
    [HttpGet]
    public IActionResult Edit(int id)
    {
        // use if-else statement to check user role
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("RewardList");
        }

        var reward = db.TopUpRewards.Find(id);
        if (reward == null)
        {
            return NotFound();
        }
        return View("Staff/EditReward", reward);
    }

    // Edit Reward (POST)
    [HttpPost]
    public IActionResult Edit(TopUpReward reward)
    {
        // use if-else statement to check user role
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("RewardList");
        }


        if (!ModelState.IsValid)
        {
            // Return the form with validation errors
            return View("Staff/EditReward", reward);
        }

        // Check for duplicate Min_topup
        var existingReward = db.TopUpRewards.FirstOrDefault(r => r.Min_topup == reward.Min_topup && r.Status == 1 && r.Id != reward.Id);
        if (existingReward != null)
        {
            TempData["Error"] = "A reward with this minimum top-up amount is already active.";
            return View("Edit", reward);
        }

        var rewardToUpdate = db.TopUpRewards.Find(reward.Id);
        if (rewardToUpdate != null)
        {
            rewardToUpdate.Min_topup = reward.Min_topup;
            rewardToUpdate.Free_credit = reward.Free_credit;
            db.SaveChanges();

            TempData["Success"] = "Reward updated successfully.";
            return RedirectToAction("RewardList");
        }

        TempData["Error"] = "Reward not found.";
        return RedirectToAction("RewardList");
    }

    [HttpPost]
    public IActionResult Deactivate(int id)
    {
        // use if-else statement to check user role
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("RewardList");
        }

        var reward = db.TopUpRewards.Find(id);
        if (reward != null && reward.Status == 1)
        {
            reward.Status = 0; // Deactivate
            db.SaveChanges();

            TempData["Success"] = "Reward deactivated successfully.";
            return RedirectToAction("RewardList");
        }

        TempData["Error"] = "Reward not found or already deactivated.";
        return RedirectToAction("RewardList");
    }

    [HttpPost]
    public IActionResult Activate(int id)
    {
        // use if-else statement to check user role
        if (!hp.CheckRole(new string[] { "staff" }, TempData))
        {
            return RedirectToAction("Home", "Home");
        }
        else if (!hp.CheckPosition("manager", TempData))
        {
            return RedirectToAction("RewardList");
        }

        var reward = db.TopUpRewards.Find(id);
        if (reward != null && reward.Status == 0)
        {
            reward.Status = 1;
            db.SaveChanges();

            TempData["Success"] = "Reward activated successfully.";
            return RedirectToAction("RewardList");
        }

        TempData["Error"] = "Reward not found or already active.";
        return RedirectToAction("RewardList");
    }

    public JsonResult CheckRewardExist(int min_topup)
    {
        var exist = db.TopUpRewards.FirstOrDefault(e => e.Min_topup == min_topup && e.Status == 1);

        if (exist == null)
        {
            return Json(new { success = true });
        }

        return Json(new { success = false, message = "Top up reward for this amount already exist." });
    }
}
