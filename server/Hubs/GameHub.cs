using Microsoft.AspNetCore.SignalR;
using Server.Core.DTOs;
using Server.Core.Game;
using Server.Core.Models;
using Server.Services;
using Server.Validators;

namespace Server.Hubs;

// methods from this file are being called by frontend
public sealed class GameHub(RoomRegistry rooms) : Hub<IGameClient>
{
    public long Ping() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public async Task<CreateRoomResult> CreateRoom(string nick)
    {
        var room = rooms.CreateRoom();
        var playerId = Guid.NewGuid().ToString("N");
        var playerToken = Guid.NewGuid().ToString("N");

        // Context.ConnectionId - unique id set by SingalR for specific WebSocket connection (one browser tab - one new id)
        await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomCode);
        Context.SetPlayer(room.RoomCode, playerId);     // save user for current ConnectionId - so we can use it later (in StartMatch)

        await room.PostAsync(new PlayerJoined(playerId, nick, Context.ConnectionId, playerToken));

        return new CreateRoomResult(room.RoomCode, playerId, playerToken);
    }

    public async Task<JoinRoomResult> JoinRoom(string roomCode, string nick)
    {
        var room = rooms.TryGet(roomCode) ?? throw new HubException("Room not found");
        var playerId = Guid.NewGuid().ToString("N");
        var playerToken = Guid.NewGuid().ToString("N");

        await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomCode);
        Context.SetPlayer(room.RoomCode, playerId);

        await room.PostAsync(new PlayerJoined(playerId, nick, Context.ConnectionId, playerToken));

        return new JoinRoomResult(playerId, playerToken);
    }

    // Called after a dropped connection re-establishes (SignalR always assigns a NEW ConnectionId on reconnect).
    // Token is verified BEFORE trusting this connection with someone's identity — otherwise
    // anyone who saw a playerId via PlayerListChanged could hijack that player's identity.
    public async Task Rejoin(string roomCode, string playerId, string playerToken)
    {
        var room = rooms.TryGet(roomCode) ?? throw new HubException("Room not found");

        if (!room.TryGetPlayerToken(playerId, out var expectedToken) || expectedToken != playerToken)
            throw new HubException("Invalid rejoin credentials");

        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);
        Context.SetPlayer(roomCode, playerId);

        await room.PostAsync(new PlayerReconnected(playerId, playerToken, Context.ConnectionId));
    }

    // SignalR automatically calls this every time WebSocket connection drops
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.TryGetPlayer(out var roomCode, out var playerId))
        {
            var room = rooms.TryGet(roomCode);
            if (room is not null)
                await room.PostAsync(new PlayerDisconnected(playerId));
        }

        await base.OnDisconnectedAsync(exception);
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
        if (!SubmissionValidator.IsValid(notes)) return;

        var (roomCode, playerId) = Context.RequirePlayer();
        var room = rooms.TryGet(roomCode) ?? throw new HubException("Room not found");

        await room.PostAsync(new SequenceSubmitted(playerId, notes));
    }

    public async Task SubmitAnswer(Guid roundId, NoteEvent[] notes)
    {
        if (!SubmissionValidator.IsValid(notes)) return;

        var (roomCode, playerId) = Context.RequirePlayer();
        var room = rooms.TryGet(roomCode) ?? throw new HubException("Room not found");

        await room.PostAsync(new AnswerSubmitted(playerId, roundId, notes));
    }
}
