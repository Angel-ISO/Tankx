using backend.domain.entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.persistence.data.configuration;

public class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        builder.ToTable("Profiles");
        builder.HasKey(profile => profile.Id);

        builder.Property(p => p.Id)
            .HasColumnName("Id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(p => p.DisplayName)
            .HasColumnName("DisplayName")
            .HasColumnType("varchar")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.TankType)
            .HasColumnName("TankType")
            .HasColumnType("varchar")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(profile => profile.DisplayName)
            .IsUnique();
    }
}
