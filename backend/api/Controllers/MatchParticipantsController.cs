using backend.api.Dtos;
using backend.api.Helpers;
using backend.domain.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.api.Controllers;

[Authorize]
public sealed class MatchParticipantsController : BaseApiController
{
    private readonly IUnitOfWork unitOfWork;

    public MatchParticipantsController(IUnitOfWork unitOfWork)
    {
        this.unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<ActionResult<Pager<MatchParticipantDto>>> GetParticipants(
        [FromQuery] Params parameters)
    {
        var (total, participants) = await unitOfWork.MatchParticipants.GetAllAsync(
            parameters.PageIndex,
            parameters.PageSize,
            parameters.Search);

        return Ok(new Pager<MatchParticipantDto>(
            participants.Select(MatchParticipantDto.FromEntity),
            total,
            parameters.PageIndex,
            parameters.PageSize,
            parameters.Search));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MatchParticipantDto>> GetParticipant(Guid id)
    {
        var participant = await unitOfWork.MatchParticipants.GetByIdAsync(id);
        return participant is null
            ? NotFound()
            : Ok(MatchParticipantDto.FromEntity(participant));
    }
}
