using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySciClub.Infrastructure.Persistence.Entities;

namespace MySciClub.Infrastructure.Persistence.Configurations;

internal sealed class CountryRecordConfiguration : IEntityTypeConfiguration<CountryRecord>
{
    public void Configure(EntityTypeBuilder<CountryRecord> builder)
    {
        builder.ToTable("Paesi");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.IsoAlpha2).HasMaxLength(2).IsUnicode(false).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.IsoAlpha2).IsUnique().HasDatabaseName("UX_Paesi_IsoAlpha2");
    }
}
