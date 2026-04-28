using System.ComponentModel.DataAnnotations;

namespace Earn_Learn.Models
{
    public class Recenzija
    {
        [Key]
        public int id { get; set; }
        public int idStudenta { get; set; }
        public int idTutora { get; set; }
        public int ocjena { get; set; }
        public string komentar { get; set; }
    }
}
