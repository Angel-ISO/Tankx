using backend.api.Dtos;
using backend.api.Helpers;
using backend.domain.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.api.Controllers;

[Authorize]
public sealed class MatchesController : BaseApiController
{
    private readonly IUnitOfWork unitOfWork;

    public MatchesController(IUnitOfWork unitOfWork)
    {
        this.unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<ActionResult<Pager<MatchDto>>> GetMatches([FromQuery] Params parameters)
    {
        var (total, matches) = parameters.Region is null
            ? await unitOfWork.Matches.GetAllAsync(
                parameters.PageIndex,
                parameters.PageSize,
                parameters.Search)
            : await unitOfWork.Matches.GetAllByRegionAsync(
                parameters.PageIndex,
                parameters.PageSize,
                parameters.Search,
                parameters.Region);

        return Ok(new Pager<MatchDto>(
            matches.Select(MatchDto.FromEntity),
            total,
            parameters.PageIndex,
            parameters.PageSize,
            parameters.Search));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MatchDto>> GetMatch(Guid id)
    {
        var match = await unitOfWork.Matches.GetByIdAsync(id);
        return match is null ? NotFound() : Ok(MatchDto.FromEntity(match));
    }
}
