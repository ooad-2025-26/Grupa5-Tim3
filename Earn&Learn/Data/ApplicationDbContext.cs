using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Earn_Learn.Models;

namespace Earn_Learn.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : IdentityDbContext<Korisnik>(options)
    {
        public DbSet<Earn_Learn.Models.Transakcija> Transakcija { get; set; } = default!;
        public DbSet<Earn_Learn.Models.Obavjestenje> Obavjestenje { get; set; } = default!;
        public DbSet<Earn_Learn.Models.Recenzija> Recenzija { get; set; } = default!;
        public DbSet<Earn_Learn.Models.Termin> Termin { get; set; } = default!;
    }
}