using GoldNote.Models.Subscription;
using GoldNote.Models.Teacher;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

public class SubscriptionController : Controller
{
    private readonly Teacher _teacherService;

    public SubscriptionController(Teacher teacherService)
    {
        _teacherService = teacherService;
    }

    // --- ADD THIS NEW METHOD ---
    // This handles the GET request to show the page at /Subscription/Subscription
    [HttpGet]
    public IActionResult Subscription()
    {
        return View();
    }

    // --- UPDATE YOUR POST METHOD SLIGHTLY ---
    [HttpPost]
    [Route("Subscription/BecomeTeacher")]
    public IActionResult BecomeTeacher(SubscriptionModel model)
    {
        // 1. Check for Model Validation Errors
        if (!ModelState.IsValid)
        {
            // IMPORTANT: Since your View is named "Subscription.cshtml" but this action 
            // is named "BecomeTeacher", you must specify the view name string "Subscription".
            // Otherwise, it looks for "BecomeTeacher.cshtml" and crashes.
            return View("Subscription", model);
        }

        var teacherId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            string joinCode = _teacherService.CreateClassroom(teacherId, model.ClassroomName);

            TempData["SuccessMessage"] = $"Welcome! Your classroom '{model.ClassroomName}' was created. Your Class Code is: {joinCode}";

            return RedirectToAction("Teacher_index", "Teacher");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "An error occurred while setting up your classroom. Please try again.");

            // IMPORTANT: Return the specific view name here too
            return View("Subscription", model);
        }
    }
}