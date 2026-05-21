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

        [Authorize]
        public async Task<IActionResult> MojiCasovi()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Student)
                return RedirectToAction("Index", "Home");

            var termini = await _context.Termin
                .Where(t => t.idStudenta == user.Id)
                .OrderByDescending(t => t.datumIVrijeme)
                .ToListAsync();

            var model = new List<TerminSaTutorom>();
            foreach (var termin in termini)
            {
                var tutorKorisnik = await _context.Users.FindAsync(termin.idTutora);
                var predmet = termin.idPredmeta.HasValue
                    ? await _context.Predmet.FindAsync(termin.idPredmeta.Value)
                    : null;

                model.Add(new TerminSaTutorom
                {
                    Termin = termin,
                    Tutor = tutorKorisnik ?? new Korisnik { Ime = "Nepoznat", Prezime = "" },
                    Predmet = predmet
                });
            }

            return View(model);
        }

        // ===================== OTKAŽI TERMIN (STUDENT) =====================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OtkaziTermin(int terminId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Student)
                return Unauthorized();

            var termin = await _context.Termin.FindAsync(terminId);
            if (termin == null || termin.idStudenta != user.Id)
                return NotFound();

            // Vrati novac studentu i skini tutoru
            if (termin.status == StatusTermina.Rezervisan)
            {
                var tutor = await _context.Users.FindAsync(termin.idTutora);
                if (tutor != null)
                {
                    user.StanjeRacuna += termin.cijena;
                    tutor.StanjeRacuna -= termin.cijena;
                    await _userManager.UpdateAsync(tutor);
                    await _userManager.UpdateAsync(user);
                }

                // Termin ostaje u bazi ali se oslobađa (ne briše se da tutor vidi)
                termin.idStudenta = string.Empty;
                termin.idPredmeta = null;
                termin.status = StatusTermina.Slobodan;
                termin.cijena = 0;
                await _context.SaveChangesAsync();
            }

            TempData["Uspjeh"] = "Termin je uspješno otkazan. Novac je vraćen na vaš račun.";
            return RedirectToAction("MojiCasovi");
        }

        [HttpGet]
        public async Task<IActionResult> PretraziTutore(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Json(new List<object>());
            }

            var q = query.ToLower().Trim();

            var rezultati = await _context.Users
                .Where(u => u.Uloga == Uloga.Tutor)
                .Where(u => u.Ime.ToLower().Contains(q) ||
                            u.Prezime.ToLower().Contains(q) ||
                            _context.KorisnikPredmet.Any(kp => kp.idKorisnika == u.Id &&
                                                               _context.Predmet.Any(p => p.id == kp.idPredmeta && p.naziv.ToLower().Contains(q))))
                .Select(u => new
                {
                    id = u.Id,
                    ime = u.Ime,
                    prezime = u.Prezime,
                    prosjecnaOcjena = u.ProsjecnaOcjena ?? 0,
                    cijenaPoSatu = u.CijenaPoSatu ?? 0,
                    predmeti = _context.KorisnikPredmet
                        .Where(kp => kp.idKorisnika == u.Id)
                        .Join(_context.Predmet, kp => kp.idPredmeta, p => p.id, (kp, p) => p.naziv)
                        .ToList()
                })
                .Take(5)
                .ToListAsync();

            return Json(rezultati);
        }
    }
}