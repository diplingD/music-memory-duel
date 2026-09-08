using System.Threading.Channels;
using Server.Core.Game;
using Server.Core.Models;

namespace Server.Services;

public sealed class RoomActor : IAsyncDisposable
{
    private readonly Channel<GameEvent> _inbox = Channel.CreateUnbounded<GameEvent>();
    private readonly EffectExecutor _effects;
    private readonly Task _loop;
    private GameState _state;

    public string RoomCode { get; }

    public string HostId => _state.Players.Count > 0 ? _state.Players[0].Id : "";

    public RoomActor(string roomCode, EffectExecutor effects)
    {
        RoomCode = roomCode;
        _effects = effects;
        _state = new GameState { Phase = Phase.Lobby, Players = [] };
        _loop = Task.Run(RunAsync);
    }

    public ValueTask PostAsync(GameEvent evt) => _inbox.Writer.WriteAsync(evt);

    private async Task RunAsync()       // non-stop running thread
    {
        // only this can change _state — that's why we dont need lock
        await foreach (var evt in _inbox.Reader.ReadAllAsync())
        {
            var (nextState, effects) = Reducer.Reduce(_state, evt, DateTime.UtcNow);
            _state = nextState;

            foreach (var effect in effects)
                await _effects.ExecuteAsync(effect, this);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _inbox.Writer.Complete();
        await _loop;
    }
}
