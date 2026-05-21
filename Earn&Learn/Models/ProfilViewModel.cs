using Earn_Learn.Enums;

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
    }
}