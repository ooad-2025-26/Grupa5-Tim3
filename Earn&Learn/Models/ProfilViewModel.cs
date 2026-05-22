using Earn_Learn.Enums;
using Microsoft.AspNetCore.Http;

namespace Earn_Learn.Models
{
    public class ProfilViewModel
    {
        public string Ime { get; set; } = "";
        public string Prezime { get; set; } = "";
        public string Email { get; set; } = "";
        public Uloga Uloga { get; set; }
        public int? BrojIndeksa { get; set; }
        public List<string> Predmeti { get; set; } = new();

        // Za "Postani tutor" formu
        public List<Predmet> SviPredmeti { get; set; } = new();
        public List<int> OdabraniPredmeti { get; set; } = new();
        public IFormFile? PrilogOcjene { get; set; }
    }
}