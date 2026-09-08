using System.Collections.Concurrent;

namespace Server.Services;

public sealed class RoomRegistry(EffectExecutor effects)
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    // Thread-safe hashmap
    private readonly ConcurrentDictionary<string, RoomActor> _rooms = new();

    public RoomActor CreateRoom()
    {
        RoomActor actor;
        string code;
        do
        {
            code = GenerateCode();
            actor = new RoomActor(code, effects);
        } while (!_rooms.TryAdd(code, actor));

        return actor;
    }

    public RoomActor? TryGet(string code)
    {
        return _rooms.GetValueOrDefault(code);
    }

    private static string GenerateCode()
    {
        var random = Random.Shared;
        return new string(Enumerable.Range(0, 5)
            .Select(_ => CodeAlphabet[random.Next(CodeAlphabet.Length)])
            .ToArray());
    }
}
