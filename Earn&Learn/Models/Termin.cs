using System.ComponentModel.DataAnnotations;
using Earn_Learn.Enums;

namespace Earn_Learn.Models
{
    public class Termin
    {
        [Key]
        public int id { get; set; }
        public string idStudenta { get; set; } = string.Empty;
        public string idTutora { get; set; } = string.Empty;
        public int? idPredmeta { get; set; }
        public DateTime datumIVrijeme { get; set; }
        public TipInstrukcija tipInstrukcija { get; set; }
        public StatusTermina status { get; set; }
        public string? qrKod { get; set; }
        public double cijena { get; set; }
        public string? mjestoCasa { get; set; }
        public bool prisustvoPotvrdjeno { get; set; } = false;
    }
}