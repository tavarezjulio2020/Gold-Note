using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims; 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GoldNote.Models.Teacher;


namespace GoldNote.Controllers.Teachers
{
    [Authorize]
    public class TeacherController : Controller
    {
        private readonly Teacher _t; // Note: This is unused and causing a warning (IDF0051)

        // Constructor for dependency injection (assuming you have one)
        public TeacherController(Teacher t)
        {
            _t = t;
        }

        [HttpGet]
        [Route("Teacher")]
        public IActionResult Teacher_index()
        {
            // 1. Get the current user's name from the identity/cookie
            ViewBag.Username = User.Identity.Name;

            // 2. Pass control to the View Engine to render Teacher_index.cshtml
            return View();
        }

        [HttpGet]
        [Authorize(Roles = "Teacher")] // Ensure only Teachers can access this data
        public IActionResult GetMyStudents()
        {
            // Implementation for fetching and returning student data as JSON
            var teacherId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var students = _t.getMyStudents(teacherId);

            return Json(students);                                     
        }
    }
}