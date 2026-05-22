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
        private readonly SignInManager<Korisnik> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ProfilController(UserManager<Korisnik> userManager,
                                SignInManager<Korisnik> signInManager,
                                ApplicationDbContext context,
                                IWebHostEnvironment env)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _env = env;
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

            if (user.Uloga == Uloga.Tutor)
            {
                ViewBag.CijenaPoSatu = user.CijenaPoSatu ?? 0;
                ViewBag.ProsjecnaOcjena = user.ProsjecnaOcjena ?? 0;
            }

            var sviPredmeti = await _context.Predmet
                .OrderBy(p => p.naziv)
                .ToListAsync();

            var model = new ProfilViewModel
            {
                Ime = user.Ime,
                Prezime = user.Prezime,
                Email = user.Email ?? "",
                Uloga = user.Uloga,
                BrojIndeksa = user.BrojIndeksa,
                Predmeti = predmeti,
                SviPredmeti = sviPredmeti
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostaniTutor(
            List<int> odabraniPredmeti,
            IFormFile? prilogOcjene)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Student)
                return RedirectToAction("Index");

            if (odabraniPredmeti == null || !odabraniPredmeti.Any())
            {
                TempData["TutorGreska"] = "Morate odabrati barem jedan predmet.";
                return RedirectToAction("Index");
            }

            // Čuvanje priloga
            string? prilogPath = null;
            if (prilogOcjene != null && prilogOcjene.Length > 0)
            {
                var dozvoljeniTipovi = new[] {
                    "image/jpeg", "image/png", "image/gif",
                    "image/webp", "application/pdf"
                };
                if (!dozvoljeniTipovi.Contains(prilogOcjene.ContentType))
                {
                    TempData["TutorGreska"] = "Dozvoljeni formati su JPG, PNG i PDF.";
                    return RedirectToAction("Index");
                }
                if (prilogOcjene.Length > 5 * 1024 * 1024)
                {
                    TempData["TutorGreska"] = "Prilog ne smije biti veći od 5MB.";
                    return RedirectToAction("Index");
                }

                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "prilozi");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = Guid.NewGuid() + Path.GetExtension(prilogOcjene.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await prilogOcjene.CopyToAsync(stream);

                prilogPath = "/uploads/prilozi/" + fileName;
            }

            // Promjena uloge u bazi
            user.Uloga = Uloga.Tutor;
            if (prilogPath != null)
                user.PrilogOcjene = prilogPath;

            await _userManager.UpdateAsync(user);

            // Dodavanje predmeta
            foreach (var predmetId in odabraniPredmeti)
            {
                var postoji = await _context.KorisnikPredmet
                    .AnyAsync(kp => kp.idKorisnika == user.Id && kp.idPredmeta == predmetId);
                if (!postoji)
                {
                    _context.KorisnikPredmet.Add(new KorisnikPredmet
                    {
                        idKorisnika = user.Id,
                        idPredmeta = predmetId
                    });
                }
            }
            await _context.SaveChangesAsync();

            // Osvježi cookie da aplikacija prepozna novu ulogu odmah
            await _signInManager.RefreshSignInAsync(user);

            TempData["TutorUspjeh"] = "Uspješno ste postali tutor!";
            return LocalRedirect("/Tutor/Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UrediCijenu(double cijenaPoSatu)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Uloga != Uloga.Tutor)
                return RedirectToAction("Index", "Home");

            if (cijenaPoSatu < 0)
            {
                TempData["Greska"] = "Cijena ne može biti negativna.";
                return RedirectToAction("Index");
            }

            user.CijenaPoSatu = cijenaPoSatu;
            await _userManager.UpdateAsync(user);

            TempData["Uspjeh"] = $"Cijena uspješno ažurirana na {cijenaPoSatu:0.00} KM/h.";
            return RedirectToAction("Index");
        }
    }
}