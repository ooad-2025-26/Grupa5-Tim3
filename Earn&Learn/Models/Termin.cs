using System.ComponentModel.DataAnnotations;
using Earn_Learn.Enums;

namespace Earn_Learn.Models
{
    public class Termin
    {
        [Key]
        public int id { get; set; }
        public int idStudenta { get; set; }
        public int idTutora { get; set; }
        public DateTime datumIVrijeme { get; set; }
        public TipInstrukcija tipInstrukcija { get; set; }
        public StatusTermina status { get; set; }
        public string qrKod { get; set; }
    }
}
