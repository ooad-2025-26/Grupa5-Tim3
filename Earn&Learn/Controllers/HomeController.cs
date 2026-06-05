using Earn_Learn.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Earn_Learn.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
        public IActionResult Terms()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Contact()
        {
            return View();
        }
        public IActionResult PristupOdbijen()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Contact(string ime, string email, string poruka)
        {
            if (string.IsNullOrWhiteSpace(ime) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(poruka))
            {
                ViewBag.Greska = "Sva polja su obavezna.";
                return View();
            }

            ViewBag.Uspjeh = "Vaša poruka je uspješno poslana! Javit ćemo vam se uskoro.";
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
