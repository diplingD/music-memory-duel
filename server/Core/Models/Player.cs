namespace Server.Core.Models;

public sealed class Player
{
    public required string Id { get; init; }
    public required string Nick { get; init; }
    public required string ConnectionId { get; set; }
    public int Score { get; set; }
}
