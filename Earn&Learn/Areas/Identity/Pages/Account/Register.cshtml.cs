using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Earn_Learn.Data;
using Earn_Learn.Enums;
using Earn_Learn.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Earn_Learn.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<Korisnik> _userManager;
        private readonly SignInManager<Korisnik> _signInManager;
        private readonly ApplicationDbContext _context;

        public RegisterModel(
            UserManager<Korisnik> userManager,
            SignInManager<Korisnik> signInManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public List<SelectListItem> PredmetiSelectList { get; set; } = new();

        public class InputModel
        {
            [Required(ErrorMessage = "Ime je obavezno.")]
            [RegularExpression(@"^[a-zA-ZčćžšđČĆŽŠĐ]+$", ErrorMessage = "Ime ne smije sadržati specijalne karaktere.")]
            public string Ime { get; set; } = string.Empty;

            [Required(ErrorMessage = "Prezime je obavezno.")]
            [RegularExpression(@"^[a-zA-ZčćžšđČĆŽŠĐ]+$", ErrorMessage = "Prezime ne može sadržati specijalne karaktere.")]
            public string Prezime { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email je obavezan.")]
            [EmailAddress(ErrorMessage = "Nije validan format email adrese.")]
            [RegularExpression(@"^[a-zA-Z0-9._%+-]+@etf\.unsa\.ba$", ErrorMessage = "Nekorektna e-mail adresa. ")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Šifra je obavezna.")]
            [StringLength(100, ErrorMessage = "{0} mora biti dugačka barem {2} karaktera.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Display(Name = "Potvrda šifre")]
            [Compare("Password", ErrorMessage = "Šifre se ne podudaraju.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            public bool ZelimBitiTutor { get; set; }

            public List<int> OdabraniPredmeti { get; set; } = new();
        }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
            await UcitajPredmete();
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                var odabranaUloga = Input.ZelimBitiTutor ? Uloga.Tutor : Uloga.Student;

                var korisnik = new Korisnik
                {
                    Ime = Input.Ime,
                    Prezime = Input.Prezime,
                    Email = Input.Email,
                    UserName = Input.Email,
                    Uloga = odabranaUloga,
                    DatumRegistracije = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(korisnik, Input.Password);

                if (result.Succeeded)
                {
                    if (Input.ZelimBitiTutor && Input.OdabraniPredmeti != null && Input.OdabraniPredmeti.Any())
                    {
                        foreach (var predmetId in Input.OdabraniPredmeti)
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
                    return LocalRedirect(returnUrl);
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // Ako validacija padne, ponovo punimo listu predmeta prije povratka na formu
            await UcitajPredmete();
            return Page();
        }

        private async Task UcitajPredmete()
        {
            var predmeti = await _context.Predmet.OrderBy(p => p.naziv).ToListAsync();
            PredmetiSelectList = predmeti.Select(p => new SelectListItem
            {
                Value = p.id.ToString(),
                Text = p.naziv
            }).ToList();
        }
    }
}