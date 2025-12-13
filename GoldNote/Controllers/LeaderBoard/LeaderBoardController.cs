using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using GoldNote.Models.LeaderBoard; // Keeps specific namespace

namespace GoldNote.Controllers.LeaderBoard
{
    public class LeaderBoardController : Controller
    {
        private readonly LeaderBoardRepository _repository;

        // Inject the Repository (not the Model)
        public LeaderBoardController(LeaderBoardRepository repo)
        {
            _repository = repo;
        }

        [HttpGet]
        public IActionResult Index()
        {
            // Get the Logged in User's ID
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Fetch data
            var model = _repository.GetLeaderboards(userId);

            // Return the View "LeaderBoard.cshtml" specifically
            return View("LeaderBoard", model);
        }
    }
}