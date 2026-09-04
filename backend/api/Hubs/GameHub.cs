using System.Security.Claims;
using backend.api.Hubs.DTOs;
using backend.api.Hubs.Runtime;
using backend.api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace backend.api.Hubs;

[Authorize]
public sealed class GameHub : Hub
{
    private readonly GameSessionManager sessions;
    private readonly RedisCacheService cache;

    public GameHub(GameSessionManager sessions, RedisCacheService cache)
    {
        this.sessions = sessions;
        this.cache = cache;
    }

    public Task<IReadOnlyCollection<RoomSummary>> ListRooms() =>
        Task.FromResult(sessions.ListRooms());

    public async Task<RoomSummary> CreateRoom(CreateRoomRequest request)
    {
        try
        {
            var result = await sessions.CreateRoomAsync(
                GetPlayerId(), Context.ConnectionId, request, Context.ConnectionAborted);
            await Groups.AddToGroupAsync(Context.ConnectionId, result.Room.RoomCode);
            await Clients.Caller.SendAsync("GameStateUpdated", result.Snapshot);
            await Clients.All.SendAsync("RoomsUpdated", sessions.ListRooms());
            return result.Room;
        }
        catch (InvalidOperationException exception)
        {
            throw new HubException(exception.Message);
        }
    }

    public async Task<RoomSummary> JoinRoom(JoinRoomRequest request)
    {
        try
        {
            var result = await sessions.JoinRoomAsync(
                GetPlayerId(), Context.ConnectionId, request, Context.ConnectionAborted);
            await Groups.AddToGroupAsync(Context.ConnectionId, result.Room.RoomCode);
            await Clients.Caller.SendAsync("GameStateUpdated", result.Snapshot);
            await Clients.All.SendAsync("RoomsUpdated", sessions.ListRooms());
            return result.Room;
        }
        catch (InvalidOperationException exception)
        {
            throw new HubException(exception.Message);
        }
    }

    public async Task LeaveRoom()
    {
        try
        {
            var roomCode = await sessions.LeaveRoomAsync(GetPlayerId());
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomCode);
            await Clients.All.SendAsync("RoomsUpdated", sessions.ListRooms());
        }
        catch (InvalidOperationException exception)
        {
            throw new HubException(exception.Message);
        }
    }

    public async Task StartMatch()
    {
        try
        {
            await sessions.StartMatchAsync(GetPlayerId(), Context.ConnectionAborted);
            await Clients.All.SendAsync("RoomsUpdated", sessions.ListRooms());
        }
        catch (InvalidOperationException exception)
        {
            throw new HubException(exception.Message);
        }
    }

    public Task SendMovement(MovementInput input)
    {
        try
        {
            sessions.Move(GetPlayerId(), input);
            return Task.CompletedTask;
        }
        catch (InvalidOperationException exception)
        {
            throw new HubException(exception.Message);
        }
    }

    public Task Shoot()
    {
        try
        {
            sessions.Shoot(GetPlayerId());
            return Task.CompletedTask;
        }
        catch (InvalidOperationException exception)
        {
            throw new HubException(exception.Message);
        }
    }

    public async Task SendMessage(ChatMessageInput input)
    {
        try
        {
            await sessions.SendMessageAsync(GetPlayerId(), input);
        }
        catch (InvalidOperationException exception)
        {
            throw new HubException(exception.Message);
        }
    }

    public override async Task OnConnectedAsync()
    {
        await cache.AddOnlinePlayerAsync(GetPlayerId());
        var token = Context.GetHttpContext()?.Request.Query["access_token"].ToString();
        if (!string.IsNullOrEmpty(token))
        {
            await cache.StoreSessionAsync(GetPlayerId(), token);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await cache.RemoveOnlinePlayerAsync(GetPlayerId());
        await sessions.HandleDisconnectedAsync(Context.ConnectionId);
        await Clients.All.SendAsync("RoomsUpdated", sessions.ListRooms());
        await base.OnDisconnectedAsync(exception);
    }

    private Guid GetPlayerId()
    {
        var subject = Context.User?.FindFirstValue("sub");
        return Guid.TryParse(subject, out var playerId)
            ? playerId
            : throw new HubException("The authenticated user identifier is invalid.");
    }
}
