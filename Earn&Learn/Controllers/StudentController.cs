using Earn_Learn.Data;
using Earn_Learn.Enums;
using Earn_Learn.Models;
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
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Student)
                return RedirectToAction("Index", "Home");

            // idStudenta u Termin je int, ali user.Id je GUID string
            // Dohvati termine gdje se idStudenta poklapa s BrojIndeksa studenta
            // ILI ako koristiš auto-increment int ID, trebaš ga čuvati odvojeno
            // Zasad dohvaćamo sve termine i filtriramo po nadolazećem datumu
            var termini = await _context.Termin
                .Where(t => t.datumIVrijeme >= DateTime.Now)
                .OrderBy(t => t.datumIVrijeme)
                .Take(5)
                .ToListAsync();

            var terminiSaTutorom = new List<TerminSaTutorom>();
            foreach (var termin in termini)
            {
                var tutorKorisnik = await _context.Users
                    .Where(u => u.Uloga == Uloga.Tutor)
                    .FirstOrDefaultAsync();

                terminiSaTutorom.Add(new TerminSaTutorom
                {
                    Termin = termin,
                    Tutor = tutorKorisnik ?? new Korisnik { Ime = "Nepoznat", Prezime = "" }
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