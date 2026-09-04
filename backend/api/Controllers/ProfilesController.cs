using backend.api.Dtos;
using backend.api.Helpers;
using backend.api.Services;
using backend.domain.entities;
using backend.domain.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.api.Controllers;

[Authorize]
public class ProfilesController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserContextService _userContextService;

    public ProfilesController(
        IUnitOfWork unitOfWork,
        UserContextService userContextService)
    {
        _unitOfWork = unitOfWork;
        _userContextService = userContextService;
    }

    [HttpGet]
    public async Task<ActionResult<Pager<ProfileDto>>> GetProfiles([FromQuery] Params parameters)
    {
        var (total, profiles) = await _unitOfWork.Profiles.GetAllAsync(
            parameters.PageIndex,
            parameters.PageSize,
            parameters.Search);
        var dtos = profiles.Select(ProfileDto.FromEntity);

        return Ok(new Pager<ProfileDto>(
            dtos,
            total,
            parameters.PageIndex,
            parameters.PageSize,
            parameters.Search));
    }

    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileDto>> GetMyProfile()
    {
        var userId = _userContextService.GetUserId();
        if (userId is null)
            return Unauthorized();

        var profile = await _unitOfWork.Profiles.GetByIdAsync(userId.Value);

        if (profile == null)
            return NotFound();

        return Ok(ProfileDto.FromEntity(profile));
    }

    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfileStatusDto>> GetProfileStatus()
    {
        var userId = _userContextService.GetUserId();
        if (userId is null)
            return Unauthorized();

        var profile = await _unitOfWork.Profiles.GetByIdAsync(userId.Value);
        var hasProfile = profile != null;

        return Ok(new ProfileStatusDto(hasProfile));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProfileDto>> CreateProfile(
        CreateProfileDto createDto)
    {
        var userId = _userContextService.GetUserId();

        if (userId is null)
            return Unauthorized();

        var profileId = userId.Value;

        var existingProfile =
            await _unitOfWork.Profiles.GetByIdAsync(profileId);

        if (existingProfile != null)
            return Conflict("Profile already exists.");

        var profile = new Profile
        {
            Id = profileId,
            DisplayName = createDto.DisplayName.Trim(),
            TankType = createDto.TankType,
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.Profiles.Add(profile);

        try
        {
            await _unitOfWork.SaveAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict("The display name is already in use.");
        }

        var profileDto = ProfileDto.FromEntity(profile);

        return CreatedAtAction(
            nameof(GetMyProfile),
            new { },
            profileDto);
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileDto>> UpdateProfile(
        UpdateProfileDto updateDto)
    {
        var userId = _userContextService.GetUserId();

        if (userId is null)
            return Unauthorized();

        var profileId = userId.Value;

        var profile =
            await _unitOfWork.Profiles.GetByIdAsync(profileId);

        if (profile == null)
            return NotFound();

        if (updateDto.DisplayName is not null)
            profile.DisplayName = updateDto.DisplayName.Trim();

        if (updateDto.TankType is not null)
            profile.TankType = updateDto.TankType;

        _unitOfWork.Profiles.Update(profile);

        try
        {
            await _unitOfWork.SaveAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict("The display name is already in use.");
        }

        return Ok(ProfileDto.FromEntity(profile));
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteProfile()
    {
        var userId = _userContextService.GetUserId();
        if (userId is null)
            return Unauthorized();

        var profile = await _unitOfWork.Profiles.GetByIdAsync(userId.Value);
        if (profile is null)
            return NotFound();

        _unitOfWork.Profiles.Remove(profile);

        try
        {
            await _unitOfWork.SaveAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict("The profile cannot be deleted while it has match history.");
        }

        return NoContent();
    }
}