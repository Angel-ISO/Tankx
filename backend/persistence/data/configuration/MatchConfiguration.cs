using backend.domain.entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.persistence.data.configuration;

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("Matches");
        builder.HasKey(match => match.Id);

        builder.Property(match => match.Id)
            .HasColumnName("Id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(match => match.StartedAt)
            .HasColumnName("StartedAt")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(match => match.FinishedAt)
            .HasColumnName("FinishedAt")
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(match => match.WinnerId)
            .HasColumnName("WinnerId")
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.HasOne<Profile>()
            .WithMany()
            .HasForeignKey(match => match.WinnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(match => match.MapName)
            .HasColumnName("MapName")
            .HasColumnType("varchar")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(match => match.GameMode)
            .HasColumnName("GameMode")
            .HasConversion<string>()
            .HasColumnType("varchar")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(match => match.Region)
            .HasColumnName("Region")
            .HasConversion<string>()
            .HasColumnType("varchar")
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(match => match.Region)
            .HasDatabaseName("IX_Matches_Region");

        builder.Ignore(match => match.Duration);
    }
}
