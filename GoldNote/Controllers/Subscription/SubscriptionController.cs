// SubscriptionController.cs

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GoldNote.Models.Subscription;

// The namespace looks correct for your file structure:
namespace GoldNote.Controllers.Subscription
{
    [Authorize]
    public class SubscriptionController : Controller
    {
        // ----------------------------------------------------
        // FIX: The GET action must match the View name (Subscription.cshtml)
        // ----------------------------------------------------
        [HttpGet]
        public IActionResult Subscription() // <-- The name is now Subscription()
        {
            // This now correctly looks for Views/Subscription/Subscription.cshtml
            return View();
        }

        // ----------------------------------------------------
        // NEW POST ACTION TO HANDLE THE FORM SUBMISSION
        // The form in your View uses asp-action="BecomeTeacher", 
        // so we need a separate action named BecomeTeacher for the POST.
        // ----------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BecomeTeacher(SubscriptionModel model)
        {
            if (ModelState.IsValid)
            {
                // **TODO: Add payment processing and update user role**

                // This logic is called when the form (in Subscription.cshtml) is submitted.
                return RedirectToAction("Teacher_index", "Teacher");
            }
            // If validation fails, return the original view (Subscription.cshtml)
            return View("Subscription", model);
        }
    }
}