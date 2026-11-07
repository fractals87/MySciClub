using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySciClub.Infrastructure.Persistence.Entities;

namespace MySciClub.Infrastructure.Persistence.Configurations;

internal sealed class SocioCitizenshipRecordConfiguration : IEntityTypeConfiguration<SocioCitizenshipRecord>
{
    public void Configure(EntityTypeBuilder<SocioCitizenshipRecord> builder)
    {
        builder.ToTable("CittadinanzeSoci");
        builder.HasKey(x => new { x.SocioId, x.CountryId });
        builder.HasOne(x => x.Socio).WithMany(x => x.Cittadinanze).HasForeignKey(x => x.SocioId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Country).WithMany().HasForeignKey(x => x.CountryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SocioId).IsUnique().HasFilter("[Principale] = 1")
            .HasDatabaseName("UX_CittadinanzeSoci_UnicaPrincipale");
    }
}
