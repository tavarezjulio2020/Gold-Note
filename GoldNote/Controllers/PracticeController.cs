using GoldNote.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GoldNote.Controllers
{
    [Authorize]
   public class PracticeController : Controller
   {
        private readonly GoldNoteDbContext _db;

        public PracticeController(GoldNoteDbContext db)
        {
            _db = db;
        }
        public IActionResult Index()
        {
         return View();
        }

        [HttpGet]
        public IActionResult GetMyInstruments()
        {
            // Get logged in user ID
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Get data from DB
            var instruments = _db.getStudentInstruments(userId);

            // Return as JSON so JavaScript can read it
            return Json(instruments);
        }

        [HttpGet]
        public IActionResult GetAssignments(int instrumentId)
        {
            // 1. Get the current logged-in user's Profile ID (GUID)
            string userProfileId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 2. Call the Database
            var assignments = _db.getAssignmentsForStudent(userProfileId, instrumentId);

            // 3. Return JSON
            return Json(assignments);
        }
        [HttpGet]
        public IActionResult getTodayPracticeTime(int instrumentId = 0)
        {
            // 1. Get the current logged-in user's Profile ID (GUID)
            string userProfileId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 2. Call the Database
            var assignments = _db.getTodaysPracticeTimes(userProfileId, instrumentId);

            // 3. Return JSON
            return Json(assignments);
        }

        // Example in PracticeController.cs
        [HttpPost]
        public IActionResult SavePracticeSession([FromBody] PracticeSessionViewModel model)
        {
            // 1. Authentication Check: Get the current student's unique ID
            // This is crucial for security and linking the session to the correct user.
            var personId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(personId))
            {
                // Return 401 Unauthorized if the user cannot be identified
                return Unauthorized(new { success = false, message = "User identity missing." });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // 2. Pass the user ID and the received data model to the DbContext
                    _db.SavePracticeSession(personId, model);

                    // 3. Return success response
                    return Json(new { success = true, message = "Practice session saved successfully!" });
                }
                catch (Exception)
                {
                    // Return 500 status code if the database operation (in the DbContext) fails
                    return StatusCode(500, new { success = false, message = "Database save failed." });
                }
            }

            // Return 400 Bad Request if the JSON data didn't match the ViewModel structure
            return BadRequest(new { success = false, message = "Invalid data format received." });
        }
    }
}

