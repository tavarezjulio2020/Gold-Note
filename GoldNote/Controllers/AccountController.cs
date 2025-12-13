using GoldNote.Data;
using GoldNote.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GoldNote.Controllers
{
    // This model helps us pass data from the form
    public class LoginViewModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class AccountController : Controller
    {
        private readonly GoldNoteDbContext _db;

        public AccountController(GoldNoteDbContext db)
        {
            _db = db;
        }

        // 1. This action SHOWS the login page
        // This matches the "options.LoginPath = /Account/Login"
        [HttpGet]
        public IActionResult Login()
        {
            // This will look for a view at /Views/Account/Login.cshtml
            return View(); 
        }

        // 2. This action HANDLES the form submission
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            // Use your DbContext to find the user
            var user = _db.getUser(model.Username, model.Password);

            // Check if the user was found
            if (user == null)
            {
                // Failed login: Add an error and return to the login page
                ViewData["Error"] = "Invalid username or password.";
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Unique_id),
                new Claim(ClaimTypes.Name, user.Name)
            };

            if(user.IsTeacher)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Teacher"));
            }

            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            // Sign the user in (creates the cookie)
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));
            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account"); // Send back to login page
        }



        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // UPDATED CALL: Pass model.Name and model.PhoneNumber
            string errorMessage = _db.CreateUser(
                model.Username,
                model.Password,
                model.Email,
                model.Name,
                model.PhoneNumber
            );

            if (errorMessage != null)
            {
                ModelState.AddModelError("", errorMessage);
                return View(model);
            }

            TempData["Success"] = "Account created successfully! Please log in.";
            return RedirectToAction("Login");
        }

    }
}