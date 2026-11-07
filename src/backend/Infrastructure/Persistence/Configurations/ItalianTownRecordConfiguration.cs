using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySciClub.Infrastructure.Persistence.Entities;

namespace MySciClub.Infrastructure.Persistence.Configurations;

internal sealed class ItalianTownRecordConfiguration : IEntityTypeConfiguration<ItalianTownRecord>
{
    public void Configure(EntityTypeBuilder<ItalianTownRecord> builder)
    {
        builder.ToTable("ComuniItaliani", table =>
            table.HasCheckConstraint("CK_ComuniItaliani_CodiceCatastale", "[CodiceCatastale] LIKE '[A-Z][0-9][0-9][0-9]'"));
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.CountryId }).HasName("AK_ComuniItaliani_Id_Paese");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CadastralCode).HasMaxLength(4).IsUnicode(false).IsRequired();
        builder.Property(x => x.Province).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.CadastralCode).IsUnique().HasDatabaseName("UX_ComuniItaliani_CodiceCatastale");
        builder.HasOne(x => x.Country).WithMany(x => x.ItalianTowns).HasForeignKey(x => x.CountryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CountryId, x.Name }).HasDatabaseName("IX_ComuniItaliani_Paese_Nome");
    }
}
