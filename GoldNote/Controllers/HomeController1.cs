using Microsoft.AspNetCore.Mvc;

namespace GoldNote.Controllers
{
	public class Home : Controller
	{
		public IActionResult Login()
		{
			return View();
		}
	}
}
