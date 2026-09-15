namespace Server.Core.Models;

public sealed class Player
{
    public required string Id { get; init; }
    public required string Nick { get; init; }
    public required string ConnectionId { get; set; }
    public required string Token { get; init; }     // secret, never broadcast — proves identity on Rejoin
    public int Score { get; set; }
    public bool IsConnected { get; set; } = true;
}
