using Earn_Learn.Data;
using Earn_Learn.Enums;
using Earn_Learn.Models;
using Earn_Learn.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Earn_Learn.Controllers
{
    [RoleOnly(Uloga.Student)]
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

                // Zapis otkazivanja u Transakcija (negativan iznos = povrat)
                _context.Transakcija.Add(new Transakcija
                {
                    idStudenta = user.Id,
                    idTutora = termin.idTutora,
                    iznos = -termin.cijena,
                    datumUplate = DateTime.UtcNow,
                    nacinPlacanja = NacinPlacanja.Cash,
                    statusPlacanja = StatusPlacanja.Vraceno
                });

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
                return Json(new List<object>());

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

        [Authorize]
        public async Task<IActionResult> Novcanik()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Student)
                return RedirectToAction("Index", "Home");

            var transakcije = await _context.Transakcija
                .Where(t => t.idStudenta == user.Id)
                .OrderByDescending(t => t.datumUplate)
                .Take(10)
                .ToListAsync();

            ViewBag.StanjeRacuna = user.StanjeRacuna;
            ViewBag.UkupnoUplaceno = transakcije
                .Where(t => t.statusPlacanja == StatusPlacanja.Uspjesno && t.iznos > 0)
                .Sum(t => t.iznos);

            return View(transakcije);
        }
        // GET: Student/RezervisiTermin/5
        [Authorize]
        public async Task<IActionResult> RezervisiTermin(int id)
        {
            var student = await _userManager.GetUserAsync(User);
            if (student == null || student.Uloga != Uloga.Student)
                return View("PristupOdbijen");

            var termin = await _context.Termin
                .FirstOrDefaultAsync(t => t.id == id && t.status == StatusTermina.Slobodan);

            if (termin == null)
                return NotFound();

            var tutor = await _context.Users.FindAsync(termin.idTutora);
            var predmet = termin.idPredmeta.HasValue
                ? await _context.Predmet.FindAsync(termin.idPredmeta.Value)
                : null;

            ViewBag.Tutor = tutor;
            ViewBag.Predmet = predmet;
            return View(termin);
        }

        // POST: Student/RezervisiTermin/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RezervisiTermin(int id, Earn_Learn.Enums.TipInstrukcija tipInstrukcija)
        {
            var student = await _userManager.GetUserAsync(User);
            if (student == null || student.Uloga != Uloga.Student)
                return View("PristupOdbijen");

            var termin = await _context.Termin
                .FirstOrDefaultAsync(t => t.id == id && t.status == StatusTermina.Slobodan);

            if (termin == null)
            {
                TempData["Greska"] = "Termin nije dostupan.";
                return RedirectToAction("Dashboard");
            }

            termin.idStudenta = student.Id;
            termin.status = StatusTermina.Rezervisan;
            termin.tipInstrukcija = tipInstrukcija;

            _context.Update(termin);
            await _context.SaveChangesAsync();

            TempData["Uspjeh"] = "Termin uspješno rezervisan!";
            return RedirectToAction("Dashboard");
        }

        [Authorize]
        public async Task<IActionResult> OdaberiTermin(string tutorId)
        {
            var student = await _userManager.GetUserAsync(User);
            if (student == null || student.Uloga != Uloga.Student)
                return View("PristupOdbijen");

            var tutor = await _context.Users.FindAsync(tutorId);
            if (tutor == null || tutor.Uloga != Uloga.Tutor)
                return NotFound();

            var termini = await _context.Termin
                .Where(t => t.idTutora == tutorId
                         && t.status == StatusTermina.Slobodan
                         && t.datumIVrijeme >= DateTime.Now)
                .OrderBy(t => t.datumIVrijeme)
                .ToListAsync();

            var terminPredmeti = new Dictionary<int, string>();
            foreach (var t in termini)
            {
                if (t.idPredmeta.HasValue)
                {
                    var p = await _context.Predmet.FindAsync(t.idPredmeta.Value);
                    terminPredmeti[t.id] = p?.naziv ?? "—";
                }
            }

            ViewBag.Tutor = tutor;
            ViewBag.TerminPredmeti = terminPredmeti;
            return View(termini);
        }
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UplatiNovac(double iznos)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Student)
                return Unauthorized();

            if (iznos <= 0 || iznos > 10000)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, poruka = "Iznos mora biti između 1 i 10,000 KM." });

                TempData["Greska"] = "Iznos mora biti između 1 i 10,000 KM.";
                return RedirectToAction(nameof(Novcanik));
            }

            user.StanjeRacuna += iznos;
            await _userManager.UpdateAsync(user);

            // Zapis uplate u Transakcija
            _context.Transakcija.Add(new Transakcija
            {
                idStudenta = user.Id,
                idTutora = string.Empty,
                iznos = iznos,
                datumUplate = DateTime.UtcNow,
                nacinPlacanja = NacinPlacanja.Kartica,
                statusPlacanja = StatusPlacanja.Uspjesno
            });
            await _context.SaveChangesAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new
                {
                    success = true,
                    novoStanje = user.StanjeRacuna,
                    poruka = $"Uspješno ste uplatili {iznos:N2} KM na vaš račun."
                });

            TempData["Uspjeh"] = $"Uspješno ste uplatili {iznos:N2} KM na vaš račun.";
            return RedirectToAction(nameof(Novcanik));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PotvrdiPrisustvo(int terminId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Student)
                return Unauthorized();

            var termin = await _context.Termin.FindAsync(terminId);
            if (termin == null || termin.idStudenta != user.Id)
                return Json(new { success = false, poruka = "Termin nije pronađen." });

            if (termin.status != StatusTermina.Rezervisan)
                return Json(new { success = false, poruka = "Termin nije u statusu rezervisan." });

            if (user.StanjeRacuna < termin.cijena)
                return Json(new { success = false, poruka = $"Nemate dovoljno sredstava. Potrebno: {termin.cijena:N2} KM." });

            var tutor = await _context.Users.FindAsync(termin.idTutora);
            if (tutor == null)
                return Json(new { success = false, poruka = "Tutor nije pronađen." });

            // Pronađi system managera (prvi u bazi)
            var systemManager = await _context.Users
                .FirstOrDefaultAsync(u => u.Uloga == Uloga.SystemManager);

            double provizija = Math.Round(termin.cijena * 0.10, 2);
            double zaradaTutora = Math.Round(termin.cijena - provizija, 2);

            // Prebaci novac
            user.StanjeRacuna -= termin.cijena;
            tutor.StanjeRacuna += zaradaTutora;
            if (systemManager != null)
                systemManager.StanjeRacuna += provizija;

            termin.status = StatusTermina.Odrzan;
            tutor.BrojOdrzanihCasova = (tutor.BrojOdrzanihCasova ?? 0) + 1;

            // Transakcija: studentova perspektiva (puno plaćanje = negativno)
            _context.Transakcija.Add(new Transakcija
            {
                idStudenta = user.Id,
                idTutora = tutor.Id,
                iznos = -termin.cijena,
                datumUplate = DateTime.UtcNow,
                nacinPlacanja = NacinPlacanja.Cash,
                statusPlacanja = StatusPlacanja.Uspjesno
            });

            // Transakcija: tutorova perspektiva (90% zarade = pozitivno)
            _context.Transakcija.Add(new Transakcija
            {
                idStudenta = string.Empty,
                idTutora = tutor.Id,
                iznos = zaradaTutora,
                datumUplate = DateTime.UtcNow,
                nacinPlacanja = NacinPlacanja.Cash,
                statusPlacanja = StatusPlacanja.Uspjesno
            });

            // Transakcija: system manager provizija (10%)
            if (systemManager != null)
            {
                _context.Transakcija.Add(new Transakcija
                {
                    idStudenta = string.Empty,
                    idTutora = systemManager.Id,
                    iznos = provizija,
                    datumUplate = DateTime.UtcNow,
                    nacinPlacanja = NacinPlacanja.Cash,
                    statusPlacanja = StatusPlacanja.Uspjesno
                });
            }

            await _userManager.UpdateAsync(user);
            await _userManager.UpdateAsync(tutor);
            if (systemManager != null)
                await _userManager.UpdateAsync(systemManager);
            await _context.SaveChangesAsync();

            return Json(new { success = true, poruka = $"Prisustvo potvrđeno! Skinuto {termin.cijena:N2} KM s računa." });
        }

        // ── PROFIL STUDENTA (admin može pregledati) ──
        public async Task<IActionResult> Profil(string id)
        {
            var student = await _userManager.FindByIdAsync(id);
            if (student == null || student.Uloga != Uloga.Student)
                return NotFound();

            var predmeti = await _context.KorisnikPredmet
                .Where(kp => kp.idKorisnika == id)
                .Join(_context.Predmet, kp => kp.idPredmeta, p => p.id, (kp, p) => p)
                .ToListAsync();

            var termini = await _context.Termin
                .Where(t => t.idStudenta == id)
                .OrderByDescending(t => t.datumIVrijeme)
                .ToListAsync();

            var recenzije = await _context.Recenzija
                .Where(r => r.idStudenta == id)
                .OrderByDescending(r => r.datumRecenzije)
                .ToListAsync();

            var recenzijeViewModel = new List<RecenzijaViewModel>();
            foreach (var rec in recenzije)
            {
                var tutor = await _context.Users.FindAsync(rec.idTutora);
                recenzijeViewModel.Add(new RecenzijaViewModel
                {
                    Recenzija = rec,
                    ImeStudenta = tutor?.Ime ?? "Tutor"
                });
            }

            var model = new StudentProfilViewModel
            {
                Student = student,
                Predmeti = predmeti,
                Termini = termini,
                Recenzije = recenzijeViewModel
            };

            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OstaviRecenziju(string tutorId, int ocjena, string komentar)
        {
            var student = await _userManager.GetUserAsync(User);
            if (student == null || student.Uloga != Uloga.Student)
                return View("PristupOdbijen");

            // Provjeri je li student odrzao cas s ovim tutorom
            var moze = await _context.Termin
                .AnyAsync(t => t.idStudenta == student.Id
                            && t.idTutora == tutorId
                            && t.status == StatusTermina.Odrzan);

            if (!moze)
            {
                TempData["Greska"] = "Možete ostaviti recenziju samo tutoru s kojim ste imali čas.";
                return RedirectToAction("TutorProfil", new { id = tutorId });
            }

            var recenzija = new Recenzija
            {
                idStudenta = student.Id,
                idTutora = tutorId,
                ocjena = ocjena,
                komentar = komentar ?? "",
                datumRecenzije = DateTime.UtcNow
            };

            _context.Recenzija.Add(recenzija);

            // Ažuriraj prosječnu ocjenu tutora
            var tutor = await _context.Users.FindAsync(tutorId);
            if (tutor != null)
            {
                var sveRecenzije = await _context.Recenzija
                    .Where(r => r.idTutora == tutorId)
                    .ToListAsync();
                tutor.ProsjecnaOcjena = sveRecenzije.Average(r => r.ocjena);
                await _userManager.UpdateAsync(tutor);
            }

            await _context.SaveChangesAsync();

            TempData["Uspjeh"] = "Recenzija uspješno poslana!";
            return RedirectToAction("Profil", "Tutor", new { id = tutorId });
        }
        // ── PRIJAVI TUTORA (student prijavljuje tutora adminu) ──
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PrijaviTutora(string tutorId, string naslov, string opis)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Student)
                return Unauthorized();

            var tutor = await _userManager.FindByIdAsync(tutorId);
            if (tutor == null)
                return NotFound();

            _context.Prijava.Add(new Prijava
            {
                Naslov = naslov,
                Opis = $"Prijava tutora {tutor.Ime} {tutor.Prezime}: {opis}",
                IdPrijavitelja = user.Id,
                DatumPrijave = DateTime.UtcNow,
                Status = Earn_Learn.Enums.StatusPrijave.Prijavljeno
            });
            await _context.SaveChangesAsync();

            TempData["Uspjeh"] = "Prijava je uspješno poslana adminu.";
            return RedirectToAction("Profil", "Tutor", new { id = tutorId });
        }

        // ── PRIJAVI RECENZIJU (tutor prijavljuje recenziju adminu) ──
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PrijaviRecenziju(int recenzijaId, string tutorId, string naslov, string opis)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Tutor)
                return Unauthorized();

            _context.Prijava.Add(new Prijava
            {
                Naslov = naslov,
                Opis = $"Prijava recenzije (ID: {recenzijaId}): {opis}",
                IdPrijavitelja = user.Id,
                DatumPrijave = DateTime.UtcNow,
                Status = Earn_Learn.Enums.StatusPrijave.Prijavljeno
            });
            await _context.SaveChangesAsync();

            TempData["Uspjeh"] = "Prijava recenzije je poslana adminu.";
            return RedirectToAction("Recenzije", "Tutor", new { id = tutorId });
        }
    }
}