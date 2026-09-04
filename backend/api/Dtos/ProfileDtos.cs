using System.ComponentModel.DataAnnotations;
using backend.domain.entities;

namespace backend.api.Dtos;

public sealed record ProfileDto(
    Guid Id,
    string DisplayName,
    string TankType,
    DateTime CreatedAt)
{
    public static ProfileDto FromEntity(Profile profile) => new(
        profile.Id,
        profile.DisplayName,
        profile.TankType,
        profile.CreatedAt);
}

public sealed class CreateProfileDto
{
    [Required, StringLength(20, MinimumLength = 3)]
    public string DisplayName { get; init; } = string.Empty;

    [Required]
    public string TankType { get; init; } = "tank_green";
}

public sealed class UpdateProfileDto
{
    [StringLength(20, MinimumLength = 3)]
    public string? DisplayName { get; init; }

    public string? TankType { get; init; }
}

public sealed record ProfileStatusDto(bool HasProfile);
