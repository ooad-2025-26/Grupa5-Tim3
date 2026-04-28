using System.ComponentModel.DataAnnotations;
using Earn_Learn.Enums;
namespace Earn_Learn.Models
{
    public class Korisnik
    {
        [Key]
        public int id { get; set; }
        public Uloga uloga { get; set; }
        public string ime { get; set; }
        public string prezime { get; set; }
        public string email { get; set; }
        public string lozinka { get; set; }
        public DateTime datumRegistracije { get; set; }
        public int? brojIndeksa { get; set; }
        public double? cijenaPoSatu { get; set; }
        public double? prosjecnaOcjena { get; set; }
        public int? brojOdrzanihCasova { get; set; }
        public double stanjeRacuna { get; set; }
    }
}
