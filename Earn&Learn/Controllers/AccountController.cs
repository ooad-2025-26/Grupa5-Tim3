using Earn_Learn.Data;
using Earn_Learn.Enums;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Earn_Learn.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<Korisnik> _signInManager;
        private readonly UserManager<Korisnik> _userManager;
        private readonly ApplicationDbContext _context;

        public AccountController(SignInManager<Korisnik> signInManager,
                                  UserManager<Korisnik> userManager,
                                  ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
        }

        // ── LOGIN ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            [FromForm(Name = "Input.Email")] string email,
            [FromForm(Name = "Input.Password")] string password,
            string? returnUrl)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["LoginError"] = "Unesite email i šifru.";
                return LocalRedirect("/Identity/Account/Login");
            }

            var result = await _signInManager.PasswordSignInAsync(
                email, password, isPersistent: false, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                var korisnik = await _userManager.FindByEmailAsync(email);
                return korisnik?.Uloga switch
                {
                    Uloga.Student => LocalRedirect("/Student/Dashboard"),
                    Uloga.Tutor => LocalRedirect("/Tutor/Dashboard"),
                    Uloga.SystemManager => LocalRedirect("/SystemManager/Dashboard"),
                    _ => LocalRedirect(returnUrl ?? "/")
                };
            }

            TempData["LoginError"] = "Pogrešan email ili šifra.";
            return LocalRedirect("/Identity/Account/Login");
        }

        // ── REGISTER ──
        // ── REGISTER GET ──
        [HttpGet]
        public IActionResult Register() => View();

        // ── REGISTER POST ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            string ime, string prezime, string email,
            string password, Uloga uloga, int? brojIndeksa)
        {
            if (uloga == Uloga.SystemManager)
            {
                TempData["RegError"] = "Nije moguće registrovati System Manager nalog.";
                return RedirectToAction("Register");
            }

            var korisnik = new Korisnik
            {
                Ime = ime ?? "",
                Prezime = prezime ?? "",
                Email = email,
                UserName = email,
                Uloga = uloga,
                BrojIndeksa = uloga == Uloga.Student ? brojIndeksa : null,
                DatumRegistracije = DateTime.UtcNow
            };

            var rezultat = await _userManager.CreateAsync(korisnik, password);

            if (rezultat.Succeeded)
            {
                await _signInManager.SignInAsync(korisnik, isPersistent: false);
                return uloga switch
                {
                    Uloga.Student => LocalRedirect("/Student/Dashboard"),
                    Uloga.Tutor => LocalRedirect("/Tutor/Dashboard"),
                    _ => LocalRedirect("/")
                };
            }

            TempData["RegError"] = string.Join(", ", rezultat.Errors.Select(e => e.Description));
            return RedirectToAction("Register");
        }

        // ── REGISTER IDENTITY (stara forma) ──
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterIdentity(
            [FromForm(Name = "Input.Ime")] string ime,
            [FromForm(Name = "Input.Prezime")] string prezime,
            [FromForm(Name = "Input.Email")] string email,
            [FromForm(Name = "Input.Password")] string password,
            [FromForm(Name = "Input.ZelimBitiTutor")] bool zelimTutor,
            [FromForm(Name = "Input.OdabraniPredmeti")] List<int>? odabraniPredmeti,
            int? uloga,
            string? returnUrl)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["RegError"] = "Unesite sve podatke.";
                return LocalRedirect("/Identity/Account/Register");
            }

            var odabranaUloga = (zelimTutor || uloga == 1) ? Uloga.Tutor : Uloga.Student;

            var korisnik = new Korisnik
            {
                Ime = ime ?? "",
                Prezime = prezime ?? "",
                Email = email,
                UserName = email,
                Uloga = odabranaUloga,
                DatumRegistracije = DateTime.UtcNow
            };

            var rezultat = await _userManager.CreateAsync(korisnik, password);

            if (rezultat.Succeeded)
            {
                if (odabranaUloga == Uloga.Tutor && odabraniPredmeti != null)
                {
                    foreach (var predmetId in odabraniPredmeti)
                    {
                        _context.KorisnikPredmet.Add(new KorisnikPredmet
                        {
                            idKorisnika = korisnik.Id,
                            idPredmeta = predmetId
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                await _signInManager.SignInAsync(korisnik, isPersistent: false);
                return odabranaUloga switch
                {
                    Uloga.Student => LocalRedirect("/Student/Dashboard"),
                    Uloga.Tutor => LocalRedirect("/Tutor/Dashboard"),
                    _ => LocalRedirect("/")
                };
            }

            TempData["RegError"] = string.Join(", ", rezultat.Errors.Select(e => e.Description));
            return LocalRedirect("/Identity/Account/Register");
        }
    }
}