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

            if (user.VerifikovanTutor != true)
                return View("VerifikacijaNaCekanju");

            var termini = await _context.Termin
                .Where(t => t.idTutora == user.Id && t.datumIVrijeme >= DateTime.Now)
                .OrderBy(t => t.datumIVrijeme)
                .Take(10)
                .ToListAsync();

            var nadolazaciDetalji = new List<TerminDetaljiViewModel>();
            foreach (var t in termini)
            {
                var predmet = t.idPredmeta.HasValue
                    ? await _context.Predmet.FindAsync(t.idPredmeta.Value)
                    : null;

                var student = !string.IsNullOrEmpty(t.idStudenta)
                    ? await _context.Users.FindAsync(t.idStudenta)
                    : null;

                nadolazaciDetalji.Add(new TerminDetaljiViewModel
                {
                    Termin = t,
                    ImePredmeta = predmet?.naziv,
                    ImeStudenta = student != null ? $"{student.Ime} {student.Prezime}" : null
                });
            }

            // ── GRAFIKON: zarada po mjesecima za tekucu godinu ──
            var godinaSada = DateTime.Now.Year;
            var transakcijeGodina = await _context.Transakcija
                .Where(t => t.idTutora == user.Id
                         && t.statusPlacanja == StatusPlacanja.Uspjesno
                         && t.datumUplate.Year == godinaSada)
                .ToListAsync();

            // iznos je negativan za tutora (odlazak novca od studenta), pa množimo s -1
            var zaradaPoMjesecima = Enumerable.Range(1, 12)
                .Select(m => transakcijeGodina
                    .Where(t => t.datumUplate.Month == m)
                    .Sum(t => t.iznos * -1))
                .ToList();

            // zarada za tekuci i prosli mjesec
            var mjesecSada = DateTime.Now.Month;
            var zaradaOvajMjesec = zaradaPoMjesecima[mjesecSada - 1];
            var zaradaOvaGodina = zaradaPoMjesecima.Sum();

            ViewBag.ZaradaPoMjesecima = System.Text.Json.JsonSerializer.Serialize(zaradaPoMjesecima);
            ViewBag.ZaradaOvajMjesec = zaradaOvajMjesec;
            ViewBag.ZaradaOvaGodina = zaradaOvaGodina;
            ViewBag.Godina = godinaSada;

            if (user.Uloga == Uloga.Tutor)
            {
                ViewBag.CijenaPoSatu = user.CijenaPoSatu ?? 0;
                ViewBag.ProsjecnaOcjena = user.ProsjecnaOcjena ?? 0;
            }

            var sviPredmeti = await _context.Predmet
                .OrderBy(p => p.naziv)
                .ToListAsync();

            var model = new TutorDashboardViewModel
            {
                Ime = user.Ime,
                BrojCasova = user.BrojOdrzanihCasova ?? 0,
                ProsjecnaOcjena = user.ProsjecnaOcjena ?? 0,
                Balans = user.StanjeRacuna,
                NadolazaciTermini = nadolazaciDetalji
            };

            return View(model);
        }
        [Authorize]
        public async Task<IActionResult> StudentDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Tutor)
                return RedirectToAction("Index", "Home");

            if (user.VerifikovanTutor != true)
                return View("VerifikacijaNaCekanju");

            var termini = await _context.Termin
                .Where(t => t.idStudenta == user.Id && t.datumIVrijeme >= DateTime.Now)
                .OrderBy(t => t.datumIVrijeme)
                .Take(10)
                .ToListAsync();

            var nadolazaciDetalji = new List<TerminSaTutorom>();
            foreach (var t in termini)
            {
                var predmet = t.idPredmeta.HasValue
                    ? await _context.Predmet.FindAsync(t.idPredmeta.Value)
                    : null;

                var tutorKorisnik = await _context.Users.FindAsync(t.idTutora);

                nadolazaciDetalji.Add(new TerminSaTutorom
                {
                    Termin = t,
                    Tutor = tutorKorisnik ?? new Korisnik { Ime = "Nepoznat", Prezime = "tutor" },
                    Predmet = predmet
                });
            }

            var topTutori = await _context.Users
                .Where(u => u.Uloga == Uloga.Tutor && u.Id != user.Id && u.VerifikovanTutor == true)
                .OrderByDescending(u => u.ProsjecnaOcjena)
                .Take(4)
                .ToListAsync();

            var model = new StudentDashboardViewModel
            {
                Ime = user.Ime,
                Prezime = user.Prezime,
                NadolazaciTermini = nadolazaciDetalji,
                TopTutori = topTutori
            };

            return View("~/Views/Student/Dashboard.cshtml", model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OtkaziTermin(int terminId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Tutor)
                return Unauthorized();

            var termin = await _context.Termin.FindAsync(terminId);
            if (termin == null || termin.idTutora != user.Id)
                return NotFound();

            _context.Termin.Remove(termin);
            await _context.SaveChangesAsync();

            TempData["Uspjeh"] = "Termin je uspješno otkazan.";
            return RedirectToAction("Dashboard");
        }

        public async Task<IActionResult> Profil(string id)
        {
            var tutor = await _userManager.FindByIdAsync(id);
            if (tutor == null || tutor.Uloga != Uloga.Tutor)
                return NotFound();

            var korisnikPredmeti = await _context.KorisnikPredmet
                .Where(kp => kp.idKorisnika == id)
                .ToListAsync();

            var idPredmeta = korisnikPredmeti.Select(kp => kp.idPredmeta).ToList();

            var predmeti = await _context.Predmet
                .Where(p => idPredmeta.Contains(p.id))
                .ToListAsync();

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

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RezervisiTermin(int terminId, int predmetId, TipInstrukcija tipInstrukcija)
        {
            var korisnik = await _userManager.GetUserAsync(User);
            if (korisnik == null || (korisnik.Uloga != Uloga.Student && korisnik.Uloga != Uloga.Tutor))
                return Unauthorized();

            var termin = await _context.Termin.FindAsync(terminId);
            if (termin == null || termin.status != StatusTermina.Slobodan || termin.datumIVrijeme < DateTime.Now)
            {
                TempData["Greska"] = "Termin nije dostupan.";
                return RedirectToAction("Profil", new { id = termin?.idTutora });
            }

            if (termin.idTutora == korisnik.Id)
            {
                TempData["Greska"] = "Ne možete rezervisati vlastiti termin.";
                return RedirectToAction("Profil", new { id = korisnik.Id });
            }

            var tutor = await _userManager.FindByIdAsync(termin.idTutora);
            if (tutor == null) return NotFound();

            double cijena = tutor.CijenaPoSatu ?? 0;

            if (korisnik.StanjeRacuna < cijena)
            {
                TempData["Broke"] = $"broke 💀😂 Nemate dovoljno para! Trebate {cijena:N2} KM, a imate {korisnik.StanjeRacuna:N2} KM. Uplatite novac u novčaniku!";
                return RedirectToAction("Profil", new { id = tutor.Id });
            }

            var predmet = await _context.Predmet.FindAsync(predmetId);

            termin.idStudenta = korisnik.Id;
            termin.idPredmeta = predmetId;
            if (termin.tipInstrukcija == TipInstrukcija.Hibridno)
                termin.tipInstrukcija = tipInstrukcija;
            termin.status = StatusTermina.Rezervisan;
            termin.cijena = cijena;

            await _context.SaveChangesAsync();
            await _userManager.UpdateAsync(korisnik);

            TempData["Uspjeh"] = "Termin uspješno rezervisan! Novac će biti skinut kada potvrdite prisustvo.";

            if (korisnik.Uloga == Uloga.Tutor)
                return RedirectToAction("StudentDashboard");
            else
                return RedirectToAction("Dashboard", "Student");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OstaviRecenziju(string tutorId, int ocjena, string komentar)
        {
            var student = await _userManager.GetUserAsync(User);
            if (student == null || (student.Uloga != Uloga.Student && student.Uloga != Uloga.Tutor))
                return Unauthorized();

            var imaOdrzanCas = await _context.Termin
                .AnyAsync(t => t.idStudenta == student.Id
                            && t.idTutora == tutorId
                            && t.status == StatusTermina.Odrzan);

            if (!imaOdrzanCas)
            {
                TempData["Greska"] = "Možete ostaviti recenziju samo nakon održanog časa.";
                return RedirectToAction("Profil", new { id = tutorId });
            }

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

        public async Task<IActionResult> Index()
        {
            var tutori = await _context.Users
                .Where(k => k.Uloga == Uloga.Tutor && k.VerifikovanTutor == true)
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
                DatumRegistracije = DateTime.UtcNow,
                VerifikovanTutor = true // Admin-kreiran tutor je automatski verifikovan
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
                .Where(u => u.Uloga == Uloga.Tutor && u.VerifikovanTutor == true &&
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
                .Where(u => tutoriPoPredmetu.Contains(u.Id) && u.Uloga == Uloga.Tutor && u.VerifikovanTutor == true)
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

            var terminPredmeti = new Dictionary<int, string>();
            foreach (var t in dostupniTermini)
            {
                if (t.idPredmeta.HasValue)
                {
                    var p = await _context.Predmet.FindAsync(t.idPredmeta.Value);
                    terminPredmeti[t.id] = p?.naziv ?? "";
                }
            }
            ViewBag.TerminPredmeti = terminPredmeti;

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

        [Authorize]
        public async Task<IActionResult> MojiCasovi()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Tutor)
                return RedirectToAction("Index", "Home");

            if (user.VerifikovanTutor != true)
                return View("VerifikacijaNaCekanju");

            var termini = await _context.Termin
                .Where(t => t.idTutora == user.Id)
                .OrderByDescending(t => t.datumIVrijeme)
                .ToListAsync();

            var model = new List<TerminSaTutorom>();
            foreach (var termin in termini)
            {
                var studentKorisnik = await _context.Users.FindAsync(termin.idStudenta);
                var predmet = termin.idPredmeta.HasValue
                    ? await _context.Predmet.FindAsync(termin.idPredmeta.Value)
                    : null;

                model.Add(new TerminSaTutorom
                {
                    Termin = termin,
                    Tutor = studentKorisnik ?? new Korisnik { Ime = "Nije", Prezime = "rezervisano" },
                    Predmet = predmet
                });
            }

            return View(model);
        }

        [Authorize]
        public async Task<IActionResult> KreirajTermin()
        {
            var tutor = await _userManager.GetUserAsync(User);
            if (tutor == null || tutor.Uloga != Uloga.Tutor)
                return Forbid();

            if (tutor.VerifikovanTutor != true)
                return View("VerifikacijaNaCekanju");

            var predmeti = await _context.KorisnikPredmet
                .Where(kp => kp.idKorisnika == tutor.Id)
                .Join(_context.Predmet, kp => kp.idPredmeta, p => p.id, (kp, p) => p)
                .ToListAsync();

            ViewBag.Predmeti = predmeti;
            ViewBag.CijenaPoSatu = tutor.CijenaPoSatu ?? 0;
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KreirajTermin(DateTime datumIVrijeme, int idPredmeta, TipInstrukcija tipInstrukcija)
        {
            var tutor = await _userManager.GetUserAsync(User);
            if (tutor == null || tutor.Uloga != Uloga.Tutor)
                return Forbid();

            var termin = new Termin
            {
                idTutora = tutor.Id,
                idStudenta = string.Empty,
                idPredmeta = idPredmeta,
                datumIVrijeme = datumIVrijeme,
                tipInstrukcija = tipInstrukcija,
                status = StatusTermina.Slobodan,
                cijena = tutor.CijenaPoSatu ?? 0
            };

            _context.Add(termin);
            await _context.SaveChangesAsync();

            return RedirectToAction("MojiCasovi");
        }

        [Authorize]
        public async Task<IActionResult> QrKod(int terminId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Tutor)
                return RedirectToAction("Index", "Home");

            var termin = await _context.Termin.FindAsync(terminId);
            if (termin == null || termin.idTutora != user.Id)
                return NotFound();

            var predmet = termin.idPredmeta.HasValue
                ? await _context.Predmet.FindAsync(termin.idPredmeta.Value)
                : null;

            if (string.IsNullOrEmpty(termin.qrKod))
            {
                termin.qrKod = $"EARNLEARN-{termin.id}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
                _context.Update(termin);
                await _context.SaveChangesAsync();
            }

            ViewBag.Termin = termin;
            ViewBag.Predmet = predmet?.naziv ?? "Nepoznat predmet";
            ViewBag.QrKodString = termin.qrKod;

            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ZavrsiCas(int terminId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Tutor)
                return Unauthorized();

            var termin = await _context.Termin.FindAsync(terminId);
            if (termin == null || termin.idTutora != user.Id)
                return NotFound();

            termin.status = StatusTermina.Odrzan;
            _context.Update(termin);
            user.BrojOdrzanihCasova = (user.BrojOdrzanihCasova ?? 0) + 1;
            await _userManager.UpdateAsync(user);
            await _context.SaveChangesAsync();

            TempData["Uspjeh"] = "Čas je uspješno završen.";
            return RedirectToAction("Dashboard");
        }

        [Authorize]
        public async Task<IActionResult> Novcanik()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Tutor)
                return RedirectToAction("Index", "Home");

            var transakcije = await _context.Transakcija
                .Where(t => t.idTutora == user.Id)
                .OrderByDescending(t => t.datumUplate)
                .Take(20)
                .ToListAsync();

            ViewBag.StanjeRacuna = user.StanjeRacuna;
            ViewBag.UkupnoZaradeno = transakcije
                .Where(t => t.statusPlacanja == StatusPlacanja.Uspjesno)
                .Sum(t => t.iznos * -1);

            return View(transakcije);
        }
    }
}