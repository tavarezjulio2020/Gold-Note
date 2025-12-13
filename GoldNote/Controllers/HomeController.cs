using GoldNote.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;  
using System.Security.Claims;
using GoldNote.Data;
using GoldNote.Models.Student;
using Microsoft.EntityFrameworkCore;

namespace GoldNote.Controllers
{
     
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
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
             
            var instruments = _st.getStudentInstruments(userId);
             
            return Json(instruments);
        }

        [HttpGet]
        public IActionResult learningInst(int instId)
        { 
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
             
            var instruments = _st.learningInst(userId, instId);
             
            return Json(instruments);
        }

        [HttpGet]
        public IActionResult GetInstruments()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
             
            var instruments = _st.getAllInstruments();
             
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
            try
            { 
                await _st.UpdateStudentInstruments(userIdString, selectedInstrumentIds);
                return Json(new { success = true, message = "Instruments updated successfully." });
            }
            catch (Exception ex)
            { 
                return StatusCode(500, new { success = false, message = "Error updating instruments: " + ex.Message });
            }
        }
         
        [HttpPost]
        public async Task<IActionResult> RegisterForTeacher(string code, int studentlearn)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            { 
                await _st.requestTeacher(code, studentlearn, userIdString);
                 
                return Json(new { success = true, message = "Request sent to teacher!" });
            }
            catch (Exception ex)
            { 
                return Json(new { success = false, message = "Error sending request: " + ex.Message });
            }
        } 

        [HttpPost]
        public IActionResult DropClass(int classId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                 
                _st.DropClass(userId, classId);

                return Json(new { success = true, message = "You have successfully dropped the class." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}