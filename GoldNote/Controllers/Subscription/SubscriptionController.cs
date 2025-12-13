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

    [HttpPost]
    [Route("Subscription/BecomeTeacher")]
    public IActionResult BecomeTeacher(SubscriptionModel model)
    {
        // 1. Check for Validation Errors
        if (!ModelState.IsValid)
        {
            // Extract error messages to send back to the JS alert
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return Json(new { success = false, message = string.Join("\n", errors) });
        }

        var teacherId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            // 2. Create the Classroom
            string joinCode = _teacherService.CreateClassroom(teacherId, model.ClassroomName, model.SelectedPlan);

            // 3. Set the success message for the next page
            TempData["SuccessMessage"] = $"Welcome! Your classroom '{model.ClassroomName}' was created. Class Code: {joinCode}";

            // 4. Return JSON success with the destination URL
            return Json(new
            {
                success = true,
                redirectUrl = Url.Action("Teacher_index", "Teacher")
            });
        }
        catch (Exception ex)
        {
            // 5. Return JSON failure
            return Json(new { success = false, message = "Database error: " + ex.Message });
        }
    }
}