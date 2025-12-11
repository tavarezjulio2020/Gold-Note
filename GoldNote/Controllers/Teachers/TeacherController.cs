using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims; 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GoldNote.Models.Teacher;
using Azure.Core;


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
            var teacherId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // 1. Get the current user's name from the identity/cookie
            ViewBag.Username = User.Identity.Name;
            var classcode = _t.GetClassCode(teacherId);
            ViewBag.ClassCode = classcode;
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

        [HttpGet]
        public IActionResult GetJoinRequest()
        {
            // Implementation for fetching and returning student data as JSON
            var teacherId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var students = _t.GetPendingRequests(teacherId);

            return Json(students);
        }

        [HttpPost] // Keep as POST for safer, non-idempotent operation
        public IActionResult Acceptrequest(int studentLearnId, int requestId)
        {
            var teacherId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            int classRoomId = _t.GetClassID(teacherId);
            if (classRoomId == 0)
            {
                // This is a correct return for an AJAX failure
                return BadRequest(new { success = false, message = "Unable to find class ID for teacher." });
            }

            // Capture the result (number of rows affected)
            int rowsAffected = _t.AcceptStudentWithInstrumnet(classRoomId, studentLearnId);

            // Check for success (1 row should be inserted)
            if (rowsAffected > 0)
            {
                // Delete the request and check that it was deleted
                int requestsDeleted = _t.DeleteRequest(requestId);

                // Return a successful JSON response
                return Ok(new
                {
                    success = true,
                    message = "Student accepted and request deleted.",
                    requestsDeleted = requestsDeleted
                });
            }
            else
            {
                // Return a failure JSON response if the insert failed
                return BadRequest(new
                {
                    success = false,
                    message = "Failed to add student to the class.",
                    studentLearnId = studentLearnId // Optional: debugging info
                });
            }
        }
        [HttpDelete]
        public IActionResult Rejectrequest(int requestID)
        {
            int requestsDeleted = _t.DeleteRequest(requestID);
            if (requestsDeleted > 0)
            {
                return Ok(new
                {
                    success = true,
                    message = "Student denied admition"
                });
            }
            return BadRequest(new
            {
                success = false,
                message = "Failed to deny admition."
            });
        }











        //cesar work HERE
        [HttpPost]
        public IActionResult AddAssignment(int learnId, string title, string notes)
        {
            try
            {
                _t.AddAssignment(learnId, title, notes);
                return Ok(new { message = "Assignment added!" });
            }
            catch
            {
                return BadRequest(new { message = "Failed to add assignment." });
            }
        }

        [HttpDelete]
        public IActionResult DeleteAssignment(int assignmentId)
        {
            try
            {
                _t.DeleteAssignment(assignmentId);
                return Ok(new { message = "Assignment deleted." });
            }
            catch
            {
                return BadRequest(new { message = "Delete failed." });
            }
        }


    }
}