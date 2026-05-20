using Earn_Learn.Data;
using Earn_Learn.Enums;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Earn_Learn.Controllers
{
    public class SystemManagerController : Controller
    {
        private readonly UserManager<Korisnik> _userManager;
        private readonly ApplicationDbContext _context;

        public SystemManagerController(UserManager<Korisnik> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.SystemManager)
                return RedirectToAction("Index", "Home");

            var model = new AdminDashboardViewModel
            {
                UkupnoKorisnika = await _context.Users.CountAsync(),
                UkupnoTutora = await _context.Users.CountAsync(u => u.Uloga == Uloga.Tutor),
                AktivnihSesija = await _context.Termin.CountAsync(t => t.datumIVrijeme >= DateTime.Now),
                UkupanPromet = await _context.Transakcija.SumAsync(t => t.iznos),
                MjesecniPromet = await _context.Transakcija
                    .Where(t => t.datumUplate.Month == DateTime.Now.Month &&
                                t.datumUplate.Year == DateTime.Now.Year)
                    .SumAsync(t => t.iznos),
                ZahtjevaNCekanju = await _context.Termin.CountAsync(t => t.status == StatusTermina.Rezervisan)
            };

            return View(model);
        }
        public async Task<IActionResult> Index()
        {
            var manageri = await _context.Users
                .Where(k => k.Uloga == Uloga.SystemManager)
                .ToListAsync();
            return View(manageri);
        }

        public async Task<IActionResult> Detalji(string id)
        {
            var manager = await _userManager.FindByIdAsync(id);
            if (manager == null || manager.Uloga != Uloga.SystemManager)
                return NotFound();

            return View(manager);
        }

        public IActionResult Kreiraj() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Kreiraj(string ime, string prezime, string email, string lozinka)
        {
            var korisnik = new Korisnik
            {
                Ime = ime,
                Prezime = prezime,
                Email = email,
                UserName = email,
                Uloga = Uloga.SystemManager,
                DatumRegistracije = DateTime.UtcNow
            };

            var rezultat = await _userManager.CreateAsync(korisnik, lozinka);
            if (rezultat.Succeeded)
                return RedirectToAction(nameof(Index));

            foreach (var greska in rezultat.Errors)
                ModelState.AddModelError("", greska.Description);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Obrisi(string id)
        {
            var manager = await _userManager.FindByIdAsync(id);
            if (manager == null || manager.Uloga != Uloga.SystemManager)
                return NotFound();

            await _userManager.DeleteAsync(manager);
            return RedirectToAction(nameof(Index));
        }
    }
}