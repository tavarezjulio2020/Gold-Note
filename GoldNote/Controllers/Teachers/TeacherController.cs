using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GoldNote.Models.Teacher;

namespace GoldNote.Controllers.Teachers
{
    [Authorize]
    public class TeacherController : Controller
    {
        private readonly Teacher _t;

        // Constructor for dependency injection
        public TeacherController(Teacher t)
        {
            _t = t;
        }

        [HttpGet]
        [Route("Teacher")]
        public IActionResult Teacher_index()
        {
            var teacherId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewBag.Username = User.Identity.Name;

            var classcode = _t.GetClassCode(teacherId);

            // Handle scenario where a new teacher hasn't created a class yet
            if (classcode == null)
            {
                ViewBag.ClassCode = new classCode { ClassCode = "No Class" };
            }
            else
            {
                ViewBag.ClassCode = classcode;
            }

            return View();
        }

        [HttpGet]
        [Authorize(Roles = "Teacher")] // Ensure only Teachers can access this data
        public IActionResult GetMyStudents()
        {
            var teacherId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var students = _t.getMyStudents(teacherId);
            return Json(students);
        }

        [HttpGet]
        public IActionResult GetJoinRequest()
        {
            var teacherId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var requests = _t.GetPendingRequests(teacherId);
            return Json(requests);
        }

        // --- Class Request Actions ---

        [HttpPost]
        [ValidateAntiForgeryToken] // Secures the endpoint
        public IActionResult Acceptrequest(int studentLearnId, int requestId)
        {
            var teacherId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            int classRoomId = _t.GetClassID(teacherId);
            if (classRoomId == 0)
            {
                return BadRequest(new { success = false, message = "Unable to find class ID for teacher." });
            }

            // Add student to class
            int rowsAffected = _t.AcceptStudentWithInstrumnet(classRoomId, studentLearnId);

            if (rowsAffected > 0)
            {
                // Remove the pending request
                _t.DeleteRequest(requestId);

                return Ok(new
                {
                    success = true,
                    message = "Student accepted and request deleted."
                });
            }
            else
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Failed to add student to the class."
                });
            }
        }

        [HttpDelete]
        [ValidateAntiForgeryToken] // Secures the endpoint
        public IActionResult Rejectrequest(int requestId) // Note: standardized param name to camelCase in JS
        {
            int requestsDeleted = _t.DeleteRequest(requestId);
            if (requestsDeleted > 0)
            {
                return Ok(new
                {
                    success = true,
                    message = "Student denied admission"
                });
            }
            return BadRequest(new
            {
                success = false,
                message = "Failed to deny admission."
            });
        }

        // --- Assignment CRUD Endpoints ---

        [HttpGet]
        public IActionResult GetAssignments(int studentLearnId)
        {
            try
            {
                var assignments = _t.GetAssignments(studentLearnId);
                return Json(assignments);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Failed to load assignments.", error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddAssignment(int learnId, string title, string description)
        {
            try
            {
                // 1. Get the Teacher's Profile ID (GUID)
                var teacherId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // 2. Pass it to the model (Added teacherId parameter)
                _t.AddAssignment(learnId, title, description, teacherId);

                return Ok(new { message = "Assignment added!" });
            }
            catch (Exception ex)
            {
                // This sends the specific SQL error back to the browser
                return BadRequest(new { message = "Failed to add assignment.", error = ex.Message });
            }
        }

        [HttpPut]
        [ValidateAntiForgeryToken]
        // IMPORTANT: Parameters match the AJAX data: { assignmentId, title, description }
        public IActionResult EditAssignment(int assignmentId, string title, string description)
        {
            try
            {
                _t.UpdateAssignment(assignmentId, title, description);
                return Ok(new { message = "Assignment updated successfully!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Failed to update assignment.", error = ex.Message });
            }
        }

        [HttpDelete]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteAssignment(int assignmentId)
        {
            try
            {
                _t.DeleteAssignment(assignmentId);
                return Ok(new { message = "Assignment deleted." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Delete failed.", error = ex.Message });
            }
        }

        // Add this inside TeacherController.cs

        [HttpDelete]
        [ValidateAntiForgeryToken]
        public IActionResult DropStudent(int learnId)
        {
            try
            {
                _t.DropStudent(learnId);
                return Ok(new { message = "Student dropped from class." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Failed to drop student.", error = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetPracticeChartData(DateTime? start, DateTime? end, string studentIds)
        {
            var teacherId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Default to last 30 days if no date provided
            DateTime startDate = start ?? DateTime.Now.AddDays(-30);
            DateTime endDate = end ?? DateTime.Now;

            // Parse student IDs (comma separated string from JS)
            List<int> selectedIds = new List<int>();
            if (!string.IsNullOrEmpty(studentIds))
            {
                selectedIds = studentIds.Split(',').Select(int.Parse).ToList();
            }

            var data = _t.GetPracticeTrends(teacherId, startDate, endDate, selectedIds);

            // If filtering by specific students is needed, do it here using Linq 
            // (requires getting learn_id in the SQL, but for now we return the dataset)

            return Json(new { success = true, data = data });
        }
    }
}