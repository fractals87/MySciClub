using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MySciClub.Infrastructure.Identity;
using MySciClub.Infrastructure.Persistence.Entities;

namespace MySciClub.Infrastructure.Persistence;

public sealed class MySciClubDbContext(
    DbContextOptions<MySciClubDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<long>, long>(options)
{
    internal DbSet<SocioRecord> Soci => Set<SocioRecord>();
    internal DbSet<CountryRecord> Paesi => Set<CountryRecord>();
    internal DbSet<ItalianTownRecord> ComuniItaliani => Set<ItalianTownRecord>();
    internal DbSet<SocioCitizenshipRecord> CittadinanzeSoci => Set<SocioCitizenshipRecord>();
    internal DbSet<AuditRecord> AuditEntries => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasSequence<long>("SeqNumeroSocio", "dbo").StartsAt(1).IncrementsBy(1);
        builder.ApplyConfigurationsFromAssembly(typeof(MySciClubDbContext).Assembly);
    }
}
