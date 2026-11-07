using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySciClub.Infrastructure.Persistence.Entities;

namespace MySciClub.Infrastructure.Persistence.Configurations;

internal sealed class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.ToTable("AuditLog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();
        builder.Property(x => x.RecordedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(x => x.Operation).HasMaxLength(80).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PreviousValues).HasColumnType("nvarchar(max)");
        builder.Property(x => x.NewValues).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => x.RecordedAtUtc);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
