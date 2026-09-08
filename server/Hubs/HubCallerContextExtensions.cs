using Microsoft.AspNetCore.SignalR;

namespace Server.Hubs;

// Extension class for HubCallerContext - so we can add some methods we need
public static class HubCallerContextExtensions
{
    private const string RoomCodeKey = "roomCode";
    private const string PlayerIdKey = "playerId";

    public static void SetPlayer(this HubCallerContext context, string roomCode, string playerId)
    {
        context.Items[RoomCodeKey] = roomCode;
        context.Items[PlayerIdKey] = playerId;
    }

    public static (string RoomCode, string PlayerId) RequirePlayer(this HubCallerContext context)
    {
        var hasRoomCode = context.Items.TryGetValue(RoomCodeKey, out var roomCodeValue);
        var hasPlayerId = context.Items.TryGetValue(PlayerIdKey, out var playerIdValue);

        if (!hasRoomCode || !hasPlayerId)
            throw new HubException("Not joined to a room");

        var roomCode = (string)roomCodeValue!;
        var playerId = (string)playerIdValue!;

        return (roomCode, playerId);
    }
}
