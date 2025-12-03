using GoldNote.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace GoldNote.Controllers.Student
{
	public class StudentController : Controller
	{
		public IActionResult studentAccount()
		{
			return View();
		}
	}
}
