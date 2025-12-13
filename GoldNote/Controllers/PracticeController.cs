using GoldNote.Data;
using GoldNote.Models.Student;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System;

namespace GoldNote.Controllers
{
    [Authorize]
    public class PracticeController : Controller
    {
        private readonly GoldNoteDbContext _db;
        private readonly StudentModel _studentModel;

        // Dependency Injection
        public PracticeController(GoldNoteDbContext db, StudentModel studentModel)
        {
            _db = db;
            _studentModel = studentModel;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult GetMyInstruments()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // Using StudentModel since it has this logic now
            var instruments = _studentModel.getStudentInstruments(userId);
            return Json(instruments);
        }

        [HttpGet]
        public IActionResult GetAssignments(int instrumentId)
        {
            string userProfileId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // Assuming _db has this method. If it's in StudentModel, switch to _studentModel
            var assignments = _db.getAssignmentsForStudent(userProfileId, instrumentId);
            return Json(assignments);
        }

        [HttpPost]
        public IActionResult SavePracticeSession([FromBody] PracticeSessionViewModel model)
        {
            var personId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(personId))
            {
                return Unauthorized(new { success = false, message = "User identity missing." });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _db.SavePracticeSession(personId, model);
                    return Json(new { success = true, message = "Practice session saved successfully!" });
                }
                catch (Exception)
                {
                    return StatusCode(500, new { success = false, message = "Database save failed." });
                }
            }

            return BadRequest(new { success = false, message = "Invalid data format received." });
        }

        // This method handles the "Today's Summary" request
        [HttpGet]
        public IActionResult getTodayPracticeTime(int instrumentId = 0)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Call the fixed method in StudentModel
            var summary = _studentModel.GetTodayPracticeTime(userId, instrumentId);

            return Json(summary);
        }
    }
}