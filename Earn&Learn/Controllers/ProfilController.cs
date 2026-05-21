using Earn_Learn.Data;
using Earn_Learn.Enums;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Earn_Learn.Controllers
{
    [Authorize]
    public class ProfilController : Controller
    {
        private readonly UserManager<Korisnik> _userManager;
        private readonly ApplicationDbContext _context;

        public ProfilController(UserManager<Korisnik> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Index", "Home");

            var predmeti = await _context.KorisnikPredmet
                .Where(kp => kp.idKorisnika == user.Id)
                .Join(_context.Predmet,
                      kp => kp.idPredmeta,
                      p => p.id,
                      (kp, p) => p.naziv)
                .ToListAsync();

            var model = new ProfilViewModel
            {
                Ime = user.Ime,
                Prezime = user.Prezime,
                Email = user.Email ?? "",
                Uloga = user.Uloga,
                BrojIndeksa = user.BrojIndeksa,
                Predmeti = predmeti
            };

            return View(model);
        }
    }
}