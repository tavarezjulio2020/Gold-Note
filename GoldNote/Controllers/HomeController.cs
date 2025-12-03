using GoldNote.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization; // <-- ***ADD THIS***
using System.Security.Claims;
using GoldNote.Data;
using GoldNote.Models.Student;

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
    }
}