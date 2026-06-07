using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Earn_Learn.Data;
using Earn_Learn.Models;
using Earn_Learn.Enums;

namespace Earn_Learn.Controllers
{
    public class TerminsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Korisnik> _userManager;

        public TerminsController(ApplicationDbContext context, UserManager<Korisnik> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Termins
        public async Task<IActionResult> Index()
        {
            return View(await _context.Termin.ToListAsync());
        }

        // GET: Termins/MojiCasovi
        [Authorize]
        public async Task<IActionResult> MojiCasovi()
        {
            var tutor = await _userManager.GetUserAsync(User);
            if (tutor == null)
                return RedirectToAction("Login", "Account");

            var termini = await _context.Termin
                .Where(t => t.idTutora == tutor.Id)
                .OrderBy(t => t.datumIVrijeme)
                .ToListAsync();

            var predmetIds = termini
                .Where(t => t.idPredmeta.HasValue)
                .Select(t => t.idPredmeta!.Value)
                .Distinct()
                .ToList();

            ViewBag.Predmeti = await _context.Predmet
                .Where(p => predmetIds.Contains(p.id))
                .ToDictionaryAsync(p => p.id, p => p.naziv);

            return View(termini);
        }

        // GET: Termins/KreirajTermin
        [Authorize]
        public async Task<IActionResult> KreirajTermin()
        {
            var tutor = await _userManager.GetUserAsync(User);
            if (tutor == null || tutor.Uloga != Uloga.Tutor)
                return Forbid();

            var predmeti = await _context.KorisnikPredmet
                .Where(kp => kp.idKorisnika == tutor.Id)
                .Join(_context.Predmet,
                      kp => kp.idPredmeta,
                      p => p.id,
                      (kp, p) => p)
                .ToListAsync();

            ViewBag.Predmeti = predmeti;
            ViewBag.CijenaPoSatu = tutor.CijenaPoSatu ?? 0;
            return View();
        }
        // GET: Termins/RezervisiTermin/5
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

        // POST: Termins/RezervisiTermin/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RezervisiTermin(int id, TipInstrukcija tipInstrukcija)
        {
            var student = await _userManager.GetUserAsync(User);
            if (student == null || student.Uloga != Uloga.Student)
                return View("PristupOdbijen");

            var termin = await _context.Termin
                .FirstOrDefaultAsync(t => t.id == id && t.status == StatusTermina.Slobodan);

            if (termin == null)
            {
                TempData["Greska"] = "Termin nije dostupan.";
                return RedirectToAction("Index", "Student");
            }

            termin.idStudenta = student.Id;
            termin.status = StatusTermina.Rezervisan;
            termin.tipInstrukcija = tipInstrukcija;

            _context.Update(termin);
            await _context.SaveChangesAsync();

            TempData["Uspjeh"] = "Termin uspješno rezervisan!";
            return RedirectToAction("Dashboard", "Student");
        }

        // POST: Termins/KreirajTermin
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KreirajTermin(
            DateTime datumIVrijeme,
            int idPredmeta,
            TipInstrukcija tipInstrukcija)
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

        // GET: Termins/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var termin = await _context.Termin.FirstOrDefaultAsync(m => m.id == id);
            if (termin == null) return NotFound();

            return View(termin);
        }

        // GET: Termins/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Termins/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("id,idStudenta,idTutora,datumIVrijeme,tipInstrukcija,status,qrKod")] Termin termin)
        {
            if (ModelState.IsValid)
            {
                _context.Add(termin);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(termin);
        }

        // GET: Termins/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var termin = await _context.Termin.FindAsync(id);
            if (termin == null) return NotFound();

            return View(termin);
        }

        // POST: Termins/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("id,idStudenta,idTutora,datumIVrijeme,tipInstrukcija,status,qrKod")] Termin termin)
        {
            if (id != termin.id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(termin);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TerminExists(termin.id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(termin);
        }

        // GET: Termins/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var termin = await _context.Termin.FirstOrDefaultAsync(m => m.id == id);
            if (termin == null) return NotFound();

            return View(termin);
        }

        // POST: Termins/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var termin = await _context.Termin.FindAsync(id);
            if (termin != null)
                _context.Termin.Remove(termin);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TerminExists(int id)
        {
            return _context.Termin.Any(e => e.id == id);
        }
    }
}