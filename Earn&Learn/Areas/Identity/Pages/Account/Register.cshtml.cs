using System.ComponentModel.DataAnnotations;
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
            public string Ime { get; set; } = string.Empty;

            [Required(ErrorMessage = "Prezime je obavezno.")]
            public string Prezime { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email je obavezan.")]
            [EmailAddress(ErrorMessage = "Neispravan format emaila.")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Šifra je obavezna.")]
            [StringLength(100, ErrorMessage = "Šifra mora imati najmanje {2} karaktera.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "Šifre se ne poklapaju.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            public bool ZelimBitiTutor { get; set; } = false;

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
            await UcitajPredmete();

            if (ModelState.IsValid)
            {
                var korisnik = new Korisnik
                {
                    UserName = Input.Email,
                    Email = Input.Email,
                    Ime = Input.Ime,
                    Prezime = Input.Prezime,
                    Uloga = Input.ZelimBitiTutor ? Uloga.Tutor : Uloga.Student,
                    DatumRegistracije = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(korisnik, Input.Password);

                if (result.Succeeded)
                {
                    // Ako je tutor, spremi odabrane predmete
                    if (Input.ZelimBitiTutor && Input.OdabraniPredmeti.Any())
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
