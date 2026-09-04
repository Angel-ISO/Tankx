using backend.domain.entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.persistence.data.configuration;

public class MatchParticipantConfiguration : IEntityTypeConfiguration<MatchParticipant>
{
    public void Configure(EntityTypeBuilder<MatchParticipant> builder)
    {
        builder.ToTable("MatchParticipants");
        builder.HasKey(participant => participant.Id);

        builder.Property(mp => mp.Id)
            .HasColumnName("Id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(mp => mp.ProfileId)
            .HasColumnName("ProfileId")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(mp => mp.MatchId)
            .HasColumnName("MatchId")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(mp => mp.Kills)
            .HasColumnName("Kills")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(mp => mp.Deaths)
            .HasColumnName("Deaths")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(mp => mp.Score)
            .HasColumnName("Score")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(mp => mp.Position)
            .HasColumnName("Position")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(mp => mp.Accuracy)
            .HasColumnName("Accuracy")
            .HasColumnType("double precision")
            .IsRequired();

        builder.Property(mp => mp.Disconnected)
            .HasColumnName("Disconnected")
            .HasColumnType("boolean")
            .IsRequired();

        builder.HasOne(mp => mp.Profile)
            .WithMany(profile => profile.MatchParticipants)
            .HasForeignKey(mp => mp.ProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(mp => mp.Match)
            .WithMany(match => match.MatchParticipants)
            .HasForeignKey(mp => mp.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(participant => new { participant.MatchId, participant.ProfileId })
            .IsUnique();
    }
}
