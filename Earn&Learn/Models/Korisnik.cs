using Microsoft.AspNetCore.Identity;
using Earn_Learn.Enums;

namespace Earn_Learn.Models
{
    public class Korisnik : IdentityUser
    {
        public Uloga Uloga { get; set; }
        public string Ime { get; set; } = string.Empty;
        public string Prezime { get; set; } = string.Empty;
        public DateTime DatumRegistracije { get; set; } = DateTime.UtcNow;
        public int? BrojIndeksa { get; set; }
        public double? CijenaPoSatu { get; set; }
        public double? ProsjecnaOcjena { get; set; }
        public int? BrojOdrzanihCasova { get; set; }
        public double StanjeRacuna { get; set; } = 0.0;
        public string? PrilogOcjene { get; set; }
    }
}
