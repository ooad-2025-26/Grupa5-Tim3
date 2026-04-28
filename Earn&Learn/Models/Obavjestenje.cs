using System.ComponentModel.DataAnnotations;

namespace Earn_Learn.Models
{
    public class Obavjestenje
    {
        [Key]
        public int id { get; set; }
        public int idKorisnika { get; set; }
        public string naslov { get; set; }
        public string sadrzaj { get; set; }
        public DateTime datumSlanja { get; set; }
        public bool procitano { get; set; }
    }
}
