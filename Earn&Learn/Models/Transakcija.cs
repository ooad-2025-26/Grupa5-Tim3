using System.ComponentModel.DataAnnotations;
using Earn_Learn.Enums;

namespace Earn_Learn.Models
{
    public class Transakcija
    {
        [Key]
        public int id { get; set; }
        public int idStudenta { get; set; }
        public int idTutora { get; set; }
        public double iznos { get; set; }
        public DateTime datumUplate { get; set; }
        public NacinPlacanja nacinPlacanja { get; set; }
        public StatusPlacanja statusPlacanja { get; set; }
    }
}
