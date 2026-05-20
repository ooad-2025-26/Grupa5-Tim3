using Earn_Learn.Data;
using Earn_Learn.Enums;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Earn_Learn.Controllers
{
    public class StudentController : Controller
    {
        private readonly UserManager<Korisnik> _userManager;
        private readonly ApplicationDbContext _context;

        public StudentController(UserManager<Korisnik> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Student)
                return RedirectToAction("Index", "Home");

            var termini = await _context.Termin
                .Where(t => t.idStudenta == user.Id && t.datumIVrijeme >= DateTime.Now)
                .OrderBy(t => t.datumIVrijeme)
                .Take(5)
                .ToListAsync();

            var terminiSaTutorom = new List<TerminSaTutorom>();
            foreach (var termin in termini)
            {
                var tutorKorisnik = await _context.Users.FindAsync(termin.idTutora);
                var predmet = termin.idPredmeta.HasValue
                    ? await _context.Predmet.FindAsync(termin.idPredmeta.Value)
                    : null;

                terminiSaTutorom.Add(new TerminSaTutorom
                {
                    Termin = termin,
                    Tutor = tutorKorisnik ?? new Korisnik { Ime = "Nepoznat", Prezime = "" },
                    Predmet = predmet
                });
            }

            var topTutori = await _context.Users
                .Where(k => k.Uloga == Uloga.Tutor)
                .OrderByDescending(k => k.ProsjecnaOcjena)
                .Take(3)
                .ToListAsync();

            var model = new StudentDashboardViewModel
            {
                Ime = user.Ime,
                Prezime = user.Prezime,
                NadolazaciTermini = terminiSaTutorom,
                TopTutori = topTutori
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Obrisi(string id)
        {
            var student = await _userManager.FindByIdAsync(id);
            if (student == null || student.Uloga != Uloga.Student)
                return NotFound();

            await _userManager.DeleteAsync(student);
            return RedirectToAction(nameof(Index));
        }
    }
}