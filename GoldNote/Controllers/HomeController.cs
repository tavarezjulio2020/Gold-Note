using GoldNote.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization; // <-- ***ADD THIS***
using System.Security.Claims;
using GoldNote.Data;
using GoldNote.Models.Student;
using Microsoft.EntityFrameworkCore;

namespace GoldNote.Controllers
{

    // ***ADD THIS ATTRIBUTE TO PROTECT THE WHOLE CONTROLLER***
    [Authorize]
    public class HomeController : Controller
    {

        private readonly StudentModel _st;

        public HomeController(StudentModel st)
        {
            _st = st;
        }

        public IActionResult Index()
        {
            // This will now correctly show the name from the cookie
            ViewBag.Username = User.Identity.Name;
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpGet]
        public IActionResult GetMyTeachers() {
            string userProfileId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var teachers = _st.getTeachers(userProfileId);
            return Json(teachers);
        }

        [HttpGet]
        public IActionResult GetMyInstruments()
        {
            // Get logged in user ID
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Get data from DB
            var instruments = _st.getStudentInstruments(userId);

            // Return as JSON so JavaScript can read it
            return Json(instruments);
        }

        [HttpGet]
        public IActionResult learningInst(int instId)
        {
            // Get logged in user ID
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Get data from DB
            var instruments = _st.learningInst(userId, instId);

            // Return as JSON so JavaScript can read it
            return Json(instruments);
        }

        [HttpGet]
        public IActionResult GetInstruments()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Get data from DB
            var instruments = _st.getAllInstruments();

            // Return as JSON so JavaScript can read it
            return Json(instruments);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateMyInstruments([FromBody] List<int> selectedInstrumentIds)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null)
            {
                return Unauthorized(new { success = false, message = "User not authenticated." });
            }

            // Your StudentModel service should handle the user ID conversion and database logic.
            // Call the new service method:
            try
            {
                // We assume UpdateStudentInstruments handles the conversion and DB logic internally
                await _st.UpdateStudentInstruments(userIdString, selectedInstrumentIds);
                return Json(new { success = true, message = "Instruments updated successfully." });
            }
            catch (Exception ex)
            {
                // Log the error (ex.Message)
                return StatusCode(500, new { success = false, message = "Error updating instruments: " + ex.Message });
            }
        }

        // Inside HomeController.cs

        [HttpPost]
        public async Task<IActionResult> RegisterForTeacher(string code, int studentlearn)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                // Call the model to save to DB
                await _st.requestTeacher(code, studentlearn, userIdString);

                // Return success JSON
                return Json(new { success = true, message = "Request sent to teacher!" });
            }
            catch (Exception ex)
            {
                // Return failure JSON
                return Json(new { success = false, message = "Error sending request: " + ex.Message });
            }
        }
    }
}