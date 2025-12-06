// SubscriptionController.cs

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GoldNote.Models.Subscription;

namespace GoldNote.Controllers.Subscription
{
    [Authorize]
    public class SubscriptionController : Controller
    {
        [HttpGet]
        public IActionResult Subscription() 
        { 
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BecomeTeacher(SubscriptionModel model)
        {
            if (ModelState.IsValid)
            {
                return RedirectToAction("Teacher_index", "Teacher");
            }
            return View("Subscription", model);
        }
    }
}