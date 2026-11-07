using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySciClub.Infrastructure.Persistence.Entities;

namespace MySciClub.Infrastructure.Persistence.Configurations;

internal sealed class SocioRecordConfiguration : IEntityTypeConfiguration<SocioRecord>
{
    public void Configure(EntityTypeBuilder<SocioRecord> builder)
    {
        builder.ToTable("Soci", table =>
        {
            table.HasCheckConstraint("CK_Soci_NumeroSocio_Positive", "[NumeroSocio] > 0");
            table.HasCheckConstraint("CK_Soci_Sesso", "[Sesso] IN (0, 1)");
            table.HasCheckConstraint("CK_Soci_ProvenienzaCF", "[ProvenienzaCodiceFiscale] IN (0, 1)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();
        builder.Property(x => x.NumeroSocio).IsRequired();
        builder.HasIndex(x => x.NumeroSocio).IsUnique().HasDatabaseName("UX_Soci_NumeroSocio");
        builder.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Cognome).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DataNascita).HasColumnType("date");
        builder.Property(x => x.Sesso).HasColumnType("tinyint");
        builder.Property(x => x.CodiceFiscale).HasMaxLength(16).IsUnicode(false).IsRequired();
        builder.HasIndex(x => x.CodiceFiscale).IsUnique().HasDatabaseName("UX_Soci_CodiceFiscale");
        builder.Property(x => x.ProvenienzaCodiceFiscale).HasColumnType("tinyint");
        builder.Property(x => x.IndirizzoResidenza).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CapResidenza).HasMaxLength(20);
        builder.Property(x => x.SuddivisioneEsteraResidenza).HasMaxLength(100);
        builder.Property(x => x.Email).HasMaxLength(254).IsRequired();
        builder.Property(x => x.Cellulare).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PhotoStorageKey).HasMaxLength(512);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(x => x.PaeseNascita).WithMany().HasForeignKey(x => x.PaeseNascitaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PaeseResidenza).WithMany().HasForeignKey(x => x.PaeseResidenzaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ComuneItalianoNascita).WithMany()
            .HasForeignKey(x => new { x.ComuneItalianoNascitaId, x.PaeseNascitaId })
            .HasPrincipalKey(x => new { x.Id, x.CountryId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ComuneItalianoResidenza).WithMany()
            .HasForeignKey(x => new { x.ComuneItalianoResidenzaId, x.PaeseResidenzaId })
            .HasPrincipalKey(x => new { x.Id, x.CountryId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.Cognome, x.Nome }).HasDatabaseName("IX_Soci_Cognome_Nome");
        builder.HasIndex(x => x.Email).HasDatabaseName("IX_Soci_Email");
    }
}
