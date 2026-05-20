using Earn_Learn.Data;
using Earn_Learn.Enums;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Earn_Learn.Controllers
{
    public class TutorController : Controller
    {
        private readonly UserManager<Korisnik> _userManager;
        private readonly ApplicationDbContext _context;

        public TutorController(UserManager<Korisnik> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Tutor)
                return RedirectToAction("Index", "Home");

            var nadolazaciTermini = await _context.Termin
                .Where(t => t.idTutora == user.Id && t.datumIVrijeme >= DateTime.Now)
                .OrderBy(t => t.datumIVrijeme)
                .Take(10)
                .ToListAsync();

            var model = new TutorDashboardViewModel
            {
                Ime = user.Ime,
                BrojCasova = user.BrojOdrzanihCasova ?? 0,
                ProsjecnaOcjena = user.ProsjecnaOcjena ?? 0,
                Balans = user.StanjeRacuna,
                NadolazaciTermini = nadolazaciTermini
            };

            return View(model);
        }

        // ===================== PROFIL TUTORA =====================
        public async Task<IActionResult> Profil(string id)
        {
            var tutor = await _userManager.FindByIdAsync(id);
            if (tutor == null || tutor.Uloga != Uloga.Tutor)
                return NotFound();

            // Predmeti tutora
            var korisnikPredmeti = await _context.KorisnikPredmet
                .Where(kp => kp.idKorisnika == id)
                .ToListAsync();

            var idPredmeta = korisnikPredmeti.Select(kp => kp.idPredmeta).ToList();

            var predmeti = await _context.Predmet
                .Where(p => idPredmeta.Contains(p.id))
                .ToListAsync();

            // Recenzije tutora — dohvati i ime studenta odmah ovdje
            var recenzijeRaw = await _context.Recenzija
                .Where(r => r.idTutora == id)
                .OrderByDescending(r => r.datumRecenzije)
                .ToListAsync();

            var recenzijeViewModel = new List<RecenzijaViewModel>();
            foreach (var rec in recenzijeRaw)
            {
                var student = await _context.Users.FindAsync(rec.idStudenta);
                recenzijeViewModel.Add(new RecenzijaViewModel
                {
                    Recenzija = rec,
                    ImeStudenta = student?.Ime ?? "Student"
                });
            }

            // Dostupni termini (slobodni, u budućnosti)
            var dostupniTermini = await _context.Termin
                .Where(t => t.idTutora == id
                         && t.datumIVrijeme >= DateTime.Now
                         && t.status == StatusTermina.Slobodan)
                .OrderBy(t => t.datumIVrijeme)
                .ToListAsync();

            var prosjecna = recenzijeRaw.Any() ? recenzijeRaw.Average(r => r.ocjena) : 0;

            var model = new TutorProfilViewModel
            {
                Tutor = tutor,
                Predmeti = predmeti,
                Recenzije = recenzijeViewModel,
                DostupniTermini = dostupniTermini,
                ProsjecnaOcjena = prosjecna,
                UkupnoRecenzija = recenzijeRaw.Count
            };

            return View(model);
        }

        // ===================== REZERVACIJA TERMINA =====================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RezervisiTermin(int terminId, int predmetId, TipInstrukcija tipInstrukcija)
        {
            var student = await _userManager.GetUserAsync(User);
            if (student == null || student.Uloga != Uloga.Student)
                return Unauthorized();

            var termin = await _context.Termin.FindAsync(terminId);
            if (termin == null || termin.status != StatusTermina.Slobodan || termin.datumIVrijeme < DateTime.Now)
            {
                TempData["Greska"] = "Termin nije dostupan.";
                return RedirectToAction("Profil", new { id = termin?.idTutora });
            }

            var tutor = await _userManager.FindByIdAsync(termin.idTutora);
            if (tutor == null)
                return NotFound();

            // Provjeri balans studenta
            double cijena = tutor.CijenaPoSatu ?? 0;
            if (student.StanjeRacuna < cijena)
            {
                TempData["Greska"] = "Nemate dovoljno sredstava na računu.";
                return RedirectToAction("Profil", new { id = tutor.Id });
            }

            // Rezerviši termin
            termin.idStudenta = student.Id;
            termin.idPredmeta = predmetId;
            termin.tipInstrukcija = tipInstrukcija;
            termin.status = StatusTermina.Rezervisan;
            termin.cijena = cijena;

            // Skini s računa studenta, dodaj tutoru
            student.StanjeRacuna -= cijena;
            tutor.StanjeRacuna += cijena;

            await _context.SaveChangesAsync();
            await _userManager.UpdateAsync(student);
            await _userManager.UpdateAsync(tutor);

            TempData["Uspjeh"] = "Termin uspješno rezervisan!";
            return RedirectToAction("Dashboard", "Student");
        }

        // ===================== OSTAVI RECENZIJU =====================
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OstaviRecenziju(string tutorId, int ocjena, string komentar)
        {
            var student = await _userManager.GetUserAsync(User);
            if (student == null || student.Uloga != Uloga.Student)
                return Unauthorized();

            // Provjeri da li je student imao čas s ovim tutorom
            var imaOdrzanCas = await _context.Termin
                .AnyAsync(t => t.idStudenta == student.Id
                            && t.idTutora == tutorId
                            && t.status == StatusTermina.Odrzan);

            if (!imaOdrzanCas)
            {
                TempData["Greska"] = "Možete ostaviti recenziju samo nakon održanog časa.";
                return RedirectToAction("Profil", new { id = tutorId });
            }

            // Provjeri da li već postoji recenzija
            var postojiRecenzija = await _context.Recenzija
                .AnyAsync(r => r.idStudenta == student.Id && r.idTutora == tutorId);

            if (postojiRecenzija)
            {
                TempData["Greska"] = "Već ste ostavili recenziju za ovog tutora.";
                return RedirectToAction("Profil", new { id = tutorId });
            }

            var recenzija = new Recenzija
            {
                idStudenta = student.Id,
                idTutora = tutorId,
                ocjena = Math.Clamp(ocjena, 1, 5),
                komentar = komentar,
                datumRecenzije = DateTime.UtcNow
            };

            _context.Recenzija.Add(recenzija);
            await _context.SaveChangesAsync();

            // Ažuriraj prosječnu ocjenu tutora
            var sve = await _context.Recenzija.Where(r => r.idTutora == tutorId).ToListAsync();
            var tutor = await _userManager.FindByIdAsync(tutorId);
            if (tutor != null)
            {
                tutor.ProsjecnaOcjena = sve.Average(r => r.ocjena);
                await _userManager.UpdateAsync(tutor);
            }

            TempData["Uspjeh"] = "Recenzija uspješno objavljena!";
            return RedirectToAction("Profil", new { id = tutorId });
        }

        // ===================== ADMIN CRUD =====================
        public async Task<IActionResult> Index()
        {
            var tutori = await _context.Users
                .Where(k => k.Uloga == Uloga.Tutor)
                .ToListAsync();
            return View(tutori);
        }

        public async Task<IActionResult> Detalji(string id)
        {
            var tutor = await _userManager.FindByIdAsync(id);
            if (tutor == null || tutor.Uloga != Uloga.Tutor)
                return NotFound();
            return View(tutor);
        }

        public IActionResult Kreiraj() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Kreiraj(string ime, string prezime, string email, string lozinka, double cijenaPoSatu)
        {
            var korisnik = new Korisnik
            {
                Ime = ime,
                Prezime = prezime,
                Email = email,
                UserName = email,
                Uloga = Uloga.Tutor,
                CijenaPoSatu = cijenaPoSatu,
                ProsjecnaOcjena = 0,
                BrojOdrzanihCasova = 0,
                DatumRegistracije = DateTime.UtcNow
            };
            var rezultat = await _userManager.CreateAsync(korisnik, lozinka);
            if (rezultat.Succeeded) return RedirectToAction(nameof(Index));
            foreach (var greska in rezultat.Errors)
                ModelState.AddModelError("", greska.Description);
            return View();
        }

        public async Task<IActionResult> Uredi(string id)
        {
            var tutor = await _userManager.FindByIdAsync(id);
            if (tutor == null || tutor.Uloga != Uloga.Tutor) return NotFound();
            return View(tutor);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Uredi(string id, double cijenaPoSatu)
        {
            var tutor = await _userManager.FindByIdAsync(id);
            if (tutor == null || tutor.Uloga != Uloga.Tutor) return NotFound();
            tutor.CijenaPoSatu = cijenaPoSatu;
            await _userManager.UpdateAsync(tutor);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Obrisi(string id)
        {
            var tutor = await _userManager.FindByIdAsync(id);
            if (tutor == null || tutor.Uloga != Uloga.Tutor) return NotFound();
            await _userManager.DeleteAsync(tutor);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Pretraga(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return RedirectToAction("Dashboard", "Student");

            var tutoriPoImenu = await _context.Users
                .Where(u => u.Uloga == Uloga.Tutor &&
                            (u.Ime + " " + u.Prezime).Contains(q))
                .ToListAsync();

            var predmetiIds = await _context.Predmet
                .Where(p => p.naziv.Contains(q))
                .Select(p => p.id)
                .ToListAsync();

            var tutoriPoPredmetu = await _context.KorisnikPredmet
                .Where(kp => predmetiIds.Contains(kp.idPredmeta))
                .Select(kp => kp.idKorisnika)
                .Distinct()
                .ToListAsync();

            var tutoriPoPredmetuKorisnici = await _context.Users
                .Where(u => tutoriPoPredmetu.Contains(u.Id) && u.Uloga == Uloga.Tutor)
                .ToListAsync();

            var sviTutori = tutoriPoImenu
                .Union(tutoriPoPredmetuKorisnici)
                .DistinctBy(t => t.Id)
                .ToList();

            return View(sviTutori);
        }

        public async Task<IActionResult> RezervacijaTermina(string id)
        {
            var tutor = await _userManager.FindByIdAsync(id);
            if (tutor == null || tutor.Uloga != Uloga.Tutor) return NotFound();

            var korisnikPredmeti = await _context.KorisnikPredmet
                .Where(kp => kp.idKorisnika == id).ToListAsync();
            var idPredmeta = korisnikPredmeti.Select(kp => kp.idPredmeta).ToList();
            var predmeti = await _context.Predmet
                .Where(p => idPredmeta.Contains(p.id)).ToListAsync();
            var dostupniTermini = await _context.Termin
                .Where(t => t.idTutora == id && t.datumIVrijeme >= DateTime.Now
                         && t.status == StatusTermina.Slobodan)
                .OrderBy(t => t.datumIVrijeme).ToListAsync();

            var model = new TutorProfilViewModel
            {
                Tutor = tutor,
                Predmeti = predmeti,
                DostupniTermini = dostupniTermini
            };
            return View(model);
        }

        public async Task<IActionResult> Recenzije(string id)
        {
            var tutor = await _userManager.FindByIdAsync(id);
            if (tutor == null || tutor.Uloga != Uloga.Tutor) return NotFound();

            var recenzijeRaw = await _context.Recenzija
                .Where(r => r.idTutora == id)
                .OrderByDescending(r => r.datumRecenzije).ToListAsync();

            var recenzijeViewModel = new List<RecenzijaViewModel>();
            foreach (var rec in recenzijeRaw)
            {
                var student = await _context.Users.FindAsync(rec.idStudenta);
                recenzijeViewModel.Add(new RecenzijaViewModel
                {
                    Recenzija = rec,
                    ImeStudenta = student?.Ime ?? "Student"
                });
            }

            var prosjecna = recenzijeRaw.Any() ? recenzijeRaw.Average(r => r.ocjena) : 0;

            var model = new TutorProfilViewModel
            {
                Tutor = tutor,
                Recenzije = recenzijeViewModel,
                ProsjecnaOcjena = prosjecna,
                UkupnoRecenzija = recenzijeRaw.Count
            };
            return View(model);
        }
    }
}