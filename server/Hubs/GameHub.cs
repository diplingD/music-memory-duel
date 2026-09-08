using Microsoft.AspNetCore.SignalR;
using Server.Core.DTOs;
using Server.Core.Game;
using Server.Core.Models;
using Server.Services;

namespace Server.Hubs;

// methods from this file are being called by frontend
public sealed class GameHub(RoomRegistry rooms) : Hub<IGameClient>
{
    public long Ping() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public async Task<CreateRoomResult> CreateRoom(string nick)
    {
        var room = rooms.CreateRoom();
        var playerId = Guid.NewGuid().ToString("N");

        // Context.ConnectionId - unique id set by SingalR for specific WebSocket connection (one browser tab - one new id)
        await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomCode);
        Context.SetPlayer(room.RoomCode, playerId);     // save user for current ConnectionId - so we can use it later (in StartMatch)

        await room.PostAsync(new PlayerJoined(playerId, nick, Context.ConnectionId));

        return new CreateRoomResult(room.RoomCode, playerId);
    }

    public async Task<JoinRoomResult> JoinRoom(string roomCode, string nick)
    {
        var room = rooms.TryGet(roomCode) ?? throw new HubException("Room not found");
        var playerId = Guid.NewGuid().ToString("N");

        await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomCode);
        Context.SetPlayer(room.RoomCode, playerId);

        await room.PostAsync(new PlayerJoined(playerId, nick, Context.ConnectionId));

        return new JoinRoomResult(playerId);
    }

    public async Task StartMatch()
    {
        var (roomCode, _) = Context.RequirePlayer();
        var room = rooms.TryGet(roomCode) ?? throw new HubException("Room not found");

        await room.PostAsync(new MatchStartRequested());
    }

    public async Task PlayNote(NoteEvent note)
    {
        var (roomCode, playerId) = Context.RequirePlayer();
        var room = rooms.TryGet(roomCode) ?? throw new HubException("Room not found");

        await room.PostAsync(new NotePlayed(playerId, Context.ConnectionId, note));
    }

    public async Task SubmitSequence(NoteEvent[] notes)
    {
        var (roomCode, playerId) = Context.RequirePlayer();
        var room = rooms.TryGet(roomCode) ?? throw new HubException("Room not found");

        await room.PostAsync(new SequenceSubmitted(playerId, notes));
    }

    public async Task SubmitAnswer(Guid roundId, NoteEvent[] notes)
    {
        var (roomCode, playerId) = Context.RequirePlayer();
        var room = rooms.TryGet(roomCode) ?? throw new HubException("Room not found");

        await room.PostAsync(new AnswerSubmitted(playerId, roundId, notes));
    }
}
