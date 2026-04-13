using GoldNote.Data;
using GoldNote.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
// NEW: These are required for sending emails
using System.Net;
using System.Net.Mail;

namespace GoldNote.Controllers
{
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

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            var user = _db.getUser(model.Username, model.Password);

            if (user == null)
            {
                ViewData["Error"] = "Invalid username or password.";
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Unique_id),
                new Claim(ClaimTypes.Name, user.Name)
            };

            if (user.IsTeacher)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Teacher"));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
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

        // =========================================================
        //  NEW FEATURE 1: FORGOT USERNAME
        // =========================================================
        [HttpGet]
        public IActionResult ForgotUsername()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ProcessForgotUsername(string email) // <-- Renamed to match HTML
        {
            var user = _db.GetUserByEmailForRecovery(email);

            if (user != null)
            {
                string subject = "Gold Note - Your Username";
                string body = $"Hi {user.Name},<br/><br/>You requested your username. It is: <b>{user.Username}</b>";

                SendEmail(user.Email, subject, body);
            }

            // <-- Changed to TempData and generalized the message for security
            TempData["Message"] = "If an account with that email exists, we have sent your username to it.";

            return RedirectToAction("ForgotUsername");
        }

        // =========================================================
        //  NEW FEATURE 2: FORGOT PASSWORD (Generate Link)
        // =========================================================
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ProcessForgotPassword(string username) // <-- Renamed and changed parameter to username (since your HTML form asks for Username!)
        {
            // You will need a method like this to get the user by their username instead of email
            var user = _db.GetUserByUsernameForRecovery(username);

            if (user != null)
            {
                string token = Guid.NewGuid().ToString();
                DateTime expiry = DateTime.Now.AddHours(1);

                _db.SavePasswordResetToken(user.Email, token, expiry);

                var resetLink = Url.Action("ResetPassword", "Account", new { token = token }, Request.Scheme);
                string subject = "Gold Note - Reset Password";
                string body = $"Hi {user.Name},<br/><br/>Click the link below to reset your password:<br/><a href='{resetLink}'>Reset Password</a>";

                SendEmail(user.Email, subject, body);
            }

            // <-- Changed to TempData and generalized the message
            TempData["Message"] = "If that username exists, we have sent a password reset link to the associated email address.";

            return RedirectToAction("ForgotPassword");
        }

        // =========================================================
        //  NEW: This handles the link click from the email!
        // =========================================================
        [HttpGet]
        public IActionResult ResetPassword(string token)
        {
            // If someone tries to just type the URL without a token, stop them
            if (string.IsNullOrEmpty(token))
            {
                ViewBag.Message = "Invalid or missing password reset token.";
                // You might want to redirect them to a generic error page, 
                // but for now, we'll just let the view handle the error message.
            }

            // Pass the token to the View so we can hide it in the form
            ViewBag.Token = token;

            return View();
        }

        // =========================================================
        //  NEW FEATURE 3: RESET PASSWORD (The Actual Change)
        // =========================================================

        [HttpPost]
        public IActionResult ResetPassword(string token, string newPassword)
        {
            // Replaced EF Core logic with your custom SQL method
            bool success = _db.ResetPasswordWithToken(token, newPassword);

            if (success)
            {
                TempData["Success"] = "Password updated! You can now log in.";
                return RedirectToAction("Login");
            }

            ViewBag.Message = "This password reset link is invalid or has expired.";
            return View();
        }

        // =========================================================
        //  EMAIL HELPER METHOD
        // =========================================================
        private void SendEmail(string toEmail, string subject, string body)
        {
            // 1. Setup the sender (Gold Note)
            var fromAddress = new MailAddress("goldnotemusictracker@gmail.com", "Gold Note Support");
            var toAddress = new MailAddress(toEmail);

            // 2. PASTE YOUR 16-CHAR GOOGLE APP PASSWORD HERE
            const string fromPassword = "lehgqdtjtsiwqnqa";

            // 3. Configure Gmail SMTP
            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
            };

            // 4. Send the message
            using (var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            })
            {
                smtp.Send(message);
            }
        }
    }
}